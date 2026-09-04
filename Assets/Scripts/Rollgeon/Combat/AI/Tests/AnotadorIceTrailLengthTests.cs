using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Status;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Movement;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    // Contra un IHazardService falso: acá se mide cuántas casillas manda a congelar y cuáles. El
    // recorrido con el servicio real (derretido, stun, expiración) vive en AnotadorIceTrailTests.
    [TestFixture]
    public class AnotadorIceTrailLengthTests
    {
        /// <summary>Casillas de la ficha. El repliegue autorado camina lo mismo, así que no recorta.</summary>
        private const int SheetTrailTiles = 4;

        private static readonly GridCoord RetreatOrigin = new GridCoord(8, 4);

        private StubMovementService _movement;
        private FakeHazardService _hazards;
        private IceStunBinder _binder;
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

            _binder = new IceStunBinder();
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

        [Test]
        public void Tick_AfterAFourStepRetreat_FreezesTheFourTilesHeWalked()
        {
            Retreat(new GridCoord(7, 4), new GridCoord(6, 4), new GridCoord(5, 4), new GridCoord(4, 4));

            var result = TrailNode().Tick(BossContext());

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
            // 6 pasos con tope 4: las que importan son las pegadas a su casilla final, que
            // son las que pisa quien lo persigue por el camino corto.
            Retreat(new GridCoord(7, 4), new GridCoord(6, 4), new GridCoord(5, 4),
                    new GridCoord(4, 4), new GridCoord(3, 4), new GridCoord(2, 4));

            TrailNode().Tick(BossContext());

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
            Retreat(new GridCoord(7, 4), new GridCoord(6, 4));

            TrailNode().Tick(BossContext());

            Assert.AreEqual(2, _hazards.LastTiles.Count,
                "El tope no rellena: la estela nunca es más larga que el repliegue.");
        }

        // El default del campo es el que usa cualquier árbol autorado a mano en el inspector: tiene
        // que ser el número de la ficha.
        [Test]
        public void TheNodeDefault_IsTheSheetsFourTiles()
        {
            Assert.AreEqual(SheetTrailTiles, new AINode_IceTrail().MaxTiles);
        }

        [Test]
        public void Tick_OnTheNextRetreat_ReplacesThePreviousTrailInsteadOfStacking()
        {
            // El mismo nodo entre turnos: el id de su estela viva vive en la instancia.
            var node = TrailNode();
            Retreat(new GridCoord(7, 4), new GridCoord(6, 4));
            node.Tick(BossContext());

            RetreatFrom(new GridCoord(6, 4), new GridCoord(5, 4), new GridCoord(4, 4));
            node.Tick(BossContext());

            Assert.AreEqual(1, _hazards.Deactivated.Count,
                "La estela del turno pasado tiene que apagarse antes de publicar la nueva.");
            CollectionAssert.AreEqual(new[] { new GridCoord(5, 4), new GridCoord(4, 4) },
                _hazards.LastTiles);
        }

        private AINode_IceTrail TrailNode() => new AINode_IceTrail
        {
            Hazard = _ice,
            MaxTiles = SheetTrailTiles,
            StunTurns = 1,
            ReplacePreviousTrail = true,
        };

        private AIContext BossContext() => new AIContext { SelfGuid = _boss };

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

        private sealed class FakeHazardService : IHazardService
        {
            public readonly List<GridCoord> LastTiles = new List<GridCoord>();
            public readonly List<Guid> Deactivated = new List<Guid>();

            private readonly Dictionary<Guid, HazardInstanceInfo> _instances =
                new Dictionary<Guid, HazardInstanceInfo>();

            public void Activate(HazardDefinitionSO definition) { }

            public Guid Activate(HazardDefinitionSO definition, IEnumerable<GridCoord> tiles,
                                 Guid ownerGuid = default)
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
