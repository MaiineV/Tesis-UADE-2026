using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combos.Play;
using Rollgeon.Economy;
using Rollgeon.Effects;
using Rollgeon.Upgrades.Dice;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Combos.Tests
{
    /// <summary>Tests de <see cref="ComboPlayService"/> — ventana de combo jugado.</summary>
    [TestFixture]
    public class ComboPlayServiceTests
    {
        private ComboPlayService _service;
        private int _raiseCount;
        private ComboPlayedPayload _lastPayload;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _service = new ComboPlayService();
            _raiseCount = 0;
            TypedEvent<ComboPlayedPayload>.Subscribe(OnComboPlayed);
        }

        [TearDown]
        public void TearDown()
        {
            TypedEvent<ComboPlayedPayload>.Clear();
            _service.Dispose();
            ServiceLocator.Clear();
        }

        private void OnComboPlayed(ComboPlayedPayload payload)
        {
            _raiseCount++;
            _lastPayload = payload;
        }

        private static EffectContext BuildComboContext(Guid source, string comboId = "combo.par")
        {
            return new EffectContext
            {
                SourceGuid = source,
                DiceResult = new[] { 2, 2, 5 },
                KeptDice = new[] { 2, 2 },
                KeptDiceOriginalIndices = new[] { 0, 1 },
                ComboResult = ComboDetectionResult.Match(comboId, baseDamage: 10, countUsed: 2,
                    contributingIndices: new[] { 0, 1 }),
            };
        }

        [Test]
        public void BeginPlay_WithMatchedCombo_RaisesPayloadOnce_AndOpensWindow()
        {
            // Arrange
            var source = Guid.NewGuid();
            var ctx = BuildComboContext(source);

            // Act
            _service.BeginPlay(ctx);

            // Assert
            Assert.AreEqual(1, _raiseCount);
            Assert.AreEqual(source, _lastPayload.SourceGuid);
            Assert.AreEqual("combo.par", _lastPayload.ComboId);
            Assert.AreEqual(10, _lastPayload.ComboResult.BaseDamage);
            Assert.AreSame(ctx.DiceResult, _lastPayload.DiceResult);
            Assert.AreSame(ctx.KeptDiceOriginalIndices, _lastPayload.KeptDiceOriginalIndices);
            Assert.IsTrue(_service.IsPlayWindowOpen);
            Assert.IsNotNull(_service.CurrentPlayScratch);
            Assert.AreEqual("combo.par", _service.CurrentComboId);
        }

        [Test]
        public void BeginPlay_WithoutComboResult_DoesNotRaise_AndScratchStaysNull()
        {
            // Arrange
            var ctx = new EffectContext { SourceGuid = Guid.NewGuid() };

            // Act
            _service.BeginPlay(ctx);

            // Assert
            Assert.AreEqual(0, _raiseCount);
            Assert.IsTrue(_service.IsPlayWindowOpen);
            Assert.IsNull(_service.CurrentPlayScratch);
            Assert.IsNull(_service.CurrentComboId);
        }

        [Test]
        public void BeginPlay_NoMatchResult_DoesNotRaise()
        {
            // Arrange
            var ctx = new EffectContext { ComboResult = ComboDetectionResult.NoMatch() };

            // Act
            _service.BeginPlay(ctx);

            // Assert
            Assert.AreEqual(0, _raiseCount);
            Assert.IsNull(_service.CurrentPlayScratch);
        }

        [Test]
        public void BeginPlay_SyntheticComboWithoutId_DoesNotRaise()
        {
            // Arrange — resultados sintéticos (action rolls) matchean pero sin ComboId.
            var ctx = new EffectContext { ComboResult = ComboDetectionResult.Match(baseDamage: 10, countUsed: 2) };

            // Act
            _service.BeginPlay(ctx);

            // Assert
            Assert.AreEqual(0, _raiseCount);
            Assert.IsNull(_service.CurrentPlayScratch);
        }

        [Test]
        public void BeginPlay_SubscriberGold_AppliedExactlyOnce()
        {
            // Arrange
            var economy = new FakeEconomyService(gold: 10);
            ServiceLocator.AddService<IEconomyService>(economy, ServiceScope.Global);
            TypedEvent<ComboPlayedPayload>.Subscribe(_ =>
                _service.CurrentPlayScratch.Modify(ResourceTarget.Gold, ResourceOperation.Add, 5));

            // Act
            _service.BeginPlay(BuildComboContext(Guid.NewGuid()));

            // Assert
            Assert.AreEqual(15, economy.CurrentGold);
            Assert.AreEqual(1, economy.AddCalls);
        }

        [Test]
        public void BeginPlay_Nested_DoesNotReRaise_NorResetScratch()
        {
            // Arrange
            var outer = BuildComboContext(Guid.NewGuid());
            _service.BeginPlay(outer);
            _service.CurrentPlayScratch.BonusComboDamage = 3;

            // Act — un efecto que ejecuta otro behavior re-entra a BeginPlay.
            _service.BeginPlay(BuildComboContext(Guid.NewGuid(), "combo.trio"));

            // Assert
            Assert.AreEqual(1, _raiseCount);
            Assert.AreEqual(3, _service.CurrentPlayScratch.BonusComboDamage);
            Assert.AreEqual("combo.par", _service.CurrentComboId);

            // El EndPlay anidado no cierra la ventana del outer.
            _service.EndPlay();
            Assert.IsTrue(_service.IsPlayWindowOpen);
            Assert.IsNotNull(_service.CurrentPlayScratch);

            _service.EndPlay();
            Assert.IsFalse(_service.IsPlayWindowOpen);
            Assert.IsNull(_service.CurrentPlayScratch);
        }

        [Test]
        public void EndPlay_ClosesWindow_AndNullsScratch()
        {
            // Arrange
            _service.BeginPlay(BuildComboContext(Guid.NewGuid()));

            // Act
            _service.EndPlay();

            // Assert
            Assert.IsFalse(_service.IsPlayWindowOpen);
            Assert.IsNull(_service.CurrentPlayScratch);
            Assert.IsNull(_service.CurrentComboId);
        }

        [Test]
        public void EndPlay_Unbalanced_LogsWarning_AndStaysClosed()
        {
            // Act
            LogAssert.Expect(LogType.Warning,
                "[ComboPlayService] EndPlay sin BeginPlay previo — wiring desbalanceado.");
            _service.EndPlay();

            // Assert
            Assert.IsFalse(_service.IsPlayWindowOpen);
        }

        [Test]
        public void OnRunEnd_ClearsOpenWindow()
        {
            // Arrange
            _service.Register();
            _service.BeginPlay(BuildComboContext(Guid.NewGuid()));

            // Act
            EventManager.Trigger(EventName.OnRunEnd);

            // Assert
            Assert.IsFalse(_service.IsPlayWindowOpen);
            Assert.IsNull(_service.CurrentPlayScratch);
        }

        private sealed class FakeEconomyService : IEconomyService
        {
            public int CurrentGold { get; private set; }
            public int AddCalls { get; private set; }

            public FakeEconomyService(int gold = 0) { CurrentGold = gold; }

            public void Add(int amount)
            {
                if (amount <= 0) return;
                CurrentGold += amount;
                AddCalls++;
            }

            public bool Spend(int amount)
            {
                if (amount > CurrentGold) return false;
                CurrentGold -= amount;
                return true;
            }

            public bool CanAfford(int amount) => CurrentGold >= amount;

            public void ResetTo(int amount) => CurrentGold = amount;
        }
    }
}
