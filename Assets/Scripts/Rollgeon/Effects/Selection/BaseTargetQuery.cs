using System;
using System.Collections.Generic;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.Phase;
using Sirenix.OdinInspector;

namespace Rollgeon.Effects.Selection
{
    /// <summary>
    /// Base polimórfica de las queries que resuelven targets lógicos. TECHNICAL.md §11.2b.
    /// <para>
    /// Serializada inline dentro de <see cref="SelectionSettings"/> via
    /// <c>[OdinSerialize, SerializeReference]</c>. El contrato de serialización polimórfica
    /// (§13.6.1) exige la terna: base abstract + <c>[Serializable]</c> en cada subtipo +
    /// <c>[SerializeReference]</c> en el campo contenedor.
    /// </para>
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public abstract class BaseTargetQuery
    {
        /// <summary>Nombre visible en inspector / logs.</summary>
        public abstract string QueryName { get; }

        /// <summary>
        /// Calcula la lista de targets válidos para el contexto. Métodos síncronos —
        /// queries que requieren I/O async deben cachear (no es caso en esta foundation).
        /// </summary>
        public abstract List<TargetRef> Evaluate(TargetQueryContext context);
    }

    /// <summary>
    /// Contexto que recibe <see cref="BaseTargetQuery.Evaluate"/>. Construido por el caller
    /// (selection controller o el propio <c>ApplyEffect</c> cuando <see cref="SelectionTiming.DuringResolve"/>).
    /// </summary>
    public class TargetQueryContext
    {
        public Guid OwnerGuid;
        public Entity OwnerEntity;
        public GridCoord OwnerPosition;
        public GamePhase CurrentPhase;
    }
}
