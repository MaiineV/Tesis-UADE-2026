using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Regla "sin repetir acciones" por turno: la energía es el presupuesto de acciones
    /// del enemigo, pero cada acción (behavior por nombre, move, kite) corre a lo sumo
    /// una vez por turno — como el player. Los behaviors de bookkeeping de energía
    /// (Reset/Remove Energy) quedan exentos para que el While drene el presupuesto.
    /// Regresión de: enemigos pegando 2-3 veces por turno (pre-b842beed) y enemigos
    /// haciendo una sola acción total (post-b842beed).
    /// </summary>
    /// <remarks>
    /// Usa <see cref="GridManager"/> + <see cref="MovementService"/> reales sobre grilla
    /// abierta (patrón de <c>AINode_MoveTests</c>); AttributesManager/IGridManager/
    /// IDamagePipeline via ServiceLocator porque los effects los resuelven de ahí.
    /// Un <see cref="AIContext"/> fresco equivale a un turno nuevo (así lo construye
    /// <c>TreeDrivenEnemyAI.BuildContext</c>).
    /// </remarks>
    [TestFixture]
    public class NoRepeatActionPerTurnTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private AttributesManager _attrs;
        private CountingDamagePipeline _pipeline;
        private Guid _enemy;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(15, 15));
            _movement = new MovementService(_grid);
            _attrs = new AttributesManager();
            _pipeline = new CountingDamagePipeline();
            _enemy = Guid.NewGuid();
            _player = Guid.NewGuid();

            var enemyAttrs = new ModifiableAttributes();
            enemyAttrs.EnsureInitialized();
            enemyAttrs.SetAttribute<Health>(new Health(50));
            enemyAttrs.SetAttribute<Attack>(new Attack(7));
            enemyAttrs.SetAttribute<Energy>(new Energy(0));
            _attrs.Register(_enemy, enemyAttrs);

            var playerAttrs = new ModifiableAttributes();
            playerAttrs.EnsureInitialized();
            playerAttrs.SetAttribute<Health>(new Health(100));
            _attrs.Register(_player, playerAttrs);

            ServiceLocator.AddService<AttributesManager>(_attrs);
            ServiceLocator.AddService<IGridManager>(_grid);
            ServiceLocator.AddService<IDamagePipeline>(_pipeline);
        }

        [TearDown]
        public void TearDown()
        {
            _attrs?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ----- helpers ---------------------------------------------------

        private AIContext NewTurnContext() => new AIContext
        {
            SelfGuid = _enemy,
            PlayerGuid = _player,
            Attributes = _attrs,
            Grid = _grid,
            Movement = _movement,
            DamagePipeline = _pipeline,
        };

        private void SetEnergy(int value) =>
            _attrs.SetAttributeValue<Energy, int>(_enemy, value);

        private int GetEnergy() =>
            _attrs.GetAttributeValue<Energy, int>(_enemy);

        private static AIIntReader Const(int v) => new AIConstantInt { Value = v };

        private static EnemyActionBehavior EnergyBookkeepingBehavior(string name, IntOperation op) =>
            new EnemyActionBehavior
            {
                ActionName = name,
                TargetSelector = new TargetSelector_Self(),
                Effects = new List<EffectData>
                {
                    new EffectData
                    {
                        Effects = new List<IEffect>
                        {
                            new EffModifyIntAttribute
                            {
                                TargetStat = StatType.Energy,
                                Operation = op,
                            },
                        },
                    },
                },
            };

        private static EnemyActionBehavior AttackBehavior() =>
            new EnemyActionBehavior
            {
                ActionName = "Attack",
                Effects = new List<EffectData>
                {
                    new EffectData { Effects = new List<IEffect> { new EffDealDamage() } },
                },
            };

        /// <summary>Réplica en código del árbol melee de ED_MeleeCardEnemy (sin el reset inicial
        /// — la energía se siembra directo en el atributo).</summary>
        private static AINode_While MeleeTree() => new AINode_While
        {
            TargetSelector = new TargetSelector_Self(),
            Conditions = new List<BasePreCondition>
            {
                new PcOwnerStatCompare
                {
                    Stat = StatType.Energy,
                    Comparison = IntComparison.Greater,
                    Value = 0,
                },
            },
            Body = new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    new AINode_Behavior
                    {
                        Behavior = EnergyBookkeepingBehavior("Remove Energy", IntOperation.Subtract),
                    },
                    new AINode_If
                    {
                        TargetSelector = new TargetSelector_AlwaysPlayer(),
                        Conditions = new List<BasePreCondition> { new PCEntityInRange { MaxRange = 1 } },
                        Then = new AINode_Behavior { Behavior = AttackBehavior() },
                        Else = new AINode_Move { MaxSteps = Const(3), DesiredRange = Const(1) },
                    },
                },
            },
            MaxIterations = 16,
        };

        private int Dist(Guid a, Guid b)
        {
            _grid.TryGetPosition(a, out var ca);
            _grid.TryGetPosition(b, out var cb);
            return ca.Manhattan(cb);
        }

        // ----- tests: árbol melee completo --------------------------------

        [Test]
        public void Tick_MeleeTree_PlayerAdjacent_Energy3_AttacksExactlyOnce()
        {
            // Arrange — bug original: con energía 3 y player al lado, pegaba 3 veces.
            _grid.Register(_enemy, new GridCoord(0, 0));
            _grid.Register(_player, new GridCoord(1, 0));
            SetEnergy(3);
            var tree = MeleeTree();

            // Act
            var result = tree.Tick(NewTurnContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(1, _pipeline.ResolveCount, "El ataque debe correr una sola vez por turno.");
            Assert.AreEqual(0, GetEnergy(), "El While debe drenar todo el presupuesto de energía.");
        }

        [Test]
        public void Tick_MeleeTree_PlayerThreeTilesAway_MovesAndAttacksSameTurn()
        {
            // Arrange — regresión del fix anterior: con energía 1 el enemigo movía O pegaba.
            _grid.Register(_enemy, new GridCoord(0, 0));
            _grid.Register(_player, new GridCoord(3, 0));
            SetEnergy(3);
            var tree = MeleeTree();

            // Act
            var result = tree.Tick(NewTurnContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(1, Dist(_enemy, _player), "Debe acercarse hasta quedar adyacente.");
            Assert.AreEqual(1, _pipeline.ResolveCount, "Debe mover Y atacar en el mismo turno, un ataque.");
        }

        [Test]
        public void Tick_MeleeTree_FreshContextPerTurn_AttacksAgainNextTurn()
        {
            // Arrange — contexto nuevo == turno nuevo (TreeDrivenEnemyAI.BuildContext).
            _grid.Register(_enemy, new GridCoord(0, 0));
            _grid.Register(_player, new GridCoord(1, 0));
            var tree = MeleeTree();

            // Act
            SetEnergy(3);
            tree.Tick(NewTurnContext());
            SetEnergy(3);
            tree.Tick(NewTurnContext());

            // Assert
            Assert.AreEqual(2, _pipeline.ResolveCount, "La regla no-repeat resetea por turno.");
        }

        // ----- tests: exención de bookkeeping ------------------------------

        [Test]
        public void Tick_EnergyBookkeepingBehavior_RepeatsWithinSameTurn()
        {
            // Arrange
            SetEnergy(3);
            var node = new AINode_Behavior
            {
                Behavior = EnergyBookkeepingBehavior("Remove Energy", IntOperation.Subtract),
            };
            var ctx = NewTurnContext();

            // Act
            node.Tick(ctx);
            node.Tick(ctx);
            node.Tick(ctx);

            // Assert
            Assert.AreEqual(0, GetEnergy(), "El bookkeeping de energía debe poder repetirse en el turno.");
        }

        [Test]
        public void Tick_AttackBehavior_SecondTickSameContext_SkipsExecution()
        {
            // Arrange
            _grid.Register(_enemy, new GridCoord(0, 0));
            _grid.Register(_player, new GridCoord(1, 0));
            var node = new AINode_Behavior { Behavior = AttackBehavior() };
            var ctx = NewTurnContext();

            // Act
            var first = node.Tick(ctx);
            var second = node.Tick(ctx);

            // Assert
            Assert.AreEqual(AIResult.Succeeded, first);
            Assert.AreEqual(AIResult.Succeeded, second, "El skip debe ser transparente, no Failed.");
            Assert.AreEqual(1, _pipeline.ResolveCount);
        }

        [Test]
        public void IsEnergyBookkeeping_EnergyOnlyEffects_ReturnsTrue()
        {
            var behavior = EnergyBookkeepingBehavior("Enemy Reset Energy", IntOperation.Set);

            Assert.IsTrue(behavior.IsEnergyBookkeeping);
        }

        [Test]
        public void IsEnergyBookkeeping_WithDealDamage_ReturnsFalse()
        {
            Assert.IsFalse(AttackBehavior().IsEnergyBookkeeping);
        }

        [Test]
        public void IsEnergyBookkeeping_NonEnergyAttributeModifier_ReturnsFalse()
        {
            // Arrange — mismo effect type que el bookkeeping pero sobre otro stat:
            // modificar Health/Attack ES una acción, no administración de energía.
            var behavior = new EnemyActionBehavior
            {
                ActionName = "Weaken",
                Effects = new List<EffectData>
                {
                    new EffectData
                    {
                        Effects = new List<IEffect>
                        {
                            new EffModifyIntAttribute
                            {
                                TargetStat = StatType.Attack,
                                Operation = IntOperation.Subtract,
                            },
                        },
                    },
                },
            };

            Assert.IsFalse(behavior.IsEnergyBookkeeping);
        }

        // ----- tests: nodos de movimiento ----------------------------------

        [Test]
        public void Tick_MoveNode_SecondTickSameContext_DoesNotMoveAgain()
        {
            // Arrange — player lejos: un solo move por turno aunque siga fuera de banda.
            _grid.Register(_enemy, new GridCoord(0, 0));
            _grid.Register(_player, new GridCoord(8, 0));
            var node = new AINode_Move { MaxSteps = Const(3), DesiredRange = Const(1) };
            var ctx = NewTurnContext();

            // Act
            var first = node.Tick(ctx);
            var second = node.Tick(ctx);

            // Assert
            Assert.AreEqual(AIResult.Succeeded, first);
            Assert.AreEqual(5, Dist(_enemy, _player), "Primer tick: avanza MaxSteps.");
            Assert.AreEqual(AIResult.Succeeded, second, "Segundo tick: no-op transparente.");
            Assert.AreEqual(5, Dist(_enemy, _player), "Segundo tick no debe mover de nuevo.");
        }

        [Test]
        public void Tick_KeepDistanceNode_SecondTickSameContext_DoesNotMoveAgain()
        {
            // Arrange — demasiado cerca del player: kitea una sola vez por turno.
            _grid.Register(_enemy, new GridCoord(7, 0));
            _grid.Register(_player, new GridCoord(8, 0));
            var node = new AINode_KeepDistance { MaxSteps = Const(2), IdealDistance = Const(5) };
            var ctx = NewTurnContext();

            // Act
            var first = node.Tick(ctx);
            int distAfterFirst = Dist(_enemy, _player);
            var second = node.Tick(ctx);

            // Assert
            Assert.AreEqual(AIResult.Succeeded, first);
            Assert.Greater(distAfterFirst, 1, "Primer tick: se aleja.");
            Assert.AreEqual(AIResult.Succeeded, second, "Segundo tick: no-op transparente.");
            Assert.AreEqual(distAfterFirst, Dist(_enemy, _player), "Segundo tick no debe kitear de nuevo.");
        }

        [Test]
        public void Tick_MoveNode_FailedMove_DoesNotConsumeAction()
        {
            // Arrange — ya está en banda (dist == DesiredRange): el nodo falla sin mover
            // y NO debe consumir la acción de movimiento del turno.
            _grid.Register(_enemy, new GridCoord(7, 0));
            _grid.Register(_player, new GridCoord(8, 0));
            var node = new AINode_Move { MaxSteps = Const(3), DesiredRange = Const(1) };
            var ctx = NewTurnContext();

            // Act
            var result = node.Tick(ctx);

            // Assert
            Assert.AreEqual(AIResult.Failed, result);
            Assert.IsFalse(ctx.HasExecuted("__move"), "Un move fallido no consume la acción.");
        }

        // ----- fakes -------------------------------------------------------

        private sealed class CountingDamagePipeline : IDamagePipeline
        {
            public int ResolveCount;

            public DamageContext Resolve(DamageContext ctx)
            {
                ResolveCount++;
                ctx.FinalDamage = ctx.BaseDamage;
                return ctx;
            }
        }
    }
}
