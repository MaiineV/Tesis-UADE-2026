using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Movement.Die;
using Rollgeon.Player;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Movement.Tests
{
    /// <summary>
    /// Dado de Movimiento con carril de encantamientos (§6.6): la tirada respeta las caras
    /// válidas (filtros + caras extra) publicadas por <see cref="IDiceEnchantmentService"/>,
    /// y <see cref="IMovementService.TryMove"/> devuelve el path caminado para la señal de
    /// movimiento voluntario.
    /// </summary>
    [TestFixture]
    public sealed class MovementDieFacesTests
    {
        private PlayerService _player;
        private MovementDieService _service;
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _player = new PlayerService();
            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _created.Add(hero);
            _player.SetPlayer(hero, Guid.NewGuid());
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _service = null;
            foreach (var so in _created) if (so != null) UnityEngine.Object.DestroyImmediate(so);
            _created.Clear();
            ServiceLocator.Clear();
        }

        private sealed class FakeFaces : IDiceEnchantmentService
        {
            public IReadOnlyCollection<int> Faces = Array.Empty<int>();
            public int MaxFaceValue = 6;

            public RuntimeDiceBag Bag => null;
            public bool IsReady => true;
            public IReadOnlyCollection<int> ComputeAllowedFaces(int bagIndex) => Faces;
            public EnchantmentApplyResult ValidateApply(int bagIndex, EnchantmentSO ench) => EnchantmentApplyResult.Fail("fake");
            public EnchantmentApplyResult Apply(int bagIndex, EnchantmentSO ench) => EnchantmentApplyResult.Fail("fake");
            public bool Remove(int bagIndex, int enchSlotIndex) => false;
            public EnchantmentScratch ResolveComboBonus(Guid sourceGuid, string comboId, IReadOnlyList<int> diceResult, int comboBaseDamage) => new EnchantmentScratch();
            public EnchantmentScratch LastComboScratch => null;
            public void InitializeFromBag(DiceBagSO bag) { }
            public int MovementDieMaxFace => MaxFaceValue;
            public int AddMovementDieFaces(int delta) => 0;
            public IReadOnlyCollection<int> ComputeMovementDieFaces() => Faces;
        }

        [Test]
        public void Roll_WithAllowedFaces_AlwaysPicksFromTheSet()
        {
            var fake = new FakeFaces { Faces = new[] { 2, 4, 6, 8 } };
            ServiceLocator.AddService<IDiceEnchantmentService>(fake, ServiceScope.Global);
            _service = new MovementDieService(_player, seed: 123);

            for (int i = 0; i < 40; i++)
            {
                int face = -1;
                _service.Roll(_player.PlayerGuid, f => face = f);
                Assert.Contains(face, new[] { 2, 4, 6, 8 });
                _service.ClearActiveRange();
            }
        }

        [Test]
        public void Roll_WithExtraFaces_CanExceedTheBaseType()
        {
            var fake = new FakeFaces { Faces = Enumerable.Range(1, 8).ToArray(), MaxFaceValue = 8 };
            ServiceLocator.AddService<IDiceEnchantmentService>(fake, ServiceScope.Global);
            _service = new MovementDieService(_player, seed: 5);

            Assert.AreEqual(8, _service.MaxFace);
            var seen = new HashSet<int>();
            for (int i = 0; i < 200; i++)
            {
                _service.Roll(_player.PlayerGuid, f => seen.Add(f));
                _service.ClearActiveRange();
            }
            Assert.IsTrue(seen.Contains(7) || seen.Contains(8), "200 tiradas de un d6+2 sin 7 ni 8.");
        }

        [Test]
        public void Roll_WithoutEnchantmentService_StaysWithinTheBaseType()
        {
            _service = new MovementDieService(_player, seed: 9);

            Assert.AreEqual(6, _service.MaxFace);
            for (int i = 0; i < 40; i++)
            {
                int face = -1;
                _service.Roll(_player.PlayerGuid, f => face = f);
                Assert.That(face, Is.InRange(1, 6));
                _service.ClearActiveRange();
            }
        }

        [Test]
        public void TryMove_ReturnsTheWalkedPathWithOriginFirst()
        {
            var grid = new GridManager();
            grid.LoadRoom(NavGraph.Rect(5, 5));
            var movement = new MovementService(grid);
            var entity = Guid.NewGuid();
            grid.Register(entity, new GridCoord(0, 0));

            bool moved = movement.TryMove(entity, new GridCoord(3, 0), out var path);

            Assert.IsTrue(moved);
            Assert.AreEqual(4, path.Count);
            Assert.AreEqual(new GridCoord(0, 0), path[0]);
            Assert.AreEqual(new GridCoord(3, 0), path[3]);
        }

        [Test]
        public void TryMove_SameCell_SucceedsWithoutPath()
        {
            var grid = new GridManager();
            grid.LoadRoom(NavGraph.Rect(3, 3));
            var movement = new MovementService(grid);
            var entity = Guid.NewGuid();
            grid.Register(entity, new GridCoord(1, 1));

            Assert.IsTrue(movement.TryMove(entity, new GridCoord(1, 1), out var path));
            Assert.IsNull(path);
        }
    }
}
