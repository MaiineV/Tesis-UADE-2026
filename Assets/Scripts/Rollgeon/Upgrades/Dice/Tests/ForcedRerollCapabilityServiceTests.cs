using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat;
using Rollgeon.Combat.Handoff;
using Rollgeon.Combat.Initiative;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Tests del driver de "Torpe" (BUG-030): <see cref="ForcedRerollCapabilityService"/>
    /// debe pedir el relanzamiento completo al handoff exactamente una vez, en el
    /// turno configurado, solo para el jugador y solo si el handoff acepta.
    /// </summary>
    [TestFixture]
    public class ForcedRerollCapabilityServiceTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private ForcedRerollCapabilityService _service;
        private StubPlayerService _player;
        private SpyHandoffService _handoff;
        private TurnOrderService _turnOrder;
        private Guid _enemyGuid;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _player = new StubPlayerService();
            _enemyGuid = Guid.NewGuid();
            _handoff = new SpyHandoffService { NextResult = true };

            ServiceLocator.AddService<IPlayerService>(_player);
            ServiceLocator.AddService<ICombatHandoffService>(_handoff);

            _service = new ForcedRerollCapabilityService();
            _service.SubscribeEventsForTests();
        }

        [TearDown]
        public void TearDown()
        {
            _service.UnsubscribeEventsForTests();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        // ---- Helpers --------------------------------------------------------

        private void RegisterBagWithEnchantment(EnchantmentSO ench)
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType> { DiceType.D6 };
            bag.name = "TestBag";
            _created.Add(bag);

            var svc = new DiceEnchantmentService(config: null);
            svc.InitializeFromBag(bag);
            if (ench != null)
            {
                var result = svc.Apply(0, ench);
                Assert.IsTrue(result.Success, $"Setup: Apply falló — {result.ErrorMessage}");
            }
            ServiceLocator.AddService<IDiceEnchantmentService>(svc);
        }

        private EnchantmentSO MakeTorpe(int triggerOnTurn)
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            ench.name = "TestTorpe";
            _created.Add(ench);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, "test.torpe");
            typeof(EnchantmentSO).GetField("_allowedDiceTypes", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<DiceType> { DiceType.D6 });
            typeof(EnchantmentSO).GetField("_capabilities", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<IEnchantmentCapability>
                {
                    new CapForceRerollOnTurn { TriggerOnTurn = triggerOnTurn },
                });
            return ench;
        }

        // Registra un TurnOrderService real y lo avanza hasta el turno del jugador
        // indicado (1-based). Player primero en la cola (CNF-006).
        private void RegisterTurnOrderAtPlayerTurn(int playerTurn)
        {
            ServiceLocator.AddService<IInitiativeProvider>(new FlatInitiativeProvider());
            _turnOrder = new TurnOrderService();
            _turnOrder.BuildForCombat(
                new[] { _player.PlayerGuid, _enemyGuid }, priorityGuid: _player.PlayerGuid);
            ServiceLocator.AddService<TurnOrderService>(_turnOrder);

            // Cada round son 2 Advance (player + enemy); RoundIndex sube en el wrap.
            for (int round = 1; round < playerTurn; round++)
            {
                _turnOrder.Advance();
                _turnOrder.Advance();
            }
            Assert.AreEqual(playerTurn - 1, _turnOrder.RoundIndex, "Setup: RoundIndex inesperado.");
        }

        private void StartCombat() =>
            EventManager.Trigger(EventName.OnCombatStart, Guid.NewGuid());

        private void EndCombat() =>
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), null);

        private void RollDice(Guid sourceGuid) =>
            EventManager.Trigger(EventName.OnDiceRolled, sourceGuid,
                (IReadOnlyList<int>)new[] { 1, 2, 3 });

        // ---- Tests ----------------------------------------------------------

        [Test]
        public void OnDiceRolled_ConfiguredTurnFirstRoll_RequestsForcedReroll()
        {
            // Arrange
            RegisterBagWithEnchantment(MakeTorpe(triggerOnTurn: 2));
            RegisterTurnOrderAtPlayerTurn(2);
            StartCombat();

            // Act
            RollDice(_player.PlayerGuid);

            // Assert
            Assert.AreEqual(1, _handoff.Calls);
            Assert.AreEqual(_player.PlayerGuid, _handoff.LastGuid);
        }

        [Test]
        public void OnDiceRolled_TurnOne_DoesNotRequest()
        {
            RegisterBagWithEnchantment(MakeTorpe(triggerOnTurn: 2));
            RegisterTurnOrderAtPlayerTurn(1);
            StartCombat();

            RollDice(_player.PlayerGuid);

            Assert.AreEqual(0, _handoff.Calls);
        }

        [Test]
        public void OnDiceRolled_SecondRollSameCombat_DoesNotRequestTwice()
        {
            RegisterBagWithEnchantment(MakeTorpe(triggerOnTurn: 2));
            RegisterTurnOrderAtPlayerTurn(2);
            StartCombat();

            RollDice(_player.PlayerGuid);
            RollDice(_player.PlayerGuid);

            Assert.AreEqual(1, _handoff.Calls);
        }

        [Test]
        public void OnDiceRolled_BagWithoutTorpe_DoesNotRequest()
        {
            RegisterBagWithEnchantment(ench: null);
            RegisterTurnOrderAtPlayerTurn(2);
            StartCombat();

            RollDice(_player.PlayerGuid);

            Assert.AreEqual(0, _handoff.Calls);
        }

        [Test]
        public void OnDiceRolled_EnemyGuid_DoesNotRequest()
        {
            RegisterBagWithEnchantment(MakeTorpe(triggerOnTurn: 2));
            RegisterTurnOrderAtPlayerTurn(2);
            StartCombat();

            RollDice(_enemyGuid);

            Assert.AreEqual(0, _handoff.Calls);
        }

        [Test]
        public void OnDiceRolled_WithoutCombatStart_DoesNotRequest()
        {
            RegisterBagWithEnchantment(MakeTorpe(triggerOnTurn: 2));
            RegisterTurnOrderAtPlayerTurn(2);

            RollDice(_player.PlayerGuid);

            Assert.AreEqual(0, _handoff.Calls);
        }

        [Test]
        public void OnCombatStart_AfterPreviousCombat_ResetsOncePerCombatFlag()
        {
            RegisterBagWithEnchantment(MakeTorpe(triggerOnTurn: 2));
            RegisterTurnOrderAtPlayerTurn(2);
            StartCombat();
            RollDice(_player.PlayerGuid);
            EndCombat();

            StartCombat();
            RollDice(_player.PlayerGuid);

            Assert.AreEqual(2, _handoff.Calls);
        }

        [Test]
        public void OnDiceRolled_HandoffRejects_KeepsChanceForNextRollSameTurn()
        {
            // El primer OnDiceRolled puede venir de un ActionRoll (Heal) — el handoff
            // lo rebota y la chance debe quedar viva para la mano de ataque.
            RegisterBagWithEnchantment(MakeTorpe(triggerOnTurn: 2));
            RegisterTurnOrderAtPlayerTurn(2);
            StartCombat();
            _handoff.NextResult = false;

            RollDice(_player.PlayerGuid);
            _handoff.NextResult = true;
            RollDice(_player.PlayerGuid);
            RollDice(_player.PlayerGuid);

            Assert.AreEqual(2, _handoff.Calls, "El tercer roll no debe pedir de nuevo — el segundo consumió el flag.");
        }

        [Test]
        public void OnDiceRolled_TwoTorpeDice_RequestsSingleReroll()
        {
            // Arrange: bag de 2 dados, cada uno con su Torpe en turno 2.
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType> { DiceType.D6, DiceType.D6 };
            bag.name = "TestBag2";
            _created.Add(bag);
            var svc = new DiceEnchantmentService(config: null);
            svc.InitializeFromBag(bag);
            Assert.IsTrue(svc.Apply(0, MakeTorpe(2)).Success);
            Assert.IsTrue(svc.Apply(1, MakeTorpe(2)).Success);
            ServiceLocator.AddService<IDiceEnchantmentService>(svc);
            RegisterTurnOrderAtPlayerTurn(2);
            StartCombat();

            // Act
            RollDice(_player.PlayerGuid);

            // Assert
            Assert.AreEqual(1, _handoff.Calls);
        }

        [Test]
        public void ResolveCurrentPlayerTurn_NoTurnOrderService_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, ForcedRerollCapabilityService.ResolveCurrentPlayerTurn());
        }

        // ---- Fakes ----------------------------------------------------------

        private sealed class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; } = Guid.NewGuid();
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => null;
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }
#pragma warning disable 67
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore 67
        }

        private sealed class SpyHandoffService : ICombatHandoffService
        {
            public int Calls { get; private set; }
            public Guid LastGuid { get; private set; }
            public bool NextResult { get; set; } = true;
            public bool IsHandoffInProgress => false;

            public bool TryScheduleForcedFullHandReroll(Guid playerGuid, float delaySeconds = 0.35f)
            {
                Calls++;
                LastGuid = playerGuid;
                return NextResult;
            }

            public bool HasCancellableSelection => false;

            public bool TryCancelFromRightClick() => false;

            public bool IsMovementSelectionCancellable => false;

            public bool TryCancelMovementSelection() => false;

            public void Dispose() { }
        }

        private sealed class FlatInitiativeProvider : IInitiativeProvider
        {
            public int RollInitiative(Guid entityGuid) => 0;
        }
    }
}
