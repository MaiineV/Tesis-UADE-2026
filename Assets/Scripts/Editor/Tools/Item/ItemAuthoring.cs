using System;
using System.Collections.Generic;
using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.EditorTools.Localization;
using Rollgeon.Items;
using Rollgeon.Localization;
using Rollgeon.Shop;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// Single entry point that authors <see cref="ItemSO"/> assets from a data specification (spec
    /// §6.7 "un unico punto de entrada en C#"). Service only — no UI. Two clients are expected: the
    /// Fase 3 creation wizard and the Fase 4 MCP skill; both build a spec and call this.
    /// </summary>
    public static class ItemAuthoring
    {
        /// <summary>Default folder for new item assets — same one <c>ItemEditorWindow</c> uses.</summary>
        public const string DefaultFolder = "Assets/Rollgeon/Items";

        // ---- creation ---------------------------------------------------------------------------

        /// <summary>
        /// Creates one item (spec §6.2, operation 1). Validates everything up front — id derivation,
        /// global uniqueness, catalog/pool availability — before writing a single asset: a failed
        /// validation never leaves a half-created item. The four writes (asset, catalog, ES/EN
        /// localization, shop price) land in one undo step.
        /// </summary>
        public static ItemCreationResult CreateItem(ItemCreationSpec spec)
        {
            var errors = new List<string>();
            var catalog = LoadCatalog();
            if (catalog == null) errors.Add("ItemCatalogSO asset not found in the project.");
            var pool = ItemShopPriceBridge.LoadDefaultPool();
            if (pool == null) errors.Add($"ShopPoolSO not found at '{ItemShopPriceBridge.DefaultShopPoolPath}'.");

            var claimed = new HashSet<string>();
            bool ok = TryPrepare(
                spec.DisplayName, spec.Description, spec.Icon, spec.Rarity, spec.Type,
                spec.FamilyId, spec.VariantIndex ?? 0, spec.BasePrice, spec.TargetFolder,
                new LocalizedText(spec.DisplayNameEn, spec.DescriptionEn),
                claimed, errors, out var prepared);

            var trigger = ResolveTrigger(spec.TriggerId, spec.Type, errors);

            if (!ok || errors.Count > 0) return new ItemCreationResult(errors);

            using (PolymorphicAuthoringContext.UndoGroup("Create Item"))
            {
                var item = WriteItem(prepared, catalog, pool);
                ApplyTrigger(item, trigger, spec.TriggerComboIds);
                return new ItemCreationResult(item, item.ItemId, AssetDatabase.GetAssetPath(item));
            }
        }

        /// <summary>
        /// Creates a whole family of variants (spec §6.2, operation 2; §3 rule 4 "Agregar variante a
        /// la familia"). All variants share <see cref="ItemFamilyCreationSpec.FamilyId"/> and land in
        /// one undo step. Every variant is validated — including collisions between variants in the
        /// same request — before any asset is written.
        /// </summary>
        public static ItemFamilyCreationResult CreateFamily(ItemFamilyCreationSpec spec)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(spec.FamilyId))
                errors.Add("FamilyId is required for a family creation.");
            if (spec.Variants == null || spec.Variants.Count == 0)
                errors.Add("At least one variant is required.");

            var catalog = LoadCatalog();
            if (catalog == null) errors.Add("ItemCatalogSO asset not found in the project.");
            var pool = ItemShopPriceBridge.LoadDefaultPool();
            if (pool == null) errors.Add($"ShopPoolSO not found at '{ItemShopPriceBridge.DefaultShopPoolPath}'.");

            if (errors.Count > 0) return new ItemFamilyCreationResult(errors);

            var claimed = new HashSet<string>();
            var prepared = new List<PreparedItem>(spec.Variants.Count);
            for (int i = 0; i < spec.Variants.Count; i++)
            {
                var v = spec.Variants[i];
                var description = string.IsNullOrEmpty(v.Description) ? spec.DefaultDescription : v.Description;
                var icon = v.Icon != null ? v.Icon : spec.DefaultIcon;
                var variantIndex = v.VariantIndex ?? i;

                bool ok = TryPrepare(
                    v.DisplayName, description, icon, v.Rarity, spec.Type,
                    spec.FamilyId, variantIndex, v.BasePrice, spec.TargetFolder,
                    new LocalizedText(v.DisplayNameEn, v.DescriptionEn),
                    claimed, errors, out var p);
                if (ok) prepared.Add(p);
            }

            var trigger = ResolveTrigger(spec.TriggerId, spec.Type, errors);

            if (errors.Count > 0) return new ItemFamilyCreationResult(errors);

            using (PolymorphicAuthoringContext.UndoGroup("Create Item Family"))
            {
                var items = new List<ItemSO>(prepared.Count);
                foreach (var p in prepared)
                {
                    var item = WriteItem(p, catalog, pool);
                    ApplyTrigger(item, trigger, spec.TriggerComboIds);
                    items.Add(item);
                }
                return new ItemFamilyCreationResult(items);
            }
        }

        // ---- rename -------------------------------------------------------------------------------

        /// <summary>
        /// Renames <paramref name="item"/>'s id (spec §3 rule 3). Explicit action, separate from
        /// editing Display Name: also renames the two localization keys
        /// (<c>&lt;oldId&gt;.name</c>/<c>.desc</c> → <c>&lt;newId&gt;.name</c>/<c>.desc</c>). The
        /// result's <see cref="ItemRenameResult.BreaksSaveCompatibility"/> is always true on success —
        /// ItemId is a save key (TECHNICAL.md §18) and this call never migrates saves; the caller (the
        /// Fase 3 UI) is responsible for warning the author before committing to it.
        /// </summary>
        public static ItemRenameResult RenameItemId(ItemSO item, string newItemId)
        {
            if (item == null) return new ItemRenameResult("Item is null.");
            if (string.IsNullOrWhiteSpace(newItemId)) return new ItemRenameResult("New id is required.");
            if (newItemId == item.ItemId) return new ItemRenameResult($"'{newItemId}' is already this item's id.");

            if (!IsIdAvailable(newItemId, out var owner))
            {
                var ownerPath = owner != null ? AssetDatabase.GetAssetPath(owner) : "<unknown>";
                return new ItemRenameResult($"Id '{newItemId}' is already used by '{ownerPath}'.");
            }

            var oldId = item.ItemId;

            using (PolymorphicAuthoringContext.UndoGroup("Rename Item Id"))
            {
                Undo.RecordObject(item, "Rename Item Id");
                item.ItemId = newItemId;
                EditorUtility.SetDirty(item);

                MoveLocalizationKeys(oldId, newItemId);
            }

            return new ItemRenameResult(oldId, newItemId);
        }

        // ---- uniqueness ---------------------------------------------------------------------------

        /// <summary>
        /// True if no <see cref="ItemSO"/> in the project already uses <paramref name="candidateId"/>.
        /// Global check (spec §3 rule 1) — scans every <c>ItemSO</c> asset via
        /// <c>AssetDatabase.FindAssets</c>, not just the ones registered in <c>ItemCatalog</c>, since
        /// items don't all live in one folder and a stray/unregistered asset still owns its id.
        /// Public: the Fase 3 list uses this to flag duplicate ids on the fly.
        /// </summary>
        public static bool IsIdAvailable(string candidateId, out ItemSO owner)
        {
            owner = null;
            if (string.IsNullOrEmpty(candidateId)) return false;

            foreach (var so in EnumerateAllItemAssets())
            {
                if (so.ItemId != candidateId) continue;
                owner = so;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Snapshot de <c>itemId → dueño</c> de todo el proyecto, para consultar muchas veces sin
        /// volver a tocar disco.
        /// </summary>
        /// <remarks>
        /// <see cref="IsIdAvailable"/> escanea con <c>FindAssets</c> y carga cada asset: medido, ~12 ms
        /// por llamada. Sirve para una consulta puntual, pero no para una UI que valida mientras el
        /// usuario escribe. Quien llame se queda con el snapshot y decide cuándo renovarlo — el
        /// servicio no cachea nada por su cuenta, porque el momento correcto de invalidar lo sabe la
        /// UI (al abrir un formulario), no esta clase.
        /// <para>
        /// Un id duplicado en disco deja al primero encontrado como dueño, igual que
        /// <see cref="IsIdAvailable"/>.
        /// </para>
        /// </remarks>
        public static Dictionary<string, ItemSO> BuildIdOwnerSnapshot()
        {
            var map = new Dictionary<string, ItemSO>(StringComparer.Ordinal);
            foreach (var so in EnumerateAllItemAssets())
            {
                if (so == null || string.IsNullOrEmpty(so.ItemId)) continue;
                if (!map.ContainsKey(so.ItemId)) map[so.ItemId] = so;
            }
            return map;
        }

        static IEnumerable<ItemSO> EnumerateAllItemAssets()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(ItemSO));
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ItemSO>(path);
                if (so != null) yield return so;
            }
        }

        // ---- validation + write ---------------------------------------------------------------------

        /// <summary>Fully-validated, ready-to-write item. Only <see cref="TryPrepare"/> constructs one.</summary>
        /// <summary>
        /// Nombre y descripcion en un idioma. Vacio = usar el texto autor.
        /// </summary>
        /// <remarks>
        /// Existe para no sumarle dos parametros mas a <c>TryPrepare</c>, que ya toma doce. El
        /// ingles no se valida — es texto libre y opcional —, asi que viaja junto sin pasar por las
        /// mismas reglas que el resto de la especificacion.
        /// </remarks>
        public readonly struct LocalizedText
        {
            public readonly string Name;
            public readonly string Description;

            public LocalizedText(string name, string description)
            {
                Name = name;
                Description = description;
            }

            public string NameOr(string fallback) => string.IsNullOrWhiteSpace(Name) ? fallback : Name;
            public string DescriptionOr(string fallback) => string.IsNullOrWhiteSpace(Description) ? fallback : Description;
        }

        readonly struct PreparedItem
        {
            public readonly string ItemId;
            public readonly string DisplayName;
            public readonly string Description;
            public readonly Sprite Icon;
            public readonly ItemRarity Rarity;
            public readonly ItemType Type;
            public readonly string FamilyId;
            public readonly int VariantIndex;
            public readonly int BasePrice;
            public readonly string TargetFolder;

            /// <summary>Textos en ingles. Vacios = se siembra el texto autor en ambos locales.</summary>
            public readonly LocalizedText English;

            public PreparedItem(
                string itemId, string displayName, string description, Sprite icon, ItemRarity rarity,
                ItemType type, string familyId, int variantIndex, int basePrice, string targetFolder,
                LocalizedText english)
            {
                ItemId = itemId;
                DisplayName = displayName;
                Description = description;
                Icon = icon;
                Rarity = rarity;
                Type = type;
                FamilyId = familyId;
                VariantIndex = variantIndex;
                BasePrice = basePrice;
                TargetFolder = targetFolder;
                English = english;
            }
        }

        /// <summary>
        /// Validates one candidate item and, if valid, appends its id to <paramref name="claimedIds"/>
        /// — so a family batch catches two variants deriving the same id before either is written, not
        /// just collisions against disk. Errors accumulate in <paramref name="errors"/>.
        /// </summary>
        static bool TryPrepare(
            string displayName, string description, Sprite icon, ItemRarity rarity, ItemType type,
            string familyId, int variantIndex, int? basePrice, string targetFolder,
            LocalizedText english,
            HashSet<string> claimedIds, List<string> errors, out PreparedItem prepared)
        {
            prepared = default;
            bool valid = true;

            if (string.IsNullOrWhiteSpace(displayName))
            {
                errors.Add("DisplayName is required.");
                valid = false;
            }

            if (!Enum.IsDefined(typeof(ItemRarity), rarity))
            {
                errors.Add($"'{rarity}' is not a valid ItemRarity.");
                valid = false;
            }

            if (!Enum.IsDefined(typeof(ItemType), type))
            {
                errors.Add($"'{type}' is not a valid ItemType.");
                valid = false;
            }

            var folder = string.IsNullOrEmpty(targetFolder) ? DefaultFolder : targetFolder;
            if (!AssetDatabase.IsValidFolder(folder))
            {
                errors.Add($"Target folder '{folder}' does not exist.");
                valid = false;
            }

            if (!valid) return false; // don't bother deriving an id off a bad display name

            var itemId = ItemIdSlug.FromDisplayName(displayName);
            if (string.IsNullOrEmpty(itemId))
            {
                errors.Add($"DisplayName '{displayName}' does not derive a usable id (only separators/symbols).");
                return false;
            }

            if (claimedIds.Contains(itemId))
            {
                errors.Add($"Id '{itemId}' collides with another variant in this same request.");
                return false;
            }

            if (!IsIdAvailable(itemId, out var owner))
            {
                var ownerPath = owner != null ? AssetDatabase.GetAssetPath(owner) : "<unknown>";
                errors.Add($"Id '{itemId}' is already used by '{ownerPath}'.");
                return false;
            }

            claimedIds.Add(itemId);

            var resolvedPrice = basePrice ?? RarityPricing.BasePriceFor(rarity);

            prepared = new PreparedItem(
                itemId, displayName, description ?? string.Empty, icon, rarity, type,
                familyId ?? string.Empty, variantIndex, resolvedPrice, folder, english);
            return true;
        }

        /// <summary>
        /// The four writes (spec §6.2/§7.2): asset + catalog + ES/EN localization + shop price. Caller
        /// wraps this in a <see cref="PolymorphicAuthoringContext.UndoGroup"/>; every write here does
        /// its own Undo/SetDirty (rule 3), so nesting several calls inside one group composes fine.
        /// </summary>
        static ItemSO WriteItem(PreparedItem p, ItemCatalogSO catalog, ShopPoolSO pool)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = p.ItemId;
            item.DisplayName = p.DisplayName;
            item.Description = p.Description;
            item.Icon = p.Icon;
            item.Rarity = p.Rarity;
            item.Type = p.Type;
            item.FamilyId = p.FamilyId;
            item.VariantIndex = p.VariantIndex;

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{p.TargetFolder}/Item_{AssetNaming.PascalCaseId(p.ItemId)}.asset");
            AssetDatabase.CreateAsset(item, assetPath);
            Undo.RegisterCreatedObjectUndo(item, "Create Item");

            catalog.EditorAdd(item);

            // El ingles cae al texto autor cuando no se escribio. Se admite a proposito: deja crear
            // rapido y el test test_localization_no_key_repeats_the_spanish_text_in_english avisa
            // despues cuales quedaron sin traducir. Completar los campos en ingles del asistente
            // evita esa deuda de entrada.
            UpsertLocalizationEntryWithUndo(
                p.ItemId + LocalizedContent.NameSuffix, p.DisplayName, p.English.NameOr(p.DisplayName));
            UpsertLocalizationEntryWithUndo(
                p.ItemId + LocalizedContent.DescSuffix, p.Description, p.English.DescriptionOr(p.Description));

            ItemShopPriceBridge.AddToPool(pool, item, p.BasePrice);

            return item;
        }

        /// <summary>
        /// Traduce el <c>TriggerId</c> de la spec a una opcion del catalogo.
        /// </summary>
        /// <remarks>
        /// Un id que no existe es un <b>error de creacion</b> y no un hook mudo: crear el item igual
        /// pero sin disparador seria repetir en el alta el mismo problema que el catalogo vino a
        /// resolver — un item que no dispara nunca y nadie sabe por que.
        /// </remarks>
        static ItemTriggerCatalog.TriggerOption? ResolveTrigger(
            string triggerId, ItemType type, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(triggerId)) return null;

            if (type != ItemType.Passive)
            {
                errors.Add($"TriggerId '{triggerId}' only applies to Passive items ('{type}' given).");
                return null;
            }

            foreach (var option in ItemTriggerCatalog.All)
                if (option.Id == triggerId) return option;

            errors.Add($"TriggerId '{triggerId}' is not in ItemTriggerCatalog.");
            return null;
        }

        static void ApplyTrigger(
            ItemSO item, ItemTriggerCatalog.TriggerOption? trigger, IReadOnlyList<string> comboIds)
        {
            if (item == null || !trigger.HasValue) return;

            var hook = new PassiveItemHook();
            ItemTriggerCatalog.Apply(hook, trigger.Value);

            if (trigger.Value.UsesComboIds && comboIds != null)
                hook.ComboFilter.ComboIds = new List<string>(comboIds);

            item.PassiveHooks ??= new List<PassiveItemHook>();
            item.PassiveHooks.Add(hook);
            EditorUtility.SetDirty(item);
        }

        static ItemCatalogSO LoadCatalog()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(ItemCatalogSO));
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // ---- localization plumbing -----------------------------------------------------------------

        /// <summary>
        /// Wraps <c>LocalizationSetupTools.UpsertEntry</c> with the <c>Undo.RecordObject</c> it skips
        /// (spec §4/§7 rule 3) — on the shared table data and on each locale's table — before mutating.
        /// </summary>
        static void UpsertLocalizationEntryWithUndo(string key, string es, string en)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizedContent.ContentTable);
            if (collection == null)
                throw new Exception($"[ItemAuthoring] String Table Collection '{LocalizedContent.ContentTable}' not found.");

            Undo.RecordObject(collection.SharedData, "Edit Item Localization");
            if (collection.GetTable(new LocaleIdentifier(LocalizationSetupTools.EsCode)) is StringTable esTable)
                Undo.RecordObject(esTable, "Edit Item Localization");
            if (collection.GetTable(new LocaleIdentifier(LocalizationSetupTools.EnCode)) is StringTable enTable)
                Undo.RecordObject(enTable, "Edit Item Localization");

            LocalizationSetupTools.UpsertEntry(LocalizedContent.ContentTable, key, es, en);
        }

        /// <summary>Moves both localization keys (name+desc) from <paramref name="oldId"/> to <paramref name="newId"/>.</summary>
        static void MoveLocalizationKeys(string oldId, string newId)
        {
            MoveLocalizationKey(oldId + LocalizedContent.NameSuffix, newId + LocalizedContent.NameSuffix);
            MoveLocalizationKey(oldId + LocalizedContent.DescSuffix, newId + LocalizedContent.DescSuffix);
        }

        /// <summary>
        /// Reads <paramref name="oldKey"/>'s ES/EN values, writes them under <paramref name="newKey"/>,
        /// then removes the old shared-table key. No-op if the old key never had localized text.
        /// </summary>
        static void MoveLocalizationKey(string oldKey, string newKey)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizedContent.ContentTable);
            if (collection == null) return; // missing table is a setup problem, not this call's job

            var esTable = collection.GetTable(new LocaleIdentifier(LocalizationSetupTools.EsCode)) as StringTable;
            var enTable = collection.GetTable(new LocaleIdentifier(LocalizationSetupTools.EnCode)) as StringTable;

            var esValue = esTable != null ? esTable.GetEntry(oldKey)?.Value : null;
            var enValue = enTable != null ? enTable.GetEntry(oldKey)?.Value : null;
            if (esValue == null && enValue == null) return;

            UpsertLocalizationEntryWithUndo(newKey, esValue, enValue);

            // Por RemoveKeyEverywhere y no por SharedData.RemoveKey: este último deja las entradas
            // de cada idioma huérfanas en el .asset, con el texto viejo adentro.
            Undo.RecordObject(collection.SharedData, "Rename Item Id");
            RemoveKeyEverywhere(collection, oldKey);
            EditorUtility.SetDirty(collection.SharedData);
        }

        /// <summary>
        /// Borra un ítem deshaciendo las cuatro escrituras que lo dieron de alta: lo saca del
        /// catálogo, lo saca del <c>ShopPool</c>, borra sus dos claves de localización y recién
        /// entonces elimina el asset.
        /// </summary>
        /// <remarks>
        /// <para>
        /// El botón Delete de la ventana borra solo el archivo, y cada una de las otras tres
        /// escrituras queda huérfana: una entry null en el catálogo, un <c>WeightedShopItem</c> sin
        /// ítem que el rolling saltea <b>en silencio</b>, y dos claves muertas en la tabla.
        /// </para>
        /// <para>
        /// <b>El orden importa.</b> Catálogo y pool se limpian con el asset todavía vivo, porque los
        /// dos localizan la entry <i>por referencia al objeto</i>: con el asset ya borrado la
        /// referencia es null y no hay con qué encontrarla.
        /// </para>
        /// <para>
        /// <b>No es undoable.</b> <c>AssetDatabase.DeleteAsset</c> queda fuera del sistema de undo,
        /// así que un Ctrl+Z revertiría las tres primeras limpiezas y dejaría el archivo borrado —
        /// peor que no hacer nada. Por eso no se agrupa en un <c>UndoGroup</c>: que cada paso quede
        /// suelto deja el rastro visible en vez de fingir una atomicidad que no existe.
        /// </para>
        /// </remarks>
        public static ItemDeletionResult DeleteItem(ItemSO item)
        {
            if (item == null) return ItemDeletionResult.Failed("No hay ítem que borrar.");

            string itemId = item.ItemId;
            string assetPath = AssetDatabase.GetAssetPath(item);
            if (string.IsNullOrEmpty(assetPath))
                return ItemDeletionResult.Failed("El ítem no tiene un asset en disco.");

            bool removedFromCatalog = false;
            var catalog = LoadCatalog();
            if (catalog != null) removedFromCatalog = catalog.EditorRemove(item);

            bool removedFromPool = false;
            var pool = ItemShopPriceBridge.LoadDefaultPool();
            if (pool != null) removedFromPool = ItemShopPriceBridge.RemoveFromPool(pool, item);

            int removedKeys = string.IsNullOrEmpty(itemId) ? 0 : RemoveLocalizationKeys(itemId);

            AssetDatabase.SaveAssets();

            if (!AssetDatabase.DeleteAsset(assetPath))
                return ItemDeletionResult.Failed($"No se pudo borrar el asset en '{assetPath}'.");

            AssetDatabase.SaveAssets();
            return ItemDeletionResult.Ok(itemId, assetPath, removedFromCatalog, removedFromPool, removedKeys);
        }

        /// <summary>Borra <c>&lt;itemId&gt;.name</c> y <c>&lt;itemId&gt;.desc</c> de la tabla <c>Content</c>. Devuelve cuántas sacó.</summary>
        static int RemoveLocalizationKeys(string itemId)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizedContent.ContentTable);
            if (collection == null) return 0;

            int removed = 0;
            Undo.RecordObject(collection.SharedData, "Delete Item Localization");
            foreach (var suffix in new[] { LocalizedContent.NameSuffix, LocalizedContent.DescSuffix })
            {
                if (RemoveKeyEverywhere(collection, itemId + suffix)) removed++;
            }

            if (removed == 0) return 0;

            EditorUtility.SetDirty(collection.SharedData);
            return removed;
        }

        /// <summary>
        /// Borra <paramref name="key"/> de la shared data <b>y</b> de cada tabla de idioma.
        /// Devuelve <c>false</c> si la clave no existía.
        /// </summary>
        /// <remarks>
        /// <c>SharedTableData.RemoveKey</c> saca únicamente la <i>definición</i> de la clave. Las
        /// entradas por locale están indexadas por el id numérico que esa definición asignaba, así
        /// que quedan huérfanas: invisibles desde el editor de Localization, pero presentes en el
        /// <c>.asset</c> y visibles en el diff.
        /// <para>
        /// De ahí el orden: primero se resuelve el id y se borran las entradas de cada tabla,
        /// después se quita la clave. Al revés no hay con qué encontrarlas.
        /// </para>
        /// </remarks>
        static bool RemoveKeyEverywhere(StringTableCollection collection, string key)
        {
            var shared = collection.SharedData.GetEntry(key);
            if (shared == null) return false;

            var id = shared.Id;
            foreach (var table in collection.StringTables)
            {
                if (table == null) continue;
                Undo.RecordObject(table, "Delete Item Localization");
                table.RemoveEntry(id);
                EditorUtility.SetDirty(table);
            }

            collection.SharedData.RemoveKey(key);
            return true;
        }
    }
}
