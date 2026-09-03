using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Cashier;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    [TestFixture]
    public class CoinClockCurseTests
    {
        private CoinClockCurseSO _curse;
        private Guid _boss;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _curse = ScriptableObject.CreateInstance<CoinClockCurseSO>();
            _boss = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_curse);
            ServiceLocator.Clear();
        }

        [Test]
        public void WithoutALedger_TheCardStaysOff()
        {
            Assert.IsFalse(_curse.IsActive(_boss),
                "El ledger es lazy y sólo existe con el Cajero en la sala: en cualquier otra " +
                "pelea la tarjeta no tiene por qué salir.");
        }

        [Test]
        public void WithAnEmptyFloor_TheCardStaysOff()
        {
            ServiceLocator.AddService<ICashierLedgerService>(new FakeCashierLedgerService());

            Assert.IsFalse(_curse.IsActive(_boss),
                "Sin monedas en el piso no hay nada que se venza, y anunciarlo promete un " +
                "castigo que todavía no opera.");
        }

        [Test]
        public void WithOneCoinTicking_TheCardComesUp()
        {
            ServiceLocator.AddService<ICashierLedgerService>(
                new FakeCashierLedgerService { ChipsOnFloor = 1 });

            Assert.IsTrue(_curse.IsActive(_boss),
                "Hay plata en el piso con el reloj corriendo: eso es exactamente lo que la " +
                "tarjeta tiene que decir.");
        }
    }
}
