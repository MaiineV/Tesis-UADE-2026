using System;
using Rollgeon.Attributes;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Player;

namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// Contexto que recibe <see cref="AIDecisionNode.Tick"/>. TECHNICAL.md §7.5.
    /// </summary>
    /// <remarks>
    /// Se construye en <see cref="TreeDrivenEnemyAI"/> una vez por turno del enemigo.
    /// Los servicios son resueltos del <see cref="Patterns.ServiceLocator"/>; los campos
    /// pueden ser null si el servicio no esta registrado (ej. tests unitarios) — cada
    /// nodo debe tolerarlo con early return <see cref="AIResult.Failed"/>.
    /// </remarks>
    public sealed class AIContext
    {
        public Guid SelfGuid;
        public Guid PlayerGuid;

        /// <summary>HP máximo de referencia de Self al spawn — usado por PcOwnerHpBelow.</summary>
        public int SelfMaxHp;

        /// <summary>
        /// Entidad owner — opcional. Lo usa el bridge a <c>PreConditionContext.Entity</c>
        /// para evitar re-query del registro. Puede quedar null si el caller no lo provee.
        /// </summary>
        public Entity Self;

        public AttributesManager Attributes;
        public IDamagePipeline DamagePipeline;
        public IGridManager Grid;
        public IMovementService Movement;
        public IPlayerService PlayerService;

        public int RoundIndex;

        /// <summary>RNG inyectado — tests pueden proveer un System.Random con seed fijo.</summary>
        public System.Random Rng;
    }
}
