using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Survey.Tests
{
    /// <summary>Fakes compartidos por las fixtures de Survey (Feature#0074).</summary>
    internal sealed class InMemorySurveyStore : ISurveyStore
    {
        public readonly Dictionary<string, string> Pending = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Sent = new Dictionary<string, string>(StringComparer.Ordinal);

        public int WriteCount { get; private set; }

        /// <summary>Hook para assertar orden (ej. que el write pase antes del send).</summary>
        public Action<string> OnWrite;

        public bool ThrowOnWrite;

        public int PendingCount => Pending.Count;

        public IReadOnlyList<string> ListPending()
        {
            var keys = new List<string>(Pending.Keys);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        public void WritePending(string key, string json)
        {
            if (ThrowOnWrite) throw new InvalidOperationException("disco lleno (fake)");
            WriteCount++;
            Pending[key] = json;
            OnWrite?.Invoke(key);
        }

        public string ReadPending(string key) => Pending.TryGetValue(key, out var json) ? json : null;

        public void MarkSent(string key)
        {
            if (!Pending.TryGetValue(key, out var json)) return;
            Pending.Remove(key);
            Sent[key] = json;
        }
    }

    internal sealed class FakeSurveySink : ISurveySink
    {
        private readonly Queue<Action<bool>> _inFlight = new Queue<Action<bool>>();

        public bool IsConfigured { get; set; } = true;

        /// <summary>Resultado que devuelve cada envío cuando <see cref="AutoComplete"/> está activo.</summary>
        public bool NextResult = true;

        /// <summary><c>false</c> = el envío queda en vuelo hasta <see cref="CompleteNext"/>.</summary>
        public bool AutoComplete = true;

        public bool ThrowOnSend;

        public readonly List<string> SentBodies = new List<string>();

        /// <summary>Hook que corre ANTES de registrar el envío (para assertar estado del store).</summary>
        public Action<string> OnSend;

        public int InFlightCount => _inFlight.Count;

        public void Send(string wireJson, Action<bool> onDone)
        {
            if (ThrowOnSend) throw new InvalidOperationException("sink roto (fake)");
            OnSend?.Invoke(wireJson);
            SentBodies.Add(wireJson);

            if (AutoComplete)
            {
                onDone?.Invoke(NextResult);
            }
            else
            {
                _inFlight.Enqueue(onDone);
            }
        }

        public void CompleteNext(bool ok)
        {
            var done = _inFlight.Dequeue();
            done?.Invoke(ok);
        }
    }

    internal sealed class FakeSurveyTransport : ISurveyTransport
    {
        public SurveyPostResult NextResult = new SurveyPostResult(true, 200, "{\"ok\":true}", null);
        public readonly List<(string Url, string Body, int Timeout)> Posts = new List<(string, string, int)>();

        public void Post(string url, string body, int timeoutSeconds, Action<SurveyPostResult> onDone)
        {
            Posts.Add((url, body, timeoutSeconds));
            onDone?.Invoke(NextResult);
        }
    }

    internal static class SurveyTestConfig
    {
        public static SurveyConfigSO Make(
            bool enabled = true,
            int triggerFloor = 0,
            string endpoint = "https://script.google.com/macros/s/fake/exec",
            string eventId = "test-event",
            string secret = null,
            int questionCount = 1)
        {
            var config = ScriptableObject.CreateInstance<SurveyConfigSO>();
            config.Enabled = enabled;
            config.EventId = eventId;
            config.TriggerFloorIndex = triggerFloor;
            config.EndpointUrl = endpoint;
            config.SharedSecret = secret;
            config.Questions = new List<SurveyQuestion>();
            for (int i = 0; i < questionCount; i++)
            {
                config.Questions.Add(new SurveyQuestion
                {
                    Id = "q" + i,
                    Type = SurveyQuestionType.Rating1to5,
                    TextEs = "Pregunta " + i,
                });
            }
            return config;
        }

        public static SurveyResponse MakeResponse(string id = null, bool raffle = false, string email = "")
        {
            return new SurveyResponse
            {
                response_id = id,
                run_id = "run-1",
                floor_index = 0,
                hero_id = "hero.test",
                locale = "es",
                raffle_opt_in = raffle,
                email = email,
                answers = new List<SurveyAnswer> { new SurveyAnswer("q0", "5") },
            };
        }
    }
}
