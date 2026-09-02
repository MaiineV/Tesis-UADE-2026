using Rollgeon.Patterns.Catalogs;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Polymorphic
{
    public abstract partial class BlockEditorWindow<T> where T : ScriptableObject
    {
        // ============================ CRUD ============================

        // ---- host hooks ----------------------------------------------------

        /// <summary>Folder new assets are created in, e.g. <c>Assets/Rollgeon/Items</c>.</summary>
        protected abstract string DefaultFolder { get; }

        /// <summary>File name stem for new assets, e.g. <c>Item_New</c>.</summary>
        protected abstract string NewAssetName { get; }

        /// <summary>The asset's authored id. Drives the catalog check and the rename suggestion.</summary>
        protected virtual string IdOf(T asset) => null;

        /// <summary>
        /// File name this asset should carry, derived from its id — e.g. <c>Item_PotionHealing</c>.
        /// Null disables the rename button.
        /// </summary>
        protected virtual string SuggestedAssetName(T asset) => null;

        /// <summary>
        /// Lets the host take over the Create button, e.g. to open an authoring wizard.
        /// </summary>
        /// <returns><c>true</c> when the host handled it; the shell then creates nothing.</returns>
        /// <remarks>
        /// Interception rather than a post-hook because a wizard has to run <i>before</i> the asset
        /// exists — it decides the id, and <c>§3.2</c> of the spec freezes the id at creation, so an
        /// asset created first and edited after would already carry the wrong one. A host that takes
        /// over is responsible for calling <see cref="RefreshAndSelect"/> when its flow ends, and for
        /// grouping its multi-asset writes with
        /// <see cref="PolymorphicAuthoringContext.UndoGroup"/>.
        /// </remarks>
        protected virtual bool TryBeginCreate() => false;

        /// <summary>
        /// Called after the shell created <paramref name="asset"/> and before it is selected, so the
        /// host can stamp defaults (id, rarity, price) inside the same undo step.
        /// </summary>
        protected virtual void OnAssetCreated(T asset) { }

        /// <summary>
        /// Lets the host take over the Duplicate button.
        /// </summary>
        /// <returns><c>true</c> when the host handled it; the shell then copies nothing.</returns>
        /// <remarks>
        /// Exists because plain duplication copies the id verbatim, which is the one reliable way to
        /// end up with two assets sharing an id (<c>§3.4</c>). A host that splits Duplicate into
        /// "new item" vs "new variant" intercepts here.
        /// </remarks>
        protected virtual bool TryBeginDuplicate(T source) => false;

        /// <summary>
        /// Called after the shell copied <paramref name="source"/> into <paramref name="copy"/> and
        /// before the copy is selected — the place to derive a fresh id instead of inheriting one.
        /// </summary>
        protected virtual void OnAssetDuplicated(T source, T copy) { }

        /// <summary>
        /// Lets the host take over the Delete button.
        /// </summary>
        /// <returns><c>true</c> when the host handled it; the shell then deletes nothing.</returns>
        /// <remarks>
        /// Exists for the same reason as <see cref="TryBeginCreate"/>: the shell's delete only
        /// removes the file, but a family whose creation writes catalog, pool and localization
        /// entries needs its deletion to clean the same four places — and only a domain service
        /// knows them. A host that takes over owns the confirmation dialog too.
        /// </remarks>
        protected virtual bool TryBeginDelete(T selected) => false;

        /// <summary>
        /// Re-scans the project and selects <paramref name="asset"/>. The way a host flow that
        /// created assets on its own (a wizard, a variant generator) hands control back to the shell.
        /// </summary>
        /// <summary>
        /// Selecciona <paramref name="asset"/> sin re-escanear el proyecto.
        /// </summary>
        /// <remarks>
        /// Para saltar a un asset que ya esta en la lista — una fila de la tab de metricas, por
        /// ejemplo. <see cref="RefreshAndSelect"/> ademas rebuildea, que es lo correcto despues de
        /// crear algo pero desperdicio cuando el asset ya existe.
        /// </remarks>
        protected void SelectAsset(T asset) => Select(asset);

        protected void RefreshAndSelect(T asset)
        {
            RefreshList();
            Select(asset);
        }

        // ---- operations ------------------------------------------------------

        void CreateAsset()
        {
            if (TryBeginCreate()) return;

            if (!AssetDatabase.IsValidFolder(DefaultFolder))
            {
                System.IO.Directory.CreateDirectory(DefaultFolder);
                AssetDatabase.Refresh();
            }
            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultFolder}/{NewAssetName}.asset");
            var asset = CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);

            // File operations don't enter the undo stack on their own (spec §7.4). Without this the
            // asset survives a Ctrl+Z that undid everything the author did to it, leaving a stub.
            Undo.RegisterCreatedObjectUndo(asset, $"Create {typeof(T).Name}");

            AssetDatabase.SaveAssets();
            OnAssetCreated(asset);
            RefreshAndSelect(asset);
        }

        void DuplicateSelected()
        {
            if (_selected == null) return;

            var source = _selected;
            if (TryBeginDuplicate(source)) return;

            string src = AssetDatabase.GetAssetPath(source);
            string dst = AssetDatabase.GenerateUniqueAssetPath(src);
            if (!AssetDatabase.CopyAsset(src, dst)) return;
            AssetDatabase.SaveAssets();

            var copy = AssetDatabase.LoadAssetAtPath<T>(dst);
            if (copy != null) Undo.RegisterCreatedObjectUndo(copy, $"Duplicate {typeof(T).Name}");

            OnAssetDuplicated(source, copy);
            RefreshAndSelect(copy);
        }

        /// <remarks>
        /// Deliberately <b>not</b> undoable (spec §7.4): restoring a deleted asset would restore it
        /// under a new instance id, so every pool and catalog entry that pointed at it would stay
        /// broken anyway. Confirmation is the protection instead.
        /// </remarks>
        void DeleteSelected()
        {
            if (_selected == null) return;
            if (TryBeginDelete(_selected)) return;
            if (!EditorUtility.DisplayDialog(
                    "Delete asset",
                    $"Delete '{LabelOf(_selected)}'? This cannot be undone.\n\nAnything referencing it " +
                    "(pools, catalogs) will be left with a missing reference.",
                    "Delete", "Cancel")) return;

            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(_selected));
            AssetDatabase.SaveAssets();
            Select(null);
            RefreshList();
        }

        // ---- root-node actions ----------------------------------------------

        /// <summary>
        /// Renames the asset file after its id. Without it every asset keeps the "_New" stem the
        /// Create button gave it, and a folder of Item_New 1 / Item_New 2 is unnavigable.
        /// </summary>
        void DrawRenameButton()
        {
            string suggested = SuggestedAssetName(_selected);
            if (string.IsNullOrEmpty(suggested)) return;
            if (suggested == _selected.name) return;

            EditorGUILayout.Space(4);
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);
            if (GUILayout.Button($"Rename asset  →  {suggested}", GUILayout.Height(22f)))
                RenameAsset(suggested);
            GUI.backgroundColor = prev;
        }

        /// <remarks>
        /// Not undoable either (spec §7.4), and it doesn't need to be: the rename is GUID-stable, so
        /// nothing breaks and the author can just rename it back.
        /// </remarks>
        void RenameAsset(string newName)
        {
            string path = AssetDatabase.GetAssetPath(_selected);
            // Renaming is GUID-stable, so pools and catalogs keep pointing at it.
            string error = AssetDatabase.RenameAsset(path, newName);
            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Rename failed", error, "OK");
                return;
            }
            AssetDatabase.SaveAssets();
            RefreshList();
        }

        /// <summary>
        /// Registers the asset in whatever catalog lists its type. An unregistered asset is
        /// invisible to everything that resolves by id — shops, effects, the dev console.
        /// </summary>
        void DrawCatalogButton()
        {
            var catalog = Catalog;
            if (catalog == null) return;

            string id = IdOf(_selected);
            if (string.IsNullOrEmpty(id))
            {
                EditorGUILayout.HelpBox("Set an id before registering this in a catalog.", MessageType.Info);
                return;
            }

            if (catalog.Contains(id))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"✓ Registered in {catalog.name}", EditorStyles.miniLabel);
                    if (GUILayout.Button("Ping", GUILayout.Width(44f))) EditorGUIUtility.PingObject(catalog);
                }
                return;
            }

            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.55f, 0.95f, 0.6f);
            if (GUILayout.Button($"Add to {catalog.name}", GUILayout.Height(24f)))
            {
                if (catalog.EditorAdd(_selected))
                {
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[{GetType().Name}] '{id}' registered in {catalog.name}.", catalog);
                }
            }
            GUI.backgroundColor = prev;
        }

        /// <summary>
        /// First catalog asset in the project whose entry type is <typeparamref name="T"/>. Found by
        /// type rather than by a per-window override — one catalog per family is the convention, and
        /// a hardcoded path would rot the moment someone moves the asset.
        /// </summary>
        /// <remarks>
        /// <b>Cacheado, y no por prolijidad.</b> Esto lo llama <c>DrawCatalogButton</c>, que corre en
        /// <b>cada repaint</b> del panel. Sin caché, cada frame hacía un <c>FindAssets</c> más un
        /// <c>LoadAssetAtPath</c> por cada catálogo del proyecto hasta dar con el suyo: medido, ~13 ms
        /// por frame. Escribir en cualquier campo del panel pagaba eso por tecla.
        /// <para>
        /// Se guarda la <b>referencia</b>, no su contenido, así que el estado que se muestra sigue
        /// siendo el real. Se suelta en <c>OnAssetsRefreshed</c>, cuando el proyecto cambió y el
        /// asset pudo haberse movido o borrado.
        /// </para>
        /// </remarks>
        protected BaseCatalogSO<T> Catalog
        {
            get
            {
                if (_catalog != null) return _catalog;

                foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(BaseCatalogSO)))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.LoadAssetAtPath<BaseCatalogSO>(path) is BaseCatalogSO<T> typed)
                    {
                        _catalog = typed;
                        return _catalog;
                    }
                }
                return null;
            }
        }

        BaseCatalogSO<T> _catalog;

        /// <summary>Suelta el catálogo cacheado. La llama el shell al rebuildear la lista.</summary>
        void InvalidateCatalogCache() => _catalog = null;
    }
}
