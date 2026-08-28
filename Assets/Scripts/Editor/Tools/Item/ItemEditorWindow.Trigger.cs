using System.Collections.Generic;
using System.Linq;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combos;
using Rollgeon.Items;
using Rollgeon.Upgrades.Dice;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// La sección "Cuándo" del panel del ítem: cuándo se ejecuta, en castellano y editable.
    /// </summary>
    /// <remarks>
    /// Es lo único que un diseñador nombró como bloqueante de la tool. El "cuándo" está repartido en
    /// cuatro campos (<c>Kind</c>, <c>TriggerEvent</c>, <c>ComboFilter</c>, <c>ActionKindFilter</c>)
    /// que aparecen y desaparecen con <c>ShowIf</c> según lo que elijas, dentro del nodo del hook en
    /// el grafo — o sea que para leer cuándo dispara un ítem había que seleccionar el nodo, entender
    /// que <c>EventBus</c> significa "evento del combate" y traducir <c>OnDamageIncoming</c>. Acá se
    /// ve armado y de entrada, con los nombres de <see cref="ItemTriggerCatalog"/>.
    /// </remarks>
    public sealed partial class ItemEditorWindow
    {
        static string[] _triggerLabels;
        List<string> _knownComboIds;

        static string[] TriggerLabels =>
            _triggerLabels ??= ItemTriggerCatalog.All.Select(o => o.DisplayName).ToArray();

        /// <summary>Los combos del proyecto. Cachean: resolverlos escanea assets y esto corre por repaint.</summary>
        List<string> KnownComboIds =>
            _knownComboIds ??= BaseComboSO.GetKnownComboIds().OrderBy(id => id).ToList();

        partial void OnTriggerAssetsRefreshed()
        {
            _knownComboIds = null;
        }

        /// <summary>
        /// Hace que el nodo del hook en el grafo diga "Cuando te pegan" en vez de volcar sus campos.
        /// </summary>
        /// <remarks>
        /// Se registra en el <c>static</c> del tipo y no al abrir la ventana: el grafo se dibuja con
        /// nodos de hooks también cuando el registro no corrió (otra ventana, un dominio recién
        /// recargado), y el describer no depende de que haya una instancia viva.
        /// </remarks>
        static ItemEditorWindow()
        {
            Polymorphic.Graph.BlockNodeDescription.Register<PassiveItemHook>(
                ItemTriggerCatalog.Describe);
        }

        partial void DrawTriggerExtras(ItemSO asset)
        {
            if (asset == null || asset.Type != ItemType.Passive) return;
            if (!DrawExtrasSection("Cuándo")) return;

            var hooks = asset.PassiveHooks;
            if (hooks == null || hooks.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Este ítem no tiene ningún disparador: nunca se ejecuta.", MessageType.Warning);
                if (GUILayout.Button("Agregar disparador")) AddHook(asset);
                return;
            }

            for (int i = 0; i < hooks.Count; i++)
            {
                if (hooks[i] == null) continue;
                if (i > 0) EditorGUILayout.Space(6);
                DrawHookTrigger(hooks[i], hooks.Count > 1 ? i + 1 : 0);
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Agregar disparador")) AddHook(asset);
        }

        void DrawHookTrigger(PassiveItemHook hook, int ordinal)
        {
            var matched = ItemTriggerCatalog.Match(hook);

            if (ordinal > 0)
                EditorGUILayout.LabelField($"Disparador {ordinal}", EditorStyles.miniBoldLabel);

            // Un hook que solo lleva modificadores persistentes no escucha nada: rinde mientras el
            // item este en el inventario. Ofrecer el desplegable ahi seria mentir — elegir un evento
            // no cambiaria cuando se aplica.
            if (ItemTriggerCatalog.IsPermanent(hook))
            {
                EditorGUILayout.LabelField("Dispara cuando", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("Mientras lo tengas en el inventario");
                DrawHelp("Es un modificador permanente: se aplica al conseguir el ítem y se saca al " +
                         "perderlo. No usa evento.");
                return;
            }

            EditorGUILayout.LabelField("Dispara cuando", EditorStyles.miniBoldLabel);

            // El desplegable va a lo ancho y con el titulo arriba: las frases del catalogo son
            // oraciones enteras ("Cuando jugás un combo específico") y en la columna de valor de un
            // panel angosto entraba la mitad.
            int current = matched.HasValue ? IndexOf(matched.Value.Id) : -1;
            int next = EditorGUILayout.Popup(current, TriggerLabels);
            if (next != current && next >= 0)
            {
                var option = ItemTriggerCatalog.All[next];
                Context.Mutate("Change Item Trigger", () => ItemTriggerCatalog.Apply(hook, option));
            }

            if (!matched.HasValue)
            {
                EditorGUILayout.HelpBox(
                    $"Hoy dispara con '{hook.TriggerEvent}', que ningún ítem puede escuchar: " +
                    "nunca se va a ejecutar. Elegí uno de la lista.", MessageType.Error);
                return;
            }

            DrawHelp(matched.Value.Help);
            var opt = matched.Value;

            if (!opt.FiltersByEntity)
                EditorGUILayout.HelpBox(
                    "Este evento no dice a quién le pasó, así que el ítem no puede filtrar por " +
                    "el jugador.", MessageType.Info);

            if (opt.Kind != PassiveHookKind.ComboPlayed) return;

            if (opt.UsesComboIds) DrawComboPicker(hook);
            DrawActionKindFilter(hook);
        }

        /// <summary>
        /// Los combos que escucha el hook, en un desplegable de selección múltiple.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Odin lo dibuja como una lista de strings con un <c>ValueDropdown</c>: para sumar un combo
        /// hay que apretar "+", abrir el desplegable del elemento nuevo y elegir. Una casilla por
        /// combo lo arregla pero se come el panel, y son diez combos que casi siempre están casi
        /// todos apagados.
        /// </para>
        /// <para>
        /// No es un <c>MaskField</c> porque el de Unity se cierra en cada clic: marcar tres combos
        /// son tres aperturas. Este es un <see cref="PopupWindowContent"/> propio, que queda abierto
        /// hasta que hacés clic afuera.
        /// </para>
        /// </remarks>
        void DrawComboPicker(PassiveItemHook hook)
        {
            hook.ComboFilter ??= new ComboFilter();
            hook.ComboFilter.ComboIds ??= new List<string>();

            var ids = KnownComboIds;
            if (ids.Count == 0)
            {
                EditorGUILayout.HelpBox("No se encontró ningún combo en el proyecto.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Combos", EditorStyles.miniBoldLabel);

            var selected = hook.ComboFilter.ComboIds;
            var rect = EditorGUILayout.GetControlRect();
            if (EditorGUI.DropdownButton(rect, new GUIContent(ComboSummary(selected, ids.Count)), FocusType.Keyboard))
            {
                // Se escribe con RecordUndo + MarkDirty pero SIN Notify: notificar reconstruye el
                // grafo, y hacerlo en cada tilde con el popup abierto es churn que ademas puede
                // robarle el foco y cerrarlo. El grafo se entera una sola vez, al cerrar.
                PopupWindow.Show(rect, new ComboMaskDropdown(
                    ids, rect.width,
                    id => selected.Contains(id),
                    (id, on) =>
                    {
                        Context.RecordUndo(on ? "Add Item Trigger Combo" : "Remove Item Trigger Combo");
                        if (on) { if (!selected.Contains(id)) selected.Add(id); }
                        else selected.Remove(id);
                        Context.MarkDirty();
                    },
                    all =>
                    {
                        Context.RecordUndo("Set Item Trigger Combos");
                        selected.Clear();
                        if (all) selected.AddRange(ids);
                        Context.MarkDirty();
                    },
                    () => Context.Notify()));
            }

            if (selected.Count == 0)
                EditorGUILayout.HelpBox(
                    "Sin ningún combo elegido no dispara nunca.", MessageType.Warning);
        }

        /// <summary>Qué dice el botón cerrado. Con más de dos, el detalle no entra y no aporta.</summary>
        static string ComboSummary(List<string> selected, int total)
        {
            if (selected.Count == 0) return "Ningún combo";
            if (selected.Count == total) return "Todos los combos";
            if (selected.Count <= 2)
                return string.Join(", ", selected.Select(ShortComboLabel));
            return $"{selected.Count} combos";
        }

        /// <summary>
        /// Desplegable de selección múltiple que no se cierra al marcar.
        /// </summary>
        /// <remarks>
        /// Escribe a medida que se clickea, no al cerrar: cerrar un popup de Unity es hacer clic
        /// afuera, y un cambio que solo se guarda ahí se pierde si el usuario aprieta Escape.
        /// </remarks>
        sealed class ComboMaskDropdown : PopupWindowContent
        {
            const float RowHeight = 18f;
            const float HeaderHeight = 24f;
            const float MaxHeight = 320f;

            readonly IReadOnlyList<string> _ids;
            readonly float _width;
            readonly System.Func<string, bool> _isOn;
            readonly System.Action<string, bool> _toggle;
            readonly System.Action<bool> _setAll;
            readonly System.Action _onClosed;
            Vector2 _scroll;

            public ComboMaskDropdown(
                IReadOnlyList<string> ids, float width,
                System.Func<string, bool> isOn,
                System.Action<string, bool> toggle,
                System.Action<bool> setAll,
                System.Action onClosed)
            {
                _ids = ids;
                _width = Mathf.Max(180f, width);
                _isOn = isOn;
                _toggle = toggle;
                _setAll = setAll;
                _onClosed = onClosed;
            }

            /// <summary>Recién acá se le avisa al grafo: una reconstrucción, no una por tilde.</summary>
            public override void OnClose() => _onClosed?.Invoke();

            public override Vector2 GetWindowSize() =>
                new Vector2(_width, Mathf.Min(MaxHeight, HeaderHeight + _ids.Count * RowHeight + 8f));

            public override void OnGUI(Rect rect)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    if (GUILayout.Button("Todos", EditorStyles.toolbarButton)) _setAll(true);
                    if (GUILayout.Button("Ninguno", EditorStyles.toolbarButton)) _setAll(false);
                }

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                foreach (var id in _ids)
                {
                    bool on = _isOn(id);
                    bool next = EditorGUILayout.ToggleLeft(
                        ShortComboLabel(id), on, GUILayout.Height(RowHeight));
                    if (next != on) _toggle(id, next);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>
        /// A qué tipo de acción se limita el hook.
        /// </summary>
        /// <remarks>
        /// No es cosmético: ataque, defensa y curación comparten el mismo play scratch, así que un
        /// bono de daño sin restringir se filtra a la curación (BUG-080). El campo existía y se
        /// llamaba <c>ActionKindFilter</c> con <c>Unknown</c> como "sin filtro" — dos cosas que
        /// nadie que no haya escrito el motor va a adivinar.
        /// </remarks>
        void DrawActionKindFilter(PassiveItemHook hook)
        {
            var kinds = new[]
            {
                RollActionKind.Unknown, RollActionKind.Attack,
                RollActionKind.Defense, RollActionKind.Heal,
            };
            var labels = new[] { "Cualquier acción", "Sólo ataques", "Sólo defensas", "Sólo curaciones" };

            int current = System.Array.IndexOf(kinds, hook.ActionKindFilter);
            if (current < 0) current = 0;

            EditorGUILayout.LabelField("Limitado a", EditorStyles.miniBoldLabel);
            int next = EditorGUILayout.Popup(current, labels);
            if (next == current) return;

            var picked = kinds[next];
            Context.Mutate("Change Item Trigger Action Kind", () => hook.ActionKindFilter = picked);
        }

        void AddHook(ItemSO asset)
        {
            Context.Mutate("Add Item Trigger", () =>
            {
                asset.PassiveHooks ??= new List<PassiveItemHook>();
                var hook = new PassiveItemHook();
                ItemTriggerCatalog.Apply(hook, ItemTriggerCatalog.All[0]);
                asset.PassiveHooks.Add(hook);
            });
        }

        /// <summary>
        /// La linea de ayuda, a todo el ancho y envolviendo.
        /// </summary>
        /// <remarks>
        /// Iba como valor de un <c>LabelField</c> con label vacio: eso la mete en la columna de
        /// valor, que en este panel son ~150 px, y <c>miniLabel</c> no envuelve — se cortaba a la
        /// mitad de la primera frase.
        /// </remarks>
        static void DrawHelp(string text)
        {
            _helpStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            EditorGUILayout.LabelField(text, _helpStyle);
        }

        static GUIStyle _helpStyle;

        static int IndexOf(string optionId)
        {
            for (int i = 0; i < ItemTriggerCatalog.All.Count; i++)
                if (ItemTriggerCatalog.All[i].Id == optionId) return i;
            return -1;
        }

        /// <summary>"combo.full_house" → "full house". El prefijo es igual en todos y sólo gasta ancho.</summary>
        static string ShortComboLabel(string id) =>
            string.IsNullOrEmpty(id) ? id : id.Replace("combo.", string.Empty).Replace('_', ' ');
    }
}
