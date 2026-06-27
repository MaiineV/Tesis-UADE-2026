using System.Collections.Generic;

namespace Rollgeon.DevConsole.Core
{
    /// <summary>Sink en memoria — usado por tests y como buffer para la vista on-screen.</summary>
    public sealed class BufferLogSink : ILogSink
    {
        private readonly List<string> _lines = new List<string>();

        public IReadOnlyList<string> Lines => _lines;

        public void Info(string message) => _lines.Add(message ?? string.Empty);
        public void Warn(string message) => _lines.Add("[warn] " + (message ?? string.Empty));
        public void Error(string message) => _lines.Add("[error] " + (message ?? string.Empty));

        public void Clear() => _lines.Clear();
    }
}
