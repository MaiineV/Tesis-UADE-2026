using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Combat.AI.Tests
{
    [TestFixture]
    public class TreeDrivenEnemyAITests
    {
        private AttributesManager _attrs;
        private EnemyAIRegistry _registry;
        private FakePlayerService _player;
        private Guid _playerId;
        private Guid _enemyId;
        private int _turnCompleteCount;
        private SpyDamagePipeline _pipeline;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            _attrs = new AttributesManager();
            _registry = new EnemyAIRegistry();
            _playerId = Guid.NewGuid();
            _enemyId = Guid.NewGuid();
            _turnCompleteCount = 0;
            _pipeline = new SpyDamagePipeline();
            _player = new FakePlayerService(_playerId);

            var pAttrs = new ModifiableAttributes();
            pAttrs.EnsureInitialized();
            pAttrs.SetAttribute<Health>(new Health(100));
            _attrs.Register(_playerId, pAttrs);

            var eAttrs = new ModifiableAttributes();
            eAttrs.EnsureInitialized();
            eAttrs.SetAttribute<Health>(new Health(50));
            eAttrs.SetAttribute<Attack>(new Attack(7));
            _attrs.Register(_enemyId, eAttrs);
        }

        [TearDown]
        public void TearDown()
        {
            _attrs?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private TreeDrivenEnemyAI NewHandler()
        {
            var fallback = new BasicEnemyAI(_attrs, _player, _pipeline, () => { });
            return new TreeDrivenEnemyAI(_registry, _attrs, _player, _pipeline, fallback,
                () => _turnCompleteCount++);
        }

        [Test]
        public void NoRegisteredRoot_FallsBackToBasicEnemyAI()
        {
            using var handler = NewHandler();
            handler.HandleEnemyTurn(_enemyId);

            Assert.IsTrue(_pipeline.ResolveCalled, "Fallback debería invocar el damage pipeline.");
            Assert.AreEqual(7, _pipeline.LastContext.BaseDamage);
            // BasicEnemyAI tiene su propio onTurnComplete (no-op aquí); TreeDrivenEnemyAI
            // NO debería llamar al outer onTurnComplete tampoco en fallback path.
            Assert.AreEqual(0, _turnCompleteCount);
        }

        [Test]
        public void TreeRegistered_TicksRoot_AndCallsTurnComplete()
        {
            var spy = new SpyNode();
            _registry.Register(_enemyId, spy, maxHp: 50);

            using var handler = NewHandler();
            handler.HandleEnemyTurn(_enemyId);

            Assert.AreEqual(1, spy.TickCount);
            Assert.AreEqual(1, _turnCompleteCount, "Tree path debe llamar onTurnComplete exactamente una vez.");
        }

        [Test]
        public void ReinforcementSpawned_DefersFirstTurn_NoDamage_ThenActs()
        {
            // Arrange — un refuerzo con árbol registrado, avisado por el bus igual que lo hace
            // AINode_SpawnReinforcements al sumarlo a la ronda en curso.
            var spy = new SpyNode();
            _registry.Register(_enemyId, spy, maxHp: 50);
            using var handler = NewHandler();
            EventManager.Trigger(EventName.OnReinforcementSpawned, _enemyId);

            // Act — turno de aparición.
            handler.HandleEnemyTurn(_enemyId);

            // Assert — NO tickeó el árbol (nada de daño gratis) pero el turno se cerró.
            Assert.AreEqual(0, spy.TickCount,
                "El refuerzo no debe actuar en la ronda en que aparece.");
            Assert.AreEqual(1, _turnCompleteCount,
                "El turno diferido igual debe cerrarse para avanzar la cola.");

            // Act — turno siguiente.
            handler.HandleEnemyTurn(_enemyId);

            // Assert — ya actúa normal.
            Assert.AreEqual(1, spy.TickCount,
                "En su segundo turno el refuerzo debe tickear su árbol.");
            Assert.AreEqual(2, _turnCompleteCount);
        }

        [Test]
        public void ReinforcementSpawned_DefersOnlyTheSpawnedGuid()
        {
            // Arrange — el aviso es para _enemyId; otro enemigo NO debe verse afectado.
            var other = Guid.NewGuid();
            var spawnedSpy = new SpyNode();
            var otherSpy = new SpyNode();
            _registry.Register(_enemyId, spawnedSpy, maxHp: 50);
            _registry.Register(other, otherSpy, maxHp: 50);
            using var handler = NewHandler();
            EventManager.Trigger(EventName.OnReinforcementSpawned, _enemyId);

            // Act
            handler.HandleEnemyTurn(other);

            // Assert — el enemigo no-refuerzo actúa de inmediato.
            Assert.AreEqual(1, otherSpy.TickCount,
                "Un enemigo que no es el refuerzo avisado no debe diferirse.");
            Assert.AreEqual(0, spawnedSpy.TickCount);
        }

        [Test]
        public void TreeThrows_StillCallsTurnComplete()
        {
            _registry.Register(_enemyId, new ThrowingNode(), maxHp: 50);

            using var handler = NewHandler();
            LogAssert.Expect(LogType.Error, new Regex(@"\[TreeDrivenEnemyAI\] Exception ticking AIRoot"));
            Assert.DoesNotThrow(() => handler.HandleEnemyTurn(_enemyId));
            Assert.AreEqual(1, _turnCompleteCount, "Excepción en tick NO debe bloquear el turn complete.");
        }

        [Test]
        public void BehaviorNode_DealsDamageThroughPipeline()
        {
            // Composición canónica del ataque: AINode_Behavior → EnemyActionBehavior →
            // EffDealDamage. El damage pipeline llega via ServiceLocator (lectura del EffDealDamage).
            ServiceLocator.AddService<IDamagePipeline>(_pipeline);

            var behavior = new EnemyActionBehavior
            {
                ActionName = "Attack",
                Effects = new List<EffectData>
                {
                    new EffectData
                    {
                        Effects = new List<IEffect> { new EffDealDamage() },
                    }
                },
            };
            _registry.Register(_enemyId, new AINode_Behavior { Behavior = behavior }, maxHp: 50);

            using var handler = NewHandler();
            handler.HandleEnemyTurn(_enemyId);

            Assert.IsTrue(_pipeline.ResolveCalled);
            Assert.AreEqual(_enemyId, _pipeline.LastContext.SourceId);
            Assert.AreEqual(_playerId, _pipeline.LastContext.TargetId);
        }

        [Test]
        public void RoundIndex_IncrementsOnTurnQueueBuilt()
        {
            using var handler = NewHandler();
            Assert.AreEqual(0, handler.CurrentRoundIndex);

            EventManager.Trigger(EventName.OnTurnQueueBuilt,
                (System.Collections.Generic.IReadOnlyList<Guid>)new Guid[0],
                0);
            Assert.AreEqual(1, handler.CurrentRoundIndex);

            EventManager.Trigger(EventName.OnTurnQueueBuilt,
                (System.Collections.Generic.IReadOnlyList<Guid>)new Guid[0],
                2);
            Assert.AreEqual(3, handler.CurrentRoundIndex);
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            var handler = NewHandler();
            handler.Dispose();

            EventManager.Trigger(EventName.OnTurnQueueBuilt,
                (System.Collections.Generic.IReadOnlyList<Guid>)new Guid[0],
                99);
            Assert.AreEqual(0, handler.CurrentRoundIndex);
        }

        // ---- Fakes -------------------------------------------------------

        // --- La apertura del combate -------------------------------------------------

        /// <summary>Dispara el evento con el que <c>TurnOrderService</c> anuncia la cola de la ronda.</summary>
        private void BuildQueue(int round) =>
            EventManager.Trigger(EventName.OnTurnQueueBuilt, new List<Guid> { _playerId, _enemyId }, round);

        /// <summary>
        /// La cola se arma en <c>CombatEnterState</c> antes de pasar a <c>PlayerTurnState</c>, así que
        /// el estado de sala del jefe tiene que quedar puesto acá: si no, la mesa, el peaje y el dado
        /// confiscado aparecen recién al cerrar el primer turno.
        /// </summary>
        [Test]
        public void QueueBuilt_OnTheOpeningRound_InstallsBossStateWithoutTickingTheTree()
        {
            // Arrange
            var spy = new OpeningSpyNode();
            _registry.Register(_enemyId, spy, maxHp: 50);
            using var handler = NewHandler();

            // Act
            BuildQueue(round: 0);

            // Assert
            Assert.AreEqual(1, spy.OpeningCount, "El jefe instala su estado antes del primer turno del jugador.");
            Assert.AreEqual(0, spy.TickCount, "La apertura instala amenaza, no la ejecuta: el árbol no tickea.");
            Assert.AreEqual(0, _turnCompleteCount, "Abrir no consume un turno de la cola.");
        }

        [Test]
        public void QueueBuilt_OnLaterRounds_DoesNotOpenAgain()
        {
            // Arrange
            var spy = new OpeningSpyNode();
            _registry.Register(_enemyId, spy, maxHp: 50);
            using var handler = NewHandler();

            // Act — el mismo evento vuelve a disparar en cada ronda.
            BuildQueue(round: 0);
            BuildQueue(round: 1);
            BuildQueue(round: 2);

            // Assert
            Assert.AreEqual(1, spy.OpeningCount, "Abrir de nuevo volvería a poner la mesa a mitad de pelea.");
        }

        [Test]
        public void Opening_DescendsThroughUnconditionalComposites()
        {
            // Arrange
            var deep = new OpeningSpyNode();
            _registry.Register(_enemyId, new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    new AINode_Selector { Children = new List<AIDecisionNode> { deep } },
                },
            }, maxHp: 50);
            using var handler = NewHandler();

            // Act
            BuildQueue(round: 0);

            // Assert
            Assert.AreEqual(1, deep.OpeningCount, "Los jefes autoran su raíz como Sequence de Selectors.");
        }

        [Test]
        public void Opening_DoesNotReachNodesBehindAConditionalGate()
        {
            // Arrange — un If es la puerta de una fase o de una cadencia.
            var gated = new OpeningSpyNode();
            _registry.Register(_enemyId, new AINode_Sequence
            {
                Children = new List<AIDecisionNode> { new AINode_If { Then = gated } },
            }, maxHp: 50);
            using var handler = NewHandler();

            // Act
            BuildQueue(round: 0);

            // Assert
            Assert.AreEqual(0, gated.OpeningCount,
                "Abrir una rama gateada pondría en la mesa algo que el jefe todavía no se ganó.");
        }

        [Test]
        public void Opening_WhenOneNodeThrows_StillOpensTheRest()
        {
            // Arrange
            var survivor = new OpeningSpyNode();
            _registry.Register(_enemyId, new AINode_Sequence
            {
                Children = new List<AIDecisionNode> { new ThrowingOpeningNode(), survivor },
            }, maxHp: 50);
            using var handler = NewHandler();
            LogAssert.Expect(LogType.Error, new Regex("Exception opening"));

            // Act
            BuildQueue(round: 0);

            // Assert
            Assert.AreEqual(1, survivor.OpeningCount,
                "Un nodo roto no puede dejar la pelea arrancando sin el resto del estado de sala.");
        }

        private sealed class SpyNode : AIDecisionNode
        {
            public int TickCount;
            public override AIResult Tick(AIContext context) { TickCount++; return AIResult.Succeeded; }
        }

        private sealed class OpeningSpyNode : AIDecisionNode, IAIOpeningNode
        {
            public int TickCount;
            public int OpeningCount;
            public override AIResult Tick(AIContext context) { TickCount++; return AIResult.Succeeded; }
            public void Opening(AIContext context) => OpeningCount++;
        }

        private sealed class ThrowingOpeningNode : AIDecisionNode, IAIOpeningNode
        {
            public override AIResult Tick(AIContext context) => AIResult.Succeeded;
            public void Opening(AIContext context) => throw new InvalidOperationException("boom");
        }

        private sealed class ThrowingNode : AIDecisionNode
        {
            public override AIResult Tick(AIContext context) => throw new InvalidOperationException("boom");
        }

        private sealed class FakePlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; }
            public Guid RunId => Guid.NewGuid();
            public ClassHeroSO CurrentHero => null;
            public Rollgeon.Dice.DiceBagSO DiceBag => null;
            public FakePlayerService(Guid id) { PlayerGuid = id; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(Rollgeon.Dice.DiceBagSO bag) { }
            public void ClearPlayer() { }
#pragma warning disable CS0067
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore CS0067
        }

        private sealed class SpyDamagePipeline : IDamagePipeline
        {
            public bool ResolveCalled;
            public DamageContext LastContext;

            public DamageContext Resolve(DamageContext ctx)
            {
                ResolveCalled = true;
                LastContext = ctx;
                ctx.FinalDamage = ctx.BaseDamage;
                return ctx;
            }

            public DamageContext Preview(DamageContext ctx)
            {
                ctx.FinalDamage = ctx.BaseDamage;
                return ctx;
            }
        }
    }
}
