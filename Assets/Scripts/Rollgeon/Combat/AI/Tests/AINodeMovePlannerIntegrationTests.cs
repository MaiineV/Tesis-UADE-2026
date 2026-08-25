using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Pathing;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Tiles;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Integración AINode_Move + planner + SpecialTileService real: la IA evita una casilla
    /// que viola su filtro de supervivencia y camina la ruta planeada.
    /// </summary>
    [TestFixture]
    public class AINodeMovePlannerIntegrationTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private UnitTraitService _traits;
        private SpecialTileService _tiles;
        private AttributesManager _attributes;
        private Guid _enemy;
        private Guid _player;
        private SpecialTileDefinitionSO _fireDef;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(7, 1));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _movement = new MovementService(_grid);
            ServiceLocator.AddService<IMovementService>(_movement, ServiceScope.Global);

            _traits = new UnitTraitService();
            ServiceLocator.AddService<IUnitTraitService>(_traits, ServiceScope.Global);

            ServiceLocator.AddService<IDamagePipeline>(new NullDamagePipeline(), ServiceScope.Global);

            _attributes = new AttributesManager();

            _player = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(5, 0));

            _enemy = Guid.NewGuid();
            _grid.Register(_enemy, new GridCoord(0, 0));
            _traits.Register(_enemy, UnitTraits.DefaultGround);
            var enemyAttrs = new ModifiableAttributes();
            enemyAttrs.EnsureInitialized();
            enemyAttrs.SetAttribute<Health>(new Health(60));
            _attributes.Register(_enemy, enemyAttrs);

            _tiles = new SpecialTileService();
            _tiles.ConfigureForTests(() => _player);

            _fireDef = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            _fireDef.TileType = SpecialTileType.Fire;
            _fireDef.Triggers = TileTrigger.OnEnter;
            _fireDef.Category = TileEffectCategory.Damage;
            _fireDef.EnterDamage = 50;
        }

        [TearDown]
        public void TearDown()
        {
            _tiles?.Dispose();
            _attributes?.Dispose();
            if (_fireDef != null) UnityEngine.Object.DestroyImmediate(_fireDef);
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private AIContext MakeContext(IAIPathPlanner planner) => new AIContext
        {
            SelfGuid = _enemy,
            PlayerGuid = _player,
            SelfMaxHp = 100,
            Attributes = _attributes,
            Grid = _grid,
            Movement = _movement,
            PathPlanner = planner,
            Personality = AIPersonalityProfile.Default,
        };

        [Test]
        public void MoveNode_WithPlanner_StopsBeforeTileThatViolatesSurvival()
        {
            // Fuego de 50 en (2,0): con 60/100 de vida, 60−50 = 10 ≤ 20% → la ruta muere ahí.
            _tiles.Place(_fireDef, new[] { new GridCoord(2, 0) });
            var planner = new AIPathPlanner(_grid, _tiles);
            var context = MakeContext(planner);
            var node = new AINode_Move();

            var result = node.Tick(context);

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsTrue(_grid.TryGetPosition(_enemy, out var pos));
            Assert.AreEqual(new GridCoord(1, 0), pos,
                "El planner descarta la ruta a través del fuego letal y avanza hasta el borde seguro.");
            Assert.AreEqual(60, _attributes.GetAttribute<Health>(_enemy).Value,
                "Nunca pisó el fuego: la vida quedó intacta.");
        }

        [Test]
        public void MoveNode_WithPlannerAndCleanRoom_MatchesLegacyDestination()
        {
            var planner = new AIPathPlanner(_grid, _tiles); // sin tiles colocadas
            var context = MakeContext(planner);
            var node = new AINode_Move();

            var result = node.Tick(context);

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsTrue(_grid.TryGetPosition(_enemy, out var pos));
            Assert.AreEqual(new GridCoord(3, 0), pos,
                "Sala limpia: mismo destino que el scoring legacy (3 pasos hacia la banda 1).");
        }

        // -----------------------------------------------------------------
        // BUG-061: planner NoMove (enemigo en una isla del NavGraph) ⇒ el nodo
        // no aborta el turno, y un AINode_Sequence sin Selector que lo envuelva
        // sigue con el resto de los hijos (ej. el ataque).
        // -----------------------------------------------------------------

        [Test]
        public void MoveNode_PlannerReturnsNoMove_SucceedsAsNoOp()
        {
            // Arrange — planner stub que siempre reporta "no hay a dónde ir" (isla).
            var context = MakeContext(new AlwaysNoMovePlanner());
            var node = new AINode_Move();

            // Act
            var result = node.Tick(context);

            // Assert — no-op, no error: no se movió y no consumió la acción de movimiento.
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsFalse(context.HasExecuted("__move"), "No se movió: no debe consumir la acción de movimiento.");
            Assert.IsTrue(_grid.TryGetPosition(_enemy, out var pos));
            Assert.AreEqual(new GridCoord(0, 0), pos, "Sin plan, el enemigo se queda donde está.");
        }

        [Test]
        public void Sequence_MoveThenMarker_PlannerReturnsNoMove_MarkerStillRuns()
        {
            // Arrange — Sequence[Move, Marker] SIN Selector que absorba el Failed. Antes de
            // BUG-061, un planner en NoMove hacía que AINode_Move devolviera Failed y el
            // Sequence abortaba ANTES del marcador (ej. el ataque nunca corría).
            var context = MakeContext(new AlwaysNoMovePlanner());
            var marker = new MarkerNode();
            var sequence = new AINode_Sequence
            {
                Children = new System.Collections.Generic.List<AIDecisionNode>
                {
                    new AINode_Move(),
                    marker,
                },
            };

            // Act
            var result = sequence.Tick(context);

            // Assert — el Sequence entero sucede, y el marcador (equivalente al ataque) corrió.
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsTrue(marker.Ticked, "El hijo posterior al Move debe correr aunque el planner diga NoMove.");
        }

        private sealed class AlwaysNoMovePlanner : IAIPathPlanner
        {
            public AIPathPlanResult PlanMove(in AIPathRequest request) => AIPathPlanResult.NoMove;
        }

        // Nodo de prueba mínimo: registra si corrió, sin efectos de juego — hace de proxy
        // del "resto del turno" (ataque) que BUG-061 dejaba sin correr.
        private sealed class MarkerNode : AIDecisionNode
        {
            public bool Ticked;
            public override string NodeName => "Marker";
            public override AIResult Tick(AIContext context)
            {
                Ticked = true;
                return AIResult.Succeeded;
            }
        }

        private sealed class NullDamagePipeline : IDamagePipeline
        {
            public DamageContext Resolve(DamageContext ctx) { ctx.FinalDamage = ctx.BaseDamage; return ctx; }
            public DamageContext Preview(DamageContext ctx) => ctx;
        }
    }
}
