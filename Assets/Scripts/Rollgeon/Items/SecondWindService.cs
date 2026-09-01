using System;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Items
{
    /// <summary>
    /// "Sello del Segundo Aliento" (GDD): si el jugador llegaría a 0 HP y tiene un item
    /// pasivo con <see cref="ItemSO.SecondWind"/>, queda con
    /// <see cref="ItemSO.SecondWindRemainingHp"/> (1) en vez de morir y el item se
    /// consume. Implementa <see cref="ILethalDamageOverride"/> — el mismo seam del
    /// tutorial, ahora con resto de vida propio y consumo one-shot.
    /// </summary>
    /// <remarks>
    /// El consumo (remover el item del inventario) ES la carga única por run, y
    /// persiste gratis: el item ya no está en el save. Registrado Run-scoped SIEMPRE
    /// (no-op sin item); en el tutorial, <c>TutorialInvulnerabilityService</c> se
    /// registra después bajo la misma key y gana — correcto, ahí no hay items.
    /// Cubre todos los caminos reales de muerte (pipeline: golpes, poison, hazards,
    /// casillas); un write directo de Health negativo saltea el pipeline por diseño.
    /// </remarks>
    public sealed class SecondWindService : ILethalDamageOverride
    {
        public bool ShouldPreventLethal(Guid targetId) => FindSecondWindItem(targetId) != null;

        public int GetRemainingHp(Guid targetId)
        {
            var item = FindSecondWindItem(targetId);
            return item != null ? Mathf.Max(1, item.SecondWindRemainingHp) : 1;
        }

        public void NotifyLethalPrevented(Guid targetId)
        {
            var item = FindSecondWindItem(targetId);
            if (item == null) return;
            if (ServiceLocator.TryGetService<IInventoryService>(out var inv) && inv != null)
            {
                inv.RemoveItem(item.ItemId);
                Debug.Log($"[SecondWindService] '{item.ItemId}' consumido — el jugador quedó " +
                          $"en {Mathf.Max(1, item.SecondWindRemainingHp)} HP en vez de morir.");
            }
        }

        private static ItemSO FindSecondWindItem(Guid targetId)
        {
            if (targetId == Guid.Empty) return null;
            if (!ServiceLocator.TryGetService<IPlayerService>(out var ps) || ps == null
                || ps.PlayerGuid != targetId)
                return null;
            if (!ServiceLocator.TryGetService<IInventoryService>(out var inv) || inv == null)
                return null;

            var passives = inv.PassiveItems;
            for (int i = 0; i < passives.Count; i++)
            {
                var item = passives[i]?.Item;
                if (item != null && item.SecondWind) return item;
            }
            return null;
        }
    }
}
