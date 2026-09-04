using System;
using System.Collections.Generic;
using Rollgeon.Combos;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>APPEND-ONLY: se serializa el int del enum en los assets.</summary>
    public enum ComboFilterMode
    {
        /// <summary>Sin filtro autorado. Equivale a <see cref="AnyCombo"/>.</summary>
        None,

        /// <summary>
        /// Cualquier combo REAL. Número Alto (<c>combo.higher_number</c>) matchea cualquier
        /// selección no vacía, así que no cuenta como combo para condiciones (decisión GD
        /// 2026-09-04): "cuando participa en un combo" con Número Alto adentro era "siempre".
        /// </summary>
        AnyCombo,

        ComboIds,

        /// <summary>
        /// Cualquier combo válido SALVO los listados. Para items que se rompen en un combo
        /// puntual: Fuente Mágica mueve el dado más alto de N a M, y en Número Mayor
        /// (<c>combo.higher_number</c>) ese dado ES el combo entero.
        /// </summary>
        ExcludeComboIds,

        /// <summary>
        /// Cualquier selección jugable, Número Alto incluido. Para mutaciones de cara que son
        /// una propiedad del dado y no una condición de combo (Invertido, Enfiestado, Frágil).
        /// Oxidado y Volátil NO van acá: por decisión GD (2026-09-04) solo mutan cuando el dado
        /// participa de un combo real — en Número Alto valen su cara.
        /// </summary>
        AnyIncludingHigherNumber,
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

        /// <summary>
        /// El combo fallback: matchea cualquier selección no vacía y contribuye un solo dado,
        /// así que como "condición de combo" no dice nada. Solo entra por
        /// <see cref="ComboFilterMode.AnyIncludingHigherNumber"/> o listado explícito.
        /// </summary>
        public static bool IsFallbackCombo(string comboId) => comboId == ComboId.HigherNumber;

        public bool Matches(string comboId)
        {
            if (string.IsNullOrEmpty(comboId)) return false;

            switch (Mode)
            {
                case ComboFilterMode.ComboIds:
                    return ComboIds != null && ComboIds.Contains(comboId);
                case ComboFilterMode.ExcludeComboIds:
                    return !IsFallbackCombo(comboId) && (ComboIds == null || !ComboIds.Contains(comboId));
                case ComboFilterMode.AnyIncludingHigherNumber:
                    return true;
                // None y AnyCombo: cualquier combo real. None equivale a AnyCombo cuando el
                // trigger igual está atado al hook de combo.
                default:
                    return !IsFallbackCombo(comboId);
            }
        }
    }
}
