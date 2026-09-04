using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>APPEND-ONLY: se serializa el int del enum en los assets.</summary>
    public enum ComboFilterMode
    {
        None,
        AnyCombo,
        ComboIds,

        /// <summary>
        /// Cualquier combo válido SALVO los listados. Para items que se rompen en un combo
        /// puntual: Fuente Mágica mueve el dado más alto de N a M, y en Número Mayor
        /// (<c>combo.higher_number</c>) ese dado ES el combo entero.
        /// </summary>
        ExcludeComboIds,
    }

    /// <summary>
    /// Filtro de combo para los triggers genéricos. Solo se consulta en el hook
    /// <c>OnComboMatched</c>; en roll/dice no hay combo y se ignora.
    /// </summary>
    [Serializable]
    public sealed class ComboFilter
    {
        public ComboFilterMode Mode = ComboFilterMode.AnyCombo;

        [ShowIf("@Mode == ComboFilterMode.ComboIds || Mode == ComboFilterMode.ExcludeComboIds")]
        [ValueDropdown("@Rollgeon.Combos.BaseComboSO.GetKnownComboIds()", ExcludeExistingValuesInList = true)]
        [ListDrawerSettings(ShowFoldout = false, DefaultExpandedState = true)]
        public List<string> ComboIds = new List<string>();

        /// <summary>True si el filtro usa la lista <see cref="ComboIds"/> (incluir o excluir).</summary>
        public bool UsesComboIds
            => Mode == ComboFilterMode.ComboIds || Mode == ComboFilterMode.ExcludeComboIds;

        public bool Matches(string comboId)
        {
            switch (Mode)
            {
                case ComboFilterMode.ComboIds:
                    return !string.IsNullOrEmpty(comboId)
                           && ComboIds != null
                           && ComboIds.Contains(comboId);
                case ComboFilterMode.ExcludeComboIds:
                    return !string.IsNullOrEmpty(comboId)
                           && (ComboIds == null || !ComboIds.Contains(comboId));
                // None y AnyCombo: cualquier combo válido. None equivale a AnyCombo cuando
                // el trigger igual está atado al hook de combo.
                default:
                    return !string.IsNullOrEmpty(comboId);
            }
        }
    }
}
