using System;
using System.Collections.Generic;
using Rollgeon.Dice;
using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.EditorTools.Localization;
using Rollgeon.Localization;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.Triggers;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enchantment
{
    /// <summary>
    /// Punto de entrada único que autora <see cref="EnchantmentSO"/> desde una
    /// especificación de datos — espejo de <c>ItemAuthoring</c> (item-editor-spec §6.7:
    /// "un único punto de entrada en C#"). Servicio puro, sin UI. Dos clientes: el
    /// formulario de <c>EnchantmentEditorWindow</c> y la skill MCP; ambos arman una spec
    /// y llaman acá, así no hay dos caminos que puedan divergir.
    /// </summary>
    public static class EnchantmentAuthoring
    {
        /// <summary>Carpeta default de los assets — la misma que usa la ventana.</summary>
        public const string DefaultFolder = "Assets/Rollgeon/Upgrades/Dice/Enchantments";

        // ---- creación ---------------------------------------------------------------

        /// <summary>
        /// Crea un encantamiento. Valida todo por adelantado — derivación del id,
        /// unicidad global, catálogo/pool presentes, categoría ≠ None — antes de escribir
        /// un solo asset: una validación fallida nunca deja un alta a medias. Las cuatro
        /// escrituras (asset, catálogo, localización es/en, entry del pool) caen en un
        /// solo paso de undo.
        /// </summary>
        public static EnchantmentCreationResult CreateEnchantment(EnchantmentCreationSpec spec)
        {
            var errors = new List<string>();
            var catalog = LoadCatalog();
            if (catalog == null) errors.Add("EnchantmentCatalogSO asset not found in the project.");
            var pool = EnchantmentPoolBridge.LoadDefaultPool();
            if (pool == null) errors.Add("EnchantmentPoolSO asset not found in the project.");

            bool ok = TryPrepare(spec, errors, out var prepared);
            var trigger = ResolveTrigger(spec.TriggerId, spec.RequireCarrierParticipates, errors);

            if (!ok || errors.Count > 0) return new EnchantmentCreationResult(errors);

            using (PolymorphicAuthoringContext.UndoGroup("Create Enchantment"))
            {
                var enchantment = WriteEnchantment(prepared, catalog, pool);
                ApplyTrigger(enchantment, trigger, spec.TriggerComboIds, spec.RequireCarrierParticipates);
                return new EnchantmentCreationResult(
                    enchantment, enchantment.UpgradeId, AssetDatabase.GetAssetPath(enchantment));
            }
        }

        // ---- rename -----------------------------------------------------------------

        /// <summary>
        /// Renombra el <c>UpgradeId</c>. Acción explícita, separada de editar el Display
        /// Name: también mueve las dos claves de localización. El resultado marca
        /// <see cref="EnchantmentRenameResult.BreaksSaveCompatibility"/> siempre — el id
        /// es clave de save (los slots del <c>RuntimeDiceBag</c> se restauran por id) y
        /// esta llamada no migra saves. Ojo: el diccionario de
        /// <c>EnchantmentCategoryAssigner</c> y las definiciones de meta-unlock apuntan
        /// por id; actualizarlos queda a cargo del caller.
        /// </summary>
        public static EnchantmentRenameResult RenameEnchantmentId(EnchantmentSO enchantment, string newUpgradeId)
        {
            if (enchantment == null) return new EnchantmentRenameResult("Enchantment is null.");
            if (string.IsNullOrWhiteSpace(newUpgradeId)) return new EnchantmentRenameResult("New id is required.");
            if (newUpgradeId == enchantment.UpgradeId)
                return new EnchantmentRenameResult($"'{newUpgradeId}' is already this enchantment's id.");
            if (!newUpgradeId.StartsWith(EnchantmentIdSlug.Prefix, StringComparison.Ordinal))
                return new EnchantmentRenameResult(
                    $"Id '{newUpgradeId}' must keep the '{EnchantmentIdSlug.Prefix}' channel prefix.");

            if (!IsIdAvailable(newUpgradeId, out var owner))
            {
                var ownerPath = owner != null ? AssetDatabase.GetAssetPath(owner) : "<unknown>";
                return new EnchantmentRenameResult($"Id '{newUpgradeId}' is already used by '{ownerPath}'.");
            }

            var oldId = enchantment.UpgradeId;

            using (PolymorphicAuthoringContext.UndoGroup("Rename Enchantment Id"))
            {
                Undo.RecordObject(enchantment, "Rename Enchantment Id");
                enchantment.EditorSetUpgradeId(newUpgradeId);
                EditorUtility.SetDirty(enchantment);

                ContentLocalizationBridge.MoveEntityKeys(oldId, newUpgradeId, "Rename Enchantment Id");
            }

            return new EnchantmentRenameResult(oldId, newUpgradeId);
        }

        // ---- unicidad ---------------------------------------------------------------

        /// <summary>
        /// True si ningún <see cref="EnchantmentSO"/> del proyecto usa ya ese id. Chequeo
        /// global vía <c>AssetDatabase.FindAssets</c>, no contra el catálogo: un asset
        /// suelto sin registrar igual es dueño de su id.
        /// </summary>
        public static bool IsIdAvailable(string candidateId, out EnchantmentSO owner)
        {
            owner = null;
            if (string.IsNullOrEmpty(candidateId)) return false;

            foreach (var so in EnumerateAllEnchantmentAssets())
            {
                if (so.UpgradeId != candidateId) continue;
                owner = so;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Snapshot de <c>upgradeId → dueño</c> para consultar muchas veces sin volver a
        /// tocar disco (una UI que valida mientras se escribe). El caller decide cuándo
        /// renovarlo. Un id duplicado deja al primero encontrado como dueño.
        /// </summary>
        public static Dictionary<string, EnchantmentSO> BuildIdOwnerSnapshot()
        {
            var map = new Dictionary<string, EnchantmentSO>(StringComparer.Ordinal);
            foreach (var so in EnumerateAllEnchantmentAssets())
            {
                if (so == null || string.IsNullOrEmpty(so.UpgradeId)) continue;
                if (!map.ContainsKey(so.UpgradeId)) map[so.UpgradeId] = so;
            }
            return map;
        }

        static IEnumerable<EnchantmentSO> EnumerateAllEnchantmentAssets()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(EnchantmentSO));
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<EnchantmentSO>(path);
                if (so != null) yield return so;
            }
        }

        // ---- validación + escritura -------------------------------------------------

        /// <summary>Spec validada y lista para escribir. Solo <see cref="TryPrepare"/> construye una.</summary>
        readonly struct PreparedEnchantment
        {
            public readonly string UpgradeId;
            public readonly string DisplayName;
            public readonly string Description;
            public readonly string DisplayNameEn;
            public readonly string DescriptionEn;
            public readonly Sprite Icon;
            public readonly EnchantmentCategory Category;
            public readonly IReadOnlyList<DiceType> AllowedDiceTypes;
            public readonly float PoolWeight;
            public readonly int MinFloorDepth;
            public readonly string TargetFolder;

            public PreparedEnchantment(
                string upgradeId, string displayName, string description,
                string displayNameEn, string descriptionEn, Sprite icon,
                EnchantmentCategory category, IReadOnlyList<DiceType> allowedDiceTypes,
                float poolWeight, int minFloorDepth, string targetFolder)
            {
                UpgradeId = upgradeId;
                DisplayName = displayName;
                Description = description;
                DisplayNameEn = displayNameEn;
                DescriptionEn = descriptionEn;
                Icon = icon;
                Category = category;
                AllowedDiceTypes = allowedDiceTypes;
                PoolWeight = poolWeight;
                MinFloorDepth = minFloorDepth;
                TargetFolder = targetFolder;
            }
        }

        static bool TryPrepare(
            EnchantmentCreationSpec spec, List<string> errors, out PreparedEnchantment prepared)
        {
            prepared = default;
            bool valid = true;

            if (string.IsNullOrWhiteSpace(spec.DisplayName))
            {
                errors.Add("DisplayName is required.");
                valid = false;
            }

            // La auditoría (AllEnchantmentAssets_HaveACategoryAssigned) rechaza None: mejor
            // fallar el alta que crear un asset que rompe la suite.
            if (!Enum.IsDefined(typeof(EnchantmentCategory), spec.Category))
            {
                errors.Add($"'{spec.Category}' is not a valid EnchantmentCategory.");
                valid = false;
            }
            else if (spec.Category == EnchantmentCategory.None)
            {
                errors.Add("Category is required (None fails the asset audit).");
                valid = false;
            }

            if (spec.AllowedDiceTypes != null)
            {
                foreach (var type in spec.AllowedDiceTypes)
                {
                    if (Enum.IsDefined(typeof(DiceType), type)) continue;
                    errors.Add($"'{type}' is not a valid DiceType.");
                    valid = false;
                }
            }

            float weight = spec.PoolWeight ?? 1f;
            if (weight < 0f)
            {
                errors.Add($"PoolWeight must be >= 0 ('{weight}' given; 0 = registered but disabled).");
                valid = false;
            }

            int minFloorDepth = spec.MinFloorDepth ?? 0;
            if (minFloorDepth < 0)
            {
                errors.Add($"MinFloorDepth must be >= 0 ('{minFloorDepth}' given).");
                valid = false;
            }

            var folder = string.IsNullOrEmpty(spec.TargetFolder) ? DefaultFolder : spec.TargetFolder;
            if (!AssetDatabase.IsValidFolder(folder))
            {
                errors.Add($"Target folder '{folder}' does not exist.");
                valid = false;
            }

            if (!valid) return false; // no derivar un id de un display name inválido

            var upgradeId = EnchantmentIdSlug.FromDisplayName(spec.DisplayName);
            if (string.IsNullOrEmpty(upgradeId))
            {
                errors.Add($"DisplayName '{spec.DisplayName}' does not derive a usable id (only separators/symbols).");
                return false;
            }

            if (!IsIdAvailable(upgradeId, out var owner))
            {
                var ownerPath = owner != null ? AssetDatabase.GetAssetPath(owner) : "<unknown>";
                errors.Add($"Id '{upgradeId}' is already used by '{ownerPath}'.");
                return false;
            }

            prepared = new PreparedEnchantment(
                upgradeId, spec.DisplayName, spec.Description ?? string.Empty,
                spec.DisplayNameEn, spec.DescriptionEn, spec.Icon,
                spec.Category, spec.AllowedDiceTypes, weight, minFloorDepth, folder);
            return true;
        }

        /// <summary>
        /// Las cuatro escrituras: asset + catálogo + localización es/en + entry del pool.
        /// El caller envuelve esto en un <c>UndoGroup</c>; cada escritura hace su propio
        /// Undo/SetDirty (spec §7 regla 3), así que componen bien dentro del grupo.
        /// </summary>
        static EnchantmentSO WriteEnchantment(
            PreparedEnchantment p, EnchantmentCatalogSO catalog, EnchantmentPoolSO pool)
        {
            var enchantment = ScriptableObject.CreateInstance<EnchantmentSO>();
            enchantment.EditorSetUpgradeId(p.UpgradeId);
            enchantment.EditorSetDisplayName(p.DisplayName);
            enchantment.EditorSetDescription(p.Description);
            enchantment.EditorSetIcon(p.Icon);
            enchantment.EditorSetCategory(p.Category);
            if (p.AllowedDiceTypes != null && p.AllowedDiceTypes.Count > 0)
                enchantment.EditorSetAllowedDiceTypes(new List<DiceType>(p.AllowedDiceTypes));

            // El nombre de archivo dropea el prefijo del canal: ench.multiplo_de_3 →
            // Ench_MultiploDe3 (misma regla que EnchantmentEditorWindow.SuggestedAssetName).
            var fileId = p.UpgradeId.Substring(EnchantmentIdSlug.Prefix.Length);
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{p.TargetFolder}/Ench_{AssetNaming.PascalCaseId(fileId)}.asset");
            AssetDatabase.CreateAsset(enchantment, assetPath);
            Undo.RegisterCreatedObjectUndo(enchantment, "Create Enchantment");

            catalog.EditorAdd(enchantment);

            // El inglés cae al texto autor cuando no se escribió. Se admite a propósito:
            // deja crear rápido, y test_localization_no_key_repeats_the_spanish_text_in_english
            // avisa después cuáles quedaron sin traducir.
            ContentLocalizationBridge.UpsertEntryWithUndo(
                p.UpgradeId + LocalizedContent.NameSuffix, p.DisplayName,
                string.IsNullOrWhiteSpace(p.DisplayNameEn) ? p.DisplayName : p.DisplayNameEn,
                "Create Enchantment");
            ContentLocalizationBridge.UpsertEntryWithUndo(
                p.UpgradeId + LocalizedContent.DescSuffix, p.Description,
                string.IsNullOrWhiteSpace(p.DescriptionEn) ? p.Description : p.DescriptionEn,
                "Create Enchantment");

            EnchantmentPoolBridge.AddToPool(pool, enchantment, p.PoolWeight, p.MinFloorDepth);

            return enchantment;
        }

        /// <summary>
        /// Traduce el <c>TriggerId</c> de la spec a una opción del catálogo. Un id que no
        /// existe es un <b>error de creación</b>, no un trigger mudo.
        /// </summary>
        static EnchantmentTriggerCatalog.TriggerOption? ResolveTrigger(
            string triggerId, bool requireCarrierParticipates, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(triggerId))
            {
                if (requireCarrierParticipates)
                    errors.Add("RequireCarrierParticipates needs a combo trigger (no TriggerId given).");
                return null;
            }

            foreach (var option in EnchantmentTriggerCatalog.All)
            {
                if (option.Id != triggerId) continue;

                bool isComboHook = option.Event == EnchantmentHookEvent.ComboMatched
                                || option.Event == EnchantmentHookEvent.ComboPlayed;
                if (requireCarrierParticipates && !isComboHook)
                    errors.Add($"RequireCarrierParticipates only applies to combo triggers ('{triggerId}' given).");
                return option;
            }

            errors.Add($"TriggerId '{triggerId}' is not in EnchantmentTriggerCatalog.");
            return null;
        }

        /// <summary>
        /// Deja el trigger armado con su disparador puesto y <b>sin efectos</b>: ya sabe
        /// cuándo dispara y todavía no hace nada. Los efectos se autoran en el paso
        /// siguiente (ventana o skill).
        /// </summary>
        static void ApplyTrigger(
            EnchantmentSO enchantment, EnchantmentTriggerCatalog.TriggerOption? trigger,
            IReadOnlyList<string> comboIds, bool requireCarrierParticipates)
        {
            if (enchantment == null || !trigger.HasValue) return;

            var bridge = new ExecuteEffectsOnDiceEvent();
            EnchantmentTriggerCatalog.Apply(bridge, trigger.Value);

            if (trigger.Value.UsesComboIds && comboIds != null)
                bridge.Filter.ComboIds = new List<string>(comboIds);
            bridge.RequireCarrierParticipates = requireCarrierParticipates;

            enchantment.EditorAddTrigger(bridge);
            EditorUtility.SetDirty(enchantment);
        }

        static EnchantmentCatalogSO LoadCatalog()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(EnchantmentCatalogSO));
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<EnchantmentCatalogSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // ---- borrado ----------------------------------------------------------------

        /// <summary>
        /// Borra un encantamiento deshaciendo las cuatro escrituras del alta: catálogo,
        /// pool, claves de localización y recién entonces el asset.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>El orden importa.</b> Catálogo y pool se limpian con el asset todavía vivo:
        /// los dos localizan la entry por referencia al objeto — borrado el asset, la
        /// referencia es null y no hay con qué encontrarla.
        /// </para>
        /// <para>
        /// <b>No es undoable.</b> <c>AssetDatabase.DeleteAsset</c> queda fuera del undo;
        /// un Ctrl+Z revertiría las tres limpiezas y dejaría el archivo borrado — peor que
        /// no hacer nada. Por eso no se agrupa en un <c>UndoGroup</c>.
        /// </para>
        /// </remarks>
        public static EnchantmentDeletionResult DeleteEnchantment(EnchantmentSO enchantment)
        {
            if (enchantment == null) return EnchantmentDeletionResult.Failed("No hay encantamiento que borrar.");

            string upgradeId = enchantment.UpgradeId;
            string assetPath = AssetDatabase.GetAssetPath(enchantment);
            if (string.IsNullOrEmpty(assetPath))
                return EnchantmentDeletionResult.Failed("El encantamiento no tiene un asset en disco.");

            bool removedFromCatalog = false;
            var catalog = LoadCatalog();
            if (catalog != null) removedFromCatalog = catalog.EditorRemove(enchantment);

            bool removedFromPool = false;
            var pool = EnchantmentPoolBridge.LoadDefaultPool();
            if (pool != null) removedFromPool = EnchantmentPoolBridge.RemoveFromPool(pool, enchantment);

            int removedKeys = string.IsNullOrEmpty(upgradeId)
                ? 0
                : ContentLocalizationBridge.RemoveEntityKeys(upgradeId, "Delete Enchantment Localization");

            AssetDatabase.SaveAssets();

            if (!AssetDatabase.DeleteAsset(assetPath))
                return EnchantmentDeletionResult.Failed($"No se pudo borrar el asset en '{assetPath}'.");

            AssetDatabase.SaveAssets();
            return EnchantmentDeletionResult.Ok(upgradeId, assetPath, removedFromCatalog, removedFromPool, removedKeys);
        }
    }
}
