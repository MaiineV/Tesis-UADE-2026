using System;
using Rollgeon.Attributes;
using Rollgeon.Effects;
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

        /// <summary>
        /// <c>EffectContext</c> del evento que disparó la evaluación. Lo populan los
        /// bridges de triggers (encantamientos / pasivas / items) para que las PCs
        /// dependientes del roll/combo (<c>PcNoComboThisRoll</c>, <c>PcCarrierFace</c>,
        /// <c>PcSlotCounterCompare</c>) lean DiceResult / ComboResult / TriggerContext.
        /// <c>null</c> en callers que no vienen de un trigger — cada PC define si eso
        /// es permisivo (true) o veto (false) según su semántica de gating.
        /// </summary>
        public EffectContext Effect;

        /// <summary>
        /// Consulta si una <c>ActionKey</c> (<see cref="Rollgeon.Combat.AI.Decisions.AINode_Move.ActionKey"/>
        /// y análogos) ya se ejecutó este turno — delega a <c>AIContext.HasExecuted</c>. Lo
        /// popula el bridge AI (<c>AIContextPcExtensions.BuildPcContext</c>); <c>null</c> en
        /// otros callers. Las PCs interesadas (<c>PcActionExecuted</c>) deben tolerar null
        /// permisivamente.
        /// </summary>
        public Func<string, bool> HasExecutedAction;
    }
}
