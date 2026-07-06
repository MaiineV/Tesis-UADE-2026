using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.UI.Tooltips;

namespace Rollgeon.UI.Tests
{
    [TestFixture]
    public sealed class HeroActionTooltipTests
    {
        private TooltipContext _context;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _context = new TooltipContext(Guid.NewGuid(), null, GamePhase.Combat);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
        }

        private static HeroActionBehavior MakeBehavior(string name, int energyCost,
            params IEffect[] effects)
        {
            var data = new EffectData();
            data.Effects.AddRange(effects);
            return new HeroActionBehavior
            {
                ActionName = name,
                EnergyCost = energyCost,
                Effects = new List<EffectData> { data },
            };
        }

        [Test]
        public void BuildFor_NoEffectsWithTooltip_FallsBackToHeaderAndCost()
        {
            // Arrange — ningún effect aporta body; el fallback nunca es vacío.
            var behavior = MakeBehavior("Golpe Misterioso", 3);

            // Act
            var text = HeroActionTooltip.BuildFor(behavior, _context);

            // Assert
            StringAssert.Contains("<b>Golpe Misterioso</b>", text);
            StringAssert.Contains("Costo: 3 de energía", text);
        }

        [Test]
        public void BuildFor_ZeroCost_OmitsCostLine()
        {
            // Arrange
            var behavior = MakeBehavior("Moverse", 0);

            // Act
            var text = HeroActionTooltip.BuildFor(behavior, _context);

            // Assert
            StringAssert.Contains("<b>Moverse</b>", text);
            StringAssert.DoesNotContain("Costo", text);
        }

        [Test]
        public void BuildFor_TopLevelEffectWithTooltip_IncludesBody()
        {
            // Arrange — EffMove implementa IHasTooltipInfo (rango de movimiento).
            var move = new EffMove();
            move.GetSelection().Range = 4;
            move.GetSelection().IsGlobal = false;
            var behavior = MakeBehavior("Moverse", 0, move);

            // Act
            var text = HeroActionTooltip.BuildFor(behavior, _context);

            // Assert
            StringAssert.Contains("Moverse hasta 4 casillas", text);
        }

        [Test]
        public void FirstEffectTooltip_EffectsInsideChain_AreFoundAndConcatenated()
        {
            // Arrange — los ataques del guerrero envuelven daño + escudo en fases de
            // EffChain: sin recursión el tooltip de attack quedaría vacío.
            var damage = new EffDealDamage(); // DamageSource.Constant default → "Daño: 10"
            var shield = new EffAddShield();  // Constant default → "Escudo: +5"
            var chain = new EffChain
            {
                Phases = new List<ChainPhase>
                {
                    new ChainPhase { Label = "Damage", Effects = MakeData(damage) },
                    new ChainPhase { Label = "Shield", Effects = MakeData(shield) },
                },
            };

            // Act
            var text = HeroActionTooltip.FirstEffectTooltip(
                new List<EffectData> { MakeData(chain) }, _context);

            // Assert — ambas fases aportan al body.
            StringAssert.Contains("Daño:", text);
            StringAssert.Contains("Escudo:", text);
        }

        [Test]
        public void ResolveDisplayCost_ActionRollEffect_OverridesBehaviorCost()
        {
            // Arrange — el spec del effect (cobrado por IActionRollService) pisa el
            // EnergyCost legacy del behavior, igual que el cost label del HUD.
            var heal = new EffHeal();
            SetPrivateField(heal, "_useBuildDice", true);
            SetPrivateField(heal, "_energyCostInCombat", 1);
            var behavior = MakeBehavior("Curarse", 2, heal);
            RegisterCombatPhase();

            // Act
            int cost = HeroActionTooltip.ResolveDisplayCost(behavior, Guid.NewGuid());

            // Assert
            Assert.AreEqual(1, cost, "El costo del ActionRollSpec debe pisar behavior.EnergyCost.");
        }

        private static EffectData MakeData(IEffect eff)
        {
            var data = new EffectData();
            data.Effects.Add(eff);
            return data;
        }

        private static void SetPrivateField(object target, string field, object value)
        {
            var f = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(f, $"Campo privado '{field}' no encontrado en {target.GetType().Name}.");
            f.SetValue(target, value);
        }

        private void RegisterCombatPhase()
        {
            ServiceLocator.AddService<IPhaseService>(new StubPhaseService
            {
                CurrentBase = GamePhase.Combat,
            }, ServiceScope.Global);
        }

        private sealed class StubPhaseService : IPhaseService
        {
            public GamePhase CurrentBase { get; set; } = GamePhase.Combat;
            public PhaseOverlay CurrentOverlay => default;
            public void ReplacePhase(GamePhase next) { }
            public void PushOverlay(PhaseOverlay overlay) { }
            public void PopOverlay() { }
        }
    }
}
