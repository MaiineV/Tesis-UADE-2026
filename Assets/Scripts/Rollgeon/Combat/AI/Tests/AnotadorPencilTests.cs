using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests de <see cref="AINode_AnotadorPencil"/> — el lápiz del Anotador (piso 2): 12 melee
    /// directos contra quien esté pegado cuando le toca el turno, sin marca y sin área.
    /// </summary>
    /// <remarks>
    /// El caso que justifica que el nodo exista en vez de un <c>AINode_AuxTelegraph</c> con anillo
    /// 3×3 es <see cref="Tick_MarksNothing_SoTheAxisTelegraphKeepsItsSlot"/>: al no tocar
    /// <see cref="IThreatenedAreaService"/>, el lápiz no compite por el único área pendiente que el
    /// servicio guarda por source guid, que es lo que obligaba al canal auxiliar.
    /// </remarks>
    [TestFixture]
    public class AnotadorPencilTests
    {
        private const int RoomWidth = 11;
        private const int RoomHeight = 7;
        private const int PencilDamage = 12;

        private static readonly GridCoord BossTile = new GridCoord(5, 3);

        private GridManager _grid;
        private ThreatenedAreaService _threat;
        private SpyDamagePipeline _pipeline;

        private Guid _boss;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(RoomWidth, RoomHeight));
            ServiceLocator.AddService<IGridManager>(_grid);

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _pipeline = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_pipeline);

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
            _grid.Register(_boss, BossTile);
            _grid.Register(_player, new GridCoord(0, 0));
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ======================================================================
        // El peaje de estar pegado
        // ======================================================================

        [Test]
        public void Tick_WithThePlayerAdjacent_HitsForTwelve()
        {
            // Arrange — el jugador terminó su turno en la casilla desde la que le pega.
            PlacePlayer(new GridCoord(4, 3));

            // Act
            var result = Pencil().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(1, _pipeline.Resolved.Count, "El lápiz pega una sola vez por turno.");
            Assert.AreEqual(PencilDamage, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(_boss, _pipeline.Resolved[0].SourceId);
            Assert.AreEqual(_player, _pipeline.Resolved[0].TargetId);
            Assert.AreEqual(AttackKind.BasicAttack, _pipeline.Resolved[0].Kind);
        }

        [Test]
        public void Tick_ResolvesTheDamageNow_WithoutWaitingATurn()
        {
            // Arrange — el lápiz dejó de telegrafiar: no hay "marca ahora, cobra el turno que viene".
            PlacePlayer(new GridCoord(5, 4));

            // Act
            Pencil().Tick(NewContext());

            // Assert
            CollectionAssert.IsNotEmpty(_pipeline.Resolved,
                "El golpe se resuelve en el mismo tick: si esperara un turno volvería a ser un telegraph.");
        }

        /// <summary>
        /// <see cref="IThreatenedAreaService"/> guarda <b>un</b> área pendiente por source guid. El
        /// anillo del lápiz necesitaba un canal aparte para no pisar la marca de fila/columna; un
        /// golpe sin área no toca el servicio y el problema desaparece con el nodo viejo.
        /// </summary>
        [Test]
        public void Tick_MarksNothing_SoTheAxisTelegraphKeepsItsSlot()
        {
            // Arrange — el eje ya quedó marcado este turno.
            PlacePlayer(new GridCoord(4, 3));
            _threat.Mark(_boss, new List<GridCoord> { new GridCoord(0, 3), new GridCoord(1, 3) },
                damage: 30, kind: AttackKind.BasicAttack);

            // Act
            Pencil().Tick(NewContext());

            // Assert
            Assert.IsTrue(_threat.TryConsume(_boss, out var pending),
                "El lápiz no puede consumir ni pisar el área marcada del eje.");
            Assert.AreEqual(30, pending.Damage, "El área pendiente sigue siendo la del eje, no la del lápiz.");
            Assert.AreEqual(2, pending.Tiles.Count);
        }

        // ======================================================================
        // Rango
        // ======================================================================

        [Test]
        public void Tick_WithThePlayerTwoTilesAway_FailsWithoutHitting()
        {
            // Arrange — el caso mayoritario: el boss se replegó y el jugador todavía no llegó.
            PlacePlayer(new GridCoord(3, 3));

            // Act
            var result = Pencil().Tick(NewContext());

            // Assert
            Assert.AreEqual(AIResult.Failed, result,
                "Fuera de rango falla: por eso en el árbol va dentro de un Selector[..., Wait].");
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        /// <summary>
        /// El rango del jugador se mide en Manhattan, así que la diagonal no es una casilla desde la
        /// que se le pueda pegar al jefe: cobrarla sería castigar una posición que no ataca.
        /// </summary>
        [Test]
        public void Tick_OnTheDiagonal_FailsBecauseTheMetricIsManhattan()
        {
            // Arrange
            PlacePlayer(new GridCoord(4, 2));

            // Act + Assert
            Assert.AreEqual(AIResult.Failed, Pencil().Tick(NewContext()));
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Tick_WithChebyshev_TakesTheDiagonalToo()
        {
            // Arrange — la métrica es un campo del nodo, no una constante escondida.
            PlacePlayer(new GridCoord(4, 2));
            var node = Pencil();
            node.Metric = DistanceMetric.Chebyshev;

            // Act + Assert
            Assert.AreEqual(AIResult.Succeeded, node.Tick(NewContext()));
            Assert.AreEqual(PencilDamage, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void Tick_WithARangeOfTwo_ReachesTwoTiles()
        {
            // Arrange
            PlacePlayer(new GridCoord(3, 3));
            var node = Pencil();
            node.Range = 2;

            // Act + Assert
            Assert.AreEqual(AIResult.Succeeded, node.Tick(NewContext()));
            Assert.AreEqual(1, _pipeline.Resolved.Count);
        }

        // ======================================================================
        // Bootstrap incompleto
        // ======================================================================

        [Test]
        public void Tick_WithoutADamagePipeline_FailsInsteadOfThrowing()
        {
            // Arrange — el pipeline llega por el AIContext y en tests puede faltar.
            PlacePlayer(new GridCoord(4, 3));
            var context = NewContext();
            context.DamagePipeline = null;

            // Act + Assert
            Assert.AreEqual(AIResult.Failed, Pencil().Tick(context));
        }

        [Test]
        public void Tick_WithoutAGrid_FailsInsteadOfThrowing()
        {
            // Arrange
            var context = NewContext();
            context.Grid = null;

            // Act + Assert
            Assert.AreEqual(AIResult.Failed, Pencil().Tick(context));
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Tick_WithTheTargetOffTheGrid_FailsInsteadOfHittingNobody()
        {
            // Arrange — el jugador todavía no está registrado en la sala.
            _grid.Unregister(_player);

            // Act + Assert
            Assert.AreEqual(AIResult.Failed, Pencil().Tick(NewContext()));
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Tick_WithZeroDamage_DoesNotResolveAnEmptyHit()
        {
            // Arrange — un 0 autorado por error no debería disparar feedback ni evento de daño.
            PlacePlayer(new GridCoord(4, 3));
            var node = Pencil();
            node.Damage = 0;

            // Act + Assert
            Assert.AreEqual(AIResult.Failed, node.Tick(NewContext()));
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private static AINode_AnotadorPencil Pencil() => new AINode_AnotadorPencil
        {
            Damage = PencilDamage,
            Range = 1,
            Metric = DistanceMetric.Manhattan,
            Kind = AttackKind.BasicAttack,
        };

        /// <summary>Mover puede fallar en silencio (casilla ocupada / fuera de sala): se afirma acá
        /// para que un arrange roto no se lea como un assert de comportamiento.</summary>
        private void PlacePlayer(GridCoord coord) =>
            Assert.IsTrue(_grid.Move(_player, coord), $"Arrange: no se pudo poner al jugador en {coord}.");

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            Grid = _grid,
            DamagePipeline = _pipeline,
            SelfMaxHp = 190,
            RoundIndex = 1,
        };

        private sealed class SpyDamagePipeline : IDamagePipeline
        {
            public readonly List<DamageContext> Resolved = new List<DamageContext>();

            public DamageContext Resolve(DamageContext ctx)
            {
                Resolved.Add(ctx);
                return ctx;
            }

            public DamageContext Preview(DamageContext ctx) => ctx;
        }
    }
}
