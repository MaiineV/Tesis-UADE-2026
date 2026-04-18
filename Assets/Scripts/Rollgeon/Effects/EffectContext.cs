using System;
using Rollgeon.Effects.Selection;
using Rollgeon.Effects.Stubs;

namespace Rollgeon.Effects
{
    /// <summary>
    /// Contenedor único que el dispatcher del behavior pasa a cada <see cref="IEffect.Apply"/>.
    /// TECHNICAL.md §8.4. Los campos son ABI — adiciones son no-breaking; rename / delete =
    /// breaking change coordinado (plan §4.5).
    /// </summary>
    public class EffectContext
    {
        /// <summary>Guid del owner del behavior (caster).</summary>
        public Guid SourceGuid;

        /// <summary>Guid del target resuelto. <see cref="System.Guid.Empty"/> si no hay uno específico.</summary>
        public Guid TargetGuid;

        /// <summary>Entidad fuente — la que posee el behavior. Suele coincidir con <see cref="SourceGuid"/>.</summary>
        public Entity SourceEntity;

        /// <summary>Entidad que disparó el trigger (p.ej. en <c>OnDamaged</c>, el atacante).</summary>
        public Entity TriggeringEntity;

        /// <summary>Resultado de la selección runtime (§11). Puede ser null si el efecto no requiere selección.</summary>
        public TargetSelectionResult SelectionResult;

        /// <summary>Índice del efecto actual dentro del <see cref="EffectData.Effects"/>.
        /// Lo setea <see cref="EffectData.Execute"/> antes de llamar <see cref="IEffect.Apply"/>.</summary>
        public int EffectIndex;

        /// <summary>
        /// Resultado del último efecto aplicado. Inicia en <c>true</c>; un <c>false</c>
        /// detiene la cadena (cortocircuito §8.8). Lo leen los efectos siguientes y
        /// <see cref="EffectData.Execute"/>.
        /// </summary>
        public bool lastResult = true;

        /// <summary>Behavior que armó este contexto — expone <c>SetBehaviorValue</c> (§9.3).</summary>
        public BaseBehavior SourceBehavior;

        /// <summary>
        /// Contexto del trigger que disparó al behavior (§7.3). Subtipo polimórfico —
        /// <c>DamageBehaviorContext</c>, <c>TurnBehaviorContext</c>, etc. Consumido via
        /// <see cref="TryGetTriggerContext{T}"/> por efectos con <see cref="IRequiresTriggerContext{TCtx}"/>.
        /// </summary>
        public BehaviorContext TriggerContext;

        /// <summary>
        /// Acceso tipado al trigger context. Devuelve <c>false</c> si el subtipo no matchea.
        /// Los efectos con <see cref="IRequiresTriggerContext{TCtx}"/> deberían disparar un
        /// warning naranja en el inspector cuando se atan a un behavior con trigger incompatible
        /// (§8.5 soft check).
        /// </summary>
        public bool TryGetTriggerContext<T>(out T ctx) where T : BehaviorContext
        {
            ctx = TriggerContext as T;
            return ctx != null;
        }
    }
}
