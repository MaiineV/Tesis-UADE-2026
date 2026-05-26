using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Effects;
using UnityEngine;

namespace Rollgeon.Combat.Actions.Tests
{
    /// <summary>
    /// Fake minimalista de <see cref="IEnergyService"/> para EditMode tests —
    /// in-memory dictionary, sin dependencias de <c>AttributesManager</c>.
    /// </summary>
    internal sealed class FakeEnergyService : IEnergyService
    {
        public readonly Dictionary<Guid, int> Current = new Dictionary<Guid, int>();
        public int MaxPerEntity = 4;
        public int SpendCallCount { get; private set; }

        public void InitializeForEntity(Guid entityId) => Current[entityId] = MaxPerEntity;

        public bool SpendEnergy(Guid entityId, int cost)
        {
            SpendCallCount++;
            if (cost < 0) return false;
            if (!Current.TryGetValue(entityId, out var have)) return false;
            if (cost > have) return false;
            Current[entityId] = have - cost;
            return true;
        }

        public void RegenerateAtTurnEnd(Guid entityId) { /* no-op en tests */ }

        public int GetCurrent(Guid entityId) => Current.TryGetValue(entityId, out var v) ? v : 0;

        public int GetMax(Guid entityId) => MaxPerEntity;
    }

    [TestFixture]
    public class TurnManagerTests
    {
        private TurnManager _tm;
        private FakeEnergyService _energy;
        private List<ActionDefinitionSO> _createdDefs;
        private Guid _actor;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _energy = new FakeEnergyService();
            _actor = Guid.NewGuid();
            _energy.Current[_actor] = 4;

            _tm = new TurnManager();
            _tm.ConfigureForTests(_energy, actions: null, ruleset: null);

            _createdDefs = new List<ActionDefinitionSO>();
        }

        [TearDown]
        public void TearDown()
        {
            _tm?.Dispose();
            _tm = null;

            foreach (var def in _createdDefs)
            {
                if (def != null) UnityEngine.Object.DestroyImmediate(def);
            }
            _createdDefs = null;

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // --- Helpers -----------------------------------------------------

        private ActionDefinitionSO MakeAction(string id, int energyCost = 1, bool blockOnRepeat = true)
        {
            var def = ScriptableObject.CreateInstance<ActionDefinitionSO>();
            def.ActionId = id;
            def.Type = ActionType.Attack;
            def.EnergyCost = energyCost;
            def.BlockOnRepeat = blockOnRepeat;
            def.Effect = new EffectData(); // listas vacias.
            _createdDefs.Add(def);
            return def;
        }

        private EffectContext MakeCtx()
        {
            return new EffectContext
            {
                SourceGuid = _actor,
                TargetGuid = Guid.Empty,
                lastResult = true,
            };
        }

        // --- CanExecute --------------------------------------------------

        [Test]
        public void CanExecute_NullAction_FalseWithReason()
        {
            bool ok = _tm.CanExecute((ActionDefinitionSO)null, _actor, out var reason);

            Assert.IsFalse(ok);
            Assert.IsNotNull(reason);
            StringAssert.Contains("null", reason.ToLowerInvariant());
        }

        [Test]
        public void CanExecute_HappyPath_TrueAndNullReason()
        {
            var def = MakeAction("attack.basic", energyCost: 1);

            bool ok = _tm.CanExecute(def, _actor, out var reason);

            Assert.IsTrue(ok);
            Assert.IsNull(reason);
        }

        [Test]
        public void CanExecute_NotEnoughEnergy_FalseWithReason()
        {
            var def = MakeAction("attack.big", energyCost: 99);

            bool ok = _tm.CanExecute(def, _actor, out var reason);

            Assert.IsFalse(ok);
            Assert.IsNotNull(reason);
            StringAssert.Contains("energy", reason.ToLowerInvariant());
        }

        [Test]
        public void CanExecute_RepeatBlocked_FalseWithReason()
        {
            var def = MakeAction("attack.basic", energyCost: 1, blockOnRepeat: true);
            // Marcamos como usada via TryExecute.
            Assert.IsTrue(_tm.TryExecute(def, _actor, MakeCtx()));

            bool ok = _tm.CanExecute(def, _actor, out var reason);

            Assert.IsFalse(ok);
            StringAssert.Contains("already used", reason);
        }

        [Test]
        public void CanExecute_BlockOnRepeatFalse_CanRepeat()
        {
            // Movement pattern — BlockOnRepeat = false.
            var def = MakeAction("move", energyCost: 1, blockOnRepeat: false);
            Assert.IsTrue(_tm.TryExecute(def, _actor, MakeCtx()));

            bool ok = _tm.CanExecute(def, _actor, out var reason);

            Assert.IsTrue(ok);
            Assert.IsNull(reason);
        }

        // --- TryExecute --------------------------------------------------

        [Test]
        public void TryExecute_HappyPath_SpendsEnergyAndMarksUsed()
        {
            var def = MakeAction("attack.basic", energyCost: 1);

            bool ok = _tm.TryExecute(def, _actor, MakeCtx());

            Assert.IsTrue(ok);
            Assert.AreEqual(3, _energy.Current[_actor], "Energia cobrada (4 -> 3).");
            Assert.AreEqual(1, _energy.SpendCallCount);
            Assert.IsTrue(_tm.WasUsedThisTurn("attack.basic"));
            Assert.AreEqual(1, _tm.UsedActionsCount);
        }

        [Test]
        public void TryExecute_RepeatBlocked_DoesNotSpendOrMutate()
        {
            var def = MakeAction("attack.basic", energyCost: 1);
            _tm.TryExecute(def, _actor, MakeCtx()); // primera — exitosa.
            int spendCountAfterFirst = _energy.SpendCallCount;
            int energyAfterFirst = _energy.Current[_actor];

            bool ok = _tm.TryExecute(def, _actor, MakeCtx()); // segunda — bloqueada.

            Assert.IsFalse(ok);
            Assert.AreEqual(spendCountAfterFirst, _energy.SpendCallCount,
                "No debe intentar cobrar energia en un repeat bloqueado.");
            Assert.AreEqual(energyAfterFirst, _energy.Current[_actor]);
        }

        [Test]
        public void TryExecute_NotEnoughEnergy_FalseNoMutation()
        {
            var def = MakeAction("attack.big", energyCost: 99);

            bool ok = _tm.TryExecute(def, _actor, MakeCtx());

            Assert.IsFalse(ok);
            Assert.AreEqual(4, _energy.Current[_actor], "Energia no debe cambiar.");
            Assert.IsFalse(_tm.WasUsedThisTurn("attack.big"));
        }

        [Test]
        public void TryExecute_EmptyEffect_PermitNoOp_ChargesAndMarks()
        {
            // Accion con EffectData vacia (Effects.Count = 0) — "permit no-op".
            // TurnManager cobra energia + marca usada, delega el dispatch del
            // BackingAsset a otro sistema.
            var def = MakeAction("combo.full_house", energyCost: 2);
            Assert.AreEqual(0, def.Effect.Effects.Count);

            bool ok = _tm.TryExecute(def, _actor, MakeCtx());

            Assert.IsTrue(ok);
            Assert.AreEqual(2, _energy.Current[_actor]);
            Assert.IsTrue(_tm.WasUsedThisTurn("combo.full_house"));
        }

        [Test]
        public void TryExecute_MovementCanRepeat_SetStaysAtZero()
        {
            var move = MakeAction("move", energyCost: 1, blockOnRepeat: false);

            Assert.IsTrue(_tm.TryExecute(move, _actor, MakeCtx()));
            Assert.IsTrue(_tm.TryExecute(move, _actor, MakeCtx()));

            Assert.AreEqual(2, _energy.Current[_actor]);
            Assert.AreEqual(0, _tm.UsedActionsCount,
                "Movement con BlockOnRepeat=false NO debe entrar al set de usadas.");
        }

        // --- OnTurnStarted clear -----------------------------------------

        [Test]
        public void OnTurnStarted_ClearsUsedSet()
        {
            var def = MakeAction("attack.basic", energyCost: 1);
            _tm.TryExecute(def, _actor, MakeCtx());
            Assert.IsTrue(_tm.WasUsedThisTurn("attack.basic"));

            EventManager.Trigger(EventName.OnTurnStarted, Guid.NewGuid());

            Assert.IsFalse(_tm.WasUsedThisTurn("attack.basic"));
            Assert.AreEqual(0, _tm.UsedActionsCount);
        }

        [Test]
        public void OnTurnStarted_AfterClear_CanRepeatSameAction()
        {
            var def = MakeAction("attack.basic", energyCost: 1);
            _tm.TryExecute(def, _actor, MakeCtx()); // usada.
            _energy.Current[_actor] = 4;            // restaurar energia manualmente.

            EventManager.Trigger(EventName.OnTurnStarted, Guid.NewGuid());

            bool ok = _tm.TryExecute(def, _actor, MakeCtx());
            Assert.IsTrue(ok, "Tras OnTurnStarted la misma accion debe poder ejecutarse de nuevo.");
            Assert.IsTrue(_tm.WasUsedThisTurn("attack.basic"));
        }

        // --- Dispose -----------------------------------------------------

        [Test]
        public void Dispose_UnsubscribesFromOnTurnStarted()
        {
            var def = MakeAction("attack.basic", energyCost: 1);
            _tm.TryExecute(def, _actor, MakeCtx());

            _tm.Dispose();
            // Re-suscribimos otro TurnManager para verificar que el disposed ya no responde.
            // El dispose limpia el set tambien — verificamos ese contrato.
            Assert.AreEqual(0, _tm.UsedActionsCount);

            // Un Trigger post-Dispose no debe lanzar (suscripcion ya retirada).
            Assert.DoesNotThrow(() => EventManager.Trigger(EventName.OnTurnStarted, Guid.NewGuid()));

            _tm = null; // evitar doble-dispose en TearDown.
        }

        // --- Multi-actor semantics (plan §10 R4) ------------------------

        [Test]
        public void MultiActor_SameTurnShareSet_ButClearOnTurnStarted()
        {
            // Dos actores distintos atacan en el mismo "turno" — el TurnManager es global
            // y comparte el set. Esto es intencional: el clear ocurre en OnTurnStarted.
            var actorB = Guid.NewGuid();
            _energy.Current[actorB] = 4;

            var def = MakeAction("attack.basic", energyCost: 1);

            Assert.IsTrue(_tm.TryExecute(def, _actor, MakeCtx()));
            // Actor B intenta la misma accion — bloqueada por repeat en el mismo slot.
            bool ok = _tm.CanExecute(def, actorB, out var reason);
            Assert.IsFalse(ok, "Semantica = slot del actor activo; clear entre OnTurnStarted. Plan R4.");
            StringAssert.Contains("already used", reason);

            // Tras OnTurnStarted (cambio de turno), actor B puede ejecutar.
            EventManager.Trigger(EventName.OnTurnStarted, actorB);
            Assert.IsTrue(_tm.CanExecute(def, actorB, out _));
        }
    }
}
