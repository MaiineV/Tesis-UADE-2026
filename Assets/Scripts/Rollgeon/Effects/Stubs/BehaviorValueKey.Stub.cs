namespace Rollgeon.Effects.Stubs
{
    /// <summary>
    /// [STUB] — TECHNICAL.md §9.2. Enum de claves del bag runtime de
    /// <see cref="BaseBehavior.SetBehaviorValue"/>. Acá sólo se declaran los valores
    /// mínimos para que <c>EffDamage</c> / <c>EffHeal</c> compilen. La foundation de
    /// Behaviors downstream amplía este enum con el catálogo completo (DirectionMagnitude,
    /// HitImpulse, ComboMatched, WeaknessHit, …) sin romper los existentes.
    /// <para>Regla de estabilidad: no renumerar, sólo agregar al final.</para>
    /// </summary>
    public enum BehaviorValueKey
    {
        None = 0,
        FloatingDamage = 1,
        FloatingHeal = 2,
        FloatingShield = 3,
    }
}
