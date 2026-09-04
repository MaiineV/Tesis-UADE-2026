using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Capabilities del carril del dado de Movimiento (§6.6): la consulta por slot tiene que
    /// aceptar el sentinela <see cref="EnchantmentSlotRef.MovementDieSlot"/> — el guard
    /// <c>bagSlot &lt; 0</c> lo rechazaba y Paso etéreo nunca aplicaba — y
    /// <see cref="EtherealMovementPolicy"/> la resuelve para el jugador.
    /// </summary>
    [TestFixture]
    public class MovementLaneCapabilityTests
    {
        private const int Lane = EnchantmentSlotRef.MovementDieSlot;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private DiceEnchantmentService _svc;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;
            foreach (var o in _created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _created.Clear();
            ServiceLocator.Clear();
        }

        private EnchantmentSO MakeMovementEnchantment(string id, params IEnchantmentCapability[] caps)
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            ench.name = id;
            _created.Add(ench);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, id);
            typeof(EnchantmentSO).GetField("_category", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, EnchantmentCategory.Movimiento);
            typeof(EnchantmentSO).GetField("_capabilities", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<IEnchantmentCapability>(caps));
            return ench;
        }

        private RuntimeDiceBag RegisterServiceWithMovementLane(params EnchantmentSO[] lane)
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType> { DiceType.D6, DiceType.D6 };
            _created.Add(bag);
            _svc = new DiceEnchantmentService(config: null);
            _svc.InitializeFromBag(bag);
            foreach (var e in lane) _svc.Bag.AddEnchantment(Lane, e);
            ServiceLocator.AddService<IDiceEnchantmentService>(_svc, ServiceScope.Global);
            return _svc.Bag;
        }

        private Guid RegisterPlayer()
        {
            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _created.Add(hero);
            var player = new PlayerService();
            player.SetPlayer(hero, Guid.NewGuid());
            ServiceLocator.AddService<IPlayerService>(player, ServiceScope.Global);
            return player.PlayerGuid;
        }

        [Test]
        public void SlotHasCapability_AcceptsTheMovementDieLane()
        {
            var bag = RegisterServiceWithMovementLane(
                MakeMovementEnchantment("ench.paso_etereo", new CapEtherealMovement()));

            Assert.IsTrue(bag.SlotHasCapability<CapEtherealMovement>(Lane));
            Assert.IsFalse(bag.SlotHasCapability<CapCursed>(Lane), "otra capability no está en el carril");
            Assert.IsFalse(bag.SlotHasCapability<CapEtherealMovement>(0), "los dados del bag no la tienen");
        }

        [Test]
        public void SlotHasCapability_StillRejectsIndicesOutsideBagAndLane()
        {
            var bag = RegisterServiceWithMovementLane(
                MakeMovementEnchantment("ench.paso_etereo", new CapEtherealMovement()));

            Assert.IsFalse(bag.SlotHasCapability<CapEtherealMovement>(EnchantmentSlotRef.RunCounterIndex));
            Assert.IsFalse(bag.SlotHasCapability<CapEtherealMovement>(-3));
            Assert.IsFalse(bag.SlotHasCapability<CapEtherealMovement>(bag.Dice.Count));
        }

        [Test]
        public void PlayerSlotHasCapability_ReadsTheMovementLaneThroughTheLocator()
        {
            RegisterServiceWithMovementLane(
                MakeMovementEnchantment("ench.paso_etereo", new CapEtherealMovement()));

            Assert.IsTrue(EnchantmentCapabilityQueries.PlayerSlotHasCapability<CapEtherealMovement>(Lane));
        }

        [Test]
        public void EtherealPolicy_LetsOnlyThePlayerWithTheCapabilityPassThroughUnits()
        {
            var player = RegisterPlayer();
            RegisterServiceWithMovementLane(
                MakeMovementEnchantment("ench.paso_etereo", new CapEtherealMovement()));
            var policy = new EtherealMovementPolicy();

            Assert.IsTrue(policy.CanPassThroughUnits(player));
            Assert.IsFalse(policy.CanPassThroughUnits(Guid.NewGuid()), "un enemigo no hereda el paso");
            Assert.IsFalse(policy.CanPassThroughUnits(Guid.Empty));
        }

        [Test]
        public void EtherealPolicy_WithoutTheCapabilityInTheLane_DoesNotPass()
        {
            var player = RegisterPlayer();
            RegisterServiceWithMovementLane(MakeMovementEnchantment("ench.carga"));
            var policy = new EtherealMovementPolicy();

            Assert.IsFalse(policy.CanPassThroughUnits(player));
        }

        [Test]
        public void EtherealPolicy_TombstonedCapability_StopsPassing()
        {
            var player = RegisterPlayer();
            var bag = RegisterServiceWithMovementLane(
                MakeMovementEnchantment("ench.paso_etereo", new CapEtherealMovement()));
            var policy = new EtherealMovementPolicy();
            Assert.IsTrue(policy.CanPassThroughUnits(player), "pre-condition");

            bag.SetEnchantmentAt(Lane, 0, null);

            Assert.IsFalse(policy.CanPassThroughUnits(player));
        }
    }
}
