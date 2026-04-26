using System;
using Patterns;
using Rollgeon.Phase;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// Chequea la fase global actual contra <see cref="ExpectedPhase"/>.
    /// TECHNICAL.md §8.2 — caveat: el catálogo previsto menciona "fase del turno",
    /// pero el único servicio expuesto hoy es <see cref="IPhaseService"/> con
    /// <see cref="GamePhase"/> (Exploration/Combat/Loading/GameOver). Cuando el
    /// Combat FSM exponga substates de turno se podrá agregar un PC paralelo.
    /// <para>
    /// Si <see cref="MatchOverlay"/> está activo, además exige que la
    /// <see cref="PhaseOverlay"/> activa coincida con <see cref="ExpectedOverlay"/>.
    /// </para>
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class PCCurrentPhase : BasePreCondition
    {
        [Tooltip("Fase global esperada. Pasa si IPhaseService.CurrentBase == ExpectedPhase.")]
        public GamePhase ExpectedPhase = GamePhase.Combat;

        [ToggleLeft]
        [Tooltip("Si true, además exige overlay específico (Pause, Cutscene, Craps).")]
        public bool MatchOverlay;

        [ShowIf(nameof(MatchOverlay))]
        public PhaseOverlay ExpectedOverlay = PhaseOverlay.None;

        public override string ConditionName =>
            MatchOverlay
                ? $"Phase == {ExpectedPhase} & Overlay == {ExpectedOverlay}"
                : $"Phase == {ExpectedPhase}";

        public override bool Evaluate(PreConditionContext context)
        {
            if (!ServiceLocator.TryGetService<IPhaseService>(out var phase)) return false;
            if (phase.CurrentBase != ExpectedPhase) return false;
            if (MatchOverlay && phase.CurrentOverlay != ExpectedOverlay) return false;
            return true;
        }
    }
}
