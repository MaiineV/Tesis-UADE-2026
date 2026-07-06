using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>Cómo se posiciona el panel de tooltip al mostrarse.</summary>
    public enum TooltipPlacementMode
    {
        /// <summary>
        /// Ancla al elemento + offset global del controller, y después se re-posiciona
        /// lo mínimo necesario para que el panel entre COMPLETO en el canvas.
        /// </summary>
        AutoFit = 0,

        /// <summary>
        /// Posición fija relativa a un RectTransform configurado + offset X/Y. No se
        /// clampea: lo que el diseñador configuró es ley.
        /// </summary>
        Fixed = 1,
    }

    /// <summary>
    /// Config de posicionamiento por-trigger, editable en Inspector. La comparten
    /// <see cref="UITooltipTrigger"/> y <see cref="WorldTooltipTrigger"/>.
    /// </summary>
    [Serializable]
    public class TooltipPlacementSettings
    {
        [Tooltip("AutoFit: sigue al elemento y se re-posiciona para entrar completo en " +
                 "pantalla. Fixed: posición fija relativa a un objeto de UI + offset X/Y.")]
        public TooltipPlacementMode Mode = TooltipPlacementMode.AutoFit;

        [ShowIf(nameof(IsFixed))]
        [Tooltip("Objeto de UI respecto al cual se posiciona el tooltip. " +
                 "Null = el RectTransform del propio trigger.")]
        public RectTransform FixedAnchor;

        [ShowIf(nameof(IsFixed))]
        [Tooltip("Offset X/Y en píxeles de referencia del canvas, relativo al anchor " +
                 "(se escala con el CanvasScaler).")]
        public Vector2 FixedOffset;

        private bool IsFixed => Mode == TooltipPlacementMode.Fixed;
    }
}
