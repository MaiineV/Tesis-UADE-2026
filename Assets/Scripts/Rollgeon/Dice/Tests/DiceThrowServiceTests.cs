using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.DiceBlock;
using Rollgeon.Dice.Throw;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Dice.Tests
{
    /// <summary>Roller determinístico: RollAll devuelve la secuencia fija; Reroll
    /// respeta keep contra previousResult y usa la secuencia en el resto.</summary>
    internal sealed class StubDiceRoller : IDiceRoller
    {
        public int[] NextFaces = { 1, 2, 3, 4, 5 };

        public int[] RollAll(DiceBagSO bag) => (int[])NextFaces.Clone();

        public int[] Reroll(DiceBagSO bag, int[] previousResult, bool[] keep)
        {
            var result = (int[])NextFaces.Clone();
            if (previousResult == null || keep == null) return result;
            for (int i = 0; i < result.Length && i < previousResult.Length; i++)
                if (i < keep.Length && keep[i]) result[i] = previousResult[i];
            return result;
        }
    }

    internal sealed class StubDiceBlockService : IDiceBlockService
    {
        private readonly HashSet<int> _blocked = new HashSet<int>();
        public void Block(int index) => _blocked.Add(index);
        public void Unblock(int index) => _blocked.Remove(index);
        public bool IsBlocked(int index) => _blocked.Contains(index);
        public IReadOnlyCollection<int> BlockedIndices => _blocked;
        public void Clear() => _blocked.Clear();
    }

    [TestFixture]
    public class DiceThrowServiceTests
    {
        private static readonly Guid Player = Guid.NewGuid();

        private StubDiceRoller _roller;
        private DiceBagSO _bag;
        private DiceThrowSettingsSO _settings;
        private readonly List<Object> _assets = new List<Object>();

        private int _diceRolledCount;
        private IReadOnlyList<int> _lastEventFaces;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _roller = new StubDiceRoller();
            ServiceLocator.AddService<IDiceRoller>(_roller, ServiceScope.Global);

            _bag = ScriptableObject.CreateInstance<DiceBagSO>();
            _bag.Dice = new List<DiceType>
            {
                DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6,
            };
            _assets.Add(_bag);

            _diceRolledCount = 0;
            _lastEventFaces = null;
            EventManager.Subscribe(EventName.OnDiceRolled, HandleDiceRolled);
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.UnSubscribe(EventName.OnDiceRolled, HandleDiceRolled);
            foreach (var a in _assets)
                if (a != null) Object.DestroyImmediate(a);
            _assets.Clear();
            ServiceLocator.Clear();
        }

        private void HandleDiceRolled(params object[] args)
        {
            _diceRolledCount++;
            _lastEventFaces = args.Length > 1 ? args[1] as IReadOnlyList<int> : null;
        }

        private DiceThrowService MakeService(DiceThrowMode mode)
        {
            _settings = ScriptableObject.CreateInstance<DiceThrowSettingsSO>();
            _settings.DefaultMode = mode;
            _assets.Add(_settings);
            return new DiceThrowService(_settings);
        }

        // Pump manual: agarrar todo lo tirable, soltar, asentar todo.
        private static void ThrowEverything(DiceThrowService svc)
        {
            var mask = svc.ThrownMask;
            for (int i = 0; i < mask.Count; i++)
                if (mask[i]) svc.TryGrab(i);
            var released = svc.ReleaseGrabbed();
            foreach (var i in released) svc.NotifyDieSettled(i);
        }

        // ---- Gate instantáneo -------------------------------------------------

        [Test]
        public void RequestRoll_ClassicMode_ResolvesSynchronously_CallbackThenEvent()
        {
            var svc = MakeService(DiceThrowMode.Classic);
            int[] received = null;
            int callbackOrder = -1, eventOrderAtCallback = -1;

            svc.RequestRoll(Player, _bag, faces =>
            {
                received = faces;
                callbackOrder = 0;
                eventOrderAtCallback = _diceRolledCount; // debe ser 0: callback ANTES del evento
            });

            Assert.AreEqual(0, callbackOrder, "callback invocado");
            Assert.AreEqual(0, eventOrderAtCallback, "orden legacy: callback primero, evento después");
            Assert.AreEqual(1, _diceRolledCount);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, received);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, _lastEventFaces);
            Assert.IsFalse(svc.IsBusy);
        }

        [Test]
        public void RequestRoll_ManualModeWithoutPresenter_StillResolvesInstantly()
        {
            var svc = MakeService(DiceThrowMode.TwoD);
            int[] received = null;

            svc.RequestRoll(Player, _bag, faces => received = faces);

            Assert.IsNotNull(received, "sin presenter no puede diferir — anti soft-lock");
            Assert.AreEqual(1, _diceRolledCount);
            Assert.IsFalse(svc.IsBusy);
        }

        // ---- Sesión diferida ----------------------------------------------------

        [Test]
        public void RequestRoll_ManualWithPresenter_DefersRevealUntilCompleteReveal()
        {
            var svc = MakeService(DiceThrowMode.TwoD);
            svc.AttachPresenter(this);
            int[] received = null;

            svc.RequestRoll(Player, _bag, faces => received = faces);

            Assert.IsNull(received, "reveal diferido");
            Assert.AreEqual(0, _diceRolledCount);
            Assert.IsTrue(svc.IsBusy);
            Assert.AreEqual(DiceThrowPhase.Pending, svc.Phase);
            Assert.AreEqual(2, svc.PeekPendingFace(1), "cara precalculada visible para el presenter");

            ThrowEverything(svc);
            Assert.AreEqual(DiceThrowPhase.Settled, svc.Phase);
            Assert.AreEqual(0, _diceRolledCount, "el settle NO revela — falta el alineado");

            svc.CompleteReveal();

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, received);
            Assert.AreEqual(1, _diceRolledCount);
            Assert.IsFalse(svc.IsBusy);
            Assert.AreEqual(DiceThrowPhase.Idle, svc.Phase);
        }

        [Test]
        public void RequestReroll_KeptDice_AreExcludedFromThrow_AndKeepTheirFaces()
        {
            var svc = MakeService(DiceThrowMode.TwoD);
            svc.AttachPresenter(this);
            _roller.NextFaces = new[] { 6, 6, 6, 6, 6 };
            var previous = new[] { 1, 2, 3, 4, 5 };
            var keep = new[] { true, false, true, false, false };
            int[] received = null;

            svc.RequestReroll(Player, _bag, previous, keep, faces => received = faces);

            Assert.IsFalse(svc.ThrownMask[0]);
            Assert.IsTrue(svc.ThrownMask[1]);
            Assert.AreEqual(DieThrowState.Excluded, svc.GetDieState(0));
            Assert.AreEqual(DieThrowState.NotThrown, svc.GetDieState(1));

            ThrowEverything(svc);
            svc.CompleteReveal();

            CollectionAssert.AreEqual(new[] { 1, 6, 3, 6, 6 }, received,
                "kept conservan cara previa, el resto usa la nueva tirada");
        }

        [Test]
        public void RequestRoll_BlockedDice_AreExcludedFromThrow()
        {
            var blocks = new StubDiceBlockService();
            blocks.Block(1);
            blocks.Block(4);
            ServiceLocator.AddService<IDiceBlockService>(blocks, ServiceScope.Global);

            var svc = MakeService(DiceThrowMode.TwoD);
            svc.AttachPresenter(this);

            svc.RequestRoll(Player, _bag, _ => { });

            Assert.IsFalse(svc.ThrownMask[1]);
            Assert.IsFalse(svc.ThrownMask[4]);
            Assert.IsTrue(svc.ThrownMask[0]);
            Assert.AreEqual(DieThrowState.Excluded, svc.GetDieState(1));
        }

        // ---- Merge 3D -----------------------------------------------------------

        [Test]
        public void CompleteReveal_ThreeD_PhysicalFacesOverridePendingOnThrownD6()
        {
            var svc = MakeService(DiceThrowMode.ThreeD);
            svc.AttachPresenter(this);
            int[] received = null;

            svc.RequestRoll(Player, _bag, faces => received = faces);

            var mask = svc.ThrownMask;
            for (int i = 0; i < mask.Count; i++) svc.TryGrab(i);
            var released = svc.ReleaseGrabbed();
            foreach (var i in released) svc.NotifyDieSettled(i, physicalFace: 6);

            svc.CompleteReveal();

            CollectionAssert.AreEqual(new[] { 6, 6, 6, 6, 6 }, received,
                "en 3D la física dicta el resultado");
        }

        [Test]
        public void CompleteReveal_ThreeD_RiggedRoll_IgnoresPhysicalFaces()
        {
            var rig = new RiggedRollState();
            rig.SetNext(new[] { 3, 3, 3, 3, 3 });
            ServiceLocator.AddService<RiggedRollState>(rig, ServiceScope.Global);

            var svc = MakeService(DiceThrowMode.ThreeD);
            svc.AttachPresenter(this);
            _roller.NextFaces = new[] { 3, 3, 3, 3, 3 }; // el rig real actúa dentro del roller
            int[] received = null;

            svc.RequestRoll(Player, _bag, faces => received = faces);
            ThrowEverythingWithFace(svc, 6);
            svc.CompleteReveal();

            CollectionAssert.AreEqual(new[] { 3, 3, 3, 3, 3 }, received,
                "tirada riggeada: mandan las caras del roller, no la física");
        }

        [Test]
        public void CompleteReveal_ThreeD_NonD6Die_UsesPrecalculatedFace()
        {
            _bag.Dice[2] = DiceType.D8; // el cubo no representa un d8

            var svc = MakeService(DiceThrowMode.ThreeD);
            svc.AttachPresenter(this);
            int[] received = null;

            svc.RequestRoll(Player, _bag, faces => received = faces);
            ThrowEverythingWithFace(svc, 6);
            svc.CompleteReveal();

            Assert.AreEqual(3, received[2], "no-d6 conserva la cara precalculada");
            Assert.AreEqual(6, received[0], "los d6 sí toman la física");
        }

        private static void ThrowEverythingWithFace(DiceThrowService svc, int face)
        {
            var mask = svc.ThrownMask;
            for (int i = 0; i < mask.Count; i++)
                if (mask[i]) svc.TryGrab(i);
            var released = svc.ReleaseGrabbed();
            foreach (var i in released) svc.NotifyDieSettled(i, face);
        }

        // ---- Cancel / abort / guards --------------------------------------------

        [Test]
        public void CancelGrab_ReturnsIndices_AndFiresEvent_WithoutRevealing()
        {
            var svc = MakeService(DiceThrowMode.TwoD);
            svc.AttachPresenter(this);
            bool cancelled = false;
            svc.OnGrabCancelled += () => cancelled = true;

            svc.RequestRoll(Player, _bag, _ => { });
            svc.TryGrab(0);
            svc.TryGrab(1);
            var result = new List<int>(svc.CancelGrab());

            CollectionAssert.AreEquivalent(new[] { 0, 1 }, result);
            Assert.IsTrue(cancelled);
            Assert.IsTrue(svc.IsBusy, "el throw sigue pendiente — se puede re-agarrar");
            Assert.AreEqual(0, _diceRolledCount);
        }

        [Test]
        public void Abort_DropsSessionWithoutRevealing()
        {
            var svc = MakeService(DiceThrowMode.TwoD);
            svc.AttachPresenter(this);
            bool aborted = false;
            svc.OnSessionAborted += () => aborted = true;
            int[] received = null;

            svc.RequestRoll(Player, _bag, faces => received = faces);
            svc.Abort();

            Assert.IsTrue(aborted);
            Assert.IsNull(received);
            Assert.AreEqual(0, _diceRolledCount);
            Assert.IsFalse(svc.IsBusy);
        }

        [Test]
        public void DetachPresenter_MidSession_Aborts()
        {
            var svc = MakeService(DiceThrowMode.TwoD);
            svc.AttachPresenter(this);
            svc.RequestRoll(Player, _bag, _ => { });
            Assert.IsTrue(svc.IsBusy);

            svc.DetachPresenter(this);

            Assert.IsFalse(svc.IsBusy, "presenter muerto no puede completar — abort");
        }

        [Test]
        public void RequestRoll_WhileBusy_IsIgnored()
        {
            var svc = MakeService(DiceThrowMode.TwoD);
            svc.AttachPresenter(this);
            svc.RequestRoll(Player, _bag, _ => { });
            var maskBefore = svc.ThrownMask;

            svc.RequestRoll(Player, _bag, _ => { }); // warning + no-op

            Assert.AreSame(maskBefore, svc.ThrownMask, "la sesión original sigue intacta");
        }

        [Test]
        public void SetMode_WhileBusy_Fails()
        {
            var svc = MakeService(DiceThrowMode.TwoD);
            svc.AttachPresenter(this);
            svc.RequestRoll(Player, _bag, _ => { });

            Assert.IsFalse(svc.SetMode(DiceThrowMode.Classic));
            svc.Abort();
            Assert.IsTrue(svc.SetMode(DiceThrowMode.Classic));
        }

        [Test]
        public void TryGrab_AfterAllSettled_Fails()
        {
            var svc = MakeService(DiceThrowMode.TwoD);
            svc.AttachPresenter(this);
            svc.RequestRoll(Player, _bag, _ => { });
            ThrowEverything(svc);

            Assert.IsFalse(svc.TryGrab(0), "con todo asentado el presenter está alineando");
        }
    }
}
