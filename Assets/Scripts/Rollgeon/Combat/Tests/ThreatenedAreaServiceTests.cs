using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="ThreatenedAreaService"/> (Sistemas prerequisito Bosses §1):
    /// Mark / HasPending / GetPendingTiles / TryConsume / Clear / ClearAll, y persistencia
    /// entre "turnos" (marcar en uno, consumir en otro).
    /// </summary>
    [TestFixture]
    public class ThreatenedAreaServiceTests
    {
        private ThreatenedAreaService _svc;
        private System.Guid _boss;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _svc = new ThreatenedAreaService();
            _boss = System.Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            ServiceLocator.Clear();
        }

        private static List<GridCoord> Cross3x3(int cx, int cy)
        {
            var list = new List<GridCoord>();
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                list.Add(new GridCoord(cx + dx, cy + dy));
            return list;
        }

        [Test]
        public void Mark_StoresPendingArea_WhenTilesProvided()
        {
            _svc.Mark(_boss, Cross3x3(2, 2), damage: 15, AttackKind.BasicAttack);

            Assert.IsTrue(_svc.HasPending(_boss));
            Assert.AreEqual(9, _svc.GetPendingTiles(_boss).Count);
        }

        [Test]
        public void Mark_WithEmptyTiles_ClearsPending()
        {
            _svc.Mark(_boss, Cross3x3(2, 2), 15, AttackKind.BasicAttack);

            _svc.Mark(_boss, new List<GridCoord>(), 15, AttackKind.BasicAttack);

            Assert.IsFalse(_svc.HasPending(_boss));
        }

        [Test]
        public void TryConsume_ReturnsStoredArea_AndRemovesPending()
        {
            _svc.Mark(_boss, Cross3x3(0, 0), 20, AttackKind.Environmental);

            bool consumed = _svc.TryConsume(_boss, out var area);

            Assert.IsTrue(consumed);
            Assert.AreEqual(20, area.Damage);
            Assert.AreEqual(AttackKind.Environmental, area.Kind);
            Assert.IsTrue(area.Contains(new GridCoord(1, 1)));
            Assert.IsFalse(area.Contains(new GridCoord(5, 5)));
            Assert.IsFalse(_svc.HasPending(_boss), "El área debe limpiarse tras consumirse.");
        }

        [Test]
        public void TryConsume_WhenNothingPending_ReturnsFalse()
        {
            bool consumed = _svc.TryConsume(_boss, out var area);

            Assert.IsFalse(consumed);
            Assert.IsNull(area.Tiles);
        }

        [Test]
        public void Mark_PerSource_AreIndependent()
        {
            var otherBoss = System.Guid.NewGuid();
            _svc.Mark(_boss, Cross3x3(0, 0), 10, AttackKind.BasicAttack);
            _svc.Mark(otherBoss, Cross3x3(9, 9), 5, AttackKind.BasicAttack);

            Assert.IsTrue(_svc.TryConsume(_boss, out _));
            Assert.IsTrue(_svc.HasPending(otherBoss), "Consumir una fuente no debe tocar la otra.");
        }

        [Test]
        public void ClearAll_RemovesEveryPendingArea()
        {
            _svc.Mark(_boss, Cross3x3(0, 0), 10, AttackKind.BasicAttack);
            _svc.Mark(System.Guid.NewGuid(), Cross3x3(3, 3), 10, AttackKind.BasicAttack);

            _svc.ClearAll();

            Assert.IsFalse(_svc.HasPending(_boss));
        }
    }
}
