using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Settings visuales del <see cref="InteractionPromptView"/> (prompt inferior de
    /// comprar/interactuar). El view es un overlay autoconstruido por código sin
    /// prefab, así que el sprite de fondo no puede vivir en un campo serializado de
    /// escena — este SO se carga desde <c>Resources</c> (mismo patrón que
    /// <c>CursorBootstrap</c> → <c>Resources/Cursor/CursorSettings</c>).
    /// </summary>
    /// <remarks>
    /// El asset lo crea el installer <c>Rollgeon → Interaction Prompt → Create
    /// Settings</c> en <c>Assets/Resources/UI/InteractionPromptSettings.asset</c>.
    /// Si el asset o el sprite faltan, el view cae al color sólido histórico.
    /// </remarks>
    public sealed class InteractionPromptSettingsSO : ScriptableObject
    {
        /// <summary>Ruta relativa a Resources usada por <c>Resources.Load</c>.</summary>
        public const string ResourcePath = "UI/InteractionPromptSettings";

        [Tooltip("Sprite 9-slice del fondo del panel (UI-Sheet-sheet_7).")]
        public Sprite PanelSprite;

        [Tooltip("Multiplicador de pixelsPerUnit del Image sliced — subirlo achica el grosor visual del borde.")]
        public float PanelPixelsPerUnitMultiplier = 1f;
    }
}
