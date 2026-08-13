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

        public const string SaveKeyConst = "run.floor_index";

        public string SaveKey => SaveKeyConst;

        public object CaptureState() => new RunContextSnapshot
        {
            FloorIndex = FloorIndex,
            RunId = RunId.ToString(),
        };

        public void RestoreState(object state)
        {
            // El RunId guardado NO se restaura acá (es init-only): el resume lo lee
            // vía SaveSystem.TryGetCached y se lo pasa a StartRun — de él deriva el
            // seed del dungeon (RunController: seed = runId.GetHashCode()).
            if (state is RunContextSnapshot snapshot) FloorIndex = snapshot.FloorIndex;
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
            IsRunActive = false;
            // Sin este Unregister, la próxima run registraría un segundo RunContext con
            // la misma SaveKey y CaptureAll (reverso) capturaría el viejo al final.
            SaveSystem.Unregister(this);
        }
    }

    /// <summary>
    /// DTO serializable de <see cref="RunContext"/>. <c>RunId</c> se persiste porque
    /// el seed del dungeon deriva de él — un resume con el mismo RunId regenera los
    /// pisos idénticos.
    /// </summary>
    [Serializable]
    public class RunContextSnapshot
    {
        public int FloorIndex;
        public string RunId;
    }
}
