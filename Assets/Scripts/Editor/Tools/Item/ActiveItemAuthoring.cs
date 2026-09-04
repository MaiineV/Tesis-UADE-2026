using System;
using System.Collections.Generic;
using Rollgeon.Dice;
using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.EditorTools.Localization;
using Rollgeon.Items;
using Rollgeon.Items.Active;
using Rollgeon.Localization;
using Rollgeon.Shop;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// Especificación de un ítem activo del rework (Feature#0085 §A6). A diferencia de
    /// <see cref="ItemCreationSpec"/> (modelo legacy: un solo <c>OnActivate</c>), declara
    /// dado propio, estructura de resolución y cortes/paridad — <see cref="ActiveItemAuthoring"/>
    /// arma el <see cref="ItemSO"/> completo del modelo nuevo (<c>UsesActiveSlot = true</c>).
    /// </summary>
    public sealed class ActiveItemCreationSpec
    {
        public string ItemId;
        public string DisplayName;
        public string DescriptionEs;
        public string DescriptionEn;
        public ItemRarity Rarity;
        public int BasePrice;
        public DiceType Die;
        public ActiveItemResolution Resolution;

        /// <summary>Solo aplica a <see cref="ActiveItemResolution.Bands"/>. 0 = tercios proporcionales.</summary>
        public int NegativeMaxFace;

        /// <summary>Solo aplica a <see cref="ActiveItemResolution.Bands"/>. 0 = tercios proporcionales.</summary>
        public int MixedMaxFace;

        /// <summary>Solo aplica a <see cref="ActiveItemResolution.Binary"/>.</summary>
        public ActiveItemParity BinaryPositiveParity;

        public ActiveItemFamily Family = ActiveItemFamily.Potencia;

        public string TargetFolder = "Assets/Rollgeon/Items/Active";

        /// <summary>
        /// Arma <c>OnNegativeBand</c>/<c>OnMixedBand</c>/<c>OnPositiveBand</c> sobre el item ya
        /// creado (asset todavía sin guardar). <c>null</c> = item sin efectos todavía — el seed
        /// de Fase 2 completa esta lambda item por item.
        /// </summary>
        public Action<ItemSO> BuildEffects;
    }

    /// <summary>
    /// Autoría de ítems activos del rework (Feature#0085 §A6, "7 items del doc").
    /// </summary>
    /// <remarks>
    /// Existe separado de <see cref="ItemAuthoring"/> porque el modelo nuevo (dado propio +
    /// bandas/binario/gradiente/jerarquía) no es el <c>ItemCreationSpec</c> legacy de un solo
    /// <c>OnActivate</c> — y porque los 7 assets llevan <c>SerializationNodes</c> Odin
    /// autoritativos en <c>EffectData</c>: el MCP <c>manage_scriptable_object</c> edita el lado
    /// nativo del ScriptableObject y <c>OnAfterDeserialize</c> lo pisa apenas Unity recarga, así
    /// que estos assets se arman en código C# y se corren por <c>execute_code</c>, nunca por
    /// una edición directa del inspector vía MCP.
    /// </remarks>
    public static class ActiveItemAuthoring
    {
        /// <summary>
        /// Crea el ítem si <see cref="ActiveItemCreationSpec.ItemId"/> todavía no está en
        /// <paramref name="catalog"/>; si ya existe, lo devuelve tal cual sin tocar nada más
        /// (idempotente — el seed puede correrse de nuevo sin duplicar ni pisar ediciones
        /// manuales posteriores). <paramref name="report"/> siempre trae una línea legible.
        /// </summary>
        public static ItemSO CreateOrSkip(
            ActiveItemCreationSpec spec, ItemCatalogSO catalog, ShopPoolSO shopPool, out string report)
        {
            if (spec == null)
            {
                report = "ActiveItemAuthoring: spec nula.";
                return null;
            }

            if (catalog == null)
            {
                report = $"[{spec.ItemId}] catálogo nulo — no se pudo crear.";
                return null;
            }

            var existing = catalog.GetById(spec.ItemId);
            if (existing != null)
            {
                report = $"[{spec.ItemId}] salteado — ya existe en el catálogo.";
                return existing;
            }

            var folder = string.IsNullOrEmpty(spec.TargetFolder)
                ? "Assets/Rollgeon/Items/Active"
                : spec.TargetFolder;
            EnsureFolder(folder);

            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = spec.ItemId;
            item.DisplayName = spec.DisplayName;
            item.Description = spec.DescriptionEs;
            item.Rarity = spec.Rarity;
            item.Type = ItemType.Active;
            item.UsesActiveSlot = true;
            item.ActiveDie = spec.Die;
            item.ActiveResolution = spec.Resolution;
            item.NegativeMaxFace = spec.NegativeMaxFace;
            item.MixedMaxFace = spec.MixedMaxFace;
            item.BinaryPositiveParity = spec.BinaryPositiveParity;
            item.ActiveFamily = spec.Family;

            // Los tres grupos ya vienen inicializados por ItemSO (campos = new EffectData()) —
            // BuildEffects los completa in-place, nunca hace falta reasignarlos acá.
            spec.BuildEffects?.Invoke(item);

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/Item_{AssetNaming.PascalCaseId(spec.ItemId)}.asset");
            AssetDatabase.CreateAsset(item, assetPath);
            Undo.RegisterCreatedObjectUndo(item, "Create Active Item");

            catalog.EditorAdd(item);

            if (shopPool != null)
                ItemShopPriceBridge.AddToPool(shopPool, item, spec.BasePrice);

            // Nombre idéntico en ES/EN a propósito (Feature#0085: "nombre del doc en los dos
            // idiomas, solo se traduce la descripción" — ver IdenticalByDesign en
            // LocalizationTablesTests). La descripción sí lleva textos distintos por idioma.
            ContentLocalizationBridge.UpsertEntryWithUndo(
                spec.ItemId + LocalizedContent.NameSuffix, spec.DisplayName, spec.DisplayName,
                "Create Active Item Localization");
            ContentLocalizationBridge.UpsertEntryWithUndo(
                spec.ItemId + LocalizedContent.DescSuffix, spec.DescriptionEs, spec.DescriptionEn,
                "Create Active Item Localization");

            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();

            // La validación de bandas nunca bloquea la creación (BuildEffects puede llegar
            // vacío en Fase 1, antes de que los efectos existan) — solo se refleja en el
            // reporte para que el seed avise qué falta antes de dar el item por terminado.
            bool valid = ActiveItemBands.Validate(item, out var validationError);
            report = valid
                ? $"[{spec.ItemId}] creado en '{assetPath}'."
                : $"[{spec.ItemId}] creado en '{assetPath}' — validación de bandas pendiente: {validationError}";
            return item;
        }

        /// <summary>
        /// Corre <see cref="CreateOrSkip"/> para toda la colección. Un fallo individual (spec
        /// nula, catálogo nulo) no corta el resto — <paramref name="report"/> junta una línea
        /// por item para que el caller vea todo el resultado de una.
        /// </summary>
        public static (int created, int skipped) CreateAll(
            IEnumerable<ActiveItemCreationSpec> specs,
            ItemCatalogSO catalog,
            ShopPoolSO shopPool,
            out string report)
        {
            int created = 0, skipped = 0;
            var lines = new List<string>();

            if (specs != null)
            {
                foreach (var spec in specs)
                {
                    bool existedBefore = spec != null && catalog != null && catalog.GetById(spec.ItemId) != null;
                    var item = CreateOrSkip(spec, catalog, shopPool, out var line);
                    lines.Add(line);
                    if (item == null) continue;
                    if (existedBefore) skipped++; else created++;
                }
            }

            report = string.Join("\n", lines);
            return (created, skipped);
        }

        /// <summary>Crea <paramref name="folder"/> y sus padres si hace falta (AssetDatabase no lo hace en un paso).</summary>
        static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            int slash = folder.LastIndexOf('/');
            if (slash < 0) return;

            var parent = folder.Substring(0, slash);
            var leaf = folder.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
