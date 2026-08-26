using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combos;
using Rollgeon.Dice;
using Rollgeon.Economy;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Upgrades.Dice.Triggers;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// BUG-060: los encantamientos de oro pagaban en cualquier contexto con tirada
    /// (pasar turno, movimiento) porque <c>OnRollResolved</c> no viajaba con ningún
    /// discriminante de acción y <c>ComboResult</c> nunca se poblaba en ese dispatch.
    /// Repro con réplicas mínimas de Ambicioso (RollResolved + PcNoComboThisRoll) y
    /// Avaro (ComboPlayed + whitelist de combo) sobre el service real.
    /// </summary>
    [TestFixture]
    public class DiceEnchantmentServiceActionKindGatingTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private DiceEnchantmentService _svc;
        private FakeEconomy _economy;
        private Guid _playerGuid;

        [SetUp]
        public void SetUp()
        {
            _economy = new FakeEconomy();
            ServiceLocator.AddService<IEconomyService>(_economy, ServiceScope.Global);
            _playerGuid = Guid.NewGuid();

            _svc = new DiceEnchantmentService(config: null);
            _svc.SubscribeEventsForTests();

            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType> { DiceType.D6 };
            _created.Add(bag);
            _svc.InitializeFromBag(bag);
        }

        [TearDown]
        public void TearDown()
        {
            _svc.UnsubscribeEventsForTests();
            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();
            ServiceLocator.Clear();
        }

        // ---- Helpers ----------------------------------------------------

        // Réplica de Ench_GoldOnRoll (Ambicioso): RollResolved + PcNoComboThisRoll → +2 oro
        // de consuelo cuando NO se formó combo. Apply es append-only — cada llamada
        // suma un encantamiento nuevo al dado sin tocar los anteriores.
        private void ApplyAmbiciosoStyleEnchantment()
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            _created.Add(ench);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, "ambicioso-repro");
            typeof(EnchantmentSO).GetField("_allowedDiceTypes", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<DiceType>());

            var group = new EffectData();
            group.PreConditions.Add(new PcNoComboThisRoll());
            group.Effects.Add(new EffModifyGold { Operation = GoldOperation.Add, Amount = new ReadConstantInt { Value = 2 } });
            var bridge = new ExecuteEffectsOnDiceEvent
            {
                Event = EnchantmentHookEvent.RollResolved,
                Effects = new List<EffectData> { group },
            };
            typeof(EnchantmentSO).GetField("_triggers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<IEnchantmentTrigger> { bridge });

            Assert.IsTrue(_svc.Apply(0, ench).Success);
        }

        // Réplica de Ench_Avaro: ComboPlayed + whitelist "combo.trio" → +3 oro.
        private void ApplyAvaroStyleEnchantment()
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            _created.Add(ench);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, "avaro-repro");
            typeof(EnchantmentSO).GetField("_allowedDiceTypes", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<DiceType>());

            var group = new EffectData();
            group.Effects.Add(new EffModifyGold { Operation = GoldOperation.Add, Amount = new ReadConstantInt { Value = 3 } });
            var bridge = new ExecuteEffectsOnDiceEvent
            {
                Event = EnchantmentHookEvent.ComboPlayed,
                Filter = new ComboFilter { Mode = ComboFilterMode.ComboIds, ComboIds = new List<string> { "combo.trio" } },
                Effects = new List<EffectData> { group },
            };
            typeof(EnchantmentSO).GetField("_triggers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<IEnchantmentTrigger> { bridge });

            Assert.IsTrue(_svc.Apply(0, ench).Success);
        }

        private void TriggerRollResolved(RollActionKind? kind, ComboDetectionResult? combo)
        {
            var faces = (IReadOnlyList<int>)new List<int> { 3, 3, 3 };
            if (kind.HasValue)
                EventManager.Trigger(EventName.OnRollResolved, _playerGuid, faces, kind.Value, combo);
            else
                // Back-compat: emisor sin discriminante (equivalente a args.Length == 2).
                EventManager.Trigger(EventName.OnRollResolved, _playerGuid, faces);
        }

        private void TriggerComboPlayed(RollActionKind kind, string comboId)
        {
            TypedEvent<ComboPlayedPayload>.Raise(new ComboPlayedPayload
            {
                SourceGuid = _playerGuid,
                ComboId = comboId,
                ComboResult = ComboDetectionResult.Match(comboId, baseDamage: 10, countUsed: 3,
                    contributingIndices: new[] { 0, 1, 2 }),
                DiceResult = new[] { 3, 3, 3 },
                ActionKind = kind,
            });
        }

        // ---- Tests --------------------------------------------------------

        [Test]
        public void RollResolved_PassTurn_WithoutActionKind_DoesNotPayAmbicioso()
        {
            // Arrange
            ApplyAmbiciosoStyleEnchantment();

            // Act — emisor sin discriminante (equivalente al viejo pasar-turno, ANTES del
            // fix removido de CombatHandoffService).
            TriggerRollResolved(kind: null, combo: null);

            // Assert
            Assert.AreEqual(0, _economy.CurrentGold,
                "Pasar turno no es una tirada de combate — no debe pagar oro.");
        }

        [Test]
        public void ComboPlayed_Movement_WithMatchingCombo_DoesNotPayAvaro()
        {
            // Arrange — un trío tirado para MOVERSE (mismo bag que un ataque puede formar
            // un combo "de paso", pero Movement no es una acción de combate pagable).
            ApplyAvaroStyleEnchantment();

            // Act
            TriggerComboPlayed(RollActionKind.Movement, "combo.trio");

            // Assert
            Assert.AreEqual(0, _economy.CurrentGold);
        }

        [Test]
        public void RollResolved_Attack_NoCombo_PaysAmbicioso()
        {
            // Arrange
            ApplyAmbiciosoStyleEnchantment();

            // Act — ataque sin combo (ComboResult NoMatch): PcNoComboThisRoll pasa.
            TriggerRollResolved(RollActionKind.Attack, ComboDetectionResult.NoMatch());

            // Assert
            Assert.AreEqual(2, _economy.CurrentGold);
        }

        [Test]
        public void RollResolved_Attack_WithTrio_DoesNotPayAmbicioso_ButComboPlayedPaysAvaro()
        {
            // Arrange — append-only: ambos encantamientos conviven en el mismo dado
            // sin pisarse (índices 0 y 1 asignados automáticamente por Apply).
            ApplyAmbiciosoStyleEnchantment();
            ApplyAvaroStyleEnchantment();
            var combo = ComboDetectionResult.Match("combo.trio", baseDamage: 10, countUsed: 3,
                contributingIndices: new[] { 0, 1, 2 });

            // Act — mismo ataque: RollResolved con ComboResult REAL (match) + ComboPlayed.
            TriggerRollResolved(RollActionKind.Attack, combo);
            TriggerComboPlayed(RollActionKind.Attack, "combo.trio");

            // Assert — Ambicioso NO paga (hubo combo), Avaro sí (trío en whitelist).
            Assert.AreEqual(3, _economy.CurrentGold);
        }

        [Test]
        public void ComboPlayed_HealInCombat_WithMatchingCombo_PaysAvaroStyleHook()
        {
            // Arrange — decisión de diseño: Curarse EN COMBATE con combo SÍ paga
            // encantamientos de oro (Attack/Defense/Heal son las 3 acciones pagables).
            ApplyAvaroStyleEnchantment();

            // Act
            TriggerComboPlayed(RollActionKind.Heal, "combo.trio");

            // Assert
            Assert.AreEqual(3, _economy.CurrentGold);
        }

        private sealed class FakeEconomy : IEconomyService
        {
            public int CurrentGold { get; private set; }
            public void Add(int amount) { if (amount > 0) CurrentGold += amount; }
            public bool Spend(int amount)
            {
                if (amount > CurrentGold) return false;
                CurrentGold -= amount;
                return true;
            }
            public bool CanAfford(int amount) => amount <= 0 || CurrentGold >= amount;
            public void ResetTo(int amount) => CurrentGold = amount;
        }
    }
}
