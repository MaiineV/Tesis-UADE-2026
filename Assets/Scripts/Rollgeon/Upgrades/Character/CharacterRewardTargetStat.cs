namespace Rollgeon.Upgrades.Character
{
    /// <summary>
    /// Stat target de un <see cref="CharacterRewardSO"/>. La <c>CharacterRewardService</c>
    /// mapea cada enum value al tipo concreto de <c>BaseAttribute&lt;int&gt;</c> via
    /// switch (Health, Energy, Speed, Attack en MVP).
    /// </summary>
    /// <remarks>
    /// Para agregar un stat nuevo: sumar la entry acá + extender el switch del
    /// service. No requiere tocar SOs autorados (los assets viejos siguen apuntando
    /// a sus enum values originales).
    /// </remarks>
    public enum CharacterRewardTargetStat
    {
        /// <summary>Vida máxima (HP pool).</summary>
        Health,
        /// <summary>Energía disponible por turno.</summary>
        Energy,
        /// <summary>Velocidad — determina frecuencia de turnos.</summary>
        Speed,
        /// <summary>Daño base de ataque.</summary>
        Attack,
    }
}
