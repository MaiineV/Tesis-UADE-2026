using System.Collections.Generic;

namespace Rollgeon.UI
{
    /// <summary>
    /// Keys de la String Table <c>UI</c> para el chrome del HUD que se setea por código.
    /// Mismo criterio que <c>TutorialTextKeys</c>: los textos viven en la tabla y las
    /// vistas los resuelven con <c>LocalizedContent.Ui(key, fallbackSerializado)</c>.
    /// </summary>
    public static class UiTextKeys
    {
        // Toast al tocar un chip de acción no usable: título + motivo concreto
        // (ActionRejectToast, resuelto por PlayerActionButtonsView).
        public const string RejectTitle = "action.reject.title";
        public const string RejectNoRange = "action.reject.no_range";
        public const string RejectNoRolls = "action.reject.no_rolls";
        public const string RejectUsed = "action.reject.used";
        public const string RejectFullHealth = "action.reject.full_health";
        public const string RejectNoDoor = "action.reject.no_door";
        public const string RejectNotYourTurn = "action.reject.not_turn";
        public const string RejectNoPotion = "action.reject.no_potion";

        /// <summary>Todas las keys de esta clase, para validación en tests.</summary>
        public static IReadOnlyList<string> All { get; } = new[]
        {
            RejectTitle, RejectNoRange, RejectNoRolls, RejectUsed,
            RejectFullHealth, RejectNoDoor, RejectNotYourTurn, RejectNoPotion,
        };
    }
}
