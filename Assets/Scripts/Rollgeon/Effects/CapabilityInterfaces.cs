using Rollgeon.Effects.Stubs;

namespace Rollgeon.Effects
{
    // TECHNICAL.md §8.5 — Capability markers.
    //
    // Las 20 interfaces de este archivo son PURAS MARKER INTERFACES (salvo
    // IRequiresTriggerContext<TCtx> que lleva un type parameter). No declaran métodos.
    // Su existencia permite al inspector de un efecto concreto revelar condicionalmente
    // secciones (attribute source, value source, feedback, …). Cada sistema downstream
    // usa las que le sean útiles.
    //
    // REGLA DE ESTABILIDAD (plan §4.6):
    //  - Agregar un nuevo marker es NO breaking.
    //  - Remover un marker existente SÍ es breaking — romperá la compilación de todo
    //    efecto que haya declarado implementarlo. Si es necesario, marcar [Obsolete]
    //    primero por 1 sprint y migrar los consumers.

    /// <summary>Declara que el efecto lee un atributo fuente (entidad self / opponent / triggering).</summary>
    public interface IUsesAttribute { }

    /// <summary>Permite que el dropdown de attribute source incluya la propia entidad (self).</summary>
    public interface ICanBeEntityAttribute { }

    /// <summary>Permite que el dropdown de attribute source incluya la triggering entity (§7.3).</summary>
    public interface ICanBeTriggeringEntityAttribute { }

    /// <summary>Declara que el efecto consume un value (constant / entity attr / generic).</summary>
    public interface IUsesValue { }

    /// <summary>Habilita la opción "Constant value" en el value dropdown.</summary>
    public interface ICanBeConstantValue { }

    /// <summary>Habilita la opción "Read from entity attribute".</summary>
    public interface ICanBeEntityValue { }

    /// <summary>Habilita la opción "Generic / parameterized value".</summary>
    public interface ICanBeGenericValue { }

    /// <summary>Declara que el efecto requiere selección (§11) — muestra SelectionSettings.</summary>
    public interface IUsesSelection { }

    /// <summary>Declara que el efecto usa grid selection booleana (<c>bool[,]</c> con <c>[TableMatrix]</c>).</summary>
    public interface IUsesGridSelection { }

    /// <summary>Declara que el efecto produce un feedback (§10).</summary>
    public interface IUsesFeedback { }

    /// <summary>Declara que el feedback toma un target específico (vs global).</summary>
    public interface IUsesFeedbackTarget { }

    /// <summary>Declara que el efecto dispara una secuencia multi-paso de feedbacks (§10.6).</summary>
    public interface IUsesFeedbackSequence { }

    /// <summary>El feedback puede ser un VFX.</summary>
    public interface ICanBeVFXFeedback { }

    /// <summary>El feedback puede ser un SFX.</summary>
    public interface ICanBeSFXFeedback { }

    /// <summary>El feedback puede ser una animación.</summary>
    public interface ICanBeAnimFeedback { }

    /// <summary>El efecto crea / afecta modificadores con duración (<see cref="Rollgeon.Attributes.Modifiers.ModifierLifetime"/>).</summary>
    public interface IHasDuration { }

    /// <summary>El efecto expone un dropdown de <see cref="Rollgeon.Attributes.Modifiers.ModifierOperation"/>.</summary>
    public interface IHasOperation { }

    /// <summary>El efecto expone un dropdown de <see cref="Rollgeon.Attributes.Modifiers.ModifierDirection"/>.</summary>
    public interface IHasModifierDirection { }

    /// <summary>El efecto escribe en el bag runtime del behavior (§9).</summary>
    public interface IShouldStoreValuesOnBehavior { }

    /// <summary>
    /// El efecto consume un <see cref="BehaviorContext"/> de subtipo <typeparamref name="TCtx"/>.
    /// El inspector downstream valida cross-reference con el trigger del behavior contenedor
    /// (§8.5 warning naranja, soft check). Runtime: <see cref="EffectContext.TryGetTriggerContext{T}"/>
    /// devuelve false si no hay match, y el efecto decide (típicamente <c>return false</c>).
    /// </summary>
    public interface IRequiresTriggerContext<TCtx> where TCtx : BehaviorContext { }
}
