using UnityEngine;

namespace Rollgeon.Patterns.Bootstrap
{
    /// <summary>
    /// Helper interno de logging con prefijo uniforme <c>[Bootstrap]</c>. Permite filtrar
    /// la consola de Unity facilmente durante el smoke-test del instructivo §8.8.
    /// </summary>
    internal static class BootstrapLog
    {
        private const string Prefix = "[Bootstrap] ";

        public static void Info(string msg) => Debug.Log(Prefix + msg);
        public static void Warn(string msg) => Debug.LogWarning(Prefix + msg);
        public static void Error(string msg) => Debug.LogError(Prefix + msg);

        public static void InfoContext(string msg, Object context) => Debug.Log(Prefix + msg, context);
        public static void WarnContext(string msg, Object context) => Debug.LogWarning(Prefix + msg, context);
        public static void ErrorContext(string msg, Object context) => Debug.LogError(Prefix + msg, context);
    }
}
