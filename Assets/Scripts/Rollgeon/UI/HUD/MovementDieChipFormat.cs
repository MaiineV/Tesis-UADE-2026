using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Texto del chip de item sobre el dado de Movimiento: "Botas Ligeras +1". El monto va
    /// con rich-text de color (positivo/negativo) y el nombre hereda el color del chip,
    /// igual que las entradas del breakdown de daño.
    /// </summary>
    public static class MovementDieChipFormat
    {
        public static string Label(string displayName, int delta, Color positive, Color negative)
        {
            string amount = delta >= 0 ? "+" + delta : delta.ToString();
            string hex = ColorUtility.ToHtmlStringRGB(delta >= 0 ? positive : negative);
            return string.IsNullOrEmpty(displayName)
                ? $"<color=#{hex}>{amount}</color>"
                : $"{displayName} <color=#{hex}>{amount}</color>";
        }
    }
}
