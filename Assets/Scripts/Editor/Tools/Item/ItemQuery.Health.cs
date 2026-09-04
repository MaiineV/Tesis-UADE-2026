using System.Collections.Generic;
using System.Linq;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Combos;
using Rollgeon.Effects.Concretes;
using Rollgeon.Items;
using Rollgeon.Shop;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item
{
    public static partial class ItemQuery
    {
        /// <summary>Severity of a <see cref="CatalogFinding"/> — drives icon/color in the metrics tab (spec §6.6); this layer never decides how it's drawn.</summary>
        public enum FindingSeverity { Info, Warning, Error }

        /// <summary>
        /// One catalog health finding. <see cref="Asset"/> is the object the UI should Ping when the
        /// designer clicks the row — null only for pool-level findings that aren't about one item.
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

        /// <summary>
        /// Catalog health findings (spec §6.6) — duplicate/missing ids, items outside the
        /// <see cref="ShopPoolSO"/>, missing icons, empty passive hooks, and hook combo ids that
        /// don't exist in the combo catalog. Read-only: this only reports, drawing is Fase 3's job.
        /// <paramref name="pool"/> defaults to <see cref="ItemShopPriceBridge.LoadDefaultPool"/> when null.
        /// </summary>
        public static IReadOnlyList<CatalogFinding> CheckCatalogHealth(ShopPoolSO pool = null) =>
            CheckCatalogHealth(GetAllItems(), pool);

        /// <summary>Pure form of <see cref="CheckCatalogHealth(ShopPoolSO)"/> — checks an arbitrary item list instead of scanning disk.</summary>
        public static IReadOnlyList<CatalogFinding> CheckCatalogHealth(IEnumerable<ItemSO> items, ShopPoolSO pool = null)
        {
            var findings = new List<CatalogFinding>();
            var itemList = (items ?? Enumerable.Empty<ItemSO>()).Where(i => i != null).ToList();

            pool ??= ItemShopPriceBridge.LoadDefaultPool();
            if (pool == null)
            {
                findings.Add(new CatalogFinding(
                    FindingSeverity.Error,
                    $"ShopPool no encontrado en '{ItemShopPriceBridge.DefaultShopPoolPath}' — no se puede " +
                    "chequear qué ítems están en la tienda.",
                    null));
            }

            // Ids vacíos / duplicados.
            var byId = new Dictionary<string, List<ItemSO>>();
            foreach (var item in itemList)
            {
                if (string.IsNullOrEmpty(item.ItemId))
                {
                    findings.Add(new CatalogFinding(
                        FindingSeverity.Error, $"'{item.name}' no tiene ItemId — no puede entrar al catálogo.", item));
                    continue;
                }

                if (!byId.TryGetValue(item.ItemId, out var list)) byId[item.ItemId] = list = new List<ItemSO>();
                list.Add(item);
            }

            foreach (var kv in byId)
            {
                if (kv.Value.Count <= 1) continue;
                foreach (var dup in kv.Value)
                    findings.Add(new CatalogFinding(
                        FindingSeverity.Error,
                        $"Id '{kv.Key}' duplicado entre {kv.Value.Count} assets ({string.Join(", ", kv.Value.Select(i => i.name))}).",
                        dup));
            }

            var knownComboIds = new HashSet<string>(BaseComboSO.GetKnownComboIds());

            foreach (var item in itemList)
            {
                var label = LabelOf(item);

                // Contra TODAS las pools, no solo la tienda. Preguntar solo por la tienda daba un
                // falso positivo en cinco items que salen de cofres: el aviso decia que no aparecian,
                // y aparecian. Lo que importa no es por que via se consigue, sino si se consigue.
                if (!ItemPoolMembership.IsInAnyPool(item))
                    findings.Add(new CatalogFinding(
                        FindingSeverity.Warning,
                        $"'{label}' no está en ninguna pool — no se puede conseguir jugando.",
                        item));

                if (item.Icon == null)
                    findings.Add(new CatalogFinding(FindingSeverity.Warning, $"'{label}' no tiene icono.", item));

                if (item.Type != ItemType.Passive) continue;

                if (item.PassiveHooks == null || item.PassiveHooks.Count == 0)
                {
                    // Un pasivo sin hooks es válido si vive de las perillas de lifecycle
                    // de ItemSO (Feature#0065): SecondWind, RollPoolBonus, ActiveSlotBonus,
                    // multiplicador del altar, override de daño base o regla de combo. Solo
                    // sin NADA de eso es un item que nunca hace nada.
                    bool hasLifecycleKnob =
                        item.SecondWind
                        || item.RollPoolBonus > 0
                        || item.LadderSkippedStep
                        || item.BlocksPassiveItemHealing
                        || (item.DecayingMultiplier != null && item.DecayingMultiplier.Enabled)
                        || item.ActiveSlotBonus > 0
                        || (item.EnchantmentCostMultiplier > 0f
                            && !UnityEngine.Mathf.Approximately(item.EnchantmentCostMultiplier, 1f))
                        || (item.BaseDamageOverride != null && item.BaseDamageOverride.Enabled);
                    if (!hasLifecycleKnob)
                    {
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Warning, $"'{label}' es Passive sin hooks — nunca se dispara.", item));
                    }
                    continue;
                }

                foreach (var hook in item.PassiveHooks)
                {
                    if (hook == null) continue;

                    var hasEffects = hook.Effect?.Effects != null && hook.Effect.Effects.Count > 0;
                    var hasModifiers = hook.PersistentModifiers != null && hook.PersistentModifiers.Count > 0;

                    // Un hook fuera del catalogo no rompe nada visible: no loguea, no tira, el item
                    // simplemente no dispara nunca. Antes eso se descubria jugando (o preguntandole
                    // a un programador por que el item "no anda").
                    //
                    // Solo cuenta si el hook tiene EFECTOS. Los PersistentModifiers los aplica
                    // ApplyPersistentModifiers al entrar el item al inventario, recorriendo los hooks
                    // sin mirar el evento: en un hook que solo lleva modificadores el TriggerEvent
                    // es decorativo, y avisar ahi seria mandar a "arreglar" dos items que andan.
                    if (hasEffects && ItemTriggerCatalog.Match(hook) == null)
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Error,
                            $"'{label}' dispara con '{hook.TriggerEvent}', que ningún ítem puede " +
                            "escuchar — nunca se va a ejecutar.",
                            item));

                    if (!hasEffects && !hasModifiers)
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Warning,
                            $"'{label}' tiene un hook sin efectos ni modificadores persistentes.",
                            item));

                    if (HealsTheHookTarget(hook))
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Error,
                            $"'{label}' cura con EffModifyIntAttribute (Health/Add) en un hook cuyo " +
                            "TargetGuid es el rival: al atacar cura al enemigo. Usar EffHeal (self-heal).",
                            item));

                    if (hook.Kind != PassiveHookKind.ComboPlayed) continue;
                    if (hook.ComboFilter?.Mode != ComboFilterMode.ComboIds) continue;
                    if (hook.ComboFilter.ComboIds == null) continue;

                    foreach (var comboId in hook.ComboFilter.ComboIds)
                    {
                        if (string.IsNullOrEmpty(comboId) || knownComboIds.Contains(comboId)) continue;
                        findings.Add(new CatalogFinding(
                            FindingSeverity.Error,
                            $"'{label}' referencia el combo '{comboId}' que no existe en el catálogo de combos.",
                            item));
                    }
                }
            }

            return findings;
        }

        /// <summary>
        /// <c>EffModifyIntAttribute</c> Health/Add cura al <c>TargetGuid</c> del contexto antes que
        /// al jugador. En un hook ComboPlayed ese guid es el objetivo de la acción, y en un evento
        /// de dos entidades (<c>OnDamageIncoming</c>/<c>OnDamageOutgoing</c>) es "el otro" sea cual
        /// sea el <c>Subject</c>: las 32 Lágrimas curaban al enemigo al atacar (Fix#0051) y al
        /// jugador solo al Curarse/Defender. La cura de un item va por <c>EffHeal</c>, que se
        /// auto-cura y respeta el bloqueo de Ayuno.
        /// </summary>
        static bool HealsTheHookTarget(PassiveItemHook hook)
        {
            bool targetIsRival = hook.Kind == PassiveHookKind.ComboPlayed
                                 || (hook.Kind == PassiveHookKind.EventBus
                                     && hook.Subject != PassiveHookSubject.None
                                     && (hook.Subject == PassiveHookSubject.Target
                                         || hook.TriggerEvent == EventName.OnDamageIncoming
                                         || hook.TriggerEvent == EventName.OnDamageOutgoing));
            if (!targetIsRival) return false;

            return EnumerateEffects(hook.Effect).Any(eff =>
                eff is EffModifyIntAttribute mod
                && mod.TargetStat == StatType.Health
                && mod.Operation == IntOperation.Add);
        }
    }
}
