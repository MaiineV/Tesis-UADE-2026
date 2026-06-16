// Foundation#0002_FSM — EditMode tests.
// Cubre los 7 escenarios del DoD (plan §9):
//  1) Happy-path A → B → C por inputs.
//  2) CheckInput false → estado invariante, no dispara eventos.
//  3) Enter/Exit se llaman exactamente una vez, en orden Exit(old) → Enter(new).
//  4) Reentrancy: Enter de B llama SendInput que lleva a C — queue+drain, sin overflow.
//  5) ForceState salta CheckInput y emite eventos.
//  6) Stop llama Exit del current.
//  7) Builder declarativo: .From(A).On(x).To(B) funciona sin overridear CheckInput.
//
// Pure C#, zero UnityEngine deps en core; tests usan sólo NUnit.

using System.Collections.Generic;
using NUnit.Framework;
using Patterns.FSM;

namespace Patterns.FSM.Tests
{
    // --- Fixtures compartidos ---

    internal enum TestInput
    {
        None = 0,
        Go,
        Next,
        Back,
        Force,
    }

    /// <summary>Contexto compartido: registra calls ordenados para aserciones.</summary>
    internal sealed class TestContext
    {
        public readonly List<string> Log = new List<string>();
        public bool GuardAllowed = true;
    }

    internal class RecordingState : BaseState<TestContext, TestInput>
    {
        private readonly string _label;
        public int EnterCount;
        public int ExitCount;
        public TestInput LastEnterInput;
        public TestInput LastExitInput;

        public RecordingState(TestContext ctx, string label) : base(ctx) { _label = label; }
        public override string Name => _label;

        public override void Enter(TestInput input)
        {
            EnterCount++;
            LastEnterInput = input;
            Context.Log.Add($"Enter({_label},{input})");
        }

        public override void Exit(TestInput input)
        {
            ExitCount++;
            LastExitInput = input;
            Context.Log.Add($"Exit({_label},{input})");
        }
    }

    /// <summary>Estado con una transición override-based simple.</summary>
    internal sealed class SwitchOnState : RecordingState
    {
        private readonly TestInput _on;
        private readonly BaseState<TestContext, TestInput> _target;

        public SwitchOnState(TestContext ctx, string label,
                             TestInput on, BaseState<TestContext, TestInput> target)
            : base(ctx, label)
        {
            _on = on;
            _target = target;
        }

        public override bool CheckInput(TestInput input, out BaseState<TestContext, TestInput> next)
        {
            if (input == _on)
            {
                next = _target;
                return true;
            }
            next = null;
            return false;
        }
    }

    /// <summary>Estado que, al entrar, dispara SendInput (prueba reentrancy).</summary>
    internal sealed class ReentrantOnEnterState : RecordingState
    {
        private readonly TestInput _sendOnEnter;
        private readonly TestInput _bridgeInput;
        private readonly BaseState<TestContext, TestInput> _bridgeTarget;
        private StateMachine<TestContext, TestInput> _sm;

        public ReentrantOnEnterState(TestContext ctx, string label,
                                     TestInput sendOnEnter,
                                     TestInput bridgeInput,
                                     BaseState<TestContext, TestInput> bridgeTarget)
            : base(ctx, label)
        {
            _sendOnEnter = sendOnEnter;
            _bridgeInput = bridgeInput;
            _bridgeTarget = bridgeTarget;
        }

        public void Bind(StateMachine<TestContext, TestInput> sm) => _sm = sm;

        public override void Enter(TestInput input)
        {
            base.Enter(input);
            // Pide la transición *desde dentro de Enter*. Debe encolarse y drenar.
            _sm.SendInput(_sendOnEnter);
        }

        public override bool CheckInput(TestInput input, out BaseState<TestContext, TestInput> next)
        {
            if (input == _bridgeInput)
            {
                next = _bridgeTarget;
                return true;
            }
            next = null;
            return false;
        }
    }

    // --- Tests ---

    [TestFixture]
    public class FSMTests
    {
        // 1) Happy-path A → B → C
        [Test]
        public void HappyPath_AToBToC_TransitionsInOrder()
        {
            var ctx = new TestContext();
            var c = new RecordingState(ctx, "C");
            var b = new SwitchOnState(ctx, "B", TestInput.Next, c);
            var a = new SwitchOnState(ctx, "A", TestInput.Go, b);

            var sm = new StateMachine<TestContext, TestInput>(ctx, a);
            var transitions = new List<string>();
            sm.OnTransition += (from, to, input) => transitions.Add($"{from.Name}->{to.Name}({input})");
            sm.Start();

            Assert.AreSame(a, sm.Current, "Start should set Current to initial.");
            Assert.IsTrue(sm.IsRunning);

            sm.SendInput(TestInput.Go);
            Assert.AreSame(b, sm.Current);

            sm.SendInput(TestInput.Next);
            Assert.AreSame(c, sm.Current);

            CollectionAssert.AreEqual(
                new[] { "A->B(Go)", "B->C(Next)" },
                transitions);
        }

        // 2) CheckInput false → no transición ni eventos.
        [Test]
        public void SendInput_WhenCheckInputReturnsFalse_NoTransitionAndNoEvents()
        {
            var ctx = new TestContext();
            var b = new RecordingState(ctx, "B");
            var a = new SwitchOnState(ctx, "A", TestInput.Go, b);

            var sm = new StateMachine<TestContext, TestInput>(ctx, a);
            int entered = 0, exited = 0, transitioned = 0;
            sm.OnStateEntered += _ => entered++;
            sm.OnStateExited += _ => exited++;
            sm.OnTransition += (_, __, ___) => transitioned++;
            sm.Start();

            // Start cuenta como un Enter. Reseteo los contadores post-Start.
            entered = 0;

            sm.SendInput(TestInput.Back); // input no mapeado
            sm.SendInput(TestInput.None);

            Assert.AreSame(a, sm.Current);
            Assert.AreEqual(0, entered);
            Assert.AreEqual(0, exited);
            Assert.AreEqual(0, transitioned);
            Assert.AreEqual(1, a.EnterCount);
            Assert.AreEqual(0, a.ExitCount);
        }

        // 3) Enter/Exit una vez en orden Exit(old) → Enter(new).
        [Test]
        public void Transition_CallsExitOldBeforeEnterNew_ExactlyOnce()
        {
            var ctx = new TestContext();
            var b = new RecordingState(ctx, "B");
            var a = new SwitchOnState(ctx, "A", TestInput.Go, b);

            var sm = new StateMachine<TestContext, TestInput>(ctx, a);
            sm.Start();

            Assert.AreEqual(1, a.EnterCount);
            Assert.AreEqual(0, a.ExitCount);

            sm.SendInput(TestInput.Go);

            Assert.AreEqual(1, a.ExitCount, "A.Exit should run exactly once on transition.");
            Assert.AreEqual(1, b.EnterCount, "B.Enter should run exactly once on transition.");
            Assert.AreEqual(0, b.ExitCount);

            // Verificamos el orden absoluto en el log.
            int idxAExit = ctx.Log.IndexOf("Exit(A,Go)");
            int idxBEnter = ctx.Log.IndexOf("Enter(B,Go)");
            Assert.Greater(idxAExit, -1, "Expected Exit(A,Go) logged.");
            Assert.Greater(idxBEnter, -1, "Expected Enter(B,Go) logged.");
            Assert.Less(idxAExit, idxBEnter, "Exit(old) must come before Enter(new).");
        }

        // 4) Reentrancy: Enter de B llama SendInput → va a C sin overflow, orden correcto.
        [Test]
        public void SendInput_IsReentrantSafe_QueueAndDrain()
        {
            var ctx = new TestContext();
            var c = new RecordingState(ctx, "C");
            var b = new ReentrantOnEnterState(ctx, "B",
                                              sendOnEnter: TestInput.Next,
                                              bridgeInput: TestInput.Next,
                                              bridgeTarget: c);
            var a = new SwitchOnState(ctx, "A", TestInput.Go, b);

            var sm = new StateMachine<TestContext, TestInput>(ctx, a);
            b.Bind(sm);

            var transitions = new List<string>();
            sm.OnTransition += (from, to, input) => transitions.Add($"{from.Name}->{to.Name}");

            sm.Start();

            Assert.DoesNotThrow(() => sm.SendInput(TestInput.Go),
                "Reentrant SendInput must not recurse into stack overflow.");

            Assert.AreSame(c, sm.Current, "Expected FSM to drain queued input and land on C.");
            Assert.AreEqual(1, a.ExitCount);
            Assert.AreEqual(1, b.EnterCount);
            Assert.AreEqual(1, b.ExitCount);
            Assert.AreEqual(1, c.EnterCount);

            CollectionAssert.AreEqual(new[] { "A->B", "B->C" }, transitions);

            // Sanity: el Log de ctx muestra el orden Exit(A)→Enter(B)→Exit(B)→Enter(C).
            int iExitA = ctx.Log.IndexOf("Exit(A,Go)");
            int iEnterB = ctx.Log.IndexOf("Enter(B,Go)");
            int iExitB = ctx.Log.IndexOf("Exit(B,Next)");
            int iEnterC = ctx.Log.IndexOf("Enter(C,Next)");
            Assert.Less(iExitA, iEnterB);
            Assert.Less(iEnterB, iExitB);
            Assert.Less(iExitB, iEnterC);
        }

        // 5) ForceState salta CheckInput y emite eventos.
        [Test]
        public void ForceState_BypassesCheckInput_AndFiresEvents()
        {
            var ctx = new TestContext();
            var b = new RecordingState(ctx, "B");
            // 'A' no mapea Force → Back; si forzáramos vía SendInput no transicionaría.
            var a = new SwitchOnState(ctx, "A", TestInput.Go, b);

            var sm = new StateMachine<TestContext, TestInput>(ctx, a);
            var entered = new List<string>();
            var exited = new List<string>();
            var transitioned = new List<string>();
            sm.OnStateEntered += s => entered.Add(s.Name);
            sm.OnStateExited += s => exited.Add(s.Name);
            sm.OnTransition += (from, to, input) => transitioned.Add($"{from.Name}->{to.Name}({input})");
            sm.Start();

            // Reset: el Start también dispara OnStateEntered una vez.
            entered.Clear();

            sm.SendInput(TestInput.Back); // no mapeado → no-op
            Assert.AreSame(a, sm.Current);

            sm.ForceState(b, TestInput.Force);

            Assert.AreSame(b, sm.Current);
            Assert.AreEqual(1, a.ExitCount);
            Assert.AreEqual(1, b.EnterCount);
            CollectionAssert.AreEqual(new[] { "A" }, exited);
            CollectionAssert.AreEqual(new[] { "B" }, entered);
            CollectionAssert.AreEqual(new[] { "A->B(Force)" }, transitioned);
        }

        // 6) Stop llama Exit del current.
        [Test]
        public void Stop_CallsExitOnCurrent_AndMarksNotRunning()
        {
            var ctx = new TestContext();
            var b = new RecordingState(ctx, "B");
            var a = new SwitchOnState(ctx, "A", TestInput.Go, b);

            var sm = new StateMachine<TestContext, TestInput>(ctx, a);
            sm.Start();

            int exitedEvents = 0;
            sm.OnStateExited += _ => exitedEvents++;

            sm.Stop();

            Assert.IsFalse(sm.IsRunning);
            Assert.AreEqual(1, a.ExitCount, "Stop() must call Exit(default) on the current state.");
            Assert.AreEqual(TestInput.None, a.LastExitInput,
                "Stop() passes default(TInput) to Exit; default(TestInput) is None.");
            Assert.AreEqual(0, exitedEvents,
                "Stop() does NOT fire OnStateExited (shutdown, not transition — plan §5.4).");

            // Tras Stop, SendInput/Update/LateUpdate/FixedUpdate son no-ops.
            sm.SendInput(TestInput.Go);
            sm.Update();
            sm.LateUpdate();
            sm.FixedUpdate();
            Assert.AreEqual(1, a.ExitCount); // sin side-effects adicionales
        }

        // 7) Builder declarativo: .From(A).On(x).To(B) funciona sin overridear CheckInput.
        [Test]
        public void Builder_Declarative_From_On_To_WorksWithoutOverride()
        {
            var ctx = new TestContext();
            var a = new RecordingState(ctx, "A");
            var b = new RecordingState(ctx, "B");
            var c = new RecordingState(ctx, "C");

            var sm = new StateMachineBuilder<TestContext, TestInput>()
                .From(a).On(TestInput.Go).To(b)
                .From(b).On(TestInput.Next).To(c)
                .Build(ctx, initial: a);

            sm.Start();
            sm.SendInput(TestInput.Go);
            Assert.AreSame(b, sm.Current);

            sm.SendInput(TestInput.Next);
            Assert.AreSame(c, sm.Current);

            Assert.AreEqual(1, a.EnterCount);
            Assert.AreEqual(1, a.ExitCount);
            Assert.AreEqual(1, b.EnterCount);
            Assert.AreEqual(1, b.ExitCount);
            Assert.AreEqual(1, c.EnterCount);
        }

        // Extra: builder con guard — confirma que If() filtra correctamente.
        [Test]
        public void Builder_WithGuard_BlocksTransitionWhenFalse()
        {
            var ctx = new TestContext();
            ctx.GuardAllowed = false;

            var a = new RecordingState(ctx, "A");
            var b = new RecordingState(ctx, "B");

            var sm = new StateMachineBuilder<TestContext, TestInput>()
                .From(a).On(TestInput.Go).If(c => c.GuardAllowed).To(b)
                .Build(ctx, initial: a);

            sm.Start();
            sm.SendInput(TestInput.Go);
            Assert.AreSame(a, sm.Current, "Guard=false must block transition.");

            ctx.GuardAllowed = true;
            sm.SendInput(TestInput.Go);
            Assert.AreSame(b, sm.Current, "Guard=true must allow transition.");
        }

        // Extra: Update/LateUpdate/FixedUpdate delegan al estado actual, y son no-op pre-Start.
        [Test]
        public void Ticks_DelegateToCurrent_AndAreNoopBeforeStart()
        {
            var ctx = new TestContext();
            int upd = 0, late = 0, fix_ = 0;

            var a = new TickCountingState(ctx, "A", () => upd++, () => late++, () => fix_++);
            var sm = new StateMachine<TestContext, TestInput>(ctx, a);

            // Pre-Start: no-op.
            sm.Update();
            sm.LateUpdate();
            sm.FixedUpdate();
            Assert.AreEqual(0, upd);
            Assert.AreEqual(0, late);
            Assert.AreEqual(0, fix_);

            sm.Start();
            sm.Update();
            sm.LateUpdate();
            sm.FixedUpdate();
            Assert.AreEqual(1, upd);
            Assert.AreEqual(1, late);
            Assert.AreEqual(1, fix_);

            sm.Stop();
            sm.Update();
            sm.LateUpdate();
            sm.FixedUpdate();
            Assert.AreEqual(1, upd, "Ticks after Stop must be no-op.");
        }

        // Helper state para el test de ticks.
        private sealed class TickCountingState : BaseState<TestContext, TestInput>
        {
            private readonly string _label;
            private readonly System.Action _onUpdate;
            private readonly System.Action _onLateUpdate;
            private readonly System.Action _onFixedUpdate;

            public TickCountingState(TestContext ctx, string label,
                                     System.Action onUpdate,
                                     System.Action onLateUpdate,
                                     System.Action onFixedUpdate) : base(ctx)
            {
                _label = label;
                _onUpdate = onUpdate;
                _onLateUpdate = onLateUpdate;
                _onFixedUpdate = onFixedUpdate;
            }
            public override string Name => _label;
            public override void Update() => _onUpdate();
            public override void LateUpdate() => _onLateUpdate();
            public override void FixedUpdate() => _onFixedUpdate();
        }
    }
}
