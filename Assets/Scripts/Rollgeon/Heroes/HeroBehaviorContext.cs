using System;
using System.Collections.Generic;
using Rollgeon.Combos;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities.Behaviors;

namespace Rollgeon.Heroes
{
    public class HeroBehaviorContext : BehaviorContext
    {
        public IReadOnlyList<int> DiceResult;
        public ComboDetectionResult? MatchedComboResult;
        public Guid TargetGuid;
        public TargetSelectionResult SelectionResult;
        public bool EnergyPrepaid;

        /// <summary>
        /// Total efectivo pre-computado por <c>IActionRollService</c> sobre el subset
        /// de dados que el user holdeó (no la suma cruda de los 5). Si tiene valor, los
        /// effects con <c>IActionRollEffect</c> deben usarlo en vez de recomputar desde
        /// <see cref="DiceResult"/>. Null = no pasó por ActionRollService — fallback al
        /// cálculo legacy (suma cruda o combo del sheet).
        /// </summary>
        public int? ActionRollEffectiveTotal;
    }
}
