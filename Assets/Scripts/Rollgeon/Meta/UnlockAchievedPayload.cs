namespace Rollgeon.Meta
{
    /// <summary>
    /// Payload tipado para "desbloqueo conseguido" (#164). Canalizado únicamente vía
    /// <c>TypedEvent&lt;UnlockAchievedPayload&gt;</c> — no existe entry legacy en
    /// <c>EventName</c> (regla de canal único §1.2.1). Lo consume la notificación
    /// no intrusiva (<c>UnlockToastView</c>) y cualquier UI de desbloqueos.
    /// </summary>
    public struct UnlockAchievedPayload
    {
        /// <summary><c>UnlockId</c> de la definición que se cumplió.</summary>
        public string UnlockId;

        /// <summary>Categoría del elemento desbloqueado.</summary>
        public UnlockableCategory Category;

        /// <summary>Id del elemento desbloqueado (según categoría).</summary>
        public string TargetId;

        /// <summary>Nombre legible para UI.</summary>
        public string DisplayName;

        /// <summary><c>true</c> si se desbloqueó durante la run (mid-run); <c>false</c> al cierre.</summary>
        public bool DuringRun;
    }
}
