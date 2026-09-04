using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.Movement;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests del nodo <see cref="AINode_Move"/> (rework "Move Toward Target"): approach con
    /// freno en <c>DesiredRange</c>, retreat (kite) configurable, retrocompatibilidad con el
    /// viejo "Move Toward Player" (Target null = player, DesiredRange null = StopAdjacent),
    /// target configurable via selector, y guards de servicios/target inválido.
    /// </summary>
    /// <remarks>
    /// Usa instancias reales de <see cref="GridManager"/> + <see cref="MovementService"/>
    /// (POCOs) sobre una grilla abierta; <c>VisualService</c> null ⇒ el nodo resuelve sin
    /// esperar animación (retorna <see cref="AIResult.Succeeded"/>).
    /// </remarks>
    [TestFixture]
    public class AINode_MoveTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private Guid _self;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(15, 15));
            _movement = new MovementService(_grid);
            _self = Guid.NewGuid();
            _player = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        // ----- helpers ---------------------------------------------------

        private AIContext Ctx() => new AIContext
        {
            SelfGuid = _self,
            PlayerGuid = _player,
            Grid = _grid,
            Movement = _movement,
        };

        private static AIIntReader Const(int v) => new AIConstantInt { Value = v };

        private int Dist(Guid a, Guid b)
        {
            _grid.TryGetPosition(a, out var ca);
            _grid.TryGetPosition(b, out var cb);
            return ca.Manhattan(cb);
        }

        // ----- tests -----------------------------------------------------

        [Test]
        public void Tick_ApproachesAndStopsAtDesiredRange()
        {
            // Arrange — self(0,0), player(8,0) dist 8; rango deseado 2.
            _grid.Register(_self, new GridCoord(0, 0));
            _grid.Register(_player, new GridCoord(8, 0));
            var node = new AINode_Move { MaxSteps = Const(10), DesiredRange = Const(2) };

            // Act
            var result = node.Tick(Ctx());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(2, Dist(_self, _player), "Debe frenar a la distancia deseada.");
        }

        [Test]
        public void Tick_AlreadyAtDesiredRange_DoesNotMove()
        {
            // Arrange — self(6,0), player(8,0) dist 2 == DesiredRange.
            _grid.Register(_self, new GridCoord(6, 0));
            _grid.Register(_player, new GridCoord(8, 0));
            var node = new AINode_Move { MaxSteps = Const(10), DesiredRange = Const(2) };

            // Act
            var result = node.Tick(Ctx());

            // Assert — BUG-061/PUL-014: "ya en la banda" es un no-op benigno, no un error;
            // Succeeded para que un Sequence sin Selector siga con el resto del turno (ataque).
            Assert.AreEqual(AIResult.Succeeded, result);
            _grid.TryGetPosition(_self, out var pos);
            Assert.AreEqual(new GridCoord(6, 0), pos, "No debe moverse si ya está en la banda.");
        }

        [Test]
        public void Tick_TooClose_RetreatOff_DoesNotMove()
        {
            // Arrange — self(7,0), player(8,0) dist 1 < DesiredRange 3, Retreat off.
            _grid.Register(_self, new GridCoord(7, 0));
            _grid.Register(_player, new GridCoord(8, 0));
            var node = new AINode_Move { MaxSteps = Const(10), DesiredRange = Const(3), Retreat = false };

            // Act
            var result = node.Tick(Ctx());

            // Assert — mismo criterio: "muy cerca, kite off" es benigno, no error.
            Assert.AreEqual(AIResult.Succeeded, result);
            _grid.TryGetPosition(_self, out var pos);
            Assert.AreEqual(new GridCoord(7, 0), pos, "Sin Retreat no debe alejarse.");
        }

        [Test]
        public void Tick_TooClose_RetreatOn_KitesToDesiredRange()
        {
            // Arrange — self(7,0), player(8,0) dist 1 < DesiredRange 3, Retreat on.
            _grid.Register(_self, new GridCoord(7, 0));
            _grid.Register(_player, new GridCoord(8, 0));
            var node = new AINode_Move { MaxSteps = Const(10), DesiredRange = Const(3), Retreat = true };

            // Act
            var result = node.Tick(Ctx());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(3, Dist(_self, _player), "Con Retreat debe alejarse hasta la banda.");
        }

        [Test]
        public void Tick_AlreadyAtDesiredRange_RequireLineOfSightFalse_BlockedByOccupant_StaysPutAnyway()
        {
            // Arrange — self(0,2), player(4,2) dist 4 == DesiredRange; un ocupante bloquea la
            // línea recta en (2,2). Sin RequireLineOfSight, "ya en la banda" no mira la LoS.
            _grid.Register(_self, new GridCoord(0, 2));
            _grid.Register(_player, new GridCoord(4, 2));
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 2));
            var node = new AINode_Move { MaxSteps = Const(10), DesiredRange = Const(4) };

            // Act
            var result = node.Tick(Ctx());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            _grid.TryGetPosition(_self, out var pos);
            Assert.AreEqual(new GridCoord(0, 2), pos,
                "Sin RequireLineOfSight, 'ya en la banda' no considera la línea de visión.");
        }

        [Test]
        public void Tick_AlreadyAtDesiredRange_RequireLineOfSightTrue_BlockedByOccupant_RepositionsForClearLine()
        {
            // Arrange — misma geometría, pero con RequireLineOfSight=true: la distancia sola no
            // alcanza si el ocupante en (2,2) tapa la línea recta — el nodo (que antes se
            // conformaba con "ya estoy a la distancia justa") debe reposicionarse a un tile con
            // línea de visión clara. Regresión del bug live: el Healer quedaba trabado para
            // siempre cuando el jugador se paraba justo entre él y su aliado herido.
            _grid.Register(_self, new GridCoord(0, 2));
            _grid.Register(_player, new GridCoord(4, 2));
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 2));
            var node = new AINode_Move
            {
                MaxSteps = Const(10), DesiredRange = Const(4), RequireLineOfSight = true,
            };

            // Act
            var result = node.Tick(Ctx());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            _grid.TryGetPosition(_self, out var pos);
            Assert.AreNotEqual(new GridCoord(0, 2), pos,
                "Con RequireLineOfSight, no debe quedarse en un tile sin línea de visión al target.");
            Assert.IsTrue(
                GridLineOfSight.HasClearLine(_grid, pos, new GridCoord(4, 2), _self, _player),
                "El tile elegido debe tener línea de visión clara al target.");
        }

        [Test]
        public void Tick_RequireLineOfSightTrue_AlreadyClear_DoesNotMove()
        {
            // Arrange — mismo DesiredRange, sin ningún bloqueo: con RequireLineOfSight=true debe
            // seguir siendo no-op si ya ve al target (no fuerza movimiento innecesario).
            _grid.Register(_self, new GridCoord(0, 2));
            _grid.Register(_player, new GridCoord(4, 2));
            var node = new AINode_Move
            {
                MaxSteps = Const(10), DesiredRange = Const(4), RequireLineOfSight = true,
            };

            // Act
            var result = node.Tick(Ctx());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            _grid.TryGetPosition(_self, out var pos);
            Assert.AreEqual(new GridCoord(0, 2), pos,
                "Con línea de visión ya clara, RequireLineOfSight no debe forzar movimiento.");
        }

        [Test]
        public void Tick_BackCompat_NullTargetNullRange_StopsAdjacentToPlayer()
        {
            // Arrange — comportamiento legacy: Target null ⇒ player, DesiredRange null +
            // StopAdjacent=true ⇒ rango 1.
            _grid.Register(_self, new GridCoord(0, 0));
            _grid.Register(_player, new GridCoord(5, 0));
            var node = new AINode_Move { MaxSteps = Const(10) }; // StopAdjacent default true

            // Act
            var result = node.Tick(Ctx());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(1, Dist(_self, _player), "Legacy: frena adyacente al player (rango 1).");
        }

        [Test]
        public void Tick_CustomTargetSelector_MovesTowardSelectedTargetNotPlayer()
        {
            // Arrange — player lejos (dist 14); enemigo cercano (dist 3). El selector Nearest
            // debe ganarle al player y el nodo moverse hacia el enemigo.
            _grid.Register(_self, new GridCoord(0, 0));
            _grid.Register(_player, new GridCoord(14, 0));

            var enemy = Guid.NewGuid();
            _grid.Register(enemy, new GridCoord(3, 0));

            var attrs = new AttributesManager();
            var ema = new ModifiableAttributes();
            ema.EnsureInitialized();
            ema.SetAttribute<Health>(new Health(10));
            attrs.Register(enemy, ema);

            var query = new RelationshipQueryStub();
            query.SetRelationship(_self, enemy, EntityFilterMask.Enemies);
            ServiceLocator.AddService<IEntityQueryService>(query);

            var ctx = Ctx();
            ctx.Attributes = attrs;

            var node = new AINode_Move
            {
                MaxSteps = Const(10),
                DesiredRange = Const(1),
                TargetSelector = new TargetSelector_Nearest { Relation = EntityFilterMask.Enemies },
            };

            // Act
            var result = node.Tick(ctx);

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(1, Dist(_self, enemy), "Debe quedar adyacente al enemigo elegido.");
            Assert.Greater(Dist(_self, _player), 1, "No debe haberse movido hacia el player.");
        }

        [Test]
        public void Tick_TargetResolvesEmpty_SucceedsAsNoOpWithoutMoving()
        {
            // Arrange — Target null ⇒ AlwaysPlayer ⇒ PlayerGuid vacío ⇒ Guid.Empty.
            _grid.Register(_self, new GridCoord(0, 0));
            var ctx = Ctx();
            ctx.PlayerGuid = Guid.Empty;
            var node = new AINode_Move { MaxSteps = Const(10) };

            // Act
            var result = node.Tick(ctx);

            // Assert — "no hay a quién ir" es benigno, no un error: con Failed el Sequence padre
            // abortaba el turno entero y se comía los nodos HERMANOS (BUG del Guardian/Healer).
            Assert.AreEqual(AIResult.Succeeded, result);
            _grid.TryGetPosition(_self, out var after);
            Assert.AreEqual(new GridCoord(0, 0), after, "Sin target no se mueve a ningún lado.");
        }

        [Test]
        public void Tick_TargetNotOnGrid_SucceedsAsNoOpWithoutMoving()
        {
            // Arrange — player con guid válido pero sin posición en grilla.
            _grid.Register(_self, new GridCoord(0, 0));
            var node = new AINode_Move { MaxSteps = Const(10) };

            // Act
            var result = node.Tick(Ctx());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            _grid.TryGetPosition(_self, out var after);
            Assert.AreEqual(new GridCoord(0, 0), after);
        }

        [Test]
        public void Tick_CustomSelectorFindsNobody_DoesNotRetargetThePlayer()
        {
            // Arrange — el selector no encuentra aliado (no hay ninguno registrado) y el player
            // está lejos. El nodo NO debe redirigirse al player: eso invertiría la intención
            // autorada (un Healer backline terminaría persiguiendo al jugador).
            _grid.Register(_self, new GridCoord(0, 0));
            _grid.Register(_player, new GridCoord(8, 0));

            var attrs = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(attrs);
            ServiceLocator.AddService<IEntityQueryService>(new RelationshipQueryStub());

            var ctx = Ctx();
            ctx.Attributes = attrs;
            var node = new AINode_Move
            {
                MaxSteps = Const(10),
                DesiredRange = Const(1),
                TargetSelector = new TargetSelector_ByAttribute { Relation = EntityFilterMask.Allies },
            };

            // Act
            var result = node.Tick(ctx);

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            _grid.TryGetPosition(_self, out var after);
            Assert.AreEqual(new GridCoord(0, 0), after, "No debe caminar hacia el player.");
        }

        [Test]
        public void Sequence_MoveWithoutTarget_StillRunsLaterSiblings()
        {
            // Regresión directa del bug del Guardian: el Move sin target le abortaba la Sequence
            // y nunca corría el ShowGuardAura que venía después.
            _grid.Register(_self, new GridCoord(0, 0));
            var ctx = Ctx();
            ctx.PlayerGuid = Guid.Empty;

            var marker = new MarkerNode();
            var seq = new AINode_Sequence
            {
                Children = new List<AIDecisionNode> { new AINode_Move { MaxSteps = Const(10) }, marker },
            };

            // Act
            var result = seq.Tick(ctx);

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsTrue(marker.Ticked, "El hermano posterior al Move tiene que correr igual.");
        }

        [Test]
        public void Tick_NullGrid_Fails()
        {
            var ctx = Ctx();
            ctx.Grid = null;
            var node = new AINode_Move { MaxSteps = Const(10) };

            Assert.AreEqual(AIResult.Failed, node.Tick(ctx));
        }

        // ----- in-memory stub --------------------------------------------

        /// <summary>Nodo testigo: sólo registra que lo tickearon.</summary>
        private sealed class MarkerNode : AIDecisionNode
        {
            public bool Ticked;
            public override AIResult Tick(AIContext context)
            {
                Ticked = true;
                return AIResult.Succeeded;
            }
        }

        private sealed class RelationshipQueryStub : IEntityQueryService
        {
            private readonly Dictionary<(Guid owner, Guid target), EntityFilterMask> _map
                = new Dictionary<(Guid, Guid), EntityFilterMask>();

            public void SetRelationship(Guid owner, Guid target, EntityFilterMask mask)
            {
                _map[(owner, target)] = mask;
            }

            public EntityFilterMask GetRelationship(Guid owner, Guid target)
            {
                return _map.TryGetValue((owner, target), out var m) ? m : EntityFilterMask.None;
            }

            public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid) => Array.Empty<Entity>();
            public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid) => Array.Empty<Entity>();
        }
    }
}
