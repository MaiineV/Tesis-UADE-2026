using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Bosses.Anotador;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Movement;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Largo de la estela helada del Anotador (piso 2): la ficha la subió a 4 casillas para que cada
    /// repliegue tape un tramo de corredor en vez de dejar una molestia de 3.
    /// </summary>
    /// <remarks>
    /// Corre <see cref="AINode_IceTrail"/> contra un <see cref="IHazardService"/> falso: acá se mide
    /// cuántas casillas manda a congelar y cuáles, no el hazard en sí. El recorrido completo con el
    /// <c>HazardService</c> real (derretido, stun, expiración) vive en <c>AnotadorIceTrailTests</c>.
    /// </remarks>
    [TestFixture]
    public class AnotadorIceTrailLengthTests
    {
        /// <summary>Casillas de la ficha. El repliegue autorado camina lo mismo, así que no recorta.</summary>
        private const int SheetTrailTiles = 4;

        private static readonly GridCoord RetreatOrigin = new GridCoord(8, 4);

        private StubMovementService _movement;
        private FakeHazardService _hazards;
        private AnotadorIceStunBinder _binder;
        private HazardDefinitionSO _ice;

        private Guid _boss;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            // Antes del binder: su suscripción a OnEntityMoved se resuelve al registrarse.
            _movement = new StubMovementService();
            ServiceLocator.AddService<IMovementService>(_movement);

            _hazards = new FakeHazardService();
            ServiceLocator.AddService<IHazardService>(_hazards);

            _binder = new AnotadorIceStunBinder();
            _binder.Register();

            _ice = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            _ice.hideFlags = HideFlags.HideAndDontSave;

            _boss = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _binder?.Dispose();
            _binder = null;

            if (_ice != null) UnityEngine.Object.DestroyImmediate(_ice);
            _ice = null;

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ======================================================================
        // Largo
        // ======================================================================

        [Test]
        public void Tick_AfterAFourStepRetreat_FreezesTheFourTilesHeWalked()
        {
            // Arrange — el repliegue de 4 pasos que habilita la ficha: 4 casillas pisadas.
            Retreat(new GridCoord(7, 4), new GridCoord(6, 4), new GridCoord(5, 4), new GridCoord(4, 4));

            // Act
            var result = TrailNode().Tick(BossContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            CollectionAssert.AreEqual(
                new[]
                {
                    new GridCoord(7, 4), new GridCoord(6, 4), new GridCoord(5, 4), new GridCoord(4, 4),
                },
                _hazards.LastTiles,
                "Con 4 pasos caminados no hay recorte: se congelan los cuatro, en orden de recorrido.");
            Assert.IsFalse(_hazards.LastTiles.Contains(RetreatOrigin),
                "El origen del movimiento no se pisa — no se congela.");
        }

        [Test]
        public void Tick_WithARetreatLongerThanTheCap_KeepsTheFourClosestToHim()
        {
            // Arrange — 6 pasos con tope 4: las que importan son las pegadas a su casilla final, que
            // son las que pisa quien lo persigue por el camino corto.
            Retreat(new GridCoord(7, 4), new GridCoord(6, 4), new GridCoord(5, 4),
                    new GridCoord(4, 4), new GridCoord(3, 4), new GridCoord(2, 4));

            // Act
            TrailNode().Tick(BossContext());

            // Assert
            CollectionAssert.AreEqual(
                new[]
                {
                    new GridCoord(5, 4), new GridCoord(4, 4), new GridCoord(3, 4), new GridCoord(2, 4),
                },
                _hazards.LastTiles);
        }

        [Test]
        public void Tick_WithAShortRetreat_FreezesOnlyWhatHeWalked()
        {
            // Arrange — acorralado contra un escritorio: dos pasos y nada más.
            Retreat(new GridCoord(7, 4), new GridCoord(6, 4));

            // Act
            TrailNode().Tick(BossContext());

            // Assert
            Assert.AreEqual(2, _hazards.LastTiles.Count,
                "El tope no rellena: la estela nunca es más larga que el repliegue.");
        }

        /// <summary>
        /// El default del campo es el que usa cualquier árbol autorado a mano en el inspector, así que
        /// tiene que ser el número de la ficha y no el 3 viejo.
        /// </summary>
        [Test]
        public void TheNodeDefault_IsTheSheetsFourTiles()
        {
            Assert.AreEqual(SheetTrailTiles, new AINode_IceTrail().MaxTiles);
        }

        /// <summary>
        /// Una sola estela viva por vez: el repliegue nuevo mata al anterior. Es lo que hace que el
        /// hielo NO se acumule pelea adentro — si alguna vez diseño quiere el muro que crece, la
        /// palanca es este flag y no la duración.
        /// </summary>
        [Test]
        public void Tick_OnTheNextRetreat_ReplacesThePreviousTrailInsteadOfStacking()
        {
            // Arrange — el mismo nodo entre turnos: el id de su estela viva vive en la instancia.
            var node = TrailNode();
            Retreat(new GridCoord(7, 4), new GridCoord(6, 4));
            node.Tick(BossContext());

            // Act — el turno siguiente sigue caminando desde donde quedó.
            RetreatFrom(new GridCoord(6, 4), new GridCoord(5, 4), new GridCoord(4, 4));
            node.Tick(BossContext());

            // Assert
            Assert.AreEqual(1, _hazards.Deactivated.Count,
                "La estela del turno pasado tiene que apagarse antes de publicar la nueva.");
            CollectionAssert.AreEqual(new[] { new GridCoord(5, 4), new GridCoord(4, 4) },
                _hazards.LastTiles);
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private AINode_IceTrail TrailNode() => new AINode_IceTrail
        {
            Hazard = _ice,
            MaxTiles = SheetTrailTiles,
            StunTurns = 1,
            ReplacePreviousTrail = true,
        };

        private AIContext BossContext() => new AIContext { SelfGuid = _boss };

        /// <summary>Publica el movimiento del repliegue tal como lo haría <c>MovementService</c>.</summary>
        private void Retreat(params GridCoord[] walked) => RetreatFrom(RetreatOrigin, walked);

        private void RetreatFrom(GridCoord from, params GridCoord[] walked)
        {
            var path = new List<GridCoord> { from };
            path.AddRange(walked);
            _movement.RaiseMoved(_boss, from, walked[walked.Length - 1], path);
        }

        private sealed class StubMovementService : IMovementService
        {
            public event Action<Guid, GridCoord, GridCoord, IReadOnlyList<GridCoord>> OnEntityMoved;

            public List<GridCoord> GetReachableTiles(GridCoord origin, int range, bool includeOrigin = false)
                => new List<GridCoord>();

            public List<GridCoord> FindPath(GridCoord from, GridCoord to) => new List<GridCoord>();

            public bool Move(Guid entity, GridCoord destination) => false;

            public void RaiseMoved(Guid entity, GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
                => OnEntityMoved?.Invoke(entity, from, to, path);
        }

        /// <summary>
        /// Registra qué casillas pidió congelar el nodo. No simula duración, derretido ni overlay:
        /// eso ya lo cubre el servicio real en <c>AnotadorIceTrailTests</c>.
        /// </summary>
        private sealed class FakeHazardService : IHazardService
        {
            public readonly List<GridCoord> LastTiles = new List<GridCoord>();
            public readonly List<Guid> Deactivated = new List<Guid>();

            private readonly Dictionary<Guid, HazardInstanceInfo> _instances =
                new Dictionary<Guid, HazardInstanceInfo>();

            public void Activate(HazardDefinitionSO definition) { }

            public Guid Activate(HazardDefinitionSO definition, IEnumerable<GridCoord> tiles)
            {
                if (definition == null || tiles == null) return Guid.Empty;

                LastTiles.Clear();
                LastTiles.AddRange(tiles);
                if (LastTiles.Count == 0) return Guid.Empty;

                var id = Guid.NewGuid();
                _instances[id] = new HazardInstanceInfo(id, definition, new List<GridCoord>(LastTiles), 3);
                return id;
            }

            public bool IsActive(HazardDefinitionSO definition) => definition != null && _instances.Count > 0;

            public bool IsActive(Guid sourceId) => _instances.ContainsKey(sourceId);

            public bool TryGetHazardAt(GridCoord coord, out HazardInstanceInfo info)
            {
                foreach (var instance in _instances.Values)
                {
                    if (!instance.Tiles.Contains(coord)) continue;
                    info = instance;
                    return true;
                }

                info = default;
                return false;
            }

            public IEnumerable<HazardInstanceInfo> ActiveInstances() =>
                new List<HazardInstanceInfo>(_instances.Values);

            public void Deactivate(Guid instanceId)
            {
                Deactivated.Add(instanceId);
                _instances.Remove(instanceId);
            }

            public void SkipNextTick(Guid instanceId) { }
        }
    }
}
