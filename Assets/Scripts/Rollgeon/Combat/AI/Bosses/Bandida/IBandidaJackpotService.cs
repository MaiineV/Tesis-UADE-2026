using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    /// <summary>Posición de un rodillo en la fila. El del medio es el que se traba en Fase 2 (HOLD).</summary>
    public enum ReelSide
    {
        Left = 0,
        Middle = 1,
        Right = 2,
    }

    /// <summary>La ranura es fija (la fila queda "alineada" toda la pelea); lo que cambia es qué rodillo la ocupa.</summary>
    public sealed class ReelSlot
    {
        public ReelSide Side;

        public GridCoord Coord;

        /// <summary>Rodillo vivo en la ranura, o <see cref="Guid.Empty"/> si está roto.</summary>
        public Guid ReelGuid;

        /// <summary>Turnos del jefe que faltan para reponer el rodillo. 0 = reponer en el próximo tick.</summary>
        public int TurnsUntilRespawn;

        /// <summary>HOLD (Fase 2): su rodillo no cancela la cuenta y se vuelve inrompible (ver <see cref="IBandidaJackpotService.LockedReelHp"/>).</summary>
        public bool Locked;

        public bool IsAlive => ReelGuid != Guid.Empty;
    }

    /// <summary>
    /// La cancelación es un hook de daño, no un chequeo de HP: se cancela <b>dañando</b> cualquier
    /// rodillo, no rompiéndolo. El servicio se suscribe a
    /// <c>TypedEvent&lt;DamageResolvedPayload&gt;</c> y cancela al primer punto de daño que entra.
    /// </summary>
    public interface IBandidaJackpotService
    {
        /// <summary>Jefe al que está atado el estado actual. <see cref="Guid.Empty"/> = sin combate.</summary>
        Guid BossGuid { get; }

        /// <summary>Valor actual del número gigante. Congelado mientras <see cref="IsCounting"/> sea false.</summary>
        int Countdown { get; }

        bool IsCounting { get; }

        /// <summary>Turnos del jefe que tarda un rodillo roto en volver (2, y 1 en Fase 2).</summary>
        int RespawnDelayTurns { get; }

        /// <summary>HP con el que se repone un rodillo trabado por HOLD — pool inagotable en la práctica.</summary>
        int LockedReelHp { get; }
        /// <summary>Las tres ranuras, en orden de fila. Vacío hasta que el jefe arma la fila.</summary>
        IReadOnlyList<ReelSlot> Slots { get; }

        /// <summary>
        /// Ata el estado a <paramref name="bossGuid"/>. Cambiar de jefe (pelea nueva) resetea todo:
        /// el servicio es Global y no puede arrastrar ranuras de un combate anterior.
        /// </summary>
        void BindBoss(Guid bossGuid);

        /// <summary>Fija el delay de reposición solo la primera vez (valor autorado del nodo).</summary>
        void InitRespawnDelay(int turns);

        /// <summary>Pisa el delay de reposición (Fase 2 lo baja a 1). Siempre gana sobre el autorado.</summary>
        void SetRespawnDelay(int turns);

        void SetSlots(IReadOnlyList<GridCoord> coords);

        void AttachReel(int index, Guid reelGuid);
        /// <summary>Marca la ranura como rota y arranca su cuenta de reposición en <see cref="RespawnDelayTurns"/>.</summary>
        void DetachReel(int index);

        /// <summary>Baja la cuenta un turno (mínimo 0) y publica el valor. No-op si está cancelada.</summary>
        int Tick();
        /// <summary>Rearma la cuenta en <paramref name="value"/> y la vuelve a poner a contar.</summary>
        void ResetCountdown(int value);

        /// <summary>Cancela la cuenta si <paramref name="reelGuid"/> es un rodillo vivo y no trabado. Devuelve <c>true</c> si canceló.</summary>
        bool CancelFromReelDamage(Guid reelGuid);

        void LockSlot(ReelSide side, int lockedHp);

        void ResetAll();
    }
}
