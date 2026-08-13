using Patterns.Save;

namespace Rollgeon.Patterns.Save.Tests
{
    /// <summary>ISaveable configurable que registra las llamadas a RestoreState.</summary>
    public sealed class FakeSaveable : ISaveable
    {
        private readonly string _key;

        /// <summary>Estado que devuelve <see cref="CaptureState"/>; se pisa en Restore.</summary>
        public object State;

        public int RestoreCalls;
        public object LastRestored;

        public FakeSaveable(string key, object state = null)
        {
            _key = key;
            State = state;
        }

        public string SaveKey => _key;

        public object CaptureState() => State;

        public void RestoreState(object state)
        {
            RestoreCalls++;
            LastRestored = state;
            State = state;
        }
    }
}
