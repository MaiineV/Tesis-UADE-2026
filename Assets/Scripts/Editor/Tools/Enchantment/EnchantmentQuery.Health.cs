using System.Collections.Generic;
using System.Linq;
using Rollgeon.Attributes;
using Rollgeon.Combos;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.PreConditions;
using Rollgeon.Upgrades.Dice.Triggers;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enchantment
{
    public static partial class EnchantmentQuery
    {
        /// <summary>Severidad de un <see cref="CatalogFinding"/> — la UI decide ícono/color, esta capa solo reporta.</summary>
        public enum FindingSeverity { Info, Warning, Error }

        /// <summary>
        /// Un hallazgo de salud del catálogo. <see cref="Asset"/> es lo que la UI pinguea
        /// al clickear la fila — null solo para hallazgos de nivel catálogo/pool.
        /// </summary>
        public sealed class CatalogFinding
        {
            public FindingSeverity Severity { get; }
            public string Message { get; }
            public Object Asset { get; }

            public CatalogFinding(FindingSeverity severity, string message, Object asset)
            {
                Severity = severity;
                Message = message;
                Asset = asset;
            }
        }

        /// <summary>Salud del catálogo escaneando disco. Catálogo y pool default cuando van null.</summary>
        public static IReadOnlyList<CatalogFinding> CheckCatalogHealth(
            EnchantmentCatalogSO catalog = null, EnchantmentPoolSO pool = null)
            => CheckCatalogHealth(GetAll(), catalog, pool);

        /// <summary>
        /// Forma pura de <see cref="CheckCatalogHealth(EnchantmentCatalogSO, EnchantmentPoolSO)"/>.
        /// Cubre los cuatro huecos que dejaban invisibles a los assets huérfanos (los dos
        /// Codicioso vivieron meses fuera del catálogo y del pool sin que nada avisara),
        /// más las reglas duras de los triggers: solo scratch-writers en ComboMatched
        /// (BUG-017) y <c>PcCarrierFace</c> con <c>RequireCarrierParticipates</c>.
        /// </summary>
        public static IReadOnlyList<CatalogFinding> CheckCatalogHealth(
            IEnumerable<EnchantmentSO> enchantments,
            EnchantmentCatalogSO catalog = null, EnchantmentPoolSO pool = null)
        {
            var findings = new List<CatalogFinding>();
            var list = (enchantments ?? Enumerable.Empty<EnchantmentSO>()).Where(e => e != null).ToList();

            catalog = catalog != null ? catalog : LoadDefaultCatalog();
            if (catalog == null)
                findings.Add(new CatalogFinding(
                    FindingSeverity.Error,
                    "EnchantmentCatalog no encontrado — sin catálogo, los saves no pueden restaurar slots.",
                    null));

            pool = pool != null ? pool : EnchantmentPoolBridge.LoadDefaultPool();
            if (pool == null)
                findings.Add(new CatalogFinding(
                    FindingSeverity.Error,
                    "EnchantmentPool no encontrado — no se puede chequear qué se ofrece en el altar.",
                    null));

            // Ids vacíos / duplicados.
            var byId = new Dictionary<string, List<EnchantmentSO>>();
            foreach (var ench in list)
            {
                if (string.IsNullOrEmpty(ench.UpgradeId))
                {
                    findings.Add(new CatalogFinding(
                        FindingSeverity.Error, $"'{ench.name}' no tiene UpgradeId — no puede entrar al catálogo.", ench));
                    continue;
                }

                if (!byId.TryGetValue(ench.UpgradeId, out var group)) byId[ench.UpgradeId] = group = new List<EnchantmentSO>();
                group.Add(ench);
            }

            foreach (var kv in byId)
            {
                if (kv.Value.Count <= 1) continue;
                foreach (var dup in kv.Value)
                    findings.Add(new CatalogFinding(
                        FindingSeverity.Error,
                        $"Id '{kv.Key}' duplicado entre {kv.Value.Count} assets ({string.Join(", ", kv.Value.Select(e => e.name))}).",
                        dup));
            }

            var inCatalog = catalog != null
                ? new HashSet<EnchantmentSO>(catalog.Entries.Where(e => e != null))
                : null;
            var knownComboIds = new HashSet<string>(BaseComboSO.GetKnownComboIds());

            foreach (var ench in list)
            {
                var label = LabelOf(ench);

                if (inCatalog != null && !inCatalog.Contains(ench))
                    findings.Add(new CatalogFinding(
                        FindingSeverity.Error,
                        $"'{label}' no está en el EnchantmentCatalog — los saves que lo tengan lo descartan al restaurar.",
                        ench));

                if (pool != null)
                {
                    if (!EnchantmentPoolBridge.IsInPool(pool, ench))
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Warning,
                            $"'{label}' no está en el pool del altar — no se ofrece nunca.",
                            ench));
                    else if (EnchantmentPoolBridge.TryGetWeight(pool, ench, out var weight) && weight <= 0f)
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Info,
                            $"'{label}' está en el pool con peso 0 — registrado pero deshabilitado.",
                            ench));
                }

                if (ench.Category == EnchantmentCategory.None)
                    findings.Add(new CatalogFinding(
                        FindingSeverity.Error,
                        $"'{label}' no tiene categoría — la auditoría lo rechaza (Assign Categories o el diccionario).",
                        ench));

                if (ench.Icon == null)
                    findings.Add(new CatalogFinding(FindingSeverity.Warning, $"'{label}' no tiene icono.", ench));

                CheckBehaviour(ench, label, knownComboIds, findings);
            }

            return findings;
        }

        static void CheckBehaviour(
            EnchantmentSO ench, string label, HashSet<string> knownComboIds, List<CatalogFinding> findings)
        {
            bool hasTriggers = ench.Triggers != null && ench.Triggers.Count > 0;
            bool hasCapabilities = ench.Capabilities != null && ench.Capabilities.Count > 0;
            bool hasStatGrants = ench.StatGrants != null && ench.StatGrants.Count > 0;

            if (ench.FaceFilter == null && !hasTriggers && !hasCapabilities && !hasStatGrants)
                findings.Add(new CatalogFinding(
                    FindingSeverity.Warning,
                    $"'{label}' no tiene filtro de caras, triggers, capabilities ni stat grants — no hace nada.",
                    ench));

            if (hasTriggers)
            {
                foreach (var trigger in ench.Triggers)
                {
                    if (trigger == null)
                    {
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Error,
                            $"'{label}' tiene un trigger null (¿rid huérfano de un tipo borrado?).",
                            ench));
                        continue;
                    }

                    if (trigger is not ExecuteEffectsOnDiceEvent bridge) continue;

                    bool hasEffects = false;
                    bool usesCarrierFace = false;
                    if (bridge.Effects != null)
                    {
                        foreach (var group in bridge.Effects)
                        {
                            if (group?.Effects != null && group.Effects.Count > 0) hasEffects = true;

                            if (group?.PreConditions != null)
                                foreach (var pc in group.PreConditions)
                                    if (pc is PcCarrierFace) usesCarrierFace = true;

                            // BUG-017: ComboMatched es preview y re-dispara por toggle de
                            // hold — un apply directo ahí es farmeable infinito.
                            if (bridge.Event == EnchantmentHookEvent.ComboMatched && group?.Effects != null)
                            {
                                foreach (var effect in group.Effects)
                                {
                                    if (effect == null || effect is Rollgeon.Effects.IComboScratchWriter) continue;
                                    findings.Add(new CatalogFinding(
                                        FindingSeverity.Error,
                                        $"'{label}': {effect.GetType().Name} en ComboMatched (apply directo en preview = farmeable).",
                                        ench));
                                }
                            }
                        }
                    }

                    if (!hasEffects)
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Warning,
                            $"'{label}' tiene un trigger ({bridge.Event}) sin efectos.",
                            ench));

                    bool isComboHook = bridge.Event == EnchantmentHookEvent.ComboMatched
                                    || bridge.Event == EnchantmentHookEvent.ComboPlayed;

                    if (isComboHook && usesCarrierFace && !bridge.RequireCarrierParticipates)
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Error,
                            $"'{label}' usa PcCarrierFace sin RequireCarrierParticipates — el gate del carrier no filtra por combo real.",
                            ench));

                    if (isComboHook
                        && bridge.Filter is { Mode: ComboFilterMode.ComboIds, ComboIds: not null })
                    {
                        foreach (var comboId in bridge.Filter.ComboIds)
                        {
                            if (string.IsNullOrEmpty(comboId) || knownComboIds.Contains(comboId)) continue;
                            findings.Add(new CatalogFinding(
                                FindingSeverity.Error,
                                $"'{label}' referencia el combo '{comboId}' que no existe en el catálogo de combos.",
                                ench));
                        }
                    }
                }
            }

            if (hasCapabilities)
            {
                foreach (var capability in ench.Capabilities)
                {
                    if (capability == null)
                    {
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Error,
                            $"'{label}' tiene una capability null (¿rid huérfano?).",
                            ench));
                        continue;
                    }

                    var attrs = capability.GetType().GetCustomAttributes(typeof(NotYetWiredAttribute), true);
                    if (attrs.Length == 0) continue;
                    findings.Add(new CatalogFinding(
                        FindingSeverity.Warning,
                        $"'{label}': {capability.GetType().Name} no está cableada in-game — {((NotYetWiredAttribute)attrs[0]).Reason}",
                        ench));
                }
            }
        }

        static EnchantmentCatalogSO LoadDefaultCatalog()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:" + nameof(EnchantmentCatalogSO));
            if (guids.Length == 0) return null;
            return UnityEditor.AssetDatabase.LoadAssetAtPath<EnchantmentCatalogSO>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
