using System;

namespace Rollgeon.Combat.Status
{
    /// <summary>
    /// Estado "recién teletransportado" post-portal: mientras dura, los portales tratan a la
    /// entidad como si fueran celdas comunes (no truncan el path ni teletransportan).
    /// Lo aplica <c>SpecialTileService</c> al resolver un teleport y lo consulta el mismo
    /// servicio como gate; la UI lo muestra vía <c>TeleportCooldownStatusProvider</c>.
    /// </summary>
    public interface ITeleportCooldownService
    {
        /// <summary>Refresh, no stack: toma max(restante, nuevo) — criterio Veneno/Stun.</summary>
        void Apply(Guid entity, int turns);

        bool IsOnCooldown(Guid entity);

        /// <summary>Turnos restantes, 0 si no hay cooldown.</summary>
        int GetTurns(Guid entity);

        /// <summary>Limpia y emite <c>OnTeleportCooldownExpired</c> si había cooldown.</summary>
        void Clear(Guid entity);

        /// <summary>Teardown silencioso (sin eventos por entidad).</summary>
        void ClearAll();
    }
}
