using System;
using Patterns;
using Patterns.Save;
using Rollgeon.Heroes;

namespace Rollgeon.Run
{
    /// <summary>
    /// Mutable run state implementing <see cref="IRunContextService"/>.
    /// Created by <see cref="RunBootstrapper.StartRun"/> and disposed when
    /// <see cref="ServiceLocator.ClearScope"/> tears down <see cref="ServiceScope.Run"/>.
    /// <para>
    /// Implementa <see cref="ISaveable"/> (#158): persiste <see cref="FloorIndex"/>
    /// para sobrevivir un save/load a mitad de run. El reset es automatico — cada
    /// <see cref="RunBootstrapper.StartRun"/> crea un <c>RunContext</c> fresco con
    /// <c>FloorIndex = 0</c>.
    /// </para>
    /// </summary>
    public sealed class RunContext : IRunContextService, ISaveable, IDisposable
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

        // ---- ISaveable (#158) ------------------------------------------------

        public string SaveKey => "run.floor_index";

        public object CaptureState() => FloorIndex;

        public void RestoreState(object state)
        {
            if (state is int floorIndex) FloorIndex = floorIndex;
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
