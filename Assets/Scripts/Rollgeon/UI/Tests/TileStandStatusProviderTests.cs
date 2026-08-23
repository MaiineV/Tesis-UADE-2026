using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using Rollgeon.UI.HUD.Status;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Tests de <see cref="TileStandStatusProvider"/>: publica los estados "parado sobre"
    /// (burn/heal/speed/attack) usando los mismos filtros que un disparo real — una Zona de
    /// Seguridad que protege del Fuego también apaga el ícono de quemándose.
    /// </summary>
    [TestFixture]
    public class TileStandStatusProviderTests
    {
        private GridManager _grid;
        private UnitTraitService _traits;
        private SpecialTileService _svc;
        private TileStandStatusProvider _provider;
        private List<StatusIconState> _states;
        private Guid _player;
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(6, 6));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _traits = new UnitTraitService();
            ServiceLocator.AddService<IUnitTraitService>(_traits, ServiceScope.Global);

            _player = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(2, 2));
            _traits.Register(_player, UnitTraits.DefaultGround);

            _svc = new SpecialTileService();
            _svc.ConfigureForTests(() => _player);
            ServiceLocator.AddService<ISpecialTileService>(_svc, ServiceScope.Global);

            _provider = new TileStandStatusProvider(catalog: null);
            _states = new List<StatusIconState>();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;

            foreach (var asset in _createdAssets)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _createdAssets.Clear();

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private SpecialTileDefinitionSO MakeDef(Action<SpecialTileDefinitionSO> configure)
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            configure(def);
            _createdAssets.Add(def);
            return def;
        }

        private void PlaceTile(SpecialTileType type, TileEffectCategory category, GridCoord coord)
        {
            _svc.Place(MakeDef(d =>
            {
                d.TileType = type;
                d.Triggers = TileTrigger.OnEnter;
                d.Category = category;
                d.Affinity = TileAffinity.All;
            }), new[] { coord });
        }

        [Test]
        public void Collect_StandingOnFire_PublishesBurnActiveWithoutTurns()
        {
            PlaceTile(SpecialTileType.Fire, TileEffectCategory.Damage, new GridCoord(2, 2));

            _provider.Collect(_player, _states);

            Assert.AreEqual(1, _states.Count);
            Assert.AreEqual(TileStandStatusProvider.BurnId, _states[0].Id);
            Assert.IsTrue(_states[0].Active);
            Assert.IsNull(_states[0].RemainingTurns,
                "Los estados 'parado sobre' no tienen turnos — duran lo que dure la estadía.");
        }

        [Test]
        public void Collect_StandingOnFireTemp_PublishesSameBurnId()
        {
            PlaceTile(SpecialTileType.FireTemp, TileEffectCategory.Damage, new GridCoord(2, 2));

            _provider.Collect(_player, _states);

            Assert.AreEqual(1, _states.Count);
            Assert.AreEqual(TileStandStatusProvider.BurnId, _states[0].Id,
                "Fire y FireTemp colapsan en el mismo estado de quemándose.");
        }

        [Test]
        public void Collect_StandingOnHealTile_PublishesHealId()
        {
            PlaceTile(SpecialTileType.Heal, TileEffectCategory.Heal, new GridCoord(2, 2));

            _provider.Collect(_player, _states);

            Assert.AreEqual(1, _states.Count);
            Assert.AreEqual(TileStandStatusProvider.HealId, _states[0].Id);
        }

        [Test]
        public void Collect_StandingOnBoostTile_PublishesSpeedId()
        {
            PlaceTile(SpecialTileType.Boost, TileEffectCategory.MoveRangeBonus, new GridCoord(2, 2));

            _provider.Collect(_player, _states);

            Assert.AreEqual(1, _states.Count);
            Assert.AreEqual(TileStandStatusProvider.SpeedId, _states[0].Id);
        }

        [Test]
        public void Collect_StandingOnStrengthTile_PublishesAttackId()
        {
            PlaceTile(SpecialTileType.Strength, TileEffectCategory.StatModifier, new GridCoord(2, 2));

            _provider.Collect(_player, _states);

            Assert.AreEqual(1, _states.Count);
            Assert.AreEqual(TileStandStatusProvider.AttackId, _states[0].Id);
        }

        [Test]
        public void Collect_OnEmptyCell_PublishesNothing()
        {
            PlaceTile(SpecialTileType.Fire, TileEffectCategory.Damage, new GridCoord(4, 4));

            _provider.Collect(_player, _states);

            Assert.AreEqual(0, _states.Count);
        }

        [Test]
        public void Collect_FireUnderProtectingSafeZone_PublishesNothing()
        {
            PlaceTile(SpecialTileType.Fire, TileEffectCategory.Damage, new GridCoord(2, 2));
            _svc.Place(MakeDef(d =>
            {
                d.TileType = SpecialTileType.SafeZone;
                d.Category = TileEffectCategory.ConditionalProtection;
                d.Affinity = TileAffinity.All;
                d.ProtectedTileTypes = new[] { SpecialTileType.Fire };
            }), new[] { new GridCoord(2, 2) });

            _provider.Collect(_player, _states);

            var ids = _states.ConvertAll(s => s.Id);
            CollectionAssert.DoesNotContain(ids, TileStandStatusProvider.BurnId,
                "Si la Zona de Seguridad te protege del Fuego, el ícono de quemándose mentiría.");
        }

        [Test]
        public void Collect_WithoutTileService_PublishesNothing()
        {
            ServiceLocator.Clear();

            _provider.Collect(_player, _states);

            Assert.AreEqual(0, _states.Count);
        }
    }
}
