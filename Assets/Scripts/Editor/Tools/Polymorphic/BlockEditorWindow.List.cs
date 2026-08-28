using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Polymorphic
{
    public abstract partial class BlockEditorWindow<T> where T : ScriptableObject
    {
        // ============================ Left list ============================

        /// <summary>Row height a host gets if it never touches <see cref="RowSize"/>. Matches the pre-split look.</summary>
        protected const float DEFAULT_ROW_SIZE = 24f;

        protected const float MIN_ROW_SIZE = 18f;
        protected const float MAX_ROW_SIZE = 96f;

        static readonly Color SELECTED_ROW_TINT = new Color(0.45f, 0.75f, 1f);

        readonly List<T> _assets = new List<T>();

        string _search = string.Empty;
        Vector2 _leftScroll;
        float _rowSize = DEFAULT_ROW_SIZE;
        bool _rowSizePrefLoaded;

        /// <summary>
        /// Persists <see cref="RowSize"/> per closed <typeparamref name="T"/> so
        /// <c>ItemEditorWindow</c> and <c>EnchantmentEditorWindow</c> each remember their own slider
        /// position instead of sharing one key (spec §6.1).
        /// </summary>
        static readonly string ROW_SIZE_PREF_KEY = "Rollgeon.BlockEditorWindow." + typeof(T).Name + ".RowSize";

        // ---- host hooks ----------------------------------------------------

        /// <summary>Label for the list row. Falls back to the asset's file name.</summary>
        protected virtual string LabelOf(T asset) => asset != null ? asset.name : "(null)";

        /// <summary>Extra searchable text (ids, display names) beyond the file name.</summary>
        protected virtual string SearchTextOf(T asset) => LabelOf(asset);

        /// <summary>
        /// Every asset of type <typeparamref name="T"/> in the project, sorted by <see cref="LabelOf"/>.
        /// Read-only on purpose: the shell owns the list and rebuilds it on <c>OnProjectChange</c>.
        /// </summary>
        /// <remarks>
        /// Project-wide, not folder-scoped — assets of the same family do live outside
        /// <c>DefaultFolder</c> (see <c>docs/tools/item-editor-spec.md §0</c>), so nothing may assume
        /// otherwise.
        /// </remarks>
        protected IReadOnlyList<T> Assets => _assets;

        /// <summary>
        /// Row height in pixels — the shell's size control writes here and
        /// <see cref="DrawRow"/> receives it. Clamped so a host can't push rows out of the panel.
        /// </summary>
        protected float RowSize
        {
            get => _rowSize;
            set => _rowSize = Mathf.Clamp(value, MIN_ROW_SIZE, MAX_ROW_SIZE);
        }

        /// <summary>
        /// Host-supplied filter UI, drawn under the search field.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="PassesFilters"/> because the shell must not know what the host is
        /// filtering by: it only composes the resulting predicate with its own text search. A host
        /// that changes filter state here does not need to request a repaint — it is already inside
        /// the list's IMGUI pass.
        /// </remarks>
        protected virtual void DrawFilterBar() { }

        /// <summary>
        /// Host-supplied predicate, ANDed with the text search.
        /// </summary>
        /// <remarks>
        /// Composed rather than replacing <c>Matches</c> so the search field keeps working no matter
        /// what the host filters by — the two are independent axes and a host overriding matching
        /// wholesale would silently break search for its window.
        /// </remarks>
        protected virtual bool PassesFilters(T asset) => true;

        /// <summary>
        /// Paints one row into <paramref name="rect"/> and reports whether it was clicked.
        /// </summary>
        /// <param name="rect">The row's reserved rect. The shell owns layout; the host owns pixels.</param>
        /// <param name="asset">The asset this row stands for.</param>
        /// <param name="isSelected">Whether <paramref name="asset"/> is the current selection.</param>
        /// <param name="rowSize">Current <see cref="RowSize"/>, so the row can grow from a text line
        /// into an icon tile without the host tracking the slider itself.</param>
        /// <returns><c>true</c> when the row was clicked this frame; the shell then selects it.</returns>
        /// <remarks>
        /// The click is returned rather than handled by the host so selection stays in one place —
        /// a host that forgot to call selection would produce a list that renders but does nothing.
        /// </remarks>
        protected virtual bool DrawRow(Rect rect, T asset, bool isSelected, float rowSize)
        {
            var prev = GUI.backgroundColor;
            if (isSelected) GUI.backgroundColor = SELECTED_ROW_TINT;
            bool clicked = GUI.Button(rect, LabelOf(asset));
            GUI.backgroundColor = prev;
            return clicked;
        }

        /// <summary>Repaints the list. Needed when filter state changes from outside the list's own pass.</summary>
        protected void RepaintList() => _leftPanel?.MarkDirtyRepaint();

        // ---- drawing --------------------------------------------------------

        void DrawLeft()
        {
            // Lazy rather than in OnEnable: EditorPrefs needs ROW_SIZE_PREF_KEY, which needs
            // typeof(T) — cheap either way, but this keeps every row-size concern in this one file.
            if (!_rowSizePrefLoaded)
            {
                _rowSizePrefLoaded = true;
                RowSize = EditorPrefs.GetFloat(ROW_SIZE_PREF_KEY, DEFAULT_ROW_SIZE);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string next = GUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                if (next != _search) _search = next;

                // Row-size slider, Project-window style: grows the rect DrawRow receives from a
                // text line into an icon tile (spec §6.1). The host never has to poll this — it
                // reads RowSize back through the DrawRow parameter the shell already threads.
                float sliderNext = GUILayout.HorizontalSlider(_rowSize, MIN_ROW_SIZE, MAX_ROW_SIZE, GUILayout.Width(70f));
                if (!Mathf.Approximately(sliderNext, _rowSize))
                {
                    RowSize = sliderNext;
                    EditorPrefs.SetFloat(ROW_SIZE_PREF_KEY, _rowSize);
                }
            }

            DrawFilterBar();

            EditorGUILayout.Space(2);
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            int shown = 0;
            foreach (var asset in _assets)
            {
                if (!Matches(asset)) continue;
                shown++;

                // Reserved with the button style so rows keep the spacing they had when this was a
                // plain GUILayout.Button, whatever the host paints inside.
                var rect = GUILayoutUtility.GetRect(
                    GUIContent.none, GUI.skin.button,
                    GUILayout.Height(_rowSize), GUILayout.ExpandWidth(true));

                if (DrawRow(rect, asset, asset == _selected, _rowSize)) Select(asset);
            }
            if (shown == 0)
                EditorGUILayout.HelpBox(_assets.Count == 0 ? "No assets found." : "Nothing matches the filter.", MessageType.Info);

            EditorGUILayout.EndScrollView();

            DrawFooterActions();
        }

        /// <summary>
        /// Acciones de asset, al pie de la lista: la primaria destacada y las secundarias en una
        /// sola fila segmentada.
        /// </summary>
        /// <remarks>
        /// Antes eran tres filas apiladas de alto completo, que le comían altura a la lista — que es
        /// lo que el usuario mira. Quedan dos: Create se mantiene grande porque es la acción con la
        /// que uno entra a la ventana, y el resto pasa a mini-botones segmentados, que es el idioma
        /// visual nativo del editor para un grupo de acciones sobre la selección.
        /// </remarks>
        void DrawFooterActions()
        {
            EditorGUILayout.Space(4);

            if (GUILayout.Button("+ Create", GUILayout.Height(26f))) CreateAsset();

            EditorGUILayout.Space(2);

            using (new EditorGUI.DisabledScope(_selected == null))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Duplicate", EditorStyles.miniButtonLeft)) DuplicateSelected();
                if (GUILayout.Button("Ping", EditorStyles.miniButtonMid)) EditorGUIUtility.PingObject(_selected);

                // El destructivo se tiñe en vez de separarse: separarlo sugeriría que actúa sobre
                // otra cosa, y actúa sobre la misma selección que los otros dos.
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.6f, 0.55f);
                if (GUILayout.Button("Delete", EditorStyles.miniButtonRight)) DeleteSelected();
                GUI.backgroundColor = prev;
            }

            EditorGUILayout.Space(2);
        }

        bool Matches(T asset)
        {
            if (!PassesFilters(asset)) return false;
            if (string.IsNullOrEmpty(_search)) return true;
            var text = SearchTextOf(asset);
            return text != null && text.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void Select(T asset)
        {
            if (_selected == asset) return;
            _selected = asset;
            _selectedNode = null;
            _ctx.Bind(asset);
            _graph.Bind(asset, _ctx);
            _graph.FrameGraph();
            _sidePanel?.MarkDirtyRepaint();
            _dataPanel?.MarkDirtyRepaint();
            MarkDeclaredTabsDirty();
        }

        void RefreshList()
        {
            _assets.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) _assets.Add(asset);
            }
            _assets.Sort((a, b) => string.CompareOrdinal(LabelOf(a), LabelOf(b)));

            if (_selected != null && !_assets.Contains(_selected)) Select(null);

            InvalidateCatalogCache();
            OnAssetsRefreshed();
        }

        /// <summary>
        /// El host vuelve a calcular acá lo que derive de <see cref="Assets"/>.
        /// </summary>
        /// <remarks>
        /// Existe por una razón medida: sin él, un host solo puede derivar datos dentro de su
        /// método de dibujo, y todo lo que dibuja IMGUI corre en <b>cada repaint</b>. Un filtro que
        /// recorría el árbol de efectos de los 24 ítems para armar su dropdown terminaba haciendo 24
        /// recorridos de árbol por frame, y el editor entero se arrastraba.
        /// <para>
        /// Se dispara cuando la lista se rebuildea: al abrir la ventana y en cada
        /// <c>OnProjectChange</c>. Lo derivado de campos que se editan sin reimportar el asset queda
        /// stale hasta el próximo cambio de proyecto — aceptable para poblar un dropdown, no para
        /// nada que afecte lo que se guarda.
        /// </para>
        /// </remarks>
        protected virtual void OnAssetsRefreshed() { }
    }
}
