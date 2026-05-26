using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Phase
{
    [CreateAssetMenu(menuName = "Rollgeon/Phase/Transition Matrix", fileName = "PhaseTransitionMatrix")]
    public class PhaseTransitionMatrixSO : SerializedScriptableObject
    {
        private const int PhaseCount = 5;

        [Title("Base Phase Transitions")]
        [InfoBox("Row = from, Column = to. Tick to allow the transition.")]
        [OdinSerialize]
        private bool[,] _allowedTransitions = new bool[PhaseCount, PhaseCount];

        [Title("Allowed Overlays per Phase")]
        [OdinSerialize]
        private Dictionary<GamePhase, List<PhaseOverlay>> _allowedOverlays = new();

        public bool CanTransition(GamePhase from, GamePhase to)
        {
            int f = (int)from;
            int t = (int)to;
            if (f < 0 || f >= PhaseCount || t < 0 || t >= PhaseCount) return false;
            return _allowedTransitions[f, t];
        }

        public bool CanPushOverlay(GamePhase currentBase, PhaseOverlay overlay)
        {
            if (!_allowedOverlays.TryGetValue(currentBase, out var list)) return false;
            return list != null && list.Contains(overlay);
        }
    }
}
