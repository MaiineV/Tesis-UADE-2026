using UnityEngine;

namespace Rollgeon.UI.HUD.CharacterFrame
{
    /// <summary>Estado visual del marco: qué sprite del anillo corresponde.</summary>
    public enum CharacterFrameVisual
    {
        Normal,
        Hover,
        Pressed,
    }

    /// <summary>
    /// Decisiones puras del marco de personaje — separadas del controller para poder
    /// testearlas en EditMode sin GameObjects (mismo criterio que
    /// <c>InventoryDrawerView.VisibleCells</c>).
    /// </summary>
    public static class CharacterFrameLogic
    {
        /// <summary>Pressed (pinned) gana sobre Hover; sin nada, Normal.</summary>
        public static CharacterFrameVisual Resolve(bool hovered, bool pinned)
        {
            if (pinned) return CharacterFrameVisual.Pressed;
            return hovered ? CharacterFrameVisual.Hover : CharacterFrameVisual.Normal;
        }

        /// <summary>Los íconos se muestran con hover O con pin.</summary>
        public static bool ShouldReveal(bool hovered, bool pinned) => hovered || pinned;

        /// <summary>
        /// Progreso local (0..1) de un elemento cuya animación ocupa la ventana
        /// [<paramref name="start"/>, <paramref name="end"/>] del progreso maestro.
        /// Antes de la ventana vale 0; después, 1.
        /// </summary>
        public static float Window(float progress, float start, float end)
        {
            if (end <= start) return progress >= end ? 1f : 0f;
            return Mathf.Clamp01((progress - start) / (end - start));
        }

        /// <summary>
        /// Rotación Z del anillo para el progreso dado: una vuelta completa horaria al
        /// abrir. El cierre reusa la misma fórmula con el progreso bajando, así que la
        /// ruleta gira sola para el lado contrario.
        /// </summary>
        public static float SpinDegrees(float progress) => -360f * progress;
    }
}
