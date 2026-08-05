using System;
using Rollgeon.Attributes;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Capacidad DECLARATIVA de un encantamiento: una propiedad estática que los
    /// services consultan (<c>ench.Capabilities.OfType&lt;CapX&gt;()</c>), no una
    /// reacción a eventos. Reemplaza a los triggers legacy <c>[NotYetWired]</c> que
    /// eran no-ops funcionales: su semántica real siempre fue "el roll service /
    /// ContractSheet me consultará algún día".
    /// </summary>
    public interface IEnchantmentCapability
    {
    }

    /// <summary>"Lento": el dado no puede holdearse entre rerolls.</summary>
    [NotYetWired("Marcador nomas: el roll service todavia no consulta esta restriccion al holdear.")]
    [Serializable, HideReferenceObjectPicker]
    public sealed class CapPreventHolding : IEnchantmentCapability
    {
    }

    /// <summary>"Comodín": el dado cuenta como cualquier número para combos.</summary>
    [NotYetWired("ContractSheet todavia no consume el flag de comodin, asi que el dado no matchea como cualquier valor.")]
    [Serializable, HideReferenceObjectPicker]
    public sealed class CapWildcard : IEnchantmentCapability
    {
    }

    /// <summary>"Escalador": el dado cuenta como valor y valor+1 para escaleras.</summary>
    [NotYetWired("ContractSheet todavia no lee valores secundarios, asi que el dado no cuenta como valor+1 para escaleras.")]
    [Serializable, HideReferenceObjectPicker]
    public sealed class CapLadderStep : IEnchantmentCapability
    {
    }

    /// <summary>"Mimético": el dado copia la cara de su último reroll.</summary>
    [NotYetWired("El roll service todavia no expone historial de rerolls, asi que no hay de donde copiar.")]
    [Serializable, HideReferenceObjectPicker]
    public sealed class CapMimeticCopy : IEnchantmentCapability
    {
    }

    /// <summary>"Cargado": una vez por combate, el reroll conserva la cara más alta.</summary>
    [NotYetWired("Solo trackea el 'usado' una-vez-por-combate; el reroll real no esta wireado.")]
    [Serializable, HideReferenceObjectPicker]
    public sealed class CapRerollKeepHighest : IEnchantmentCapability
    {
    }

    /// <summary>
    /// "Torpe": en el turno configurado, al revelarse la mano del jugador, TODA la
    /// mano se relanza sola una vez (gratis, una vez por combate). Consumidor:
    /// <see cref="ForcedRerollCapabilityService"/>.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class CapForceRerollOnTurn : IEnchantmentCapability
    {
        [MinValue(1)]
        public int TriggerOnTurn = 2;
    }

    /// <summary>"Ancla": acumula bonus por turnos consecutivos holdeado, hasta un tope.</summary>
    [NotYetWired("El roll service todavia no expone hold-detection, asi que el contador nunca sube. Configurable pero sin efecto.")]
    [Serializable, HideReferenceObjectPicker]
    public sealed class CapAnchorAccumulate : IEnchantmentCapability
    {
        [MinValue(1)]
        public int MaxAccumulation = 3;
    }
}
