using System.Collections.Generic;
using Patterns;

namespace Rollgeon.Phase
{
    public sealed class PhaseService : IPhaseService
    {
        private PhaseTransitionMatrixSO _matrix;
        private bool _matrixResolved;
        private bool _isDraining;
        private readonly Queue<GamePhase> _pendingTransitions = new();

        public GamePhase CurrentBase { get; private set; } = GamePhase.None;
        public PhaseOverlay CurrentOverlay { get; private set; } = PhaseOverlay.None;

        public void ReplacePhase(GamePhase next)
        {
            if (_isDraining)
            {
                _pendingTransitions.Enqueue(next);
                return;
            }

#if UNITY_EDITOR || DEBUG || DEVELOPMENT_BUILD
            if (CurrentOverlay != PhaseOverlay.None)
            {
                throw new InvalidPhaseTransitionException(
                    $"Cannot ReplacePhase while overlay {CurrentOverlay} is active. PopOverlay first.");
            }
#endif

            if (next == CurrentBase) return;

            ResolveMatrix();
            if (_matrix != null && !_matrix.CanTransition(CurrentBase, next))
            {
                throw new InvalidPhaseTransitionException(
                    $"Transition from {CurrentBase} to {next} is not allowed by the transition matrix.");
            }

            _isDraining = true;

            EventManager.Trigger(EventName.OnPhaseExit, CurrentBase);
            CurrentBase = next;
            EventManager.Trigger(EventName.OnPhaseEnter, next);

            DrainQueue();
        }

        public void PushOverlay(PhaseOverlay overlay)
        {
            ResolveMatrix();
            if (_matrix != null && !_matrix.CanPushOverlay(CurrentBase, overlay))
            {
                throw new InvalidPhaseTransitionException(
                    $"Overlay {overlay} is not allowed during phase {CurrentBase}.");
            }

            CurrentOverlay = overlay;
            EventManager.Trigger(EventName.OnOverlayPushed, overlay);
        }

        public void PopOverlay()
        {
            var previous = CurrentOverlay;
            CurrentOverlay = PhaseOverlay.None;
            EventManager.Trigger(EventName.OnOverlayPopped, previous);
        }

        private void DrainQueue()
        {
            while (_pendingTransitions.Count > 0)
            {
                var next = _pendingTransitions.Dequeue();
                if (next == CurrentBase) continue;

                EventManager.Trigger(EventName.OnPhaseExit, CurrentBase);
                CurrentBase = next;
                EventManager.Trigger(EventName.OnPhaseEnter, next);
            }

            _isDraining = false;
        }

        private void ResolveMatrix()
        {
            if (_matrixResolved) return;
            ServiceLocator.TryGetService<PhaseTransitionMatrixSO>(out _matrix);
            _matrixResolved = true;
        }

        /// <summary>
        /// Test hook: injects a matrix without ServiceLocator lookup.
        /// </summary>
        public void ConfigureForTests(PhaseTransitionMatrixSO matrix)
        {
            _matrix = matrix;
            _matrixResolved = true;
        }
    }
}
