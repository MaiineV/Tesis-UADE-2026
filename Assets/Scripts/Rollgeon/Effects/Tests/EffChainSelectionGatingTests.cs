using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.PreConditions;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Regresión del bug del viejo ataque a rango (slot 2, hoy Class Skill): la Selection heredada del EffChain (oculta
    /// por ShowSelection == false pero serializada en assets viejos con Range=1) gateaba
    /// el botón y el hover preview con un rango que nadie puede ver ni editar, mientras
    /// el targeting real usaba la selección de la fase 0 (Range=4).
    /// </summary>
    [TestFixture]
    public sealed class EffChainSelectionGatingTests
    {
        private GridManager _grid;
        private Guid _owner;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _grid = new GridManager();
            _owner = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
        }

        private static SelectionSettings AttackSelection(int range)
        {
            return new SelectionSettings
            {
                SlotState = SlotState.Occupied,
                EntityFilter = EntityFilterMask.Enemies,
                Range = range,
            };
        }

        // Chain como queda en CH_Warrior.asset: selection heredada "fantasma" (autoría
        // vieja, hoy invisible en el inspector) + la selección real en la fase 0.
        private static EffChain BuildChain(int hiddenRange, int phaseRange)
        {
            var inner = new TestEffect { Selection = AttackSelection(phaseRange) };
            return new EffChain
            {
                Selection = AttackSelection(hiddenRange),
                Phases = new List<ChainPhase>
                {
                    new ChainPhase
                    {
                        Effects = new EffectData { Effects = new List<IEffect> { inner } },
                    },
                },
            };
        }

        private static HeroActionBehavior BuildChainBehavior(EffChain chain)
        {
            return new HeroActionBehavior
            {
                Effects = new List<EffectData>
                {
                    new EffectData
                    {
                        PreConditions = new List<BasePreCondition>(),
                        Effects = new List<IEffect> { chain },
                    },
                },
            };
        }

        // ── A1: ShowSelection == false ⇒ el efecto no participa de la selección ──

        [Test]
        public void EffChain_HiddenInteractiveSelection_DoesNotRequireSelection()
        {
            // Arrange — la selection fantasma es interactiva (Occupied, manual): antes
            // del fix respondía "requiero selección BeforeRoll" y gateaba el botón.
            var chain = BuildChain(hiddenRange: 1, phaseRange: 4);

            // Act + Assert
            Assert.IsFalse(chain.HasSelectionRequirement(),
                "Un efecto con ShowSelection=false no tiene selection propia.");
            Assert.IsFalse(chain.RequiresSelectionAt(SelectionTiming.BeforeRoll),
                "La selection fantasma del chain no debe pedir selección pre-roll.");
        }

        [Test]
        public void EffChain_ValidateSelection_WithNullResult_Passes()
        {
            // Arrange — sin requerimiento de selección propia, no hay nada que validar.
            var chain = BuildChain(hiddenRange: 1, phaseRange: 4);

            // Act
            var valid = chain.ValidateSelection(null, _owner, out var error);

            // Assert
            Assert.IsTrue(valid, $"El chain no debe exigir un resultado de selección propio: {error}");
        }

        // ── Selección efectiva (gate del botón + hover preview) ──────────────

        [Test]
        public void ResolveEffectiveBeforeRollSelection_PlainEffect_ReturnsOwnSelection()
        {
            // Arrange
            var eff = new TestEffect { Selection = AttackSelection(2) };

            // Act + Assert — un efecto normal expone su propia selección.
            Assert.AreSame(eff.Selection,
                HeroActionBehavior.ResolveEffectiveBeforeRollSelection(eff));
        }

        [Test]
        public void ResolveEffectiveBeforeRollSelection_Chain_ReturnsPhase0Selection()
        {
            // Arrange
            var chain = BuildChain(hiddenRange: 1, phaseRange: 4);

            // Act
            var effective = HeroActionBehavior.ResolveEffectiveBeforeRollSelection(chain);

            // Assert — la selección efectiva es la de la fase (la que usa el handoff),
            // no la fantasma del chain.
            Assert.IsNotNull(effective);
            Assert.AreEqual(4, effective.Range);
        }

        [Test]
        public void ResolveEffectiveBeforeRollSelection_NestedChain_RecursesToInnerPhase()
        {
            // Arrange — chain cuya fase 0 contiene OTRO chain (mismo criterio de
            // recursión que FindDealDamageIn para la fórmula de daño).
            var innerChain = BuildChain(hiddenRange: 1, phaseRange: 3);
            var outer = new EffChain
            {
                Selection = AttackSelection(1),
                Phases = new List<ChainPhase>
                {
                    new ChainPhase
                    {
                        Effects = new EffectData { Effects = new List<IEffect> { innerChain } },
                    },
                },
            };

            // Act
            var effective = HeroActionBehavior.ResolveEffectiveBeforeRollSelection(outer);

            // Assert
            Assert.IsNotNull(effective);
            Assert.AreEqual(3, effective.Range);
        }

        [Test]
        public void ResolveEffectiveBeforeRollSelection_ChainWithSelfPhase_ReturnsNull()
        {
            // Arrange — fase 0 con efecto Self (sin interacción): no hay selección
            // efectiva pre-roll y el gate no debe chequear targets.
            var inner = new TestEffect
            {
                Selection = new SelectionSettings { SlotState = SlotState.Self },
            };
            var chain = new EffChain
            {
                Selection = AttackSelection(1),
                Phases = new List<ChainPhase>
                {
                    new ChainPhase
                    {
                        Effects = new EffectData { Effects = new List<IEffect> { inner } },
                    },
                },
            };

            // Act + Assert
            Assert.IsNull(HeroActionBehavior.ResolveEffectiveBeforeRollSelection(chain));
        }

        // ── A2: el gate del botón usa la selección de la fase ────────────────

        [Test]
        public void HasUsableEffectGroup_ChainBehavior_EnemyWithinPhaseRange_IsUsable()
        {
            // Arrange — EL bug: enemigo a Manhattan 4, fase con Range 4, selection
            // fantasma del chain con Range 1. Antes del fix el botón quedaba muerto.
            _grid.LoadRoom(NavGraph.Rect(6, 1));
            _grid.Register(_owner, new GridCoord(0, 0));
            var enemy = Guid.NewGuid();
            _grid.Register(enemy, new GridCoord(4, 0));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);
            var behavior = BuildChainBehavior(BuildChain(hiddenRange: 1, phaseRange: 4));

            // Act
            var usable = behavior.HasUsableEffectGroup(_owner, enemy, out var reason);

            // Assert
            Assert.IsTrue(usable,
                $"El gate debe usar el rango de la fase (4), no el de la selection fantasma (1): {reason}");
        }

        [Test]
        public void HasUsableEffectGroup_ChainBehavior_EnemyBeyondPhaseRange_IsNotUsable()
        {
            // Arrange — enemigo a Manhattan 5, fuera del rango 4 de la fase.
            _grid.LoadRoom(NavGraph.Rect(6, 1));
            _grid.Register(_owner, new GridCoord(0, 0));
            var enemy = Guid.NewGuid();
            _grid.Register(enemy, new GridCoord(5, 0));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);
            var behavior = BuildChainBehavior(BuildChain(hiddenRange: 1, phaseRange: 4));

            // Act
            var usable = behavior.HasUsableEffectGroup(_owner, enemy, out var reason);

            // Assert
            Assert.IsFalse(usable, "Sin targets dentro del rango de la fase el botón no se habilita.");
            Assert.IsNotNull(reason);
        }

        [Test]
        public void HasUsableEffectGroup_SelfOnlyChainBehavior_UsableWithoutTargets()
        {
            // Arrange — el shape de la acción Defense (Feature#0051): chain de UNA
            // fase con efecto Self. Sin selección efectiva pre-roll no hay gate de
            // rango: el chip debe estar usable aunque no haya enemigos en rango.
            var inner = new TestEffect
            {
                Selection = new SelectionSettings { SlotState = SlotState.Self },
            };
            var chain = new EffChain
            {
                Phases = new List<ChainPhase>
                {
                    new ChainPhase
                    {
                        Effects = new EffectData { Effects = new List<IEffect> { inner } },
                    },
                },
            };
            var behavior = BuildChainBehavior(chain);

            // Act
            var usable = behavior.HasUsableEffectGroup(_owner, Guid.Empty, out var reason);

            // Assert
            Assert.IsTrue(usable, $"Una acción Self (defensa pura) no gatea por targets: {reason}");
        }

        // ── Semántica top-level intacta (FSM / handoff no-chain) ─────────────

        [Test]
        public void HasEffectsWithSelectionAt_ChainOnlyBehavior_IsFalse()
        {
            // Arrange + Act + Assert — intencional: el chain NO pide selección pre-roll
            // propia (el handoff la maneja por fase), así que el FSM saltea el sub-estado
            // de selección. Los behaviors no-chain (Movement) no cambian.
            var behavior = BuildChainBehavior(BuildChain(hiddenRange: 1, phaseRange: 4));
            Assert.IsFalse(behavior.HasEffectsWithSelectionAt(SelectionTiming.BeforeRoll));
        }

        private sealed class TestEffect : BaseEffect
        {
            public override bool ApplyEffect(EffectContext context) => true;
        }
    }
}
