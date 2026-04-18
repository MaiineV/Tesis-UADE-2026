namespace Rollgeon.Effects.Stubs
{
    /// <summary>
    /// [STUB] — ahora es un <b>thin inheritance alias</b> sobre
    /// <see cref="Rollgeon.Entities.Behaviors.BaseBehavior"/> (Content#0099).
    /// <para>
    /// Los consumers originales de Foundation#0004 (EffHeal/EffDamage via
    /// <see cref="Rollgeon.Effects.EffectContext.SourceBehavior"/>) siguen compilando
    /// sin cambios: el API publico (<c>SetBehaviorValue</c>, <c>TryGetBehaviorValues</c>,
    /// <c>ClearBehaviorValues</c>) lo provee la clase real. Cuando EffDamage/EffHeal
    /// migren a <c>using Rollgeon.Entities.Behaviors;</c>, este archivo se elimina.
    /// </para>
    /// </summary>
    public abstract class BaseBehavior : Rollgeon.Entities.Behaviors.BaseBehavior
    {
    }
}
