using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Effects.Concretes;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Tests del modo build dice de <see cref="EffHeal"/> (Spec Heal N×M): la curación
    /// usa la fórmula compartida daño/escudo — base desde la HealBaseTable del sheet +
    /// ATQ + Σcaras, gate sin entrada, y fallback sin combo = dado holdeado más alto
    /// (espejo de EffDealDamage). También cubre el spec del ActionRoll y la poción
    /// (<see cref="EffHeal.ComputeDiceRollHeal"/>, camino independiente).
    /// </summary>
    [TestFixture]
    public class EffHealBuildDiceTests
    {
        private const int MaxHp = 100;

        private AttributesManager _attrManager;
        private ModifiableAttributes _sourceAttrs;
        private Guid _sourceId;
        private readonly List<ScriptableObject> _createdObjects = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _attrManager = new AttributesManager();
            _sourceId = Guid.NewGuid();

            _sourceAttrs = new ModifiableAttributes();
            _sourceAttrs.EnsureInitialized();
            _sourceAttrs.SetAttribute<Health>(new Health(50));
            _attrManager.Register(_sourceId, _sourceAttrs);

            ServiceLocator.AddService<AttributesManager>(_attrManager, ServiceScope.Run);
            ServiceLocator.AddService<IHealPipeline>(
                new HealPipeline(_attrManager, _ => MaxHp), ServiceScope.Run);
        }

        [TearDown]
        public void TearDown()
        {
            _attrManager.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            foreach (var so in _createdObjects)
                if (so != null) UnityEngine.Object.DestroyImmediate(so);
            _createdObjects.Clear();
        }

        // ── Fórmula N×M (combo real) ─────────────────────────────────────

        [Test]
        public void BuildDice_ComboWithTableEntry_UsesSharedFormula()
        {
            // Arrange — HealBase 8 en tabla; ATQ 5; sin bag → Σcaras 0.
            // Ruido: las tablas de daño (99) y escudo (50) presentes — no deben leerse.
            _sourceAttrs.SetAttribute<Attack>(new Attack(5));
            RegisterHeroWithHealTable(healBase: 8);
            var eff = CreateBuildDiceEffect();
            var ctx = MakeBuildDiceCtx(ComboDetectionResult.Match("combo.par", 99, 2, null));

            // Act
            eff.ApplyEffect(ctx);

            // Assert — N = 8 + 5 = 13; M = 1 → 50 + 13 = 63.
            Assert.AreEqual(63, _attrManager.GetAttribute<Health>(_sourceId).Value);
        }

        [Test]
        public void BuildDice_ComboMultiplier_ScalesWholeN()
        {
            // Arrange — perilla por habilidad: escala el N entero (igual que en daño).
            _sourceAttrs.SetAttribute<Attack>(new Attack(5));
            RegisterHeroWithHealTable(healBase: 8);
            var eff = CreateBuildDiceEffect(comboMultiplier: 2f);
            var ctx = MakeBuildDiceCtx(ComboDetectionResult.Match("combo.par", 99, 2, null));

            // Act
            eff.ApplyEffect(ctx);

            // Assert — N = 13; M = 2 → 50 + 26 = 76.
            Assert.AreEqual(76, _attrManager.GetAttribute<Health>(_sourceId).Value);
        }

        [Test]
        public void BuildDice_ComboWithoutTableEntry_HealsZero()
        {
            // Arrange — gate: combo matcheado pero la clase no define heal para ese combo.
            _sourceAttrs.SetAttribute<Attack>(new Attack(5));
            RegisterHeroWithHealTable(healBase: null);
            var eff = CreateBuildDiceEffect();
            var ctx = MakeBuildDiceCtx(ComboDetectionResult.Match("combo.par", 99, 2, null));

            // Act
            bool result = eff.ApplyEffect(ctx);

            // Assert — sin entrada = 0 heal, ni siquiera el término de ATQ; no-op exitoso.
            Assert.IsTrue(result);
            Assert.AreEqual(50, _attrManager.GetAttribute<Health>(_sourceId).Value);
        }

        [Test]
        public void BuildDice_NoPlayerService_HealsZero()
        {
            // Arrange — sin IPlayerService no hay sheet → no hay tabla → 0.
            var eff = CreateBuildDiceEffect();
            var ctx = MakeBuildDiceCtx(ComboDetectionResult.Match("combo.par", 99, 2, null));

            // Act
            bool result = eff.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(50, _attrManager.GetAttribute<Health>(_sourceId).Value);
        }

        [Test]
        public void BuildDice_HealClampedToMaxHp_ByPipeline()
        {
            // Arrange — heal grande: el freno es el clamp del HealPipeline, no un cap propio.
            RegisterHeroWithHealTable(healBase: 90);
            var eff = CreateBuildDiceEffect();
            var ctx = MakeBuildDiceCtx(ComboDetectionResult.Match("combo.par", 99, 2, null));

            // Act
            eff.ApplyEffect(ctx);

            // Assert — 50 + 90 = 140 → clamp a 100.
            Assert.AreEqual(MaxHp, _attrManager.GetAttribute<Health>(_sourceId).Value);
        }

        // ── Fallback sin combo (espejo del ataque) ───────────────────────

        [Test]
        public void BuildDice_NoCombo_HealsWithHighestKeptDie()
        {
            // Arrange — sin combo: el dado holdeado más alto entra a la fórmula,
            // sin pasar por el gate de la tabla.
            _sourceAttrs.SetAttribute<Attack>(new Attack(5));
            RegisterHeroWithHealTable(healBase: 8);
            var eff = CreateBuildDiceEffect();
            var ctx = MakeBuildDiceCtx(comboResult: null);
            ctx.KeptDice = new[] { 3, 5 };

            // Act
            eff.ApplyEffect(ctx);

            // Assert — sin bag el dado más alto entra como comboBase: N = 5 + 5 (ATQ) = 10.
            Assert.AreEqual(60, _attrManager.GetAttribute<Health>(_sourceId).Value);
        }

        [Test]
        public void BuildDice_SyntheticComboWithoutId_FallsBackToHighestDie()
        {
            // Arrange — resultado sintético (ComboId vacío = contexto degradado): no hay
            // lookup posible en la tabla, cae al fallback del dado más alto.
            RegisterHeroWithHealTable(healBase: 8);
            var eff = CreateBuildDiceEffect();
            var ctx = MakeBuildDiceCtx(ComboDetectionResult.Match(30, 2));

            // Act
            eff.ApplyEffect(ctx);

            // Assert — DiceResult [4,4,4,4,4], sin KeptDice → max 4; sin ATQ → 50 + 4 = 54.
            Assert.AreEqual(54, _attrManager.GetAttribute<Health>(_sourceId).Value);
        }

        [Test]
        public void BuildDice_NoDiceResult_FallsBackToBaseAmount()
        {
            // Arrange — wiring roto (sin DiceResult): warning + _baseAmount como red.
            var eff = CreateBuildDiceEffect(baseAmount: 10);
            var ctx = MakeCtx();
            ctx.DiceResult = null;

            // Act
            eff.ApplyEffect(ctx);

            // Assert
            Assert.AreEqual(60, _attrManager.GetAttribute<Health>(_sourceId).Value);
        }

        // ── Poción (dice roll genérico — camino independiente, sobrevive N×M) ──

        // Escala 100: la poción 1d10 con multiplicador 10 cura {10..100} preservando
        // la distribución uniforme del d10 original.
        [TestCase(1, 10, 10)]
        [TestCase(7, 10, 70)]
        [TestCase(10, 10, 100)]
        [TestCase(4, 1, 4)]   // multiplicador neutro
        [TestCase(4, 0, 4)]   // valores inválidos clampean a 1
        public void ComputeDiceRollHeal_MultipliesRollSum(int sum, int multiplier, int expected)
        {
            Assert.AreEqual(expected, EffHeal.ComputeDiceRollHeal(sum, multiplier));
        }

        // ── ActionRollSpec ───────────────────────────────────────────────

        [Test]
        public void TryGetRollSpec_BuildDiceModeOff_ReturnsFalse()
        {
            var heal = new EffHeal(); // _useBuildDice default false
            bool got = heal.TryGetRollSpec(Guid.Empty, out _);
            Assert.IsFalse(got);
        }

        [Test]
        public void TryGetRollSpec_BuildDiceModeOn_InCombat_CostsRolls()
        {
            var heal = CreateBuildDiceEffect();
            RegisterPhase(GamePhase.Combat);

            bool got = heal.TryGetRollSpec(Guid.Empty, out var spec);

            Assert.IsTrue(got);
            Assert.AreEqual(0, spec.Threshold, "N×M no tiene umbral — la acción siempre cura algo.");
            Assert.IsTrue(spec.AlwaysSucceeds, "Heal no debe tratar la tirada como fallo.");
            Assert.IsFalse(spec.RequireConfirm, "Heal va directo a roll, sin confirm dialog.");
            Assert.IsTrue(spec.AllowReroll);
            Assert.IsTrue(spec.CostsRolls, "En combate Curarse debe cobrar 1 roll por tirada.");
            Assert.AreEqual(Rollgeon.Combat.Rolls.RollActionKind.Heal, spec.Kind,
                "BUG-060: Curarse EN combate debe reportar Kind=Heal (pagable por encantamientos de oro).");
        }

        [Test]
        public void TryGetRollSpec_BuildDiceModeOn_OutOfCombat_IsFree()
        {
            var heal = CreateBuildDiceEffect();
            RegisterPhase(GamePhase.Exploration);

            bool got = heal.TryGetRollSpec(Guid.Empty, out var spec);

            Assert.IsTrue(got);
            Assert.IsFalse(spec.CostsRolls, "Fuera de combate curarse no debe gastar rolls.");
            Assert.AreEqual(Rollgeon.Combat.Rolls.RollActionKind.Exploration, spec.Kind,
                "BUG-060: Curarse fuera de combate debe reportar Kind=Exploration (no pagable).");
        }

        [Test]
        public void TryGetRollSpec_BuildDiceModeOn_NoPhaseService_DefaultsToOutOfCombat()
        {
            // Sin IPhaseService registrado → IsInCombat() = false → gratis.
            var heal = CreateBuildDiceEffect();

            bool got = heal.TryGetRollSpec(Guid.Empty, out var spec);

            Assert.IsTrue(got);
            Assert.IsFalse(spec.CostsRolls);
        }

        // ── Tooltip ──────────────────────────────────────────────────────

        [Test]
        public void BuildTooltip_BuildDiceOff_ConstantSource_ShowsFlatHeal()
        {
            // Sin _useBuildDice el tooltip describe la fuente del heal (Constant default).
            // El header/costo los agrega HeroActionTooltip.BuildFor, acá solo el body.
            var heal = new EffHeal();
            SetField(heal, "_baseAmount", 12);

            var text = heal.BuildTooltip();

            Assert.IsNotNull(text);
            StringAssert.Contains("Curación: 12 HP", text);
        }

        [Test]
        public void BuildTooltip_BuildDiceOn_DescribesSharedFormula()
        {
            // El tooltip espeja el de daño/escudo: ATQ + base del combo, con el
            // fallback sin combo explícito. Nada de "umbral".
            var heal = CreateBuildDiceEffect();

            var text = heal.BuildTooltip();

            Assert.IsNotNull(text);
            StringAssert.Contains("Curación: ATQ (", text);
            StringAssert.Contains("base del combo", text);
            StringAssert.Contains("Sin combo: ATQ + dado más alto", text);
            StringAssert.DoesNotContain("Umbral", text);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        // Hero con contrato mínimo [Par] y (opcional) entrada de heal para combo.par.
        // Ruido deliberado: tablas de daño y escudo SIEMPRE presentes y altas, para que
        // cualquier regresión que derive el heal de otra tabla reviente estos tests.
        private void RegisterHeroWithHealTable(int? healBase)
        {
            var par = ScriptableObject.CreateInstance<Combo_Par>();
            SetField(par, "_comboId", "combo.par");
            _createdObjects.Add(par);

            var sheet = new ContractSheet();
            sheet.Combos.Add(par);
            sheet.BaseDamageTable.Add(new ComboBaseDamageEntry
            {
                ComboId = "combo.par", BaseDamage = 99,
            });
            sheet.ShieldBaseTable.Add(new ComboShieldBaseEntry
            {
                ComboId = "combo.par", ShieldBase = 50,
            });
            if (healBase.HasValue)
            {
                sheet.HealBaseTable.Add(new ComboHealBaseEntry
                {
                    ComboId = "combo.par", HealBase = healBase.Value,
                });
            }

            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            hero.Sheet = sheet;
            _createdObjects.Add(hero);

            ServiceLocator.AddService<IPlayerService>(
                new StubPlayerService { CurrentHero = hero }, ServiceScope.Run);
        }

        private static EffHeal CreateBuildDiceEffect(float comboMultiplier = 1f, int baseAmount = 0)
        {
            var heal = new EffHeal();
            SetField(heal, "_useBuildDice", true);
            SetField(heal, "_comboMultiplier", comboMultiplier);
            SetField(heal, "_baseAmount", baseAmount);
            return heal;
        }

        private EffectContext MakeCtx()
        {
            return new EffectContext
            {
                SourceGuid = _sourceId,
                TargetGuid = Guid.Empty,
                lastResult = true,
            };
        }

        private EffectContext MakeBuildDiceCtx(ComboDetectionResult? comboResult)
        {
            var ctx = MakeCtx();
            ctx.DiceResult = new[] { 4, 4, 4, 4, 4 };
            ctx.ComboResult = comboResult;
            return ctx;
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private static void RegisterPhase(GamePhase phase)
        {
            var fake = new FakePhaseServiceForHeal { CurrentBase = phase };
            ServiceLocator.AddService<IPhaseService>(fake, ServiceScope.Run);
        }

        private sealed class FakePhaseServiceForHeal : IPhaseService
        {
            public GamePhase CurrentBase { get; set; }
            public PhaseOverlay CurrentOverlay => PhaseOverlay.None;
            public void ReplacePhase(GamePhase next) => CurrentBase = next;
            public void PushOverlay(PhaseOverlay overlay) { }
            public void PopOverlay() { }
        }

        private sealed class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; } = Guid.NewGuid();
            public Guid RunId { get; set; } = Guid.NewGuid();
            public ClassHeroSO CurrentHero { get; set; }
            public Rollgeon.Dice.DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(Rollgeon.Dice.DiceBagSO bag) { DiceBag = bag; }
            public void ClearPlayer() { }
#pragma warning disable 67
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore 67
        }
    }
}
