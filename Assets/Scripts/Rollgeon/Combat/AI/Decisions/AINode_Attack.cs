using System;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Pipelines;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Ataca al player usando el stat <see cref="Attack"/> del Self. TECHNICAL.md §7.5.
    /// </summary>
    /// <remarks>
    /// Reutiliza la lógica de <see cref="BasicEnemyAI"/> pero via el <c>IDamagePipeline</c>
    /// directamente. Si Self no tiene stat Attack, o Attack &lt;= 0, retorna
    /// <see cref="AIResult.Failed"/> (el árbol puede ramificar a otra acción).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_Attack : AIActionNode
    {
        [Tooltip("Multiplicador sobre el stat Attack del enemigo. 1.0 = base; 2.0 = doble daño (ej. Boss con energía llena).")]
        [MinValue(0f)]
        public float DamageMultiplier = 1f;

        public override string NodeName => "Attack Player";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;
            if (context.Attributes == null) return AIResult.Failed;
            if (context.DamagePipeline == null) return AIResult.Failed;
            if (context.PlayerGuid == Guid.Empty) return AIResult.Failed;

            var attackAttr = context.Attributes.GetAttribute<Attack>(context.SelfGuid);
            if (attackAttr == null) return AIResult.Failed;

            int baseDamage = Mathf.Max(0, Mathf.RoundToInt(attackAttr.ModifiedValue * DamageMultiplier));
            if (baseDamage <= 0) return AIResult.Failed;

            var ctx = new DamageContext
            {
                SourceId = context.SelfGuid,
                TargetId = context.PlayerGuid,
                BaseDamage = baseDamage,
                Kind = AttackKind.BasicAttack,
            };
            context.DamagePipeline.Resolve(ctx);
            return AIResult.Succeeded;
        }
    }
}
