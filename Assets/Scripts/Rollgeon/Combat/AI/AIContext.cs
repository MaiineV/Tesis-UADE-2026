using System;
using System.Collections;
using System.Collections.Generic;
using Rollgeon.Attributes;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities;
using Rollgeon.Entities.Visuals;
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

        /// <summary>Servicio visual — null en tests EditMode. Permite a action nodes esperar animaciones.</summary>
        public IEntityVisualService VisualService;

        /// <summary>
        /// Wait handle que un action node popula cuando retorna <see cref="AIResult.Running"/>.
        /// El consumer (TickCoroutine) lo drena y luego lo resetea a null.
        /// </summary>
        [NonSerialized] public IEnumerator PendingWait;

        /// <summary>
        /// Keys de acciones ya ejecutadas este turno — regla "sin repetir acciones":
        /// la energía es el presupuesto, pero cada acción corre a lo sumo una vez por
        /// turno (como el player). El contexto se construye fresco por turno, así que
        /// el set se resetea solo. Los behaviors de bookkeeping de energía están
        /// exentos (ver <c>EnemyActionBehavior.IsEnergyBookkeeping</c>).
        /// </summary>
        public readonly HashSet<string> ExecutedActions = new HashSet<string>(StringComparer.Ordinal);

        public bool HasExecuted(string actionKey) =>
            actionKey != null && ExecutedActions.Contains(actionKey);

        public void MarkExecuted(string actionKey)
        {
            if (actionKey != null) ExecutedActions.Add(actionKey);
        }
    }
}
