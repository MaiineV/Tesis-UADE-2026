using System;
using System.Collections.Generic;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combos;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;

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

        /// <summary>Resultado de la tirada de dados (las caras). Null si el behavior no usa dados.</summary>
        public IReadOnlyList<int> DiceResult;

        /// <summary>
        /// Subset de <see cref="DiceResult"/> que el jugador holdeó para el ataque (los dados
        /// que participan del combo). Null = sin keep explícito (usar <see cref="DiceResult"/>).
        /// Lo consume el fallback sin combo de <c>EffDealDamage</c> (GD §5: daño mínimo =
        /// dado más alto de los ELEGIDOS, no de toda la tirada).
        /// </summary>
        public IReadOnlyList<int> KeptDice;

        /// <summary>
        /// Índices de slot del bag (0-based) que corresponden 1:1 a cada entrada de
        /// <see cref="KeptDice"/> — ej. holdear los dados 0, 2 y 3 de una bolsa de 5 da
        /// <c>[0, 2, 3]</c>. Null = sin mapeo disponible.
        /// </summary>
        public IReadOnlyList<int> KeptDiceOriginalIndices;

        /// <summary>Resultado del combo matching via ContractSheet.MatchBest. Null si no hubo match.</summary>
        public ComboDetectionResult? ComboResult;

        /// <summary>
        /// Discriminante de qué acción generó esta tirada (BUG-060) — Attack/Defense/Heal
        /// en combate son "pagables" para encantamientos de oro; Movement/EndTurn/ForceDoor/
        /// Exploration no. <see cref="RollActionKind.Unknown"/> (default) = sin clasificar,
        /// tratado como NO pagable (fail-safe). Lo setea quien arma el context (behaviors,
        /// action rolls, chains) — ver <see cref="RollActionKindExtensions.IsCombatPayable"/>.
        /// </summary>
        public RollActionKind ActionKind;

        /// <summary>
        /// Total efectivo pre-computado por <c>IActionRollService</c> sobre el subset de
        /// dados que el user holdeó. Si tiene valor, tiene prioridad sobre el cálculo
        /// derivado de <see cref="DiceResult"/> + <see cref="ComboResult"/>. Null = sin
        /// override (usar cálculo legacy: combo.BaseDamage o suma cruda).
        /// </summary>
        public int? ActionRollEffectiveTotal;

        /// <summary>
        /// ItemId del item pasivo cuyo hook armó este contexto. Null fuera del canal de
        /// items. Permite a efectos/PCs derivar identidad estable por item (ej.
        /// <c>ItemPassiveSourceId</c> para modifiers one-shot) sin acoplar el pipeline
        /// genérico de efectos al inventario.
        /// </summary>
        public string SourceItemId;

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
