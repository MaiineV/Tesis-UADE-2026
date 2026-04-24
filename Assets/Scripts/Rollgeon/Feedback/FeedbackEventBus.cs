using System.Collections.Generic;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Pub/sub <b>latched</b> por secuencia: un key publicado queda firado hasta
    /// que la secuencia termina. Subscribers late que consulten <see cref="HasFired"/>
    /// reciben el estado acumulado. TECHNICAL.md §10.8.1.
    /// </summary>
    public sealed class FeedbackEventBus
    {
        private readonly HashSet<string> _fired = new HashSet<string>();

        public void Publish(string key)
        {
            if (!string.IsNullOrEmpty(key)) _fired.Add(key);
        }

        public bool HasFired(string key) =>
            !string.IsNullOrEmpty(key) && _fired.Contains(key);

        public void Clear() => _fired.Clear();
    }
}
