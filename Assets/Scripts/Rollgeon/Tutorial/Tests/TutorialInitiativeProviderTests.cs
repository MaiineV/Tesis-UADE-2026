using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Initiative;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Player;

namespace Rollgeon.Tutorial.Tests
{
    [TestFixture]
    public class TutorialInitiativeProviderTests
    {
        private static readonly Guid PlayerGuid = new Guid("11111111-1111-1111-1111-111111111111");
        private static readonly Guid EnemyGuid = new Guid("22222222-2222-2222-2222-222222222222");

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
        }

        private static void RegisterPlayerService()
        {
            ServiceLocator.AddService<IPlayerService>(
                new FakePlayerService { PlayerGuid = PlayerGuid }, ServiceScope.Run);
        }

        [Test]
        public void RollInitiative_PlayerGuid_ReturnsMaxValue()
        {
            // Arrange
            RegisterPlayerService();
            var provider = new TutorialInitiativeProvider(new FakeInitiativeProvider(10));

            // Act
            int initiative = provider.RollInitiative(PlayerGuid);

            // Assert
            Assert.AreEqual(int.MaxValue, initiative, "El jugador siempre va primero en la cola.");
        }

        [Test]
        public void RollInitiative_NonPlayerGuid_DelegatesToInner()
        {
            // Arrange
            RegisterPlayerService();
            var provider = new TutorialInitiativeProvider(new FakeInitiativeProvider(37));

            // Act
            int initiative = provider.RollInitiative(EnemyGuid);

            // Assert
            Assert.AreEqual(37, initiative, "El orden relativo de los enemigos lo decide el provider default.");
        }

        [Test]
        public void RollInitiative_InnerReturnsMaxValue_ClampsBelowPlayer()
        {
            // Arrange
            RegisterPlayerService();
            var provider = new TutorialInitiativeProvider(new FakeInitiativeProvider(int.MaxValue));

            // Act
            int initiative = provider.RollInitiative(EnemyGuid);

            // Assert
            Assert.AreEqual(int.MaxValue - 1, initiative, "Ningún enemigo puede empatar al jugador.");
        }

        [Test]
        public void RollInitiative_WithoutPlayerService_DelegatesToInner()
        {
            // Arrange — sin IPlayerService registrado, nadie es "el jugador".
            var provider = new TutorialInitiativeProvider(new FakeInitiativeProvider(5));

            // Act
            int initiative = provider.RollInitiative(PlayerGuid);

            // Assert
            Assert.AreEqual(5, initiative);
        }

        [Test]
        public void Inner_ExposesDecoratedProvider_ForTeardownRestore()
        {
            // Arrange
            var inner = new FakeInitiativeProvider(1);

            // Act
            var provider = new TutorialInitiativeProvider(inner);

            // Assert
            Assert.AreSame(inner, provider.Inner);
        }

        private sealed class FakeInitiativeProvider : IInitiativeProvider
        {
            private readonly int _value;
            public FakeInitiativeProvider(int value) { _value = value; }
            public int RollInitiative(Guid entityGuid) => _value;
        }

        private sealed class FakePlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; }
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => null;
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet { add { } remove { } }
            public event Action OnPlayerCleared { add { } remove { } }
        }
    }
}
