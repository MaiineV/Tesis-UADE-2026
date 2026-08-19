using System;

namespace Rollgeon.Combat.Rolls
{
    /// <summary>
    /// API pública del Pool de Rolls del jugador (GDD "Turn System", Feature#0050).
    /// Reemplaza a <c>IEnergyService</c>: el recurso ya no es energía por acción
    /// sino tiradas de dado individuales — cada tirada (primera o reroll) cuesta
    /// 1 roll, sin tope de retiradas por acción.
    /// <list type="bullet">
    ///   <item>Exclusivo de combate: arranca en <c>RollsAtCombatStart</c> al entrar,
    ///         suma <c>RollsPerTurn</c> (+ bonus de rewards) al cerrar cada turno del
    ///         jugador, clampea a <c>RollPoolCap</c> y se vacía al terminar el combate.</item>
    ///   <item>Consumers leen <see cref="GetCurrent"/>/<see cref="GetMax"/> y se
    ///         suscriben a <c>EventName.OnPlayerRollsChanged</c>.</item>
    /// </list>
    /// </summary>
    public interface IRollPoolService
    {
        /// <summary>
        /// Cachea el Guid del jugador activo. El pool queda en 0 hasta el próximo
        /// <c>OnCombatStart</c>. El caller que spawnea al player (RunController)
        /// debe invocarlo explícitamente — <c>OnRunStart</c> no trae el player Guid.
        /// </summary>
        void InitializeForEntity(Guid entityId);

        /// <summary>
        /// Intenta cobrar <paramref name="count"/> rolls del pool.
        /// </summary>
        /// <returns>
        /// <c>true</c> si había suficiente y se cobró; <c>false</c> si
        /// <c>count &gt; current</c> o la entidad no es el jugador cacheado (sin
        /// mutar nada). Dispara <c>OnPlayerRollsChanged</c> solo en caso de éxito.
        /// </returns>
        bool TrySpendRolls(Guid entityId, int count);

        /// <summary>
        /// Drena hasta <paramref name="amount"/> rolls, floor en 0 (a diferencia de
        /// <see cref="TrySpendRolls"/> no falla por insuficiencia: un peaje toma lo
        /// que haya). Path del ReelToll de Bandida.
        /// </summary>
        /// <returns>La cantidad efectivamente drenada.</returns>
        int Drain(Guid entityId, int amount);

        /// <summary>Suma rolls al pool, clampeado a <see cref="GetMax"/>.</summary>
        void AddRolls(Guid entityId, int amount);

        /// <summary>
        /// <c>true</c> mientras hay un combate en curso (entre <c>OnCombatStart</c> y
        /// <c>OnCombatEnd</c>). Los gates de affordability solo aplican acá: fuera de
        /// combate el pool no existe y las acciones son gratis.
        /// </summary>
        bool IsCombatActive { get; }

        /// <summary>Rolls disponibles. 0 fuera de combate o para entidades ≠ jugador.</summary>
        int GetCurrent(Guid entityId);

        /// <summary>Tope de acumulación del pool (<c>RollPoolCap</c> del ruleset).</summary>
        int GetMax(Guid entityId);

        /// <summary>
        /// Rolls otorgados al cierre de cada turno: <c>RollsPerTurn</c> del ruleset
        /// + bonus acumulado vía <see cref="AddPerTurnGrantBonus"/>.
        /// </summary>
        int GetRollsPerTurn(Guid entityId);

        /// <summary>
        /// Suma un bonus permanente (por run) al grant por turno — hook del reward
        /// "+1 Roll por turno" (ex "Energía +1"). Se resetea en <c>OnRunStart</c>.
        /// </summary>
        void AddPerTurnGrantBonus(int amount);

        /// <summary>
        /// Setea el pool a un valor guardado (combat resume) — sobrescribe el
        /// arranque-en-5 de <c>OnCombatStart</c>. Clampea a [0, max] y dispara
        /// <c>OnPlayerRollsChanged</c>.
        /// </summary>
        void RestoreCurrent(Guid entityId, int value);
    }
}
