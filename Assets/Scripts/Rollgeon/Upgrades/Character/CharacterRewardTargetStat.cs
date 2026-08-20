namespace Rollgeon.Upgrades.Character
{
    /// <summary>
    /// Stat target de un <see cref="CharacterRewardSO"/>. <c>PlayerStatGrants</c>
    /// mapea cada enum value al stat concreto (Health, RollRegen, Speed, Attack).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Para agregar un stat nuevo: sumar la entry acá + extender el switch del
    /// aplicador. No requiere tocar SOs autorados (los assets viejos siguen
    /// apuntando a sus enum values originales).
    /// </para>
    /// <para>
    /// <b>NO reordenar ni eliminar entries</b>: el enum se serializa por ordinal en
    /// los CharacterRewardSO. <see cref="RollRegen"/> ocupa el slot del viejo
    /// <c>Energy</c> (Feature#0050) — los assets "Energía +1" pasan a otorgar
    /// +1 roll por turno sin re-autorarse.
    /// </para>
    /// </remarks>
    public enum CharacterRewardTargetStat
    {
        /// <summary>Vida máxima (HP pool).</summary>
        Health,
        /// <summary>+N rolls otorgados al cierre de cada turno (Pool de Rolls). Ex Energy.</summary>
        RollRegen,
        /// <summary>Velocidad — determina frecuencia de turnos.</summary>
        Speed,
        /// <summary>Daño base de ataque.</summary>
        Attack,
    }
}
