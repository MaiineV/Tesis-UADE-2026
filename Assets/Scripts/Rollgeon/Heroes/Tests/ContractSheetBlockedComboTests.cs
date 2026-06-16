using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.ComboBlock;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Combos.Tests;
using UnityEngine;

namespace Rollgeon.Heroes.Tests
{
    /// <summary>
    /// Cobertura del patch [T103] en <see cref="ContractSheet.MatchBest"/> (via
    /// <see cref="ContractSheet.EvaluateRoll"/>): un combo bloqueado se skipea; sin servicio
    /// registrado degrada a "nada bloqueado".
    /// </summary>
    [TestFixture]
    public class ContractSheetBlockedComboTests
    {
        private Combo_Par _par;
        private Combo_DoblePar _doblePar;
        private Combo_Trio _trio;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _par = ComboTestUtils.CreateCombo<Combo_Par>(ComboId.Par, 10);
            _doblePar = ComboTestUtils.CreateCombo<Combo_DoblePar>(ComboId.DoublePair, 18);
            _trio = ComboTestUtils.CreateCombo<Combo_Trio>(ComboId.Triple, 28);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_par);
            Object.DestroyImmediate(_doblePar);
            Object.DestroyImmediate(_trio);
            ServiceLocator.Clear();
        }

        private ContractSheet BuildSheet() => new ContractSheet
        {
            Combos = new List<BaseComboSO> { _par, _doblePar, _trio },
        };

        [Test]
        public void EvaluateRoll_WithoutBlockService_WorksAsBefore()
        {
            var sheet = BuildSheet();
            // [2,2,3,3,5] → Par and DoblePar match; DoblePar wins by priority.
            var best = sheet.EvaluateRoll(new[] { 2, 2, 3, 3, 5 });
            Assert.IsNotNull(best);
            Assert.AreEqual(ComboId.DoublePair, best.ComboId);
        }

        [Test]
        public void EvaluateRoll_BlockedCombo_IsSkipped()
        {
            var svc = new ComboBlockService();
            svc.ConfigureForTests(() => System.Guid.Empty);
            ServiceLocator.AddService<IComboBlockService>(svc);

            svc.Block(ComboId.DoublePair, 3);

            var sheet = BuildSheet();
            var best = sheet.EvaluateRoll(new[] { 2, 2, 3, 3, 5 });

            Assert.IsNotNull(best, "Con DoblePar bloqueado, debe quedar Par como mejor match.");
            Assert.AreEqual(ComboId.Par, best.ComboId);

            svc.Dispose();
        }

        [Test]
        public void EvaluateRoll_AllMatchingBlocked_ReturnsNull()
        {
            var svc = new ComboBlockService();
            svc.ConfigureForTests(() => System.Guid.Empty);
            ServiceLocator.AddService<IComboBlockService>(svc);

            svc.Block(ComboId.Par, 3);
            svc.Block(ComboId.DoublePair, 3);

            var sheet = BuildSheet();
            var best = sheet.EvaluateRoll(new[] { 2, 2, 3, 3, 5 });

            Assert.IsNull(best, "Todos los matches bloqueados → null.");

            svc.Dispose();
        }

        [Test]
        public void EvaluateRoll_ServiceAbsent_FallsBackToUnblocked()
        {
            // Explicit: NO registrar el servicio → EvaluateRoll no debe lanzar y no bloquea nada.
            var sheet = BuildSheet();
            var best = sheet.EvaluateRoll(new[] { 2, 2, 3, 3, 5 });
            Assert.IsNotNull(best);
            Assert.AreEqual(ComboId.DoublePair, best.ComboId);
        }

        [Test]
        public void EvaluateRoll_CrossedPlusBlocked_BothFiltered()
        {
            var svc = new ComboBlockService();
            svc.ConfigureForTests(() => System.Guid.Empty);
            ServiceLocator.AddService<IComboBlockService>(svc);

            var sheet = BuildSheet();
            sheet.CrossCombo(_doblePar);     // tachado.
            svc.Block(ComboId.Par, 2);       // bloqueado.

            var best = sheet.EvaluateRoll(new[] { 2, 2, 3, 3, 5 });
            Assert.IsNull(best, "Ambos candidates fueron filtrados.");

            svc.Dispose();
        }
    }
}
