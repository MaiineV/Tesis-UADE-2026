using System;
using Rollgeon.Entities;

namespace Rollgeon.PreConditions
{
    /// <summary>
    /// Contexto que recibe cada <see cref="BasePreCondition.Evaluate"/>.
    /// TECHNICAL.md §8.2.
    /// <para>
    /// Shape minimalista — sólo los campos que el catálogo inicial de preconditions
    /// (PCHasIntAttribute, PCHasModifier, PCCurrentPhase, PCEntityInRange, …) va a consultar.
    /// Adicones son no-breaking (plan §4.5 extensión por analogía).
    /// </para>
    /// </summary>
    public class PreConditionContext
    {
        /// <summary>Guid del owner que está evaluando las precondiciones.</summary>
        public Guid OwnerGuid;

        /// <summary>Guid de la entidad rival / contraparte (atacante, defensor, partner de combo).</summary>
        public Guid OpponentGuid;

        /// <summary>Entidad owner — acceso directo para lecturas tipadas sin re-query.</summary>
        public Entity Entity;
    }
}
