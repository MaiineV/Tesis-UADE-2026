using System;
using System.Collections.Generic;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.Run;

namespace Rollgeon.Analytics.Tests
{
    /// <summary>Fakes compartidos por las fixtures de Analytics (Feature#0029).</summary>
    internal sealed class FakeAnalyticsSink : IAnalyticsSink
    {
        public bool Ready { get; set; } = true;

        public readonly List<(string Name, Dictionary<string, object> Params)> Sent =
            new List<(string, Dictionary<string, object>)>();

        public int FlushCount { get; private set; }
        public int DroppedEvents { get; private set; }

        public void Send(string eventName, Dictionary<string, object> parameters)
        {
            // Mismo contrato que el sink real: sin Ready se dropea silencioso.
            if (!Ready)
            {
                DroppedEvents++;
                return;
            }
            Sent.Add((eventName, parameters));
        }

        public void Flush() => FlushCount++;

        public Dictionary<string, object> Last(string eventName)
        {
            for (int i = Sent.Count - 1; i >= 0; i--)
            {
                if (Sent[i].Name == eventName) return Sent[i].Params;
            }
            return null;
        }

        public int CountOf(string eventName)
        {
            int count = 0;
            foreach (var entry in Sent)
            {
                if (entry.Name == eventName) count++;
            }
            return count;
        }
    }

    internal sealed class FakeConsentService : IAnalyticsConsentService
    {
        public bool HasDecision { get; set; }
        public bool IsGranted { get; set; }
        public string PrivacyUrl => "https://fake.test/privacy";

        public void SetConsent(bool granted)
        {
            HasDecision = true;
            IsGranted = granted;
        }

        public bool TryRequestDataDeletion() => false;
    }

    internal sealed class FakeRunContextService : IRunContextService
    {
        public Guid RunId { get; set; }
        public int FloorIndex { get; set; }
        public ClassHeroSO SelectedHero { get; set; }
        public bool IsRunActive { get; set; } = true;

        public void AdvanceFloor() => FloorIndex++;
    }

#pragma warning disable 67 // eventos de la interfaz sin uso en el fake
    internal sealed class FakePlayerService : IPlayerService
    {
        public Guid PlayerGuid { get; set; }
        public Guid RunId { get; set; }
        public ClassHeroSO CurrentHero { get; set; }
        public DiceBagSO DiceBag => null;

        public event Action<ClassHeroSO> OnPlayerSet;
        public event Action OnPlayerCleared;

        public void SetPlayer(ClassHeroSO hero, Guid runId)
        {
            CurrentHero = hero;
            RunId = runId;
        }

        public void SetDiceBag(DiceBagSO bag) { }

        public void ClearPlayer() => PlayerGuid = Guid.Empty;
    }
#pragma warning restore 67
}
