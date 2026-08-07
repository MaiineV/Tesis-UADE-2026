using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AntiRepeat;
using Rollgeon.Combat.DiceBlock;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat.Pipelines.Tests
{
    /// <summary>
    /// EditMode tests para <see cref="AntiRepeatModeService"/>: seeding desde config, flip de
    /// modo con evento, y el bloqueo de un dado por turno en Mode Dice.
    /// <para>
    /// NOTE: estos tests disparan <c>OnTurnStarted</c> a mano — NO validan el orden real
    /// respecto del roll del jugador (eso es el caveat de playtest documentado en el service).
    /// Solo validan la lógica del handler dado que el evento llega.
    /// </para>
    /// </summary>
    [TestFixture]
    public class AntiRepeatModeServiceTests
    {
        private readonly Guid _playerGuid = Guid.NewGuid();
        private AntiRepeatModeService _service;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _service = null;
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private AntiRepeatModeService NewRegisteredService()
        {
            var svc = new AntiRepeatModeService();
            svc.Register();
            return svc;
        }

        [Test]
        public void Register_SeedsModeFromConfig()
        {
            var config = ScriptableObject.CreateInstance<AntiRepeatConfigSO>();
            config.Mode = AntiRepeatMode.Dice;
            ServiceLocator.AddService<AntiRepeatConfigSO>(config, ServiceScope.Global);

            _service = NewRegisteredService();

            Assert.AreEqual(AntiRepeatMode.Dice, _service.Mode, "El modo debe sembrarse del config.");
        }

        [Test]
        public void Register_NoConfig_DefaultsToCombo()
        {
            _service = NewRegisteredService();

            Assert.AreEqual(AntiRepeatMode.Combo, _service.Mode, "Sin config, el default debe ser Combo.");
        }

        [Test]
        public void SetMode_ChangesMode_AndFiresEvent()
        {
            _service = NewRegisteredService(); // Combo por default

            bool fired = false;
            EventManager.Subscribe(EventName.OnAntiRepeatModeChanged, _ => fired = true);

            _service.SetMode(AntiRepeatMode.Dice);

            Assert.AreEqual(AntiRepeatMode.Dice, _service.Mode);
            Assert.IsTrue(fired, "SetMode debe disparar OnAntiRepeatModeChanged cuando cambia.");
        }

        [Test]
        public void SetMode_SameValue_DoesNotFireEvent()
        {
            _service = NewRegisteredService(); // Combo por default

            bool fired = false;
            EventManager.Subscribe(EventName.OnAntiRepeatModeChanged, _ => fired = true);

            _service.SetMode(AntiRepeatMode.Combo);

            Assert.IsFalse(fired, "SetMode al mismo valor no debe disparar el evento.");
        }

        [Test]
        public void ModeDice_OnPlayerTurnStarted_BlocksExactlyOneDie()
        {
            var dice = RegisterFakeDiceBlock();
            RegisterFakePlayer(bagSize: 5);

            _service = NewRegisteredService();
            _service.SetMode(AntiRepeatMode.Dice);

            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            Assert.AreEqual(1, dice.BlockedIndices.Count, "Mode Dice debe bloquear exactamente un dado.");
        }

        [Test]
        public void ModeCombo_OnPlayerTurnStarted_BlocksNoDie()
        {
            var dice = RegisterFakeDiceBlock();
            RegisterFakePlayer(bagSize: 5);

            _service = NewRegisteredService(); // Combo por default

            EventManager.Trigger(EventName.OnTurnStarted, _playerGuid);

            Assert.AreEqual(0, dice.BlockedIndices.Count, "Mode Combo no debe bloquear dados.");
        }

        [Test]
        public void ModeDice_OnNonPlayerTurnStarted_BlocksNoDie()
        {
            var dice = RegisterFakeDiceBlock();
            RegisterFakePlayer(bagSize: 5);

            _service = NewRegisteredService();
            _service.SetMode(AntiRepeatMode.Dice);

            EventManager.Trigger(EventName.OnTurnStarted, Guid.NewGuid()); // turno de un enemigo

            Assert.AreEqual(0, dice.BlockedIndices.Count, "Solo el turno del jugador debe bloquear un dado.");
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private FakeDiceBlockService RegisterFakeDiceBlock()
        {
            var dice = new FakeDiceBlockService();
            ServiceLocator.AddService<IDiceBlockService>(dice, ServiceScope.Global);
            return dice;
        }

        private void RegisterFakePlayer(int bagSize)
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType>();
            for (int i = 0; i < bagSize; i++) bag.Dice.Add(DiceType.D6);
            ServiceLocator.AddService<IPlayerService>(new FakePlayerService(_playerGuid, bag), ServiceScope.Global);
        }

        // ── Fakes ────────────────────────────────────────────────────────

        private sealed class FakeDiceBlockService : IDiceBlockService
        {
            private readonly HashSet<int> _blocked = new();
            public void Block(int index) { if (index >= 0) _blocked.Add(index); }
            public void Unblock(int index) => _blocked.Remove(index);
            public bool IsBlocked(int index) => index >= 0 && _blocked.Contains(index);
            public IReadOnlyCollection<int> BlockedIndices => _blocked;
            public void Clear() => _blocked.Clear();
        }

        private sealed class FakePlayerService : IPlayerService
        {
            public FakePlayerService(Guid playerGuid, DiceBagSO bag)
            {
                PlayerGuid = playerGuid;
                DiceBag = bag;
            }

            public Guid PlayerGuid { get; }
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag { get; private set; }

            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) => DiceBag = bag;
            public void ClearPlayer() { }

#pragma warning disable 67 // eventos de la interfaz no usados por el fake
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore 67
        }
    }
}
