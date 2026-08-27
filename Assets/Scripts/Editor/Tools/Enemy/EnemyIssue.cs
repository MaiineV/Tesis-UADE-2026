using UnityEditor;

namespace Rollgeon.Editor.Tools.Enemy
{
    public enum EnemyIssueSeverity { Info, Warning, Error }

    /// <summary>Un problema de la ficha, con la sección del panel donde se arregla.</summary>
    public sealed class EnemyIssue
    {
        public readonly EnemyIssueSeverity Severity;
        public readonly string Section;
        public readonly string Message;

        public EnemyIssue(EnemyIssueSeverity severity, string section, string message)
        {
            Severity = severity; Section = section; Message = message;
        }

        public static MessageType ToMessageType(EnemyIssueSeverity severity)
        {
            switch (severity)
            {
                case EnemyIssueSeverity.Error:   return MessageType.Error;
                case EnemyIssueSeverity.Warning: return MessageType.Warning;
                default:                         return MessageType.Info;
            }
        }

        public override string ToString() => $"[{Section}] {Message}";
    }
}
