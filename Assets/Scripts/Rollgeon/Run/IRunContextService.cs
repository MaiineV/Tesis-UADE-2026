using System;
using Rollgeon.Heroes;

namespace Rollgeon.Run
{
    /// <summary>
    /// Read-only view of the current run state (TECHNICAL.md §1.1.3).
    /// Registered in <see cref="Patterns.ServiceScope.Run"/> by <see cref="RunBootstrapper"/>.
    /// </summary>
    public interface IRunContextService
    {
        /// <summary>Unique identifier for this run instance.</summary>
        Guid RunId { get; }

        /// <summary>Zero-based floor index within the current run.</summary>
        int FloorIndex { get; }

        /// <summary>Hero class selected for this run.</summary>
        ClassHeroSO SelectedHero { get; }

        /// <summary><c>true</c> while the run is in progress.</summary>
        bool IsRunActive { get; }

        /// <summary>Increments <see cref="FloorIndex"/> and fires <c>OnFloorChanged</c>.</summary>
        void AdvanceFloor();
    }
}
