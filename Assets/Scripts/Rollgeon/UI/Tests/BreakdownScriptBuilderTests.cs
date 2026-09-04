using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combat.Damage;
using Rollgeon.Dice;
using Rollgeon.UI.HUD.Breakdown;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Tests de <see cref="BreakdownScriptBuilder"/>: orden de pasos de la secuencia
    /// (player base → dados por slot con procs pegados → globales) y reconciliación
    /// contra los valores finales de la fórmula.
    /// </summary>
    [TestFixture]
    public class BreakdownScriptBuilderTests
    {
        private static ScratchContribution Enchant(int bagSlot, int bonus, float factor = 1f, float multBonus = 0f)
            => new ScratchContribution(ScratchSourceKind.Enchantment, $"ench.slot{bagSlot}",
                null, bagSlot, bonus, factor, false, multBonus);

        private static ScratchContribution Item(string id, int bonus, float factor = 1f, float multBonus = 0f)
            => new ScratchContribution(ScratchSourceKind.Item, id, null, -1, bonus, factor, false, multBonus);

        private static ScratchContribution FaceMutator(int bagSlot, int faceDelta, string id = "ench.oxidado")
            => new ScratchContribution(ScratchSourceKind.Enchantment, id, null, bagSlot, 0, 1f, false, 0f, faceDelta: faceDelta);

        // ---- Cara efectiva (Fix#0053) ---------------------------------------------------

        [Test]
        public void Build_FaceMutatedDie_FliesAsItsEffectiveFace_WithTheMutatorAsSource_AndNoProc()
        {
            // Oxidado: cara 6 → 0. bd.Dice ya trae la efectiva; el journal solo atribuye.
            var bd = new DamageBreakdown
            {
                ComboBase = 10,
                FacesSum = 0,
                N = 10,
                AbilityMultiplier = 1f,
                M = 1f,
                Final = 10,
                Dice = new List<ContributingDie> { new ContributingDie(0, 0, DiceType.D6) },
                Sources = new List<ScratchContribution> { FaceMutator(0, -6) },
            };

            var script = BreakdownScriptBuilder.Build(bd);

            Assert.AreEqual(1, script.Steps.Count, "un solo paso: el dado, sin proc que lo deshaga");
            Assert.AreEqual(BreakdownStepKind.Die, script.Steps[0].Kind);
            Assert.AreEqual(0f, script.Steps[0].Amount);
            Assert.AreEqual("ench.oxidado", script.Steps[0].SourceId);
            Assert.IsTrue(script.Reconciled);
        }

        [Test]
        public void Build_DieWithBonusProcButNoMutation_HasNoSourceOnTheDieStep()
        {
            var bd = new DamageBreakdown
            {
                ComboBase = 10,
                FacesSum = 4,
                N = 19,
                AbilityMultiplier = 1f,
                M = 1f,
                Final = 19,
                Dice = new List<ContributingDie> { new ContributingDie(0, 4, DiceType.D6) },
                Sources = new List<ScratchContribution> { Enchant(0, 5) },
            };

            var script = BreakdownScriptBuilder.Build(bd);

            Assert.AreEqual(2, script.Steps.Count);
            Assert.IsNull(script.Steps[0].SourceId);
            Assert.AreEqual(BreakdownStepKind.DieProc, script.Steps[1].Kind);
            Assert.AreEqual(5f, script.Steps[1].Amount);
            Assert.IsTrue(script.Reconciled);
        }

        // ---- Dado movido a M (Fuente Mágica) ---------------------------------------------

        [Test]
        public void Build_DieMovedToMultiplier_FliesToAddM_WithTheMoverSource_AndReconciles()
        {
            // Trío 4-4-6, el 6 (slot 2) va a M: N = 10 + 8 = 18; M = 1 + 6 = 7 → 126.
            var bd = new DamageBreakdown
            {
                ComboBase = 10,
                FacesSum = 8,
                MovedFacesSum = 6,
                N = 18,
                ScratchMultiplierBonus = 6f,
                ScratchMultiplier = 1f,
                AbilityMultiplier = 1f,
                M = 7f,
                Final = 126,
                Dice = new[]
                {
                    new ContributingDie(0, 4, DiceType.D6),
                    new ContributingDie(1, 4, DiceType.D6),
                    new ContributingDie(2, 6, DiceType.D6),
                },
                DiceMovedToMultiplier = new[] { 2 },
                Sources = new List<ScratchContribution>
                {
                    new ScratchContribution(ScratchSourceKind.Item, "fuente.magica", null, -1,
                        0, 1f, false, 0f, movedDieBagSlot: 2),
                },
            };

            var script = BreakdownScriptBuilder.Build(bd);

            // La entrada neutra del journal no genera paso global: solo etiqueta al dado.
            Assert.AreEqual(3, script.Steps.Count);
            Assert.AreEqual(BreakdownStepKind.Die, script.Steps[0].Kind);
            Assert.AreEqual(BreakdownTarget.BaseN, script.Steps[0].Target);
            Assert.IsNull(script.Steps[0].SourceId);
            Assert.AreEqual(BreakdownStepKind.Die, script.Steps[2].Kind);
            Assert.AreEqual(2, script.Steps[2].BagSlot);
            Assert.AreEqual(BreakdownTarget.AddM, script.Steps[2].Target);
            Assert.AreEqual(6f, script.Steps[2].Amount, 0.0001f);
            Assert.AreEqual("fuente.magica", script.Steps[2].SourceId);
            Assert.IsTrue(script.Reconciled);
        }

        [Test]
        public void Build_DieMovedToMultiplier_WithoutJournal_StillFliesToAddM()
        {
            var bd = new DamageBreakdown
            {
                ComboBase = 0,
                FacesSum = 0,
                MovedFacesSum = 5,
                N = 0,
                ScratchMultiplierBonus = 5f,
                ScratchMultiplier = 1f,
                AbilityMultiplier = 1f,
                M = 6f,
                Final = 0,
                Dice = new[] { new ContributingDie(3, 5, DiceType.D6) },
                DiceMovedToMultiplier = new[] { 3 },
            };

            var script = BreakdownScriptBuilder.Build(bd);

            Assert.AreEqual(1, script.Steps.Count);
            Assert.AreEqual(BreakdownTarget.AddM, script.Steps[0].Target);
            Assert.IsNull(script.Steps[0].SourceId);
            Assert.IsTrue(script.Reconciled);
        }

        // ---- Canal aditivo sobre M (AddM) ----------------------------------------------

        [Test]
        public void Build_SourceWithMultiplierBonus_EmitsAddMStep()
        {
            // Piedra Angular: +2 sobre el 1 de M → M = 1 × (1 + 2) = 3
            var bd = new DamageBreakdown
            {
                ComboBase = 10,
                N = 10,
                ScratchMultiplierBonus = 2f,
                ScratchMultiplier = 1f,
                AbilityMultiplier = 1f,
                M = 3f,
                Final = 30,
                Sources = new List<ScratchContribution> { Item("piedra.angular", 0, multBonus: 2f) },
            };

            var script = BreakdownScriptBuilder.Build(bd);

            Assert.AreEqual(1, script.Steps.Count);
            Assert.AreEqual(BreakdownStepKind.GlobalMod, script.Steps[0].Kind);
            Assert.AreEqual(BreakdownTarget.AddM, script.Steps[0].Target);
            Assert.AreEqual(2f, script.Steps[0].Amount, 0.0001f);
            Assert.IsTrue(script.Reconciled);
        }

        [Test]
        public void Build_MixedAddsAndMults_ReconcilesInJournalOrder()
        {
            // ×1.5 primero, +2 después, ability 0.75 → M = 0.75 × (1 + 2) × 1.5 = 3.375
            var bd = new DamageBreakdown
            {
                ComboBase = 10,
                N = 10,
                ScratchMultiplierBonus = 2f,
                ScratchMultiplier = 1.5f,
                AbilityMultiplier = 0.75f,
                M = 3.375f,
                Final = 34,
                Sources = new List<ScratchContribution>
                {
                    Item("item.gemelo", 0, factor: 1.5f),
                    Item("piedra.angular", 0, multBonus: 2f),
                },
            };

            var script = BreakdownScriptBuilder.Build(bd);

            Assert.AreEqual(2, script.Steps.Count);
            Assert.AreEqual(BreakdownTarget.MultM, script.Steps[0].Target);
            Assert.AreEqual(BreakdownTarget.AddM, script.Steps[1].Target);
            Assert.IsTrue(script.Reconciled, "el orden AddM/MultM del journal no cambia el M final");
        }

        [Test]
        public void Build_SourceWithBonusAddMAndMult_EmitsThreeSteps_BaseN_AddM_MultM()
        {
            var bd = new DamageBreakdown
            {
                ComboBase = 10,
                AdditiveBonus = 2,
                N = 12,
                ScratchMultiplierBonus = 1f,
                ScratchMultiplier = 2f,
                AbilityMultiplier = 1f,
                M = 4f,
                Final = 48,
                Sources = new List<ScratchContribution> { Item("item.todo", 2, factor: 2f, multBonus: 1f) },
            };

            var script = BreakdownScriptBuilder.Build(bd);

            Assert.AreEqual(3, script.Steps.Count);
            Assert.AreEqual(BreakdownTarget.BaseN, script.Steps[0].Target);
            Assert.AreEqual(BreakdownTarget.AddM, script.Steps[1].Target);
            Assert.AreEqual(BreakdownTarget.MultM, script.Steps[2].Target);
            Assert.IsTrue(script.Reconciled);
        }

        [Test]
        public void Build_MultiplierBonusMissingFromJournal_MarksNotReconciled()
        {
            var bd = new DamageBreakdown
            {
                ComboBase = 10,
                N = 10,
                ScratchMultiplierBonus = 2f,
                ScratchMultiplier = 1f,
                AbilityMultiplier = 1f,
                M = 3f,
                Final = 30,
            };

            var script = BreakdownScriptBuilder.Build(bd);

            Assert.IsFalse(script.Reconciled);
        }

        [Test]
        public void Build_OrdersSteps_PlayerBase_DiceBySlotWithProcs_ThenGlobals()
        {
            // Arrange — N = 5 + (2+1) + (6+4) + (3+2) = 23; M = 1.5 × 0.75 = 1.125 → 26
            var bd = new DamageBreakdown
            {
                ComboBase = 5,
                AttackBase = 2,
                AttackBonus = 1,
                FacesSum = 10,
                AdditiveBonus = 5,
                N = 23,
                ScratchMultiplier = 1.5f,
                AbilityMultiplier = 0.75f,
                M = 1.125f,
                Final = 26,
                Dice = new[]
                {
                    new ContributingDie(2, 4, DiceType.D6),
                    new ContributingDie(0, 6, DiceType.D6),
                },
                Sources = new List<ScratchContribution>
                {
                    Enchant(bagSlot: 0, bonus: 3),
                    Item("item.cadena", bonus: 2),
                    Item("item.gemelo", bonus: 0, factor: 1.5f),
                },
            };

            // Act
            var script = BreakdownScriptBuilder.Build(bd);

            // Assert — contadores iniciales
            Assert.AreEqual(5, script.InitialN);
            Assert.AreEqual(0.75f, script.InitialM, 0.0001f);
            Assert.AreEqual(26, script.FinalTotal);

            // Orden: PlayerBase → Die(0) → DieProc(0) → Die(2) → Global(+2) → Global(×1.5)
            Assert.AreEqual(6, script.Steps.Count);
            Assert.AreEqual(BreakdownStepKind.PlayerBase, script.Steps[0].Kind);
            Assert.AreEqual(3f, script.Steps[0].Amount);
            Assert.AreEqual(BreakdownStepKind.Die, script.Steps[1].Kind);
            Assert.AreEqual(0, script.Steps[1].BagSlot);
            Assert.AreEqual(6f, script.Steps[1].Amount);
            Assert.AreEqual(BreakdownStepKind.DieProc, script.Steps[2].Kind);
            Assert.AreEqual(0, script.Steps[2].BagSlot);
            Assert.AreEqual(3f, script.Steps[2].Amount);
            Assert.AreEqual(BreakdownStepKind.Die, script.Steps[3].Kind);
            Assert.AreEqual(2, script.Steps[3].BagSlot);
            Assert.AreEqual(BreakdownStepKind.GlobalMod, script.Steps[4].Kind);
            Assert.AreEqual(BreakdownTarget.BaseN, script.Steps[4].Target);
            Assert.AreEqual(2f, script.Steps[4].Amount);
            Assert.AreEqual(BreakdownStepKind.GlobalMod, script.Steps[5].Kind);
            Assert.AreEqual(BreakdownTarget.MultM, script.Steps[5].Target);
            Assert.AreEqual(1.5f, script.Steps[5].Amount, 0.0001f);

            Assert.IsTrue(script.Reconciled, "InitialN + Σaportes == FinalN e InitialM × Πfactores == FinalM");
        }

        [Test]
        public void Build_SourceMissingFromJournal_MarksNotReconciled()
        {
            // AdditiveBonus 4 en N pero SIN journal (fuente que escribió sin atribución):
            // la suma de pasos no cierra → el director debe forzar los finales.
            var bd = new DamageBreakdown
            {
                ComboBase = 10,
                AdditiveBonus = 4,
                N = 14,
                ScratchMultiplier = 1f,
                AbilityMultiplier = 1f,
                M = 1f,
                Final = 14,
            };

            var script = BreakdownScriptBuilder.Build(bd);

            Assert.IsFalse(script.Reconciled);
            Assert.AreEqual(14, script.FinalTotal, "Los finales quedan como fuente de verdad.");
        }

        [Test]
        public void Build_PlayerBaseZero_EmitsNoPlayerBaseStep()
        {
            var bd = new DamageBreakdown
            {
                ComboBase = 10,
                N = 10,
                ScratchMultiplier = 1f,
                AbilityMultiplier = 1f,
                M = 1f,
                Final = 10,
            };

            var script = BreakdownScriptBuilder.Build(bd);

            Assert.AreEqual(0, script.Steps.Count);
            Assert.IsTrue(script.Reconciled);
        }

        [Test]
        public void Build_SourceWithBonusAndMultiplier_EmitsTwoSteps()
        {
            var bd = new DamageBreakdown
            {
                ComboBase = 10,
                AdditiveBonus = 2,
                N = 12,
                ScratchMultiplier = 2f,
                AbilityMultiplier = 1f,
                M = 2f,
                Final = 24,
                Sources = new List<ScratchContribution> { Item("item.doble", 2, 2f) },
            };

            var script = BreakdownScriptBuilder.Build(bd);

            Assert.AreEqual(2, script.Steps.Count);
            Assert.AreEqual(BreakdownTarget.BaseN, script.Steps[0].Target);
            Assert.AreEqual(2f, script.Steps[0].Amount);
            Assert.AreEqual(BreakdownTarget.MultM, script.Steps[1].Target);
            Assert.AreEqual(2f, script.Steps[1].Amount);
            Assert.IsTrue(script.Reconciled);
        }

        [Test]
        public void Build_ProcOnNonContributingDie_ComesAfterContributingDice()
        {
            // Encanto en el slot 4 que aporta sin que su dado participe del combo:
            // popup desde su dado, DESPUÉS de los dados contribuyentes.
            var bd = new DamageBreakdown
            {
                ComboBase = 10,
                FacesSum = 5,
                AdditiveBonus = 2,
                N = 17,
                ScratchMultiplier = 1f,
                AbilityMultiplier = 1f,
                M = 1f,
                Final = 17,
                Dice = new[] { new ContributingDie(1, 5, DiceType.D6) },
                Sources = new List<ScratchContribution> { Enchant(bagSlot: 4, bonus: 2) },
            };

            var script = BreakdownScriptBuilder.Build(bd);

            Assert.AreEqual(2, script.Steps.Count);
            Assert.AreEqual(BreakdownStepKind.Die, script.Steps[0].Kind);
            Assert.AreEqual(BreakdownStepKind.DieProc, script.Steps[1].Kind);
            Assert.AreEqual(4, script.Steps[1].BagSlot);
            Assert.IsTrue(script.Reconciled);
        }

        [Test]
        public void Build_FractionalPlayerBase_Reconciles()
        {
            // Arrange — base override de Furia con fracción: N = 10 + 2.75 = 12.75.
            var bd = new DamageBreakdown
            {
                ComboBase = 10,
                AttackBase = 2.75f,
                AttackBonus = 0,
                FacesSum = 0,
                AdditiveBonus = 0,
                N = 12.75f,
                ScratchMultiplier = 1f,
                AbilityMultiplier = 1f,
                M = 1f,
                Final = 13, // RoundNxM(12.75, 1)
            };

            // Act
            var script = BreakdownScriptBuilder.Build(bd);

            // Assert — el paso PlayerBase lleva la fracción y el guion reconcilia
            // contra FinalN float (antes casteaba a int y 12.75 nunca cerraba).
            Assert.AreEqual(2.75f, script.Steps[0].Amount, 0.0001f);
            Assert.AreEqual(12.75f, script.FinalN, 0.0001f);
            Assert.IsTrue(script.Reconciled);
        }

        [Test]
        public void Build_Blocked_PassesFlagThrough()
        {
            var bd = new DamageBreakdown
            {
                ComboBase = 10,
                N = 10,
                ScratchMultiplier = 1f,
                AbilityMultiplier = 1f,
                M = 1f,
                Blocked = true,
                Final = 0,
            };

            var script = BreakdownScriptBuilder.Build(bd);

            Assert.IsTrue(script.Blocked);
            Assert.AreEqual(0, script.FinalTotal);
        }
    }
}
