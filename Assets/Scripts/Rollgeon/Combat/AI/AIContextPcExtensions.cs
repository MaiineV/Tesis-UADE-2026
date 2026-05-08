using System;
using Rollgeon.PreConditions;

namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// Bridge AI → PC: arma el <see cref="PreConditionContext"/> que las precondiciones
    /// portadas (<c>PcOwnerHpBelow</c>, <c>PcTargetInRange</c>, <c>PcAllyAliveExists</c>,
    /// <c>PcRoundNumber</c>) consumen sin saber del lado AI.
    /// </summary>
    public static class AIContextPcExtensions
    {
        /// <summary>
        /// Devuelve un <see cref="PreConditionContext"/> nuevo con Owner=Self,
        /// Opponent=<paramref name="opponentGuid"/>, y Round/MaxHp poblados desde el AIContext.
        /// </summary>
        public static PreConditionContext BuildPcContext(this AIContext ctx, Guid opponentGuid)
        {
            if (ctx == null) return new PreConditionContext { OpponentGuid = opponentGuid };

            return new PreConditionContext
            {
                OwnerGuid = ctx.SelfGuid,
                OpponentGuid = opponentGuid,
                Entity = ctx.Self,
                RoundIndex = ctx.RoundIndex,
                OwnerMaxHp = ctx.SelfMaxHp > 0 ? (int?)ctx.SelfMaxHp : null,
                Attributes = ctx.Attributes,
            };
        }
    }
}
