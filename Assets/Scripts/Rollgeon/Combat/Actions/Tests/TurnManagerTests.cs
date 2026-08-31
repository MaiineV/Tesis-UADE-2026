using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.Rolls;
using Rollgeon.Effects;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Combat.Actions.Tests
{
    /// <summary>
    /// Fake minimalista de <see cref="IRollPoolService"/> para EditMode tests —
    /// in-memory dictionary, sin RulesetSO ni eventos.
    /// </summary>
    internal sealed class FakeRollPoolService : IRollPoolService
    {
        public readonly Dictionary<Guid, int> Current = new Dictionary<Guid, int>();
        public int Cap = 15;
        public bool InCombat = true;
        public int SpendCallCount { get; private set; }

        public bool IsCombatActive => InCombat;

        public void InitializeForEntity(Guid entityId) => Current[entityId] = 5;

        public bool TrySpendRolls(Guid entityId, int count)
        {
            SpendCallCount++;
            if (count < 0) return false;
            if (count == 0) return true;
            if (!Current.TryGetValue(entityId, out var have)) return false;
            if (count > have) return false;
            Current[entityId] = have - count;
            return true;
        }

        public int Drain(Guid entityId, int amount)
        {
            if (amount <= 0 || !Current.TryGetValue(entityId, out var have)) return 0;
            int drained = Math.Min(amount, have);
            Current[entityId] = have - drained;
            return drained;
        }

        public void AddRolls(Guid entityId, int amount)
        {
            if (amount <= 0) return;
            Current.TryGetValue(entityId, out var have);
            Current[entityId] = Math.Min(Cap, have + amount);
        }

        public int GetCurrent(Guid entityId) => Current.TryGetValue(entityId, out var v) ? v : 0;

        public int GetMax(Guid entityId) => Cap;

        public int GetRollsPerTurn(Guid entityId) => 5;

        public void AddRollPoolBonus(int amount) { }

        public void RestoreCurrent(Guid entityId, int value)
            => Current[entityId] = Math.Clamp(value, 0, Cap);
    }

    [TestFixture]
    public class TurnManagerTests
    {
        private TurnManager _tm;
        private FakeRollPoolService _energy;
        private List<ActionDefinitionSO> _createdDefs;
        private Guid _actor;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _energy = new FakeRollPoolService();
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

        private ActionDefinitionSO MakeAction(string id)
        {
            var def = ScriptableObject.CreateInstance<ActionDefinitionSO>();
            def.ActionId = id;
            def.Type = ActionType.Attack;
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
            var def = MakeAction("attack.basic");

            bool ok = _tm.CanExecute(def, _actor, out var reason);

            Assert.IsTrue(ok);
            Assert.IsNull(reason);
        }

        [Test]
        public void CanExecute_EmptyPool_FalseWithReason()
        {
            var def = MakeAction("attack.big");
            _energy.Current[_actor] = 0;

            bool ok = _tm.CanExecute(def, _actor, out var reason);

            Assert.IsFalse(ok);
            Assert.IsNotNull(reason);
            StringAssert.Contains("rolls", reason.ToLowerInvariant());
        }

        [Test]
        public void CanExecute_EmptyPoolOutsideCombat_True()
        {
            // Fuera de combate el pool no existe — items/acciones no se gatean.
            var def = MakeAction("item.potion");
            _energy.Current[_actor] = 0;
            _energy.InCombat = false;

            Assert.IsTrue(_tm.CanExecute(def, _actor, out _));
        }

        [Test]
        public void CanExecute_AfterExecution_StillTrueWhileRollsRemain()
        {
            // Sin limite de acciones por turno: ejecutar una accion no la bloquea.
            var def = MakeAction("attack.basic");
            Assert.IsTrue(_tm.TryExecute(def, _actor, MakeCtx()));

            bool ok = _tm.CanExecute(def, _actor, out var reason);

            Assert.IsTrue(ok);
            Assert.IsNull(reason);
        }

        // --- TryExecute --------------------------------------------------

        [Test]
        public void TryExecute_HappyPath_SpendsOneRoll()
        {
            var def = MakeAction("attack.basic");

            bool ok = _tm.TryExecute(def, _actor, MakeCtx());

            Assert.IsTrue(ok);
            Assert.AreEqual(3, _energy.Current[_actor], "1 roll cobrado (4 -> 3).");
            Assert.AreEqual(1, _energy.SpendCallCount);
        }

        [Test]
        public void TryExecute_SameActionRepeated_SpendsEachTimeUntilPoolEmpty()
        {
            // El unico presupuesto del turno es el pool: 4 rolls = 4 ejecuciones.
            var def = MakeAction("attack.basic");

            for (int i = 0; i < 4; i++)
                Assert.IsTrue(_tm.TryExecute(def, _actor, MakeCtx()), $"ejecucion #{i + 1}");

            Assert.AreEqual(0, _energy.Current[_actor]);
            Assert.AreEqual(4, _energy.SpendCallCount);

            bool ok = _tm.TryExecute(def, _actor, MakeCtx()); // quinta — sin rolls.

            Assert.IsFalse(ok);
            Assert.AreEqual(4, _energy.SpendCallCount, "Sin rolls no debe intentar cobrar.");
        }

        [Test]
        public void TryExecute_EmptyPool_FalseNoMutation()
        {
            var def = MakeAction("attack.big");
            _energy.Current[_actor] = 0;

            bool ok = _tm.TryExecute(def, _actor, MakeCtx());

            Assert.IsFalse(ok);
            Assert.AreEqual(0, _energy.Current[_actor], "El pool no debe cambiar.");
        }

        [Test]
        public void TryExecute_EmptyEffect_PermitNoOp_Charges()
        {
            // Accion con EffectData vacia (Effects.Count = 0) — "permit no-op".
            // TurnManager cobra 1 roll y delega el dispatch del BackingAsset a otro sistema.
            var def = MakeAction("combo.full_house");
            Assert.AreEqual(0, def.Effect.Effects.Count);

            bool ok = _tm.TryExecute(def, _actor, MakeCtx());

            Assert.IsTrue(ok);
            Assert.AreEqual(3, _energy.Current[_actor]);
        }

        [Test]
        public void TryExecute_MovementCanRepeat_SpendsPerExecution()
        {
            var move = MakeAction("move");

            Assert.IsTrue(_tm.TryExecute(move, _actor, MakeCtx()));
            Assert.IsTrue(_tm.TryExecute(move, _actor, MakeCtx()));

            Assert.AreEqual(2, _energy.Current[_actor]);
        }

        // --- Multi-actor semantics ---------------------------------------

        [Test]
        public void MultiActor_PoolsAreIndependent()
        {
            // El gate es por pool del actor: agotar el de A no afecta a B.
            var actorB = Guid.NewGuid();
            _energy.Current[actorB] = 1;
            _energy.Current[_actor] = 1;

            var def = MakeAction("attack.basic");

            Assert.IsTrue(_tm.TryExecute(def, _actor, MakeCtx()));
            Assert.IsFalse(_tm.CanExecute(def, _actor, out _), "A se quedo sin rolls.");
            Assert.IsTrue(_tm.CanExecute(def, actorB, out _), "B conserva su pool.");
        }

        // --- Feedback gate (§10.9) --------------------------------------
        // Sostienen el diferido del daño al frame de impacto: el chain del héroe
        // corre en CombatHandoffService, que no es MonoBehaviour y no puede usar
        // WaitForFeedbackCompletion, así que encola su continuación acá.

        [Test]
        public void RunWhenFeedbackSettles_NoFeedbackInFlight_RunsSynchronously()
        {
            var ran = 0;

            _tm.RunWhenFeedbackSettles(() => ran++);

            Assert.AreEqual(1, ran,
                "Sin feedback en vuelo el caller espera comportamiento sincrónico — " +
                "es el flujo viejo, no debe cambiar.");
        }

        [Test]
        public void RunWhenFeedbackSettles_FeedbackInFlight_DefersUntilComplete()
        {
            var ran = 0;
            _tm.BeginFeedbackWait();

            _tm.RunWhenFeedbackSettles(() => ran++);
            Assert.AreEqual(0, ran, "El golpe todavía no conectó — la continuación no debe correr.");

            _tm.OnFeedbackComplete();

            Assert.AreEqual(1, ran);
        }

        [Test]
        public void RunWhenFeedbackSettles_NestedWaits_RunsOnlyAfterTheLastCompletes()
        {
            var ran = 0;
            _tm.BeginFeedbackWait();
            _tm.BeginFeedbackWait();
            _tm.RunWhenFeedbackSettles(() => ran++);

            _tm.OnFeedbackComplete();
            Assert.AreEqual(0, ran, "Queda un feedback en vuelo — todavía no.");

            _tm.OnFeedbackComplete();
            Assert.AreEqual(1, ran);
        }

        [Test]
        public void RunWhenFeedbackSettles_ContinuationQueuingAnother_DoesNotLoseIt()
        {
            // El caso real: la fase 1 del chain encola su propia continuación mientras
            // corre la de la fase 0. Si el flush no copia antes de invocar, se pierde
            // o revienta por mutación en pleno recorrido.
            var order = new List<string>();
            _tm.BeginFeedbackWait();
            _tm.RunWhenFeedbackSettles(() =>
            {
                order.Add("first");
                _tm.RunWhenFeedbackSettles(() => order.Add("second"));
            });

            _tm.OnFeedbackComplete();

            Assert.AreEqual(new[] { "first", "second" }, order.ToArray());
        }

        [Test]
        public void RunWhenFeedbackSettles_ThrowingContinuation_DoesNotBlockTheRest()
        {
            var ran = 0;
            _tm.BeginFeedbackWait();
            _tm.RunWhenFeedbackSettles(() => throw new InvalidOperationException("boom"));
            _tm.RunWhenFeedbackSettles(() => ran++);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                @"\[TurnManager\] Feedback continuation falló"));
            _tm.OnFeedbackComplete();

            Assert.AreEqual(1, ran, "Una continuación rota no debe dejar el turno colgado.");
        }

        [Test]
        public void RunWhenFeedbackSettles_NullContinuation_IsSafeNoOp()
        {
            Assert.DoesNotThrow(() => _tm.RunWhenFeedbackSettles(null));
        }
    }
}
