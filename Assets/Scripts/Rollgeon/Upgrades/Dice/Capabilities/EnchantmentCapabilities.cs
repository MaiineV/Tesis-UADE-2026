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

    /// <summary>
    /// "Lento": el dado no puede holdearse entre rerolls — siempre vuela. Consumidores:
    /// <c>DiceZoneView.CanChangeHold</c> (gate del toggle) y
    /// <c>CombatHandoffService.ApplyKeepConstraints</c> (fuerza keep=false en el reroll).
    /// </summary>
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

    /// <summary>
    /// Maldición: el encantamiento es un downside puro. La UI pinta el dado con el
    /// visual maldito (negativo + banda oscura) en vez del holo. Consumidor:
    /// <c>DiceEnchantVisualResolver</c>.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class CapCursed : IEnchantmentCapability
    {
    }

    /// <summary>
    /// Queries sobre las capabilities de un encantamiento. Viven del lado dominio para
    /// que futuros consumidores (pricing de tienda, tooltips) no dependan de UI.
    /// </summary>
    public static class EnchantmentCapabilityQueries
    {
        /// <summary><c>true</c> si el encantamiento declara <see cref="CapCursed"/>. Null-safe.</summary>
        public static bool IsCursed(this EnchantmentSO enchantment)
        {
            if (enchantment == null) return false;

            var caps = enchantment.Capabilities;
            if (caps == null) return false;

            // 'is' también filtra entradas null que pueda dejar la autoría.
            for (int i = 0; i < caps.Count; i++)
                if (caps[i] is CapCursed) return true;

            return false;
        }

        /// <summary><c>true</c> si el encantamiento declara una capability de tipo <typeparamref name="T"/>. Null-safe.</summary>
        public static bool HasCapability<T>(this EnchantmentSO enchantment) where T : class, IEnchantmentCapability
        {
            var caps = enchantment?.Capabilities;
            if (caps == null) return false;
            for (int i = 0; i < caps.Count; i++)
                if (caps[i] is T) return true;
            return false;
        }

        /// <summary>
        /// <c>true</c> si ALGÚN encantamiento del dado <paramref name="bagSlot"/> declara
        /// <typeparamref name="T"/>. Es la consulta que hacen los gates de hold/reroll
        /// (Lento). Null-safe y tolerante a índices fuera de rango.
        /// </summary>
        public static bool SlotHasCapability<T>(this RuntimeDiceBag bag, int bagSlot) where T : class, IEnchantmentCapability
        {
            if (bag == null || bagSlot < 0 || bagSlot >= bag.Dice.Count) return false;
            var slots = bag.GetEnchantments(bagSlot);
            if (slots == null) return false;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].HasCapability<T>()) return true;
            return false;
        }

        /// <summary>Atajo con el service del locator: false si el bag no está inicializado.</summary>
        public static bool PlayerSlotHasCapability<T>(int bagSlot) where T : class, IEnchantmentCapability
        {
            return global::Patterns.ServiceLocator.TryGetService<IDiceEnchantmentService>(out var ench)
                   && ench?.Bag != null
                   && ench.Bag.SlotHasCapability<T>(bagSlot);
        }
    }
}
