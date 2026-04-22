using System;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Efecto programático que cruza al player por una puerta hacia la sala
    /// vecina via <see cref="IDungeonService.EnterRoomByDoor"/>. TECHNICAL.md §13.6.
    /// <para>
    /// Uso típico: wirear en un behavior del <c>InteractableComponent</c> del
    /// prefab de puerta (§7.7) — el behavior asigna <see cref="Direction"/> a
    /// match del <see cref="DoorController.Direction"/> en la puerta dueña. La
    /// spec §13.6 lista <c>EffLeaveRoom</c> como parte de la cadena Combat
    /// (<c>EffSpendEnergy</c> + <c>EffRollSkillCheck</c> + <c>EffConditional</c>)
    /// y Exploration (auto al pisar tile adyacente al Anchor).
    /// </para>
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffLeaveRoom : BaseEffect
    {
        [Title("Leave Room")]
        [InfoBox("Dirección de la puerta contra la sala actual. El behavior que " +
                 "cable a este effect debe setear Direction al DoorController.Direction " +
                 "del prefab en el que vive.")]
        public DoorDirection Direction = DoorDirection.North;

        public override string GetEffectName() => "Leave Room";

        public override bool ApplyEffect(EffectContext context)
        {
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null)
            {
                Debug.LogWarning("[EffLeaveRoom] IDungeonService no registrado — no-op.");
                return false;
            }

            return dungeon.EnterRoomByDoor(Direction);
        }
    }
}
