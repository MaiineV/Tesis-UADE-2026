using System;
using Patterns;
using Rollgeon.Heroes;

namespace Rollgeon.Run
{
    /// <summary>
    /// Mutable run state implementing <see cref="IRunContextService"/>.
    /// Created by <see cref="RunBootstrapper.StartRun"/> and disposed when
    /// <see cref="ServiceLocator.ClearScope"/> tears down <see cref="ServiceScope.Run"/>.
    /// </summary>
    public sealed class RunContext : IRunContextService, IDisposable
    {
        public Guid RunId { get; }
        public int FloorIndex { get; private set; }
        public ClassHeroSO SelectedHero { get; }
        public bool IsRunActive { get; private set; }

        public RunContext(Guid runId, ClassHeroSO selectedHero)
        {
            if (selectedHero == null)
                throw new ArgumentNullException(nameof(selectedHero));

            RunId = runId;
            SelectedHero = selectedHero;
            FloorIndex = 0;
            IsRunActive = true;
        }

        public void AdvanceFloor()
        {
            FloorIndex++;
            EventManager.Trigger(EventName.OnFloorChanged, RunId, FloorIndex);
        }

        /// <summary>
        /// Marks the run as finished. Called internally by <see cref="RunBootstrapper.EndRun"/>.
        /// </summary>
        internal void EndRun()
        {
            IsRunActive = false;
        }

        public void Dispose()
        {
            // No unmanaged resources; exists so ClearScope disposes cleanly.
            IsRunActive = false;
        }
    }
}
