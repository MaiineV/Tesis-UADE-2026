using System;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Heroes;

namespace Rollgeon.Combat.FSM.States.PlayerTurn
{
    public sealed class PlayerTurnSubContext
    {
        public CombatContext CombatContext;
        public Guid ActingGuid;
        public HeroActionBehavior PendingAction;
        public BehaviorContext PendingBehaviorContext;
        public TargetSelectionResult SelectionResult;
    }
}
