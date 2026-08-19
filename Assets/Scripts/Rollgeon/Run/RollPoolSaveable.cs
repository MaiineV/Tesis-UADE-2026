using System;
using Patterns;
using Patterns.Save;
using Rollgeon.Combat.Rolls;

namespace Rollgeon.Run
{
    /// <summary>
    /// <see cref="ISaveable"/> del Pool de Rolls (Feature#0050): persiste el bonus
    /// por turno acumulado por rewards ("+1 Roll por turno"). El bonus vive como
    /// estado del <see cref="RollPoolService"/> (no como modifier de atributo, a
    /// diferencia del viejo MaxEnergy), así que sin este snapshot un save/load de
    /// run lo perdería. El pool ACTUAL no se persiste acá: es combat-only y viaja
    /// en <c>CombatResumeSnapshot.PlayerRolls</c>.
    /// </summary>
    public sealed class RollPoolSaveable : ISaveable, IDisposable
    {
        public const string SaveKeyConst = "run.roll_pool";

        public string SaveKey => SaveKeyConst;

        public object CaptureState()
        {
            var snapshot = new RollPoolRunSnapshot();
            if (TryResolve(out var pool))
                snapshot.PerTurnGrantBonus = pool.PerTurnGrantBonus;
            return snapshot;
        }

        public void RestoreState(object state)
        {
            if (state is not RollPoolRunSnapshot snapshot) return;
            if (TryResolve(out var pool))
                pool.RestorePerTurnGrantBonus(snapshot.PerTurnGrantBonus);
        }

        /// <summary>Invocado por <c>ClearScope(Run)</c> — evita duplicados de SaveKey entre runs.</summary>
        public void Dispose()
        {
            SaveSystem.Unregister(this);
        }

        // RestorePerTurnGrantBonus no está en la interfaz — mismo downcast que usaba
        // CombatResumeService con el EnergyService.
        private static bool TryResolve(out RollPoolService pool)
        {
            pool = ServiceLocator.TryGetService<IRollPoolService>(out var poolIf)
                   && poolIf is RollPoolService concrete
                ? concrete
                : null;
            return pool != null;
        }
    }

    /// <summary>DTO serializable de <see cref="RollPoolSaveable"/>.</summary>
    [Serializable]
    public class RollPoolRunSnapshot
    {
        public int PerTurnGrantBonus;
    }
}
