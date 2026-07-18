using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Upgrades;
using Rollgeon.Upgrades.Combos;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.Effects;
using Rollgeon.Upgrades.Dice.PreConditions;
using Rollgeon.Upgrades.Dice.Readers;
using Rollgeon.Upgrades.Dice.Triggers;
using Rollgeon.Upgrades.Dice.Triggers.Concretes;
using UnityEditor;
using UnityEngine;
using ComboBridge = Rollgeon.Upgrades.Combos.Triggers.Concretes.ExecuteEffectsOnEvent;
using ComboHookEvent = Rollgeon.Upgrades.Combos.Triggers.Concretes.ComboPassiveHookEvent;
using ComboPassiveTriggers = Rollgeon.Upgrades.Combos.Triggers.Concretes;

namespace Rollgeon.Editor.Tools.Enchantment
{
    /// <summary>
    /// Migrador one-shot de la Etapa 3 (Feature#0035): re-expresa los triggers legacy
    /// de EnchantmentSO / ComboPassiveSO como composiciones Eff/PC
    /// (<see cref="ExecuteEffectsOnDiceEvent"/> / ExecuteEffectsOnEvent) según la tabla
    /// de mapeo del plan. Flujo: 1-Dump (antes) → 2-Dry-run → 3-APPLY → 1-Dump (después)
    /// → diff + suite. Un tipo no mapeado deja el asset intacto y loguea error.
    /// </summary>
    public static class EnchantmentTriggerMigrator
    {
        private const string DumpPath = "Logs/enchantment-migration-dump.txt";
        private const string LegacyDiceNamespace = "Rollgeon.Upgrades.Dice.Triggers.Concretes";

        // ====================================================================
        // Menús
        // ====================================================================

        [MenuItem("Tools/Rollgeon/Migration/1 - Dump Enchantment Compositions")]
        public static void DumpCompositions()
        {
            var sb = new StringBuilder();

            foreach (var (path, ench) in LoadAll<EnchantmentSO>())
            {
                sb.AppendLine($"=== {path} | {ench.UpgradeId}");
                sb.AppendLine($"  triggers: {ench.Triggers?.Count ?? 0}");
                if (ench.Triggers != null)
                    foreach (var t in ench.Triggers) Describe(t, sb, indent: 2);
                sb.AppendLine($"  capabilities: {ench.Capabilities?.Count ?? 0}");
                if (ench.Capabilities != null)
                    foreach (var c in ench.Capabilities) Describe(c, sb, indent: 2);
            }

            foreach (var (path, passive) in LoadAll<ComboPassiveSO>())
            {
                sb.AppendLine($"=== {path} | {passive.UpgradeId} | target={passive.TargetComboId}");
                sb.AppendLine($"  extraTriggers: {passive.ExtraTriggers?.Count ?? 0}");
                if (passive.ExtraTriggers != null)
                    foreach (var t in passive.ExtraTriggers) Describe(t, sb, indent: 2);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(DumpPath));
            File.WriteAllText(DumpPath, sb.ToString());
            Debug.Log($"[Migrator] Dump escrito en {DumpPath}\n{sb}");
        }

        [MenuItem("Tools/Rollgeon/Migration/2 - Migrate Legacy Triggers (dry-run)")]
        public static void MigrateDryRun() => Migrate(apply: false);

        [MenuItem("Tools/Rollgeon/Migration/3 - Migrate Legacy Triggers (APPLY)")]
        public static void MigrateApply() => Migrate(apply: true);

        // ====================================================================
        // Núcleo
        // ====================================================================

        private static void Migrate(bool apply)
        {
            int scanned = 0, migrated = 0, untouched = 0, errors = 0;
            var log = new StringBuilder();
            string mode = apply ? "APPLY" : "dry-run";

            foreach (var (path, ench) in LoadAll<EnchantmentSO>())
            {
                scanned++;
                var legacy = ench.Triggers;
                if (legacy == null || legacy.Count == 0) { untouched++; continue; }

                var newTriggers = new List<IEnchantmentTrigger>();
                var newCaps = new List<IEnchantmentCapability>(ench.Capabilities ?? (IReadOnlyList<IEnchantmentCapability>)Array.Empty<IEnchantmentCapability>());
                bool anyLegacy = false, failed = false;

                foreach (var trigger in legacy)
                {
                    if (trigger == null) continue;
                    if (trigger.GetType().Namespace != LegacyDiceNamespace)
                    {
                        newTriggers.Add(trigger); // ya migrado / bridge nuevo
                        continue;
                    }

                    anyLegacy = true;
                    if (!TryConvertDiceTrigger(trigger, newTriggers, newCaps, out var error))
                    {
                        Debug.LogError($"[Migrator] {path}: trigger {trigger.GetType().Name} sin mapeo — asset salteado. {error}");
                        errors++;
                        failed = true;
                        break;
                    }
                    log.AppendLine($"  {path}: {trigger.GetType().Name} → composición");
                }

                if (failed || !anyLegacy) { if (!failed) untouched++; continue; }

                migrated++;
                if (!apply) continue;

                SetPrivate(ench, typeof(EnchantmentSO), "_triggers", newTriggers);
                SetPrivate(ench, typeof(EnchantmentSO), "_capabilities", newCaps);
                EditorUtility.SetDirty(ench);
            }

            foreach (var (path, passive) in LoadAll<ComboPassiveSO>())
            {
                scanned++;
                var legacy = passive.ExtraTriggers;
                if (legacy == null || legacy.Count == 0) { untouched++; continue; }

                var newTriggers = new List<IComboPassiveTrigger>();
                bool anyLegacy = false, failed = false;

                foreach (var trigger in legacy)
                {
                    if (trigger == null) continue;
                    if (trigger is ComboBridge)
                    {
                        newTriggers.Add(trigger);
                        continue;
                    }

                    anyLegacy = true;
                    if (!TryConvertComboTrigger(trigger, newTriggers, out var error))
                    {
                        Debug.LogError($"[Migrator] {path}: trigger {trigger.GetType().Name} sin mapeo — asset salteado. {error}");
                        errors++;
                        failed = true;
                        break;
                    }
                    log.AppendLine($"  {path}: {trigger.GetType().Name} → ExecuteEffectsOnEvent");
                }

                if (failed || !anyLegacy) { if (!failed) untouched++; continue; }

                migrated++;
                if (!apply) continue;

                SetPrivate(passive, typeof(ComboPassiveSO), "_extraTriggers", newTriggers);
                EditorUtility.SetDirty(passive);
            }

            if (apply)
            {
                AssetDatabase.SaveAssets();
                ValidateAfterApply();
            }

            Debug.Log($"[Migrator] {mode}: {scanned} assets escaneados, {migrated} con triggers migrables, " +
                      $"{untouched} sin cambios, {errors} errores.\n{log}");
        }

        /// <summary>Post-condición del APPLY: ningún asset queda con triggers legacy.</summary>
        private static void ValidateAfterApply()
        {
            int offenders = 0;

            foreach (var (path, ench) in LoadAll<EnchantmentSO>())
            {
                if (ench.Triggers == null) continue;
                foreach (var t in ench.Triggers)
                {
                    if (t != null && t.GetType().Namespace == LegacyDiceNamespace)
                    {
                        Debug.LogError($"[Migrator] POST-CHECK: {path} todavía tiene {t.GetType().Name}.");
                        offenders++;
                    }
                }
            }

            foreach (var (path, passive) in LoadAll<ComboPassiveSO>())
            {
                if (passive.ExtraTriggers == null) continue;
                foreach (var t in passive.ExtraTriggers)
                {
                    if (t != null && !(t is ComboBridge))
                    {
                        Debug.LogError($"[Migrator] POST-CHECK: {path} todavía tiene {t.GetType().Name}.");
                        offenders++;
                    }
                }
            }

            if (offenders == 0)
                Debug.Log("[Migrator] POST-CHECK OK: 0 triggers legacy en assets.");
        }

        // ====================================================================
        // Mapeo — canal dados (tabla B2 del plan)
        // ====================================================================

        private static bool TryConvertDiceTrigger(
            IEnchantmentTrigger legacy,
            List<IEnchantmentTrigger> outTriggers,
            List<IEnchantmentCapability> outCaps,
            out string error)
        {
            error = null;
            switch (legacy)
            {
                case AddComboDamage t:
                    outTriggers.Add(DiceBridge(EnchantmentHookEvent.ComboMatched,
                        FilterFrom(t.RestrictToComboIds),
                        Group(null, new EffAddComboBonus { Amount = t.Bonus })));
                    return true;

                case AddFlatToResult t:
                    outTriggers.Add(DiceBridge(EnchantmentHookEvent.RollResolved, null,
                        Group(null, new EffAddComboBonus { Amount = t.Bonus })));
                    return true;

                case SubtractFromResult t:
                    if (!(t.Amount is ReadConstantInt constant))
                    {
                        error = "SubtractFromResult con reader no-constante: no se puede negar estáticamente.";
                        return false;
                    }
                    outTriggers.Add(DiceBridge(EnchantmentHookEvent.RollResolved, null,
                        Group(null, new EffAddComboBonus
                        {
                            Amount = new ReadConstantInt { Value = -constant.Value },
                        })));
                    return true;

                case InvertResult _:
                    outTriggers.Add(DeltaBridge(CarrierRollDeltaOp.Invert));
                    return true;

                case ClampMinToHalfMax _:
                    outTriggers.Add(DeltaBridge(CarrierRollDeltaOp.ClampMinToHalfMax));
                    return true;

                case DoubleMaxZeroMin _:
                    outTriggers.Add(DeltaBridge(CarrierRollDeltaOp.DoubleMaxZeroMin));
                    return true;

                case TwinBonus t:
                    outTriggers.Add(DiceBridge(EnchantmentHookEvent.ComboMatched, null,
                        Group(new PcCarrierFace { Mode = CarrierFaceMode.HasDuplicate },
                            new EffMultiplyComboDamage { Multiplier = t.BonusMultiplier })));
                    return true;

                case ResonantDoubleCount _:
                    outTriggers.Add(DiceBridge(EnchantmentHookEvent.ComboMatched, null,
                        Group(new PcCarrierFace { Mode = CarrierFaceMode.HasDuplicate },
                            new EffAddComboBonus { Amount = new ReadCarrierFace() })));
                    return true;

                case ParityScoreMultiplier t:
                    outTriggers.Add(DiceBridge(EnchantmentHookEvent.ComboMatched, null,
                        Group(new PcCarrierFace { Mode = CarrierFaceMode.Odd },
                            new EffMultiplyComboDamage { Multiplier = t.MultiplierOdd }),
                        Group(new PcCarrierFace { Mode = CarrierFaceMode.Even },
                            new EffMultiplyComboDamage { Multiplier = t.MultiplierEven })));
                    return true;

                case ChanceToNotCount t:
                    outTriggers.Add(DiceBridge(EnchantmentHookEvent.ComboMatched, null,
                        Group(new PcChance { Mode = ChanceMode.Percent01, Chance = t.FailChance },
                            new EffBlockComboDamage())));
                    return true;

                case LuckyChanceComboBonus t:
                    outTriggers.Add(DiceBridge(EnchantmentHookEvent.ComboMatched, null,
                        Group(new PcChance { Mode = ChanceMode.OneInN, OneIn = t.OneInChance },
                            new EffAddComboBonus { Amount = t.Bonus })));
                    return true;

                case BlockComboIfBelowGold t:
                    outTriggers.Add(DiceBridge(EnchantmentHookEvent.ComboMatched, null,
                        Group(new PcGoldCompare
                        {
                            Comparison = IntComparison.Less,
                            Value = t.Threshold ?? new ReadConstantInt { Value = 1 },
                        }, new EffBlockComboDamage())));
                    return true;

                case SpendGoldOnComboParticipation t:
                    // El mismo reader de costo se comparte entre los 3 puntos de uso
                    // ([SerializeReference] preserva referencias compartidas).
                    outTriggers.Add(DiceBridge(EnchantmentHookEvent.ComboMatched, null,
                        Group(new PcGoldCompare { Comparison = IntComparison.GreaterOrEqual, Value = t.Cost },
                            new EffModifyGold
                            {
                                Operation = GoldOperation.Spend,
                                Amount = t.Cost,
                                FailChainIfInsufficient = false,
                            }),
                        Group(new PcGoldCompare { Comparison = IntComparison.Less, Value = t.Cost },
                            new EffBlockComboDamage())));
                    return true;

                case SpendGoldForComboBonus t:
                    outTriggers.Add(DiceBridge(EnchantmentHookEvent.ComboMatched, null,
                        Group(new PcGoldCompare { Comparison = IntComparison.GreaterOrEqual, Value = t.Cost },
                            new EffModifyGold { Operation = GoldOperation.Spend, Amount = t.Cost },
                            new EffAddComboBonus { Amount = t.Bonus })));
                    return true;

                case ModifyResourceTrigger t:
                    return TryConvertModifyResource(t, outTriggers, ref error);

                case ExplodeIfUnusedForTurns t:
                    outTriggers.Add(DiceBridge(EnchantmentHookEvent.EnchantmentApplied, null,
                        Group(null, CounterEff(SlotCounterOperation.Reset))));
                    outTriggers.Add(DiceBridge(EnchantmentHookEvent.ComboMatched, null,
                        Group(null, CounterEff(SlotCounterOperation.Reset))));
                    outTriggers.Add(DiceBridge(EnchantmentHookEvent.TurnFinished, null,
                        Group(null, CounterEff(SlotCounterOperation.Increment)),
                        Group(new PcSlotCounterCompare
                        {
                            Key = "explode_if_unused",
                            Comparison = IntComparison.GreaterOrEqual,
                            Value = t.MaxTurnsUnused,
                        }, new EffRemoveEnchantment())));
                    return true;

                // No-ops [NotYetWired] → capabilities declarativas.
                case PreventHolding _: outCaps.Add(new CapPreventHolding()); return true;
                case WildcardForCombo _: outCaps.Add(new CapWildcard()); return true;
                case EscaladorStep _: outCaps.Add(new CapLadderStep()); return true;
                case MimeticCopy _: outCaps.Add(new CapMimeticCopy()); return true;
                case RerollKeepHighest _: outCaps.Add(new CapRerollKeepHighest()); return true;
                case ForceRerollOnTurn t:
                    outCaps.Add(new CapForceRerollOnTurn { TriggerOnTurn = t.TriggerOnTurn });
                    return true;
                case AnchorAccumulate t:
                    outCaps.Add(new CapAnchorAccumulate { MaxAccumulation = t.MaxAccumulation });
                    return true;

                default:
                    error = $"tipo legacy desconocido: {legacy.GetType().FullName}";
                    return false;
            }
        }

        private static bool TryConvertModifyResource(
            ModifyResourceTrigger t, List<IEnchantmentTrigger> outTriggers, ref string error)
        {
            EnchantmentHookEvent evt;
            switch (t.When)
            {
                case TriggerWhen.ComboMatched: evt = EnchantmentHookEvent.ComboMatched; break;
                case TriggerWhen.RollResolved: evt = EnchantmentHookEvent.RollResolved; break;
                case TriggerWhen.DiceRolled: evt = EnchantmentHookEvent.DiceRolled; break;
                default:
                    error = $"TriggerWhen desconocido: {t.When}";
                    return false;
            }

            BasePreCondition pc = null;
            switch (t.Condition)
            {
                case TriggerCondition.None: break;
                case TriggerCondition.NoComboMatched: pc = new PcNoComboThisRoll(); break;
                case TriggerCondition.DieOnMaxFace:
                    pc = new PcCarrierFace { Mode = CarrierFaceMode.OnMaxFace };
                    break;
                default:
                    error = $"TriggerCondition desconocida: {t.Condition}";
                    return false;
            }

            if (!TryBuildResourceEffect(t.Target, t.Operation, t.Amount, out var effect, ref error))
                return false;

            var filter = t.When == TriggerWhen.ComboMatched && t.Filter != null
                ? new ComboFilter
                {
                    Mode = t.Filter.Mode,
                    ComboIds = t.Filter.ComboIds != null ? new List<string>(t.Filter.ComboIds) : new List<string>(),
                }
                : null;

            outTriggers.Add(DiceBridge(evt, filter, Group(pc, effect)));
            return true;
        }

        private static bool TryBuildResourceEffect(
            ResourceTarget target, ResourceOperation op, EffectIntReader amount,
            out IEffect effect, ref string error)
        {
            effect = null;
            if (target.Kind == ResourceKind.Gold)
            {
                switch (op)
                {
                    case ResourceOperation.Add:
                        effect = new EffModifyGold { Operation = GoldOperation.Add, Amount = amount };
                        return true;
                    case ResourceOperation.Subtract:
                        effect = new EffModifyGold
                        {
                            Operation = GoldOperation.Spend,
                            Amount = amount,
                            FailChainIfInsufficient = false,
                        };
                        return true;
                    case ResourceOperation.Set:
                        effect = new EffModifyGold { Operation = GoldOperation.Set, Amount = amount };
                        return true;
                    default:
                        error = $"ResourceOperation.{op} sobre Gold sin equivalente en EffModifyGold.";
                        return false;
                }
            }

            IntOperation intOp;
            switch (op)
            {
                case ResourceOperation.Add: intOp = IntOperation.Add; break;
                case ResourceOperation.Subtract: intOp = IntOperation.Subtract; break;
                case ResourceOperation.Multiply: intOp = IntOperation.Multiply; break;
                case ResourceOperation.Set: intOp = IntOperation.Set; break;
                default:
                    error = $"ResourceOperation.{op} sin equivalente en IntOperation.";
                    return false;
            }

            var statEff = new EffModifyIntAttribute { TargetStat = target.Stat, Operation = intOp };
            var sourceField = typeof(EffModifyIntAttribute).GetField("_amountSource", BindingFlags.NonPublic | BindingFlags.Instance);
            sourceField?.SetValue(statEff, Enum.Parse(sourceField.FieldType, "FromReader"));
            SetPrivate(statEff, typeof(EffModifyIntAttribute), "_reader", amount);
            effect = statEff;
            return true;
        }

        // ====================================================================
        // Mapeo — canal combos
        // ====================================================================

        private static bool TryConvertComboTrigger(
            IComboPassiveTrigger legacy, List<IComboPassiveTrigger> outTriggers, out string error)
        {
            error = null;
            switch (legacy)
            {
                case ComboPassiveTriggers.AddGoldOnComboMatch t:
                    outTriggers.Add(ComboBridgeWith(ComboHookEvent.ComboMatched,
                        new EffModifyGold { Operation = GoldOperation.Add, Amount = t.Amount }));
                    return true;

                case ComboPassiveTriggers.AddShieldOnTurnStart t:
                {
                    if (!TryBuildResourceEffect(ResourceTarget.OfStat(Rollgeon.Attributes.StatType.Shield),
                            ResourceOperation.Add, t.Amount, out var effect, ref error))
                        return false;
                    outTriggers.Add(ComboBridgeWith(ComboHookEvent.TurnStarted, effect));
                    return true;
                }

                case ComboPassiveTriggers.AddGoldOnRoomEntered t:
                    if (!string.IsNullOrEmpty(t.RoomIdFilter))
                    {
                        error = "AddGoldOnRoomEntered con RoomIdFilter: PcRoomId está diferido (sin uso en assets).";
                        return false;
                    }
                    outTriggers.Add(ComboBridgeWith(ComboHookEvent.RoomEntered,
                        new EffModifyGold { Operation = GoldOperation.Add, Amount = t.Amount }));
                    return true;

                case ComboPassiveTriggers.ModifyResourceComboPassiveTrigger t:
                {
                    if (t.Filter != null && t.Filter.Mode == ComboFilterMode.ComboIds)
                    {
                        error = "ModifyResourceComboPassiveTrigger con ComboIds extra: el bridge scopea por TargetComboId (sin uso en assets).";
                        return false;
                    }
                    if (!TryBuildResourceEffect(t.Target, t.Operation, t.Amount, out var effect, ref error))
                        return false;
                    outTriggers.Add(ComboBridgeWith(ComboHookEvent.ComboMatched, effect));
                    return true;
                }

                default:
                    error = $"tipo legacy desconocido: {legacy.GetType().FullName}";
                    return false;
            }
        }

        // ====================================================================
        // Helpers de construcción / reflexión / dump
        // ====================================================================

        private static ExecuteEffectsOnDiceEvent DiceBridge(
            EnchantmentHookEvent evt, ComboFilter filter, params EffectData[] groups)
        {
            var bridge = new ExecuteEffectsOnDiceEvent
            {
                Event = evt,
                Effects = new List<EffectData>(groups),
            };
            if (filter != null) bridge.Filter = filter;
            return bridge;
        }

        private static ExecuteEffectsOnDiceEvent DeltaBridge(CarrierRollDeltaOp op)
        {
            return DiceBridge(EnchantmentHookEvent.RollResolved, null,
                Group(null, new EffAddComboBonus { Amount = new ReadCarrierRollDelta { Op = op } }));
        }

        private static ComboBridge ComboBridgeWith(ComboHookEvent evt, params IEffect[] effects)
        {
            var group = new EffectData();
            foreach (var eff in effects) group.Effects.Add(eff);
            return new ComboBridge
            {
                Event = evt,
                Effects = new List<EffectData> { group },
            };
        }

        private static EffSlotCounter CounterEff(SlotCounterOperation op) =>
            new EffSlotCounter { Operation = op, Key = "explode_if_unused" };

        private static EffectData Group(BasePreCondition pc, params IEffect[] effects)
        {
            var data = new EffectData();
            if (pc != null) data.PreConditions.Add(pc);
            foreach (var eff in effects) data.Effects.Add(eff);
            return data;
        }

        private static ComboFilter FilterFrom(List<string> comboIds)
        {
            if (comboIds == null || comboIds.Count == 0) return null;
            return new ComboFilter { Mode = ComboFilterMode.ComboIds, ComboIds = new List<string>(comboIds) };
        }

        private static void SetPrivate(object target, Type declaringType, string field, object value) =>
            declaringType.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(target, value);

        private static IEnumerable<(string path, T asset)> LoadAll<T>() where T : ScriptableObject
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) yield return (path, asset);
            }
        }

        /// <summary>Descripción canónica recursiva para el dump comparativo before/after.</summary>
        private static void Describe(object node, StringBuilder sb, int indent)
        {
            if (node == null)
            {
                sb.Append(' ', indent * 2).AppendLine("(null)");
                return;
            }

            var pad = new string(' ', indent * 2);
            switch (node)
            {
                case ExecuteEffectsOnDiceEvent bridge:
                    sb.Append(pad).AppendLine(
                        $"ExecuteEffectsOnDiceEvent(Event={bridge.Event}, Filter={DescribeFilter(bridge.Filter)}, " +
                        $"RequireCarrier={bridge.RequireCarrierParticipates})");
                    foreach (var g in bridge.Effects ?? new List<EffectData>()) Describe(g, sb, indent + 1);
                    return;

                case ComboBridge bridge:
                    sb.Append(pad).AppendLine($"ExecuteEffectsOnEvent(Event={bridge.Event})");
                    foreach (var g in bridge.Effects ?? new List<EffectData>()) Describe(g, sb, indent + 1);
                    return;

                case EffectData data:
                    sb.Append(pad).AppendLine("EffectData");
                    if (data.PreConditions != null)
                        foreach (var pc in data.PreConditions) Describe(pc, sb, indent + 1);
                    if (data.Effects != null)
                        foreach (var eff in data.Effects) Describe(eff, sb, indent + 1);
                    return;

                case ReadConstantInt constant:
                    sb.Append(pad).AppendLine($"ReadConstantInt({constant.Value})");
                    return;

                default:
                    sb.Append(pad).Append(node.GetType().Name);
                    AppendPublicScalars(node, sb);
                    sb.AppendLine();
                    // Recursar readers embebidos conocidos (EffAddComboBonus.Amount, etc.).
                    foreach (var prop in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (typeof(EffectIntReader).IsAssignableFrom(prop.PropertyType) && prop.GetIndexParameters().Length == 0)
                        {
                            var reader = prop.GetValue(node);
                            if (reader != null) Describe(reader, sb, indent + 1);
                        }
                    }
                    foreach (var field in node.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (typeof(EffectIntReader).IsAssignableFrom(field.FieldType))
                        {
                            var reader = field.GetValue(node);
                            if (reader != null) Describe(reader, sb, indent + 1);
                        }
                    }
                    return;
            }
        }

        private static void AppendPublicScalars(object node, StringBuilder sb)
        {
            var parts = new List<string>();
            foreach (var field in node.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType.IsPrimitive || field.FieldType.IsEnum || field.FieldType == typeof(string))
                    parts.Add($"{field.Name}={field.GetValue(node)}");
            }
            if (parts.Count > 0) sb.Append('(').Append(string.Join(", ", parts)).Append(')');
        }

        private static string DescribeFilter(ComboFilter filter)
        {
            if (filter == null) return "null";
            return filter.Mode == ComboFilterMode.ComboIds
                ? $"ComboIds[{string.Join(",", filter.ComboIds ?? new List<string>())}]"
                : filter.Mode.ToString();
        }
    }
}
