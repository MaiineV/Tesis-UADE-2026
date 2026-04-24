using System;
using System.Collections.Generic;
using Rollgeon.Combos;
using Rollgeon.Entities.Behaviors;

namespace Rollgeon.Heroes
{
    public class HeroBehaviorContext : BehaviorContext
    {
        public IReadOnlyList<int> DiceResult;
        public ComboDetectionResult? MatchedComboResult;
        public Guid TargetGuid;
    }
}
