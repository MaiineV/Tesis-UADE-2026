using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.UI.HUD.Breakdown;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Tests de <see cref="BreakdownSequencePlayer"/> con un stage fake sincrónico:
    /// orden de reproducción, skip en dos etapas, mitigación condicional y abort.
    /// </summary>
    [TestFixture]
    public class BreakdownSequencePlayerTests
    {
        private sealed class RecordingStage : IBreakdownStage
        {
            public readonly List<string> Calls = new List<string>();
            public Action PendingDone; // si se setea, los pasos NO completan solos

            private void Step(string name, Action onDone)
            {
                Calls.Add(name);
                if (PendingDone == null) onDone();
                else PendingDone = onDone;
            }

            public void ShowCounters(int n, float m) => Calls.Add($"show:{n}x{m}");
            public void PlayPlayerBase(BreakdownStep s, Action d) => Step("player:" + s.Amount, d);
            public void PlayDie(BreakdownStep s, Action d) => Step($"die{s.BagSlot}:{s.Amount}", d);
            public void PlayDieProc(BreakdownStep s, Action d) => Step($"proc{s.BagSlot}:{s.Amount}", d);
            public void PlayGlobalMod(BreakdownStep s, Action d) => Step("global:" + s.Amount, d);
            public void PlayFinalClash(int total, Action d) => Step("clash:" + total, d);
            public void PlayMitigation(int total, Action d) => Step("mitigation:" + total, d);
            public void ForceFinalState(int n, float m) => Calls.Add($"force:{n}x{m}");
        }

        private static BreakdownScript Script()
        {
            var script = new BreakdownScript
            {
                InitialN = 5,
                InitialM = 1f,
                FinalN = 20,
                FinalM = 1f,
                FinalTotal = 20,
            };
            script.Steps.Add(new BreakdownStep { Kind = BreakdownStepKind.PlayerBase, Amount = 3 });
            script.Steps.Add(new BreakdownStep { Kind = BreakdownStepKind.Die, BagSlot = 0, Amount = 6 });
            script.Steps.Add(new BreakdownStep { Kind = BreakdownStepKind.DieProc, BagSlot = 0, Amount = 2 });
            script.Steps.Add(new BreakdownStep { Kind = BreakdownStepKind.GlobalMod, Amount = 4 });
            return script;
        }

        [Test]
        public void Play_RunsStepsInOrder_ThenClash_ThenFinishesOnce()
        {
            var stage = new RecordingStage();
            var player = new BreakdownSequencePlayer();
            int finished = 0;

            player.Play(Script(), stage, mitigatedTotal: null, () => finished++);

            CollectionAssert.AreEqual(new[]
            {
                "show:5x1", "player:3", "die0:6", "proc0:2", "global:4", "clash:20",
            }, stage.Calls);
            Assert.IsTrue(player.Done);
            Assert.AreEqual(1, finished);
        }

        [Test]
        public void Play_WithMitigatedTotal_PlaysMitigationAfterClash()
        {
            var stage = new RecordingStage();
            var player = new BreakdownSequencePlayer();

            player.Play(Script(), stage, mitigatedTotal: 14, () => { });

            Assert.AreEqual("mitigation:14", stage.Calls[stage.Calls.Count - 1]);
        }

        [Test]
        public void Play_MitigatedEqualsTotal_SkipsMitigation()
        {
            var stage = new RecordingStage();
            var player = new BreakdownSequencePlayer();

            player.Play(Script(), stage, mitigatedTotal: 20, () => { });

            Assert.IsFalse(stage.Calls.Contains("mitigation:20"));
            Assert.IsTrue(player.Done);
        }

        [Test]
        public void RequestSkip_Twice_JumpsToClashWithForcedFinals()
        {
            // Arrange — el primer paso queda pendiente (no completa solo)
            var stage = new RecordingStage { PendingDone = () => { } };
            var player = new BreakdownSequencePlayer();
            player.Play(Script(), stage, null, () => { });
            Assert.AreEqual(BreakdownSequencePlayer.SkipStage.None, player.Skip);

            // Act — dos clicks de skip; luego el paso pendiente completa
            player.RequestSkip();
            Assert.AreEqual(BreakdownSequencePlayer.SkipStage.Fast, player.Skip);
            player.RequestSkip();
            Assert.AreEqual(BreakdownSequencePlayer.SkipStage.Jump, player.Skip);
            var pending = stage.PendingDone;
            stage.PendingDone = null;
            pending();

            // Assert — saltó directo al choque con los finales forzados, sin pasos restantes
            Assert.Contains("force:20x1", stage.Calls);
            Assert.Contains("clash:20", stage.Calls);
            Assert.IsFalse(stage.Calls.Exists(c => c.StartsWith("die")),
                "El salto no reproduce los pasos que faltaban.");
            Assert.IsTrue(player.Done);
        }

        [Test]
        public void Abort_StopsWithoutFinishing()
        {
            var stage = new RecordingStage { PendingDone = () => { } };
            var player = new BreakdownSequencePlayer();
            int finished = 0;
            player.Play(Script(), stage, null, () => finished++);

            player.Abort();
            var pending = stage.PendingDone;
            stage.PendingDone = null;
            pending();

            Assert.IsFalse(player.Done);
            Assert.AreEqual(0, finished);
        }

        [Test]
        public void Play_EmptyScript_StillShowsCountersAndClash()
        {
            var stage = new RecordingStage();
            var player = new BreakdownSequencePlayer();
            var script = new BreakdownScript { InitialN = 10, InitialM = 1f, FinalN = 10, FinalM = 1f, FinalTotal = 10 };

            player.Play(script, stage, null, () => { });

            CollectionAssert.AreEqual(new[] { "show:10x1", "clash:10" }, stage.Calls);
            Assert.IsTrue(player.Done);
        }
    }
}
