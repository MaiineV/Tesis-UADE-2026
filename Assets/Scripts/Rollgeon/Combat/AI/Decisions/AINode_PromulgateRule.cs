using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.ContractMod;
using Rollgeon.Combos;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// "Promulgar Regla" del Boss 3 (Director General). Efecto de inicio de turno (va como primer
    /// hijo del sequence, antes de la acción del pool — no consume la acción). Cada
    /// <see cref="IntervalPhase1"/> turnos (o <see cref="IntervalPhase2"/> en Fase 2) cancela las
    /// reglas previas (<see cref="IContractModifierService.ClearAll"/>) y promulga
    /// <see cref="RulesPerPromulgation"/> regla(s) aleatoria(s) de <see cref="EnabledRules"/>.
    /// Sistemas prerequisito Bosses §4.
    /// </summary>
    /// <remarks>
    /// <b>Fases (ad-hoc, sin tocar stats).</b> El intervalo de cambio se acorta al cruzar
    /// <see cref="Phase2HpThreshold"/> — leído de la vida del Boss en cada tick. Mantener un único
    /// nodo (un único contador) evita el desincronizado que provocaría ramificar el árbol en dos
    /// instancias con contadores separados.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_PromulgateRule : AIActionNode
    {
        /// <summary>Reglas base del Contrato del Boss 3 (R01–R06).</summary>
        public enum ContractRule
        {
            R01_DecretoDeValor,        // un combo random duplica su daño
            R02_AuditoriaFiscal,       // un combo random reduce su daño a la mitad
            R03_ClausuraTemporal,      // un combo random prohibido (daño 0)
            R04_BonoProductividad,     // el combo de menor daño base sube al inmediatamente superior
            R05_PenalizacionEficiencia,// el combo de mayor daño base baja al inmediatamente inferior
            R06_DecretoNeutro,         // sin efecto
        }

        [Tooltip("Reglas habilitadas para sortear. Vacío = R06 (neutro) por defensa.")]
        public List<ContractRule> EnabledRules = new List<ContractRule>
        {
            ContractRule.R01_DecretoDeValor, ContractRule.R02_AuditoriaFiscal,
            ContractRule.R03_ClausuraTemporal, ContractRule.R04_BonoProductividad,
            ContractRule.R05_PenalizacionEficiencia, ContractRule.R06_DecretoNeutro,
        };

        [Tooltip("Cuántas reglas activas a la vez. Empezar en 1; la arquitectura soporta N.")]
        [MinValue(1)]
        public int RulesPerPromulgation = 1;

        [Tooltip("Turnos del Boss entre cambios de regla en Fase 1.")]
        [MinValue(1)]
        public int IntervalPhase1 = 3;

        [Tooltip("Turnos del Boss entre cambios de regla en Fase 2 (HP <= umbral).")]
        [MinValue(1)]
        public int IntervalPhase2 = 2;

        [Tooltip("Ratio de HP (0..1) al que el Boss entra en Fase 2 y acorta el intervalo. 0 = sin Fase 2.")]
        [Range(0f, 1f)]
        public float Phase2HpThreshold = 0.5f;

        [Tooltip("Factor de R01 (Decreto de Valor).")]
        public float DoubleFactor = 2f;

        [Tooltip("Factor de R02 (Auditoría Fiscal).")]
        public float HalfFactor = 0.5f;

        [NonSerialized] private int _bossTurnCounter;

        public override string NodeName => "Promulgate Rule (Boss 3)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;

            _bossTurnCounter++;
            int interval = ResolveInterval(context);

            // Promulga en el primer turno y luego cada `interval` turnos.
            bool promulgateNow = (_bossTurnCounter - 1) % interval == 0;
            if (!promulgateNow) return AIResult.Succeeded;

            if (!ServiceLocator.TryGetService<IContractModifierService>(out var mods) || mods == null)
            {
                Debug.LogError("[AINode_PromulgateRule] IContractModifierService no registrado. " +
                               "Agrega ContractModifierServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return AIResult.Failed;
            }

            var sheet = ResolveSheet(context);
            if (sheet?.Combos == null || sheet.Combos.Count == 0)
            {
                Debug.LogWarning("[AINode_PromulgateRule] Sin ContractSheet del jugador — no se promulga.");
                return AIResult.Failed;
            }

            // Al cambiar, la regla anterior se cancela completamente.
            mods.ClearAll();

            if (EnabledRules == null || EnabledRules.Count == 0)
                return AIResult.Succeeded; // solo neutro

            int n = RulesPerPromulgation < 1 ? 1 : RulesPerPromulgation;
            for (int i = 0; i < n; i++)
            {
                var rule = EnabledRules[NextInt(context, EnabledRules.Count)];
                ApplyRule(rule, sheet, mods, context);
            }

            return AIResult.Succeeded;
        }

        // -- Reglas -------------------------------------------------------------------
        private void ApplyRule(ContractRule rule, ContractSheet sheet, IContractModifierService mods, AIContext context)
        {
            switch (rule)
            {
                case ContractRule.R01_DecretoDeValor:
                    mods.MultiplyCombo(PickRandomComboId(sheet, context), DoubleFactor);
                    break;
                case ContractRule.R02_AuditoriaFiscal:
                    mods.MultiplyCombo(PickRandomComboId(sheet, context), HalfFactor);
                    break;
                case ContractRule.R03_ClausuraTemporal:
                    mods.ForbidCombo(PickRandomComboId(sheet, context));
                    break;
                case ContractRule.R04_BonoProductividad:
                    mods.SetComboToNeighbor(LowestBaseComboId(sheet), +1);
                    break;
                case ContractRule.R05_PenalizacionEficiencia:
                    mods.SetComboToNeighbor(HighestBaseComboId(sheet), -1);
                    break;
                case ContractRule.R06_DecretoNeutro:
                    break;
            }
        }

        private static string PickRandomComboId(ContractSheet sheet, AIContext context)
        {
            var combos = sheet.Combos;
            // Reintenta hasta encontrar uno no-nulo (los nulls no deberían existir tras Validate).
            for (int attempt = 0; attempt < combos.Count; attempt++)
            {
                var c = combos[NextInt(context, combos.Count)];
                if (c != null && !string.IsNullOrEmpty(c.ComboId)) return c.ComboId;
            }
            return null;
        }

        private static string LowestBaseComboId(ContractSheet sheet)
        {
            BaseComboSO best = null;
            foreach (var c in sheet.Combos)
                if (c != null && (best == null || c.BaseDamage < best.BaseDamage)) best = c;
            return best?.ComboId;
        }

        private static string HighestBaseComboId(ContractSheet sheet)
        {
            BaseComboSO best = null;
            foreach (var c in sheet.Combos)
                if (c != null && (best == null || c.BaseDamage > best.BaseDamage)) best = c;
            return best?.ComboId;
        }

        // -- Helpers ------------------------------------------------------------------
        private int ResolveInterval(AIContext context)
        {
            if (Phase2HpThreshold <= 0f) return Mathf.Max(1, IntervalPhase1);
            return IsInPhase2(context) ? Mathf.Max(1, IntervalPhase2) : Mathf.Max(1, IntervalPhase1);
        }

        private bool IsInPhase2(AIContext context)
        {
            var attrs = context.Attributes;
            if (attrs == null) ServiceLocator.TryGetService<AttributesManager>(out attrs);
            if (attrs == null || context.SelfMaxHp <= 0) return false;

            var hp = attrs.GetAttribute<Health>(context.SelfGuid);
            if (hp == null) return false;
            return (float)hp.ModifiedValue / context.SelfMaxHp <= Phase2HpThreshold;
        }

        private static ContractSheet ResolveSheet(AIContext context)
        {
            var ps = context.PlayerService;
            if (ps == null) ServiceLocator.TryGetService<IPlayerService>(out ps);
            return ps?.CurrentHero?.Sheet;
        }

        private static int NextInt(AIContext context, int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 1) return 0;
            return context.Rng != null
                ? context.Rng.Next(exclusiveUpperBound)
                : UnityEngine.Random.Range(0, exclusiveUpperBound);
        }
    }
}
