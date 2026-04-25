using System;
using Patterns;
using Rollgeon.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// Chequea si el combo identificado por <see cref="ComboId"/> está disponible
    /// en el <c>ContractSheet</c> del hero activo (existe en la lista y no está
    /// tachado). TECHNICAL.md §8.2.
    /// <para>
    /// Si no hay <see cref="IPlayerService"/> registrado, hero null o sheet null,
    /// evalúa <c>false</c>.
    /// </para>
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class PCComboAvailable : BasePreCondition
    {
        [Required]
        [Tooltip("Id estable del combo (BaseComboSO.ComboId) — debe coincidir con el del catalog.")]
        public string ComboId;

        public override string ConditionName =>
            string.IsNullOrEmpty(ComboId) ? "ComboAvailable(<unset>)" : $"ComboAvailable({ComboId})";

        public override bool Evaluate(PreConditionContext context)
        {
            if (string.IsNullOrEmpty(ComboId)) return false;
            if (!ServiceLocator.TryGetService<IPlayerService>(out var player)) return false;

            var sheet = player.CurrentHero != null ? player.CurrentHero.Sheet : null;
            if (sheet == null || sheet.Combos == null) return false;

            for (int i = 0; i < sheet.Combos.Count; i++)
            {
                var combo = sheet.Combos[i];
                if (combo == null) continue;
                if (combo.ComboId != ComboId) continue;
                return !sheet.IsCrossed(combo);
            }

            return false;
        }
    }
}
