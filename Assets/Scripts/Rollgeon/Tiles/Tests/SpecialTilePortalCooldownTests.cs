using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Status;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Tiles.Forced;
using UnityEngine;

namespace Rollgeon.Tiles.Tests
{
    /// <summary>
    /// Tests del cooldown post-teleport: usar un portal aplica <c>TeleportCooldownTurns</c>
    /// del portal de ENTRADA; mientras dura, cualquier portal es una celda común (no trunca
    /// el path ni teletransporta). Sin el servicio registrado los portales funcionan como
    /// siempre — degradación defensiva que protege a los tests de cadenas preexistentes.
    /// </summary>
    [TestFixture]
    public class SpecialTilePortalCooldownTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private UnitTraitService _traits;
        private SpecialTileService _svc;
        private ForcedMovementService _forced;
        private TeleportCooldownService _cooldown;
        private Guid _player;
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(10, 10));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _movement = new MovementService(_grid);
            ServiceLocator.AddService<IMovementService>(_movement, ServiceScope.Global);

            _traits = new UnitTraitService();
            ServiceLocator.AddService<IUnitTraitService>(_traits, ServiceScope.Global);

            _player = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(0, 0));
            _traits.Register(_player, UnitTraits.DefaultGround);

            _cooldown = new TeleportCooldownService();
            _cooldown.ConfigureForTests();
            ServiceLocator.AddService<ITeleportCooldownService>(_cooldown, ServiceScope.Global);

            _svc = new SpecialTileService();
            _svc.ConfigureForTests(() => _player);
            ServiceLocator.AddService<SpecialTileService>(_svc, ServiceScope.Global);

            _forced = new ForcedMovementService();
            ServiceLocator.AddService<IForcedMovementService>(_forced, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _cooldown?.Dispose();
            _svc = null;

            foreach (var asset in _createdAssets)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _createdAssets.Clear();

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private SpecialTileDefinitionSO Portal(int cooldownTurns = 2)
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            def.TileType = SpecialTileType.Portal;
            def.Triggers = TileTrigger.OnEnter | TileTrigger.OnForcedMovementInto;
            def.Category = TileEffectCategory.Teleport;
            def.Affinity = TileAffinity.All;
            def.TeleportCooldownTurns = cooldownTurns;
            _createdAssets.Add(def);
            return def;
        }

        private void PlacePortalPair(GridCoord a, GridCoord b, int cooldownTurns = 2)
        {
            var idA = _svc.Place(Portal(cooldownTurns), new[] { a });
            _svc.Place(Portal(cooldownTurns), new[] { b }, new TilePlacementOptions { LinkTo = idA });
        }

        private GridCoord PositionOf(Guid entity)
        {
            Assert.IsTrue(_grid.TryGetPosition(entity, out var pos), "La entidad tiene que seguir en el grid.");
            return pos;
        }

        [Test]
        public void Move_IntoPortal_AppliesCooldownWithDefinitionTurns()
        {
            PlacePortalPair(new GridCoord(2, 0), new GridCoord(5, 5), cooldownTurns: 3);

            Assert.IsTrue(_movement.Move(_player, new GridCoord(2, 0)));

            Assert.AreEqual(new GridCoord(6, 5), PositionOf(_player),
                "El primer uso teletransporta y reubica normal.");
            Assert.IsTrue(_cooldown.IsOnCooldown(_player));
            Assert.AreEqual(3, _cooldown.GetTurns(_player),
                "El cooldown sale del TeleportCooldownTurns del portal de entrada.");
        }

        [Test]
        public void Move_IntoPortalWhileOnCooldown_DoesNotTeleport()
        {
            PlacePortalPair(new GridCoord(2, 0), new GridCoord(5, 5));
            _cooldown.Apply(_player, 2);

            Assert.IsTrue(_movement.Move(_player, new GridCoord(2, 0)));

            Assert.AreEqual(new GridCoord(2, 0), PositionOf(_player),
                "En cooldown el portal es una celda común: la unidad queda parada encima.");
        }

        [Test]
        public void Move_PathThroughPortalWhileOnCooldown_DoesNotTruncatePath()
        {
            PlacePortalPair(new GridCoord(2, 0), new GridCoord(5, 5));
            _cooldown.Apply(_player, 2);

            Assert.IsTrue(_movement.Move(_player, new GridCoord(4, 0)));

            Assert.AreEqual(new GridCoord(4, 0), PositionOf(_player),
                "El path que cruza el portal llega entero a destino — sin truncado ni teleport.");
        }

        [Test]
        public void Push_ThroughPortal_AppliesCooldownAndRemainderContinues()
        {
            PlacePortalPair(new GridCoord(1, 0), new GridCoord(4, 4));

            var result = _forced.Push(_player, Cardinal.East, 5, Guid.NewGuid());

            Assert.AreEqual(new GridCoord(8, 4), result.FinalCoord,
                "El remanente del empuje sigue del otro lado, como siempre.");
            Assert.IsTrue(_cooldown.IsOnCooldown(_player),
                "El empuje a través del portal también deja el cooldown aplicado.");
        }

        [Test]
        public void Move_IntoPortal_WithZeroCooldownTurns_TeleportsWithoutCooldown()
        {
            PlacePortalPair(new GridCoord(2, 0), new GridCoord(5, 5), cooldownTurns: 0);

            Assert.IsTrue(_movement.Move(_player, new GridCoord(2, 0)));

            Assert.AreEqual(new GridCoord(6, 5), PositionOf(_player));
            Assert.IsFalse(_cooldown.IsOnCooldown(_player),
                "TeleportCooldownTurns = 0 es el opt-out del asset: portal sin cooldown.");
        }

        [Test]
        public void CooldownExpired_EnteringPortalAgain_Teleports()
        {
            PlacePortalPair(new GridCoord(2, 0), new GridCoord(5, 5));
            _cooldown.Apply(_player, 2);
            EventManager.Trigger(EventName.OnTurnStarted, _player);
            EventManager.Trigger(EventName.OnTurnStarted, _player);
            Assert.IsFalse(_cooldown.IsOnCooldown(_player), "Setup: el cooldown ya expiró.");

            Assert.IsTrue(_movement.Move(_player, new GridCoord(2, 0)));

            Assert.AreEqual(new GridCoord(6, 5), PositionOf(_player),
                "Expirado el cooldown, el portal vuelve a funcionar.");
            Assert.AreEqual(2, _cooldown.GetTurns(_player), "Y el uso re-aplica el cooldown.");
        }

        [Test]
        public void AdjacentPortals_RelocationOntoSecondPortal_DoesNotChainTeleport()
        {
            // A teleporta a B; la reubicación desde B (hacia el Este) cae sobre C, otro portal.
            // Con el cooldown aplicado ANTES de reubicar, C se ve como celda común y la cadena
            // muere ahí — antes de este fix solo la frenaba el ChainBudget.
            var idA = _svc.Place(Portal(), new[] { new GridCoord(2, 0) });
            _svc.Place(Portal(), new[] { new GridCoord(5, 5) }, new TilePlacementOptions { LinkTo = idA });
            var idC = _svc.Place(Portal(), new[] { new GridCoord(6, 5) });
            _svc.Place(Portal(), new[] { new GridCoord(0, 9) }, new TilePlacementOptions { LinkTo = idC });

            Assert.IsTrue(_movement.Move(_player, new GridCoord(2, 0)));

            Assert.AreEqual(new GridCoord(6, 5), PositionOf(_player),
                "La reubicación termina sobre el segundo portal sin volver a teletransportar.");
        }
    }
}
