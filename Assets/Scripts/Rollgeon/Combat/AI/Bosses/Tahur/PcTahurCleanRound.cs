using System;
using Patterns;
using Rollgeon.PreConditions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Tahur
{
    /// <summary>
    /// True si la ronda del Tahúr quedó limpia: la liquidación de este turno no marcó Castigo
    /// (el jugador armó el canto exacto, o todavía no había canto que liquidar). Es el gate de
    /// la rama del poke.
    /// </summary>
    /// <remarks>
    /// Sin el servicio del pozo registrado devuelve <c>true</c>: semántica permisiva del catálogo
    /// ("sin servicio no veta").
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcTahurCleanRound : BasePreCondition
    {
        [Tooltip("Invertir: true cuando la ronda SÍ marcó Castigo.")]
        public bool Invert;

        public override string ConditionName => Invert ? "Tahúr: ronda con Castigo" : "Tahúr: ronda limpia";

        public override bool Evaluate(PreConditionContext context)
        {
            if (!ServiceLocator.TryGetService<ITahurWagerService>(out var wager) || wager == null)
                return !Invert;

            bool clean = !wager.MarkedPunishmentThisTurn;
            return Invert ? !clean : clean;
        }
    }
}
