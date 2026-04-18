namespace Rollgeon.Effects.Stubs
{
    /// <summary>
    /// [STUB] — TECHNICAL.md §7.3. Base polimórfica de los contextos de trigger que
    /// alimentan un <see cref="BaseBehavior"/>. La jerarquía real (DamageBehaviorContext,
    /// TurnBehaviorContext, InteractBehaviorContext, …) se declara en la foundation
    /// downstream de Behaviors; acá sólo se expone el shape mínimo que consumen
    /// <see cref="EffectContext"/> y <see cref="EffectContext.TryGetTriggerContext{T}"/>.
    /// </summary>
    public abstract class BehaviorContext
    {
        public Entity SourceEntity;
        public Entity TriggeringEntity;
    }
}
