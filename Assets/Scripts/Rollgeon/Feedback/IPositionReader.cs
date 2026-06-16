using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Info que el position resolver pasa al reader — solo el player que "posee"
    /// el request (para patrones como "centro del HUD del jugador X").
    /// </summary>
    public struct PositionReadInfo
    {
        public FeedbackPlayer Player;
    }

    /// <summary>
    /// Delegación pluggable de resolución de posición para <see cref="SpawnPosition.FromReader"/>.
    /// Típicamente un <c>ScriptableObject</c> con lógica custom (ej. cámara + offset, HUD anchor).
    /// TECHNICAL.md §10.6.
    /// </summary>
    public interface IPositionReader
    {
        Vector3 Read(PositionReadInfo info);
    }
}
