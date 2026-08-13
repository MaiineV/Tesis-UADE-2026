namespace Rollgeon.Achievements
{
    /// <summary>
    /// Fachada mínima sobre la plataforma Steam (Feature#0019). La implementación
    /// real vive en el assembly <c>Rollgeon.Steam</c> (Steamworks.NET); este contrato
    /// existe para que el resto del juego — <see cref="AchievementService"/>, DevConsole —
    /// no dependa del package ni de sus DLLs nativas.
    /// <para>
    /// Si Steam no está disponible (cliente cerrado, DLL ausente) el servicio se
    /// registra igual con <see cref="Available"/> en <c>false</c> y todas las
    /// operaciones devuelven <c>false</c> sin lanzar.
    /// </para>
    /// </summary>
    public interface ISteamService
    {
        /// <summary>SteamAPI inicializado y operativo para esta sesión.</summary>
        bool Available { get; }

        /// <summary>Persona name de la cuenta logueada, o <c>null</c> si no disponible.</summary>
        string PlayerName { get; }

        /// <summary>
        /// Marca el logro (API name del partner site) y persiste con StoreStats —
        /// eso es lo que dispara el toast del cliente.
        /// </summary>
        bool UnlockAchievement(string apiName);

        /// <summary>Revierte el logro (solo desarrollo/QA — no exponer en gameplay).</summary>
        bool ClearAchievement(string apiName);

        /// <summary>Estado actual del logro según el cliente de Steam.</summary>
        bool TryGetAchievementState(string apiName, out bool unlocked);
    }
}
