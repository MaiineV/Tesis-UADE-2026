using System;
using System.Collections.Generic;
using Rollgeon.Combat.Damage;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Convierte un <see cref="DamageBreakdown"/> en el guion ordenado de la secuencia
    /// animada. Orden (spec de la mejora de UI): base del PJ → cada dado contribuyente
    /// (slot ascendente) con sus procs pegados → modificadores globales en orden de journal.
    /// POCO puro, sin tweens ni escena — testeable en EditMode.
    /// </summary>
    public static class BreakdownScriptBuilder
    {
        private const float MultiplierEpsilon = 1e-4f;

        public static BreakdownScript Build(in DamageBreakdown bd)
        {
            var script = new BreakdownScript
            {
                InitialN = bd.ComboBase,
                InitialM = bd.AbilityMultiplier,
                FinalN = bd.N,
                FinalM = bd.M,
                FinalTotal = bd.Final,
                Blocked = bd.Blocked,
            };

            float playerBase = bd.AttackBase + bd.AttackBonus;
            if (playerBase != 0f)
                script.Steps.Add(new BreakdownStep
                {
                    Kind = BreakdownStepKind.PlayerBase,
                    Amount = playerBase,
                });

            // Fuentes por dado (BagSlot >= 0) agrupadas por slot; las globales conservan
            // su orden de journal (pasivas → encantos → play, ver DamageBreakdown.Sources).
            Dictionary<int, List<ScratchContribution>> perDie = null;
            List<ScratchContribution> globals = null;
            if (bd.Sources != null)
            {
                for (int i = 0; i < bd.Sources.Count; i++)
                {
                    var src = bd.Sources[i];
                    if (src.BagSlot >= 0)
                    {
                        perDie ??= new Dictionary<int, List<ScratchContribution>>();
                        if (!perDie.TryGetValue(src.BagSlot, out var list))
                            perDie[src.BagSlot] = list = new List<ScratchContribution>(2);
                        list.Add(src);
                    }
                    else
                    {
                        (globals ??= new List<ScratchContribution>(4)).Add(src);
                    }
                }
            }

            // Dados en orden de slot ascendente, cada uno seguido por sus procs.
            if (bd.Dice != null)
            {
                var ordered = new List<ContributingDie>(bd.Dice);
                ordered.Sort((a, b) => a.BagSlot.CompareTo(b.BagSlot));
                for (int i = 0; i < ordered.Count; i++)
                {
                    var die = ordered[i];
                    // Dado movido a M (Fuente Mágica): vuela al multiplicador como bono
                    // aditivo, con el icono de la fuente que lo movió — nunca pasa por N.
                    bool towardM = IsMovedToMultiplier(bd.DiceMovedToMultiplier, die.BagSlot);
                    var mover = towardM ? FindMover(bd.Sources, die.BagSlot) : null;
                    script.Steps.Add(new BreakdownStep
                    {
                        Kind = BreakdownStepKind.Die,
                        BagSlot = die.BagSlot,
                        Amount = die.Face,
                        Target = towardM ? BreakdownTarget.AddM : BreakdownTarget.BaseN,
                        SourceId = mover?.SourceId,
                        SourceAsset = mover?.SourceAsset,
                    });
                    if (perDie != null && perDie.TryGetValue(die.BagSlot, out var procs))
                    {
                        AddSourceSteps(script, procs, BreakdownStepKind.DieProc);
                        perDie.Remove(die.BagSlot);
                    }
                }
            }

            // Procs de dados que no participan del combo (ej. encanto sin
            // RequireCarrierParticipates): popup desde su dado, después de los contribuyentes.
            if (perDie != null)
                foreach (var kv in perDie)
                    AddSourceSteps(script, kv.Value, BreakdownStepKind.DieProc);

            if (globals != null)
                AddSourceSteps(script, globals, BreakdownStepKind.GlobalMod);

            script.Reconciled = CheckReconciliation(script);
            return script;
        }

        private static bool IsMovedToMultiplier(IReadOnlyList<int> moved, int bagSlot)
        {
            if (moved == null || bagSlot < 0) return false;
            for (int i = 0; i < moved.Count; i++)
                if (moved[i] == bagSlot) return true;
            return false;
        }

        // La entrada neutra del journal que marcó el dado (ScratchSnapshot.RecordDelta): trae
        // la fuente para el icono. Null si el scratch se escribió sin journal.
        private static ScratchContribution? FindMover(IReadOnlyList<ScratchContribution> sources, int bagSlot)
        {
            if (sources == null) return null;
            for (int i = 0; i < sources.Count; i++)
                if (sources[i].MovedDieBagSlot == bagSlot) return sources[i];
            return null;
        }

        private static void AddSourceSteps(
            BreakdownScript script, List<ScratchContribution> sources, BreakdownStepKind kind)
        {
            for (int i = 0; i < sources.Count; i++)
            {
                var src = sources[i];
                if (src.BonusDelta != 0)
                    script.Steps.Add(new BreakdownStep
                    {
                        Kind = kind,
                        BagSlot = src.BagSlot,
                        Amount = src.BonusDelta,
                        Target = BreakdownTarget.BaseN,
                        SourceId = src.SourceId,
                        SourceAsset = src.SourceAsset,
                    });
                // Por fuente: N → AddM → MultM. El orden entre AddM y MultM no afecta el
                // resultado (el contador compone Core × (1 + Σadd)); solo la lectura.
                if (Math.Abs(src.MultiplierBonusDelta) > MultiplierEpsilon)
                    script.Steps.Add(new BreakdownStep
                    {
                        Kind = kind,
                        BagSlot = src.BagSlot,
                        Amount = src.MultiplierBonusDelta,
                        Target = BreakdownTarget.AddM,
                        SourceId = src.SourceId,
                        SourceAsset = src.SourceAsset,
                    });
                if (Math.Abs(src.MultiplierFactor - 1f) > MultiplierEpsilon)
                    script.Steps.Add(new BreakdownStep
                    {
                        Kind = kind,
                        BagSlot = src.BagSlot,
                        Amount = src.MultiplierFactor,
                        Target = BreakdownTarget.MultM,
                        SourceId = src.SourceId,
                        SourceAsset = src.SourceAsset,
                    });
            }
        }

        // InitialN + Σ(aportes a N) debe dar FinalN, e InitialM × (1 + Σ AddM) × Π(MultM)
        // debe dar FinalM. Si una fuente escribió al scratch sin pasar por el journal, no
        // cierra: el flag avisa al director que fuerce los finales.
        private static bool CheckReconciliation(BreakdownScript script)
        {
            double n = script.InitialN;
            double addSum = 0;
            double mult = 1;
            for (int i = 0; i < script.Steps.Count; i++)
            {
                var step = script.Steps[i];
                switch (step.Target)
                {
                    case BreakdownTarget.BaseN: n += step.Amount; break;
                    case BreakdownTarget.AddM: addSum += step.Amount; break;
                    default: mult *= step.Amount; break;
                }
            }
            double m = script.InitialM * (1 + addSum) * mult;
            return Math.Abs(n - script.FinalN) < 1e-3 && Math.Abs(m - script.FinalM) < 1e-3;
        }
    }
}
