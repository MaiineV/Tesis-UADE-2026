using System;
using System.Collections.Generic;
using Rollgeon.Entities.Behaviors;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// DTO que un effect (típicamente <c>EffPlayFeedback</c>) arma y pasa al
    /// <see cref="IFeedbackService"/>. TECHNICAL.md §10.4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Versión inicial stub (plan Sprint 03 FP — tickets posteriores completan §10.2/10.5/10.7).
    /// Los campos reflejan el DTO del spec pero la mayoría no se consumen todavía. El único
    /// uso real por ahora es transportar ids + StoredValues para el logging del stub service.
    /// </para>
    /// <para>
    /// <b>StoredValues</b> es un snapshot del bag del behavior — copiado al momento de armar
    /// el request por <see cref="Rollgeon.Effects.BaseEffect.GetFeedbackRequest"/>, no una
    /// referencia viva. El behavior puede limpiar su bag sin afectar al request en vuelo.
    /// </para>
    /// </remarks>
    public struct FeedbackRequest
    {
        /// <summary>Id de la entry en el <c>FeedbackDBSO</c>. Obligatorio para feedbacks no secuenciados.</summary>
        public string FeedbackId;

        /// <summary>Guid del autor del feedback (source pawn).</summary>
        public Guid SourceGuid;

        /// <summary>Guid del target (puede ser <see cref="System.Guid.Empty"/> si no aplica).</summary>
        public Guid TargetGuid;

        /// <summary>Snapshot del bag del behavior al momento de armar el request (§9.5).</summary>
        public IReadOnlyDictionary<BehaviorValueKey, List<BaseBehaviorStoredValue>> StoredValues;

        /// <summary>Posición mundial opcional — usada cuando la entry tiene <c>SpawnPosition.WorldPosition</c>.</summary>
        public Vector3 WorldPosition;

        /// <summary><c>true</c> si el request es una secuencia (ver §10.8).</summary>
        public bool IsSequence;

        /// <summary>Steps de la secuencia cuando <see cref="IsSequence"/> es <c>true</c>.</summary>
        public List<FeedbackSequenceStep> SequenceSteps;

        /// <summary>Player autor (para <see cref="SpawnPosition.FromReader"/>). Default <c>Player</c>.</summary>
        public FeedbackPlayer Player;
    }
}
