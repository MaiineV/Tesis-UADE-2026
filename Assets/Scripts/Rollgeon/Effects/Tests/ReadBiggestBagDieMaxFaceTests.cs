using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Effects.Readers;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// <see cref="ReadBiggestBagDieMaxFace"/>: cara máxima del dado más grande de la bolsa
    /// (Feature#0084, Blood Transfusion banda A) — no la cara que salió, el techo del dado.
    /// Fallback 6 sin servicio / sin bolsa.
    /// </summary>
    [TestFixture]
    public sealed class ReadBiggestBagDieMaxFaceTests
    {
        private sealed class FakePlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; }
            public Guid RunId { get; set; }
            public ClassHeroSO CurrentHero { get; set; }
            public DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) => DiceBag = bag;
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        [Test]
        public void Read_ReturnsMaxFaceOfBiggestDieInBag()
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType> { DiceType.D6, DiceType.D10, DiceType.D4 };
            var players = new FakePlayerService { DiceBag = bag };
            ServiceLocator.AddService<IPlayerService>(players, ServiceScope.Global);

            var reader = new ReadBiggestBagDieMaxFace();

            Assert.AreEqual(DiceType.D10.MaxFace(), reader.Read(new EffectContext()));
        }

        [Test]
        public void Read_WithoutPlayerService_FallsBackToSix()
        {
            var reader = new ReadBiggestBagDieMaxFace();

            Assert.AreEqual(6, reader.Read(new EffectContext()));
        }

        [Test]
        public void Read_WithEmptyBag_FallsBackToSix()
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType>();
            var players = new FakePlayerService { DiceBag = bag };
            ServiceLocator.AddService<IPlayerService>(players, ServiceScope.Global);

            var reader = new ReadBiggestBagDieMaxFace();

            Assert.AreEqual(6, reader.Read(new EffectContext()));
        }
    }
}
