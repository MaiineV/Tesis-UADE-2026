using System;
using Patterns;
using Rollgeon.PreConditions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Tahur
{
    /// <summary>
    /// True si la liquidación de este turno no marcó Castigo. Sin el servicio del pozo registrado
    /// devuelve <c>true</c>: semántica permisiva del catálogo ("sin servicio no veta").
    /// </summary>
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
