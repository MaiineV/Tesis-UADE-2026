using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Dice;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.Upgrades.Dice.Filters;
using Rollgeon.Upgrades.Dice.Triggers;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Carril del dado de Movimiento (§6.6) en <see cref="RuntimeDiceBag"/> /
    /// <see cref="DiceEnchantmentService"/>: índice sentinela
    /// <see cref="EnchantmentSlotRef.MovementDieSlot"/>, caras extra, regla de categoría
    /// (<see cref="EnchantmentTargeting"/>), save round-trip y dispatch del hook
    /// <c>PlayerMoved</c> desde <c>EntityWalkedPayload</c>.
    /// </summary>
    [TestFixture]
    public class MovementDieLaneTests
    {
        private const int Lane = EnchantmentSlotRef.MovementDieSlot;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private DiceEnchantmentService _svc;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            TypedEvent<EntityWalkedPayload>.Clear();
            SaveSystem.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;
            foreach (var o in _created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _created.Clear();
            ServiceLocator.Clear();
            TypedEvent<EntityWalkedPayload>.Clear();
            SaveSystem.ResetForTests();
        }

        // ---- Helpers ---------------------------------------------------------

        private EnchantmentSO MakeEnchantment(string id, EnchantmentCategory category,
            IFaceFilter filter = null, params IEnchantmentTrigger[] triggers)
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            ench.name = id;
            _created.Add(ench);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, id);
            typeof(EnchantmentSO).GetField("_category", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, category);
            if (filter != null)
                typeof(EnchantmentSO).GetField("_faceFilter", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(ench, filter);
            if (triggers.Length > 0)
                typeof(EnchantmentSO).GetField("_triggers", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(ench, new List<IEnchantmentTrigger>(triggers));
            return ench;
        }

        private DiceBagSO MakeBag(params DiceType[] dice)
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType>(dice);
            _created.Add(bag);
            return bag;
        }

        private DiceEnchantmentService MakeService(params DiceType[] dice)
        {
            _svc = new DiceEnchantmentService(config: null);
            _svc.InitializeFromBag(MakeBag(dice));
            return _svc;
        }

        private sealed class RecordingMoveTrigger : IOnPlayerMovedTrigger
        {
            public readonly List<(int tiles, int turn, int bag)> Calls = new List<(int, int, int)>();
            public void OnPlayerMoved(EnchantmentTriggerContext ctx)
                => Calls.Add((ctx.TilesTraversed, ctx.TilesTraversedThisTurn, ctx.Slot.BagSlotIndex));
        }

        private static IReadOnlyList<GridCoord> Path(int tiles)
        {
            var path = new List<GridCoord>();
            for (int i = 0; i <= tiles; i++) path.Add(new GridCoord(i, 0));
            return path;
        }

        private static void Walk(Guid who, int tiles)
        {
            var path = Path(tiles);
            TypedEvent<EntityWalkedPayload>.Raise(new EntityWalkedPayload
            {
                EntityGuid = who,
                From = path[0],
                To = path[path.Count - 1],
                Path = path,
                TilesTraversed = tiles,
            });
        }

        // ---- RuntimeDiceBag: carril + caras -------------------------------------

        [Test]
        public void Bag_MovementLane_IsIndependentFromCombatSlots()
        {
            var bag = new RuntimeDiceBag(new[] { DiceType.D6, DiceType.D8 });
            var ench = MakeEnchantment("ench.move", EnchantmentCategory.Movimiento);

            int slot = bag.AddEnchantment(Lane, ench);

            Assert.AreEqual(0, slot);
            Assert.AreEqual(1, bag.GetEnchantmentCount(Lane));
            Assert.AreEqual(0, bag.GetEnchantmentCount(0));
            Assert.AreEqual(0, bag.GetEnchantmentCount(1));
            Assert.AreEqual(2, bag.Dice.Count, "El carril no es un dado del bag.");
            Assert.IsTrue(bag.IsValidIndex(Lane));
            Assert.IsFalse(bag.IsValidIndex(EnchantmentSlotRef.RunCounterIndex));
        }

        [Test]
        public void Bag_MovementLane_TombstonesKeepIndicesStable()
        {
            var bag = new RuntimeDiceBag(new[] { DiceType.D6 });
            var a = MakeEnchantment("ench.a", EnchantmentCategory.Movimiento);
            var b = MakeEnchantment("ench.b", EnchantmentCategory.Movimiento);
            bag.AddEnchantment(Lane, a);
            bag.AddEnchantment(Lane, b);

            Assert.IsTrue(bag.SetEnchantmentAt(Lane, 0, null));

            Assert.AreEqual(2, bag.GetEnchantmentCount(Lane));
            Assert.IsNull(bag.GetEnchantmentAt(Lane, 0));
            Assert.AreSame(b, bag.GetEnchantmentAt(Lane, 1));
        }

        [Test]
        public void Bag_AddMovementExtraFaces_AccumulatesAndClampsAtZero()
        {
            var bag = new RuntimeDiceBag(new[] { DiceType.D6 });

            Assert.AreEqual(2, bag.AddMovementExtraFaces(2));
            Assert.AreEqual(3, bag.AddMovementExtraFaces(1));
            Assert.AreEqual(0, bag.AddMovementExtraFaces(-10));
        }

        [Test]
        public void Bag_Snapshot_RoundTripsMovementLaneAndExtraFaces()
        {
            var ench = MakeEnchantment("ench.move", EnchantmentCategory.Movimiento);
            var combat = MakeEnchantment("ench.combat", EnchantmentCategory.Ataque);
            var catalog = new Dictionary<string, EnchantmentSO> { ["ench.move"] = ench, ["ench.combat"] = combat };
            EnchantmentSO Resolve(string id) => catalog.TryGetValue(id, out var e) ? e : null;

            var source = new RuntimeDiceBag(new[] { DiceType.D6 }, Resolve);
            source.AddEnchantment(0, combat);
            source.AddEnchantment(Lane, ench);
            source.AddEnchantment(Lane, ench);
            source.SetEnchantmentAt(Lane, 0, null);
            source.AddMovementExtraFaces(2);
            source.IncrementCounter(new EnchantmentSlotRef(DiceType.D6, Lane, 1), "k", 3);

            var restored = new RuntimeDiceBag(new[] { DiceType.D6 }, Resolve);
            restored.RestoreState(source.CaptureState());

            Assert.AreEqual(2, restored.MovementExtraFaces);
            Assert.AreEqual(2, restored.GetEnchantmentCount(Lane), "El tombstone se restaura como padding.");
            Assert.IsNull(restored.GetEnchantmentAt(Lane, 0));
            Assert.AreSame(ench, restored.GetEnchantmentAt(Lane, 1));
            Assert.AreSame(combat, restored.GetEnchantmentAt(0, 0));
            Assert.AreEqual(3, restored.GetCounter(new EnchantmentSlotRef(DiceType.D6, Lane, 1), "k"));
        }

        [Test]
        public void Bag_Snapshot_Legacy_WithoutMovementList_RestoresCombatSlotsOnly()
        {
            var combat = MakeEnchantment("ench.combat", EnchantmentCategory.Ataque);
            var bag = new RuntimeDiceBag(new[] { DiceType.D6 }, id => id == "ench.combat" ? combat : null);
            var legacy = new RuntimeDiceBagSnapshot { MovementEnchantments = null };
            legacy.Enchantments.Add(new EnchantmentSlotSnapshot { BagIndex = 0, SlotIndex = 0, EnchantmentId = "ench.combat" });

            bag.RestoreState(legacy);

            Assert.AreSame(combat, bag.GetEnchantmentAt(0, 0));
            Assert.AreEqual(0, bag.GetEnchantmentCount(Lane));
            Assert.AreEqual(0, bag.MovementExtraFaces);
        }

        // ---- Service: regla de categoría + caras ---------------------------------

        [Test]
        public void ValidateApply_MovementEnchantment_OnCombatDie_Fails()
        {
            var svc = MakeService(DiceType.D6);
            var ench = MakeEnchantment("ench.move", EnchantmentCategory.Movimiento);

            Assert.IsFalse(svc.ValidateApply(0, ench).Success);
        }

        [Test]
        public void ValidateApply_CombatEnchantment_OnMovementLane_Fails()
        {
            var svc = MakeService(DiceType.D6);
            var ench = MakeEnchantment("ench.combat", EnchantmentCategory.Control);

            Assert.IsFalse(svc.ValidateApply(Lane, ench).Success);
        }

        [Test]
        public void Apply_MovementEnchantment_OnMovementLane_Succeeds()
        {
            var svc = MakeService(DiceType.D6);
            var ench = MakeEnchantment("ench.move", EnchantmentCategory.Movimiento);

            var result = svc.Apply(Lane, ench);

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(0, result.AppliedSlotIndex);
            Assert.AreSame(ench, svc.Bag.GetEnchantmentAt(Lane, 0));
            Assert.IsTrue(svc.Remove(Lane, 0));
        }

        [Test]
        public void ComputeMovementDieFaces_ExtraFacesAndFilters_Compose()
        {
            // Sin IMovementDieService registrado ⇒ tipo base default (D6).
            var svc = MakeService(DiceType.D4);
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 6), svc.ComputeMovementDieFaces());
            Assert.AreEqual(6, svc.MovementDieMaxFace);

            svc.AddMovementDieFaces(2);
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 8), svc.ComputeMovementDieFaces());
            Assert.AreEqual(8, svc.MovementDieMaxFace);

            var evens = MakeEnchantment("ench.par_move", EnchantmentCategory.Movimiento,
                new ParityFilter { Allowed = Parity.Even });
            Assert.IsTrue(svc.Apply(Lane, evens).Success);
            CollectionAssert.AreEquivalent(new[] { 2, 4, 6, 8 }, svc.ComputeMovementDieFaces());

            // El bag de combate no se enteró de nada.
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 4), svc.ComputeAllowedFaces(0));
        }

        // ---- Service: hook PlayerMoved ---------------------------------------------

        private Guid RegisterPlayerAndService()
        {
            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _created.Add(hero);
            var player = new PlayerService();
            player.SetPlayer(hero, Guid.NewGuid());
            ServiceLocator.AddService<IPlayerService>(player, ServiceScope.Global);

            MakeService(DiceType.D6);
            _svc.Register();
            return player.PlayerGuid;
        }

        [Test]
        public void PlayerMoved_InCombat_DispatchesToMovementLaneWithTilesAndTurnTotal()
        {
            var playerGuid = RegisterPlayerAndService();
            var trigger = new RecordingMoveTrigger();
            Assert.IsTrue(_svc.Apply(Lane, MakeEnchantment("ench.move", EnchantmentCategory.Movimiento, null, trigger)).Success);
            EventManager.Trigger(EventName.OnCombatStart, Guid.NewGuid());

            Walk(playerGuid, 4);
            Walk(playerGuid, 3);

            Assert.AreEqual(2, trigger.Calls.Count);
            Assert.AreEqual((4, 4, Lane), trigger.Calls[0]);
            Assert.AreEqual((3, 7, Lane), trigger.Calls[1]);
        }

        [Test]
        public void PlayerMoved_TurnTotal_ResetsWhenThePlayerTurnFinishes()
        {
            var playerGuid = RegisterPlayerAndService();
            var trigger = new RecordingMoveTrigger();
            _svc.Apply(Lane, MakeEnchantment("ench.move", EnchantmentCategory.Movimiento, null, trigger));
            EventManager.Trigger(EventName.OnCombatStart, Guid.NewGuid());

            Walk(playerGuid, 5);
            EventManager.Trigger(EventName.OnTurnFinished, playerGuid);
            Walk(playerGuid, 2);

            Assert.AreEqual((2, 2, Lane), trigger.Calls[1]);
        }

        private sealed class RecordingRollTrigger : IOnMovementDieRolledTrigger
        {
            public readonly List<int> Faces = new List<int>();
            public void OnMovementDieRolled(EnchantmentTriggerContext ctx) => Faces.Add(ctx.MovementDieFace);
        }

        [Test]
        public void PlayerMoved_CarriesTheWalkedPath()
        {
            var playerGuid = RegisterPlayerAndService();
            IReadOnlyList<GridCoord> seen = null;
            var trigger = new PathTrigger(p => seen = p);
            _svc.Apply(Lane, MakeEnchantment("ench.move", EnchantmentCategory.Movimiento, null, trigger));
            EventManager.Trigger(EventName.OnCombatStart, Guid.NewGuid());

            Walk(playerGuid, 3);

            Assert.IsNotNull(seen);
            Assert.AreEqual(4, seen.Count);
            Assert.AreEqual(new GridCoord(0, 0), seen[0]);
        }

        private sealed class PathTrigger : IOnPlayerMovedTrigger
        {
            private readonly Action<IReadOnlyList<GridCoord>> _onPath;
            public PathTrigger(Action<IReadOnlyList<GridCoord>> onPath) { _onPath = onPath; }
            public void OnPlayerMoved(EnchantmentTriggerContext ctx) => _onPath(ctx.Path);
        }

        [Test]
        public void MovementDieRolled_InCombat_DispatchesWithTheFace()
        {
            var playerGuid = RegisterPlayerAndService();
            var trigger = new RecordingRollTrigger();
            _svc.Apply(Lane, MakeEnchantment("ench.torb", EnchantmentCategory.Movimiento, null, trigger));

            EventManager.Trigger(EventName.OnMovementDieRolled, playerGuid, 5, DiceType.D6); // sin combate
            EventManager.Trigger(EventName.OnCombatStart, Guid.NewGuid());
            EventManager.Trigger(EventName.OnMovementDieRolled, Guid.NewGuid(), 2, DiceType.D6); // otro guid
            EventManager.Trigger(EventName.OnMovementDieRolled, playerGuid, 4, DiceType.D6);

            CollectionAssert.AreEqual(new[] { 4 }, trigger.Faces);
        }

        [Test]
        public void PlayerMoved_OutsideCombatOrOtherEntity_DoesNotDispatch()
        {
            var playerGuid = RegisterPlayerAndService();
            var trigger = new RecordingMoveTrigger();
            _svc.Apply(Lane, MakeEnchantment("ench.move", EnchantmentCategory.Movimiento, null, trigger));

            Walk(playerGuid, 3); // sin OnCombatStart
            EventManager.Trigger(EventName.OnCombatStart, Guid.NewGuid());
            Walk(Guid.NewGuid(), 3); // un enemigo
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid());
            Walk(playerGuid, 3); // combate cerrado

            Assert.AreEqual(0, trigger.Calls.Count);
        }
    }
}
