using System.Collections.Generic;
using Rollgeon.Dice;

namespace Rollgeon.Meta.Conditions
{
    /// <summary>
    /// Snapshot inmutable-por-convención contra el que se evalúan los
    /// <see cref="IUnlockCondition"/> (#164). Lo arma <c>UnlockProgressService</c>
    /// combinando el <c>RunUnlockState</c> run-scoped, los contadores del
    /// <c>IComboCountersService</c> y los contadores persistentes del
    /// <c>IMetaProgressionService</c>.
    /// <para>
    /// Las condiciones de consistencia ("sin usar pociones", "sin huir") deben
    /// chequear <see cref="RunEnded"/>: durante la run la consistencia todavía
    /// puede romperse, así que solo pueden dar <c>true</c> al cierre.
    /// </para>
    /// </summary>
    public sealed class UnlockEvaluationContext
    {
        /// <summary><c>true</c> cuando la run ya terminó (Victory o Defeat).</summary>
        public bool RunEnded;

        /// <summary><c>true</c> si la run terminó ganada. Solo significativo con <see cref="RunEnded"/>.</summary>
        public bool RunWon;

        /// <summary><c>EntityId</c> de la clase jugada en esta run.</summary>
        public string ClassId;

        /// <summary>Bolsa de dados armada para la run (fija desde el run start).</summary>
        public IReadOnlyList<DiceType> DiceBuild;

        /// <summary>Conteo de ejecuciones por <c>ComboId</c> en la run actual.</summary>
        public IReadOnlyDictionary<string, int> ComboCounts;

        /// <summary><c>ComboId</c>s del Contrato de la clase jugada.</summary>
        public IReadOnlyCollection<string> ContractComboIds;

        /// <summary><c>ItemId</c>s de items activos usados durante la run.</summary>
        public IReadOnlyCollection<string> UsedActiveItemIds;

        /// <summary>Combates ganados sin recibir daño a Health.</summary>
        public int FlawlessCombats;

        /// <summary>Combates abandonados (huidas — <c>CombatOutcome.Aborted</c>).</summary>
        public int CombatsFled;

        /// <summary>Bosses derrotados en la run.</summary>
        public int BossesDefeated;

        /// <summary>Pisos visitados en la run (FloorIndex máximo + 1).</summary>
        public int FloorsVisited;

        /// <summary>Racha persistente de runs ganadas consecutivas (incluye esta run si ganó).</summary>
        public int ConsecutiveWins;

        /// <summary>Set persistente de clases distintas jugadas entre runs (incluye la actual al cierre).</summary>
        public IReadOnlyCollection<string> ClassesPlayed;

        /// <summary>Lectura segura del conteo de un combo: 0 si no hay entry.</summary>
        public int GetComboCount(string comboId)
        {
            if (string.IsNullOrEmpty(comboId) || ComboCounts == null) return 0;
            return ComboCounts.TryGetValue(comboId, out var v) ? v : 0;
        }
    }
}
