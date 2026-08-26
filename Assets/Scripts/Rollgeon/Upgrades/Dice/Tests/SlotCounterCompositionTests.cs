using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Combos;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Upgrades.Dice.Effects;
using Rollgeon.Upgrades.Dice.PreConditions;
using Rollgeon.Upgrades.Dice.Triggers;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Réplica composicional de <c>ExplodeIfUnusedForTurns</c> (el único trigger
    /// stateful real) corriendo contra el <see cref="DiceEnchantmentService"/> de
    /// verdad: reset en Applied/ComboMatched, increment en TurnFinished y
    /// self-remove exacto al turno N — incluida la mutación del bag durante el
    /// dispatch (set-null, patrón heredado del legacy).
    /// </summary>
    [TestFixture]
    public class SlotCounterCompositionTests
    {
        private const string Key = "explode_if_unused";

        private DiceEnchantmentService _svc;
        private StubPlayerService _player;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            TypedEvent<ComboMatchedPayload>.Clear();
            SaveSystem.ResetForTests();

            _svc = new DiceEnchantmentService(config: null);
            _svc.SubscribeEventsForTests();
            ServiceLocator.AddService<IDiceEnchantmentRuntime>(_svc, ServiceScope.Global);

            _player = new StubPlayerService();
            ServiceLocator.AddService<IPlayerService>(_player, ServiceScope.Global);

            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType> { DiceType.D6 };
            _created.Add(bag);
            _svc.InitializeFromBag(bag);
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.UnsubscribeEventsForTests();
            _svc = null;

            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            TypedEvent<ComboMatchedPayload>.Clear();
            SaveSystem.ResetForTests();
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static ExecuteEffectsOnDiceEvent ResetOn(EnchantmentHookEvent evt)
        {
            var reset = new EffectData();
            reset.Effects.Add(new EffSlotCounter { Operation = SlotCounterOperation.Reset, Key = Key });
            return new ExecuteEffectsOnDiceEvent
            {
                Event = evt,
                Effects = new List<EffectData> { reset },
            };
        }

        private static ExecuteEffectsOnDiceEvent IncrementAndExplodeOnTurnFinished(int maxTurns)
        {
            var increment = new EffectData();
            increment.Effects.Add(new EffSlotCounter { Operation = SlotCounterOperation.Increment, Key = Key });

            var explode = new EffectData();
            explode.PreConditions.Add(new PcSlotCounterCompare
            {
                Key = Key,
                Comparison = IntComparison.GreaterOrEqual,
                Value = maxTurns,
            });
            explode.Effects.Add(new EffRemoveEnchantment());

            return new ExecuteEffectsOnDiceEvent
            {
                Event = EnchantmentHookEvent.TurnFinished,
                Effects = new List<EffectData> { increment, explode },
            };
        }

        private EnchantmentSO ApplyExplodingEnchantment(int maxTurns)
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            ench.name = "e-explode";
            _created.Add(ench);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, "e-explode");
            typeof(EnchantmentSO).GetField("_allowedDiceTypes", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<DiceType>());
            typeof(EnchantmentSO).GetField("_triggers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<Triggers.IEnchantmentTrigger>
                {
                    ResetOn(EnchantmentHookEvent.EnchantmentApplied),
                    ResetOn(EnchantmentHookEvent.ComboMatched),
                    IncrementAndExplodeOnTurnFinished(maxTurns),
                });

            Assert.IsTrue(_svc.Apply(0, ench).Success);
            return ench;
        }

        private void FinishPlayerTurn() =>
            EventManager.Trigger(EventName.OnTurnFinished, _player.PlayerGuid);

        private bool EnchantmentStillApplied() => _svc.Bag.GetEnchantmentAt(0, 0) != null;

        // ================================================================
        // Tests
        // ================================================================

        [Test]
        public void Composition_RemovesEnchantment_ExactlyAtTurnN()
        {
            // Arrange
            ApplyExplodingEnchantment(maxTurns: 3);

            // Act / Assert — sobrevive los turnos 1 y 2, explota exacto en el 3.
            FinishPlayerTurn();
            Assert.IsTrue(EnchantmentStillApplied(), "Turno 1: aún vivo.");
            FinishPlayerTurn();
            Assert.IsTrue(EnchantmentStillApplied(), "Turno 2: aún vivo.");
            FinishPlayerTurn();
            Assert.IsFalse(EnchantmentStillApplied(), "Turno 3: debe auto-removerse.");
        }

        [Test]
        public void Composition_ComboMatch_ResetsTheCountdown()
        {
            // Arrange
            ApplyExplodingEnchantment(maxTurns: 3);

            // Act — 2 turnos sin uso, el combo resetea, después hacen falta 3 más.
            FinishPlayerTurn();
            FinishPlayerTurn();
            TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
            {
                SourceGuid = _player.PlayerGuid,
                ComboId = "combo.par",
                BaseDamage = 10,
            });
            FinishPlayerTurn();
            FinishPlayerTurn();
            Assert.IsTrue(EnchantmentStillApplied(), "El combo reseteó: 2 turnos post-reset no alcanzan.");

            FinishPlayerTurn();

            // Assert
            Assert.IsFalse(EnchantmentStillApplied(), "Tercer turno post-reset: explota.");
        }

        [Test]
        public void Composition_EnemyTurns_DoNotAdvanceCountdown()
        {
            // Arrange
            ApplyExplodingEnchantment(maxTurns: 1);

            // Act — turnos de OTRA entidad no cuentan (filtro del service).
            EventManager.Trigger(EventName.OnTurnFinished, Guid.NewGuid());
            EventManager.Trigger(EventName.OnTurnFinished, Guid.NewGuid());

            // Assert
            Assert.IsTrue(EnchantmentStillApplied());
        }

        private sealed class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; } = Guid.NewGuid();
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
