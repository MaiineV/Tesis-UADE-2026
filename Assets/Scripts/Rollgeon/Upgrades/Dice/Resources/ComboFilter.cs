using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice
{
    public enum ComboFilterMode
    {
        None,
        AnyCombo,
        ComboIds,
    }

    /// <summary>
    /// Filtro de combo para los triggers genéricos. Solo se consulta en el hook
    /// <c>OnComboMatched</c>; en roll/dice no hay combo y se ignora.
    /// </summary>
    [Serializable]
    public sealed class ComboFilter
    {
        public ComboFilterMode Mode = ComboFilterMode.AnyCombo;

        [ShowIf(nameof(Mode), ComboFilterMode.ComboIds)]
        [ListDrawerSettings(ShowFoldout = false, DefaultExpandedState = true)]
        public List<string> ComboIds = new List<string>();

        public bool Matches(string comboId)
        {
            switch (Mode)
            {
                case ComboFilterMode.ComboIds:
                    return !string.IsNullOrEmpty(comboId)
                           && ComboIds != null
                           && ComboIds.Contains(comboId);
                // None y AnyCombo: cualquier combo válido. None equivale a AnyCombo cuando
                // el trigger igual está atado al hook de combo.
                default:
                    return !string.IsNullOrEmpty(comboId);
            }
        }
    }
}
