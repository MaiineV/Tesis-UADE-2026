using System;

namespace Rollgeon.Combat.Weakness
{
    /// <summary>
    /// Registro runtime de la debilidad de cada entidad spawneada. Se populariza en el
    /// enemy-spawn pipeline (T99) desde <c>EnemyDataSO.WeaknessComboId</c> +
    /// <c>EnemyDataSO.WeaknessMultiplierOverride</c>.
    /// <para>
    /// Lo consume <see cref="IWeaknessChecker"/> — el checker pregunta "este target tiene
    /// weakness, y si si, cual combo + que multiplier override". Abstrae el lookup para que
    /// los tests puedan usar un registry en memoria sin depender del entity pipeline.
    /// </para>
    /// </summary>
    public interface IWeaknessRegistry
    {
        /// <summary>
        /// Registra (o sobreescribe) la weakness de una entidad.
        /// </summary>
        /// <param name="entityId">InstanceId del target (enemy).</param>
        /// <param name="comboId"><c>ComboId</c> al que el target es debil. Null/empty = sin debilidad.</param>
        /// <param name="multiplierOverride">0 = usar default del <see cref="Rollgeon.Balance.RulesetSO"/>. &gt;0 = override.</param>
        void SetWeakness(Guid entityId, string comboId, float multiplierOverride);

        /// <summary>
        /// Retorna la weakness registrada para <paramref name="entityId"/>.
        /// </summary>
        /// <param name="entityId">InstanceId del target.</param>
        /// <param name="data">Tuple <c>(comboId, mult)</c>. <c>mult</c> es el override (0 = usar default).</param>
        /// <returns><c>true</c> si hay entrada registrada; <c>false</c> si el target es desconocido.</returns>
        bool TryGet(Guid entityId, out (string comboId, float mult) data);

        /// <summary>Borra la entry asociada al entity. No-op si no existe.</summary>
        void Unregister(Guid entityId);
    }
}
