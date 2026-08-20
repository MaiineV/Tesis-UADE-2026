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

        private static HeroActionBehavior MakeBehavior(string name,
            params IEffect[] effects)
        {
            var data = new EffectData();
            data.Effects.AddRange(effects);
            return new HeroActionBehavior
            {
                ActionName = name,
                Effects = new List<EffectData> { data },
            };
        }

        [Test]
        public void BuildFor_NoEffectsWithTooltip_FallsBackToHeaderAndCost()
        {
            // Arrange — ningún effect aporta body; el fallback nunca es vacío.
            var behavior = MakeBehavior("Golpe Misterioso");

            // Act
            var text = HeroActionTooltip.BuildFor(behavior, _context);

            // Assert — el costo es uniforme: 1 roll por tirada (Pool de Rolls).
            StringAssert.Contains("<b>Golpe Misterioso</b>", text);
            StringAssert.Contains("Costo: 1 Roll por tirada", text);
        }

        [Test]
        public void BuildFor_TopLevelEffectWithTooltip_IncludesBody()
        {
            // Arrange — EffMove implementa IHasTooltipInfo (rango de movimiento).
            var move = new EffMove();
            move.GetSelection().Range = 4;
            move.GetSelection().IsGlobal = false;
            var behavior = MakeBehavior("Moverse", move);

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
        public void BuildFor_DefenseShapedBehavior_ShowsShieldBody()
        {
            // Arrange — la acción Defense (Feature#0051): chain de UNA fase con solo
            // EffAddShield. El tooltip del chip debe mostrar el body de escudo.
            var shield = new EffAddShield(); // Constant default → "Escudo: +5"
            var chain = new EffChain
            {
                Phases = new List<ChainPhase>
                {
                    new ChainPhase { Label = "Shield", Effects = MakeData(shield) },
                },
            };
            var behavior = MakeBehavior("Defense", chain);

            // Act
            var text = HeroActionTooltip.BuildFor(behavior, _context);

            // Assert
            StringAssert.Contains("<b>Defense</b>", text);
            StringAssert.Contains("Costo: 1 Roll por tirada", text);
            StringAssert.Contains("Escudo:", text);
            StringAssert.DoesNotContain("Daño:", text);
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
