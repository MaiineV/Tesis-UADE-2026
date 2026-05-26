using System;
using Rollgeon.Attributes;
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

        /// <summary>
        /// Round actual del combate (1-based). <c>null</c> si el caller no lo provee — las
        /// PCs que dependen de round (<c>PcRoundNumber</c>) deben tolerarlo con semántica
        /// permisiva ("no lo sabemos → no decimos que false") y devolver true.
        /// </summary>
        public int? RoundIndex;

        /// <summary>
        /// HP máximo de referencia del owner. <c>null</c> si el caller no lo provee — las
        /// PCs interesadas (<c>PcOwnerHpBelow</c>) caen al lookup del registro/AttributesManager.
        /// </summary>
        public int? OwnerMaxHp;

        /// <summary>
        /// AttributesManager para lectura directa de stats del owner. Lo popula el bridge
        /// AI (<c>AIContextPcExtensions.BuildPcContext</c>); <c>null</c> en otros callers
        /// (hero UI, effects pipeline). Las PCs interesadas (<c>PcOwnerStatCompare</c>)
        /// deben tolerar null permisivamente — semántica "sin servicio → no veta".
        /// </summary>
        public AttributesManager Attributes;
    }
}
