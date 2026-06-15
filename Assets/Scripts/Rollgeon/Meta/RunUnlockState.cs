using System;
using System.Collections.Generic;
using Patterns.Save;
using Rollgeon.Dice;

namespace Rollgeon.Meta
{
    /// <summary>
    /// POCO run-scoped con el progreso de condiciones de unlock dentro de la run
    /// actual (#164). Instanciado por <see cref="UnlockProgressService"/> en
    /// <c>OnRunStart</c>, registrado bajo <c>ServiceScope.Run</c> y descartado por
    /// <c>ClearScope(Run)</c> al terminar la run. Implementa <see cref="ISaveable"/>
    /// (§15) con key <c>"run.unlock_tracker"</c> — los conteos de combos NO viven
    /// acá (ya los persiste <c>RunComboCounterState</c>).
    /// </summary>
    [Serializable]
    public sealed class RunUnlockState : ISaveable
    {
        public const string SaveKeyConst = "run.unlock_tracker";

        /// <summary><c>EntityId</c> de la clase jugada.</summary>
        public string ClassId;

        /// <summary>Bolsa de dados armada para esta run (fija desde el run start).</summary>
        public List<DiceType> DiceBuild = new List<DiceType>();

        /// <summary><c>ComboId</c>s del Contrato de la clase jugada.</summary>
        public HashSet<string> ContractComboIds = new HashSet<string>();

        /// <summary><c>ItemId</c>s de items activos usados durante la run.</summary>
        public List<string> UsedActiveItemIds = new List<string>();

        /// <summary>Combates ganados sin recibir daño a Health.</summary>
        public int FlawlessCombats;

        /// <summary>Combates abandonados (<c>CombatOutcome.Aborted</c>).</summary>
        public int CombatsFled;

        /// <summary>Bosses derrotados.</summary>
        public int BossesDefeated;

        /// <summary>Pisos visitados (FloorIndex máximo + 1). La run arranca en el piso 0.</summary>
        public int FloorsVisited = 1;

        /// <summary>
        /// <c>UnlockId</c>s cuyas condiciones de consistencia ya se rompieron en
        /// esta run — quedan fuera de la evaluación de cierre.
        /// </summary>
        public HashSet<string> InvalidatedUnlockIds = new HashSet<string>();

        // ---- Tracking transient del combate en curso ----

        /// <summary><c>true</c> entre OnCombatStart y OnCombatEnd.</summary>
        public bool InCombat;

        /// <summary><c>true</c> si el player recibió daño a Health en el combate en curso.</summary>
        public bool TookDamageThisCombat;

        /// <summary><c>true</c> si el combate en curso es contra un Boss.</summary>
        public bool CurrentCombatIsBoss;

        /// <summary><c>true</c> una vez corrida la evaluación de cierre (Victory/Defeat).</summary>
        public bool Finalized;

        // ---------------------------------------------------------------- ISaveable

        /// <inheritdoc />
        public string SaveKey => SaveKeyConst;

        /// <inheritdoc />
        public object CaptureState()
        {
            return new RunUnlockSnapshot
            {
                ClassId = ClassId,
                DiceBuild = new List<DiceType>(DiceBuild),
                ContractComboIds = new List<string>(ContractComboIds),
                UsedActiveItemIds = new List<string>(UsedActiveItemIds),
                FlawlessCombats = FlawlessCombats,
                CombatsFled = CombatsFled,
                BossesDefeated = BossesDefeated,
                FloorsVisited = FloorsVisited,
                InvalidatedUnlockIds = new List<string>(InvalidatedUnlockIds),
            };
        }

        /// <inheritdoc />
        public void RestoreState(object state)
        {
            DiceBuild.Clear();
            ContractComboIds.Clear();
            UsedActiveItemIds.Clear();
            InvalidatedUnlockIds.Clear();
            ClassId = null;
            FlawlessCombats = 0;
            CombatsFled = 0;
            BossesDefeated = 0;
            FloorsVisited = 1;

            if (state is not RunUnlockSnapshot snapshot) return;

            ClassId = snapshot.ClassId;
            if (snapshot.DiceBuild != null) DiceBuild.AddRange(snapshot.DiceBuild);
            if (snapshot.ContractComboIds != null)
            {
                foreach (var id in snapshot.ContractComboIds)
                {
                    if (!string.IsNullOrEmpty(id)) ContractComboIds.Add(id);
                }
            }
            if (snapshot.UsedActiveItemIds != null) UsedActiveItemIds.AddRange(snapshot.UsedActiveItemIds);
            if (snapshot.InvalidatedUnlockIds != null)
            {
                foreach (var id in snapshot.InvalidatedUnlockIds)
                {
                    if (!string.IsNullOrEmpty(id)) InvalidatedUnlockIds.Add(id);
                }
            }
            FlawlessCombats = snapshot.FlawlessCombats;
            CombatsFled = snapshot.CombatsFled;
            BossesDefeated = snapshot.BossesDefeated;
            FloorsVisited = snapshot.FloorsVisited;
        }
    }

    /// <summary>DTO serializable producido por <see cref="RunUnlockState.CaptureState"/>.</summary>
    [Serializable]
    public class RunUnlockSnapshot
    {
        public string ClassId;
        public List<DiceType> DiceBuild = new List<DiceType>();
        public List<string> ContractComboIds = new List<string>();
        public List<string> UsedActiveItemIds = new List<string>();
        public int FlawlessCombats;
        public int CombatsFled;
        public int BossesDefeated;
        public int FloorsVisited;
        public List<string> InvalidatedUnlockIds = new List<string>();
    }
}
