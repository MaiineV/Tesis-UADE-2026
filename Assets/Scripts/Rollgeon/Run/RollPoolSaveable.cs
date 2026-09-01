using System;
using Patterns;
using Patterns.Save;
using Rollgeon.Combat.Rolls;

namespace Rollgeon.Run
{
    /// <summary>
    /// <see cref="ISaveable"/> del Pool de Rolls (Feature#0050): persiste el bonus
    /// de pool acumulado por rewards ("Rolls +1": sube el máximo y el arranque de
    /// combate — BUG-85). El bonus vive como estado del <see cref="RollPoolService"/>
    /// (no como modifier de atributo, a diferencia del viejo MaxEnergy), así que sin
    /// este snapshot un save/load de run lo perdería. El pool ACTUAL no se persiste
    /// acá: es combat-only y viaja en <c>CombatResumeSnapshot.PlayerRolls</c>.
    /// El campo del DTO conserva el nombre viejo (<c>PerTurnGrantBonus</c>) por
    /// compatibilidad con saves de run existentes.
    /// </summary>
    public sealed class RollPoolSaveable : ISaveable, IDisposable
    {
        public const string SaveKeyConst = "run.roll_pool";

        public string SaveKey => SaveKeyConst;

        public object CaptureState()
        {
            var snapshot = new RollPoolRunSnapshot();
            if (TryResolve(out var pool))
                snapshot.PerTurnGrantBonus = pool.RollPoolBonus;
            return snapshot;
        }

        public void RestoreState(object state)
        {
            if (state is not RollPoolRunSnapshot snapshot) return;
            if (TryResolve(out var pool))
                pool.RestoreRollPoolBonus(snapshot.PerTurnGrantBonus);
        }

        /// <summary>Invocado por <c>ClearScope(Run)</c> — evita duplicados de SaveKey entre runs.</summary>
        public void Dispose()
        {
            SaveSystem.Unregister(this);
        }

        // RestoreRollPoolBonus no está en la interfaz — mismo downcast que usaba
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
