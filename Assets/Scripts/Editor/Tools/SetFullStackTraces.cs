using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Sube el stack trace de TODOS los tipos de log a Full para builds de diagnóstico
    /// (bug hunting). Full = managed + nativo; pesa en runtime — revertir a ScriptOnly
    /// para builds de release con el segundo MenuItem.
    /// </summary>
    public static class SetFullStackTraces
    {
        [MenuItem("Rollgeon/Tools/Stack Traces - Full (dev build)")]
        public static void SetFull() => Apply(StackTraceLogType.Full, "Full");

        [MenuItem("Rollgeon/Tools/Stack Traces - ScriptOnly (default)")]
        public static void SetScriptOnly() => Apply(StackTraceLogType.ScriptOnly, "ScriptOnly");

        private static void Apply(StackTraceLogType type, string label)
        {
            PlayerSettings.SetStackTraceLogType(LogType.Log, type);
            PlayerSettings.SetStackTraceLogType(LogType.Warning, type);
            PlayerSettings.SetStackTraceLogType(LogType.Assert, type);
            PlayerSettings.SetStackTraceLogType(LogType.Error, type);
            PlayerSettings.SetStackTraceLogType(LogType.Exception, type);
            AssetDatabase.SaveAssets();
            Debug.Log($"[StackTraces] Todos los LogType en {label}.");
        }
    }
}
