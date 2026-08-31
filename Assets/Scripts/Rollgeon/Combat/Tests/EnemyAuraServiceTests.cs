using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Auras;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Aura del Guardian: reduce el daño entrante de los ALIADOS a ≤ radio mientras el
    /// portador siga en la grilla; sin stacking (aplica la mayor); el portador no se protege.
    /// </summary>
    [TestFixture]
    public class EnemyAuraServiceTests
    {
        private sealed class StubQuery : IEntityQueryService
        {
            public Func<Guid, Guid, EntityFilterMask> Relationship;
            public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid) { yield break; }
            public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid) { yield break; }
            public EntityFilterMask GetRelationship(Guid owner, Guid target)
                => Relationship?.Invoke(owner, target) ?? EntityFilterMask.None;
        }

        private GridManager _grid;
        private EnemyAuraService _aura;
        private StubQuery _query;
        private Guid _guardian;
        private Guid _ally;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(10, 10));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _guardian = Guid.NewGuid();
            _ally = Guid.NewGuid();
            _player = Guid.NewGuid();
            _grid.Register(_guardian, new GridCoord(2, 2));
            _grid.Register(_player, new GridCoord(9, 9));

            _query = new StubQuery
            {
                // Todos los no-player son aliados entre sí; el player es enemigo de todos.
                Relationship = (owner, target) =>
                    target == _player || owner == _player
                        ? EntityFilterMask.Enemies | (target == _player ? EntityFilterMask.Player : 0)
                        : EntityFilterMask.Allies,
            };
            ServiceLocator.AddService<IEntityQueryService>(_query, ServiceScope.Global);

            _aura = EnemyAuraService.ResolveOrCreate();
            _aura.Register(_guardian, radius: 2, flatReduction: 5);
        }

        [TearDown]
        public void TearDown()
        {
            _aura?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private int ReductionFor(Guid target)
            => _aura.GetFlatReduction(new DamageContext { SourceId = _player, TargetId = target, BaseDamage = 10 });

        [Test]
        public void Ally_WithinRadius_GetsReduction()
        {
            _grid.Register(_ally, new GridCoord(4, 2)); // Manhattan 2
            Assert.AreEqual(5, ReductionFor(_ally));
        }

        [Test]
        public void Ally_OutsideRadius_NoReduction()
        {
            _grid.Register(_ally, new GridCoord(5, 2)); // Manhattan 3
            Assert.AreEqual(0, ReductionFor(_ally));
        }

        [Test]
        public void Player_EnemyOfBearer_NoReduction()
        {
            _grid.Unregister(_player);
            _grid.Register(_player, new GridCoord(2, 3)); // pegado al guardián
            Assert.AreEqual(0, ReductionFor(_player));
        }

        [Test]
        public void Bearer_DoesNotProtectItself()
        {
            Assert.AreEqual(0, ReductionFor(_guardian), "la ficha protege ALIADOS, no al portador");
        }

        [Test]
        public void DeadBearer_OffGrid_AuraTurnsOff()
        {
            _grid.Register(_ally, new GridCoord(4, 2));
            _grid.Unregister(_guardian); // CombatDeathWatcher lo saca del grid al morir
            Assert.AreEqual(0, ReductionFor(_ally));
        }

        [Test]
        public void TwoAuras_ApplyTheLargest_NotTheSum()
        {
            var second = Guid.NewGuid();
            _grid.Register(second, new GridCoord(4, 3));
            _aura.Register(second, radius: 2, flatReduction: 8);
            _grid.Register(_ally, new GridCoord(4, 2)); // a ≤2 de ambos

            Assert.AreEqual(8, ReductionFor(_ally));
        }

        [Test]
        public void MultiCellBearer_MeasuresRectToRect()
        {
            // Guardián 2×2 en (2,2) cubre (2,2)-(3,3); aliado en (5,3): dist rect = 2 (desde
            // el ancla sería 4).
            _grid.Unregister(_guardian);
            Assert.IsTrue(_grid.TryRegister(_guardian, new GridCoord(2, 2), new Vector2Int(2, 2)));
            _grid.Register(_ally, new GridCoord(5, 3));

            Assert.AreEqual(5, ReductionFor(_ally));
        }
    }
}
