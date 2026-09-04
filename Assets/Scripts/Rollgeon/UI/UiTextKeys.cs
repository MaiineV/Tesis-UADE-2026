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

        // Rechazo al clickear un slot de item activo del HUD (ActiveItemsView).
        public const string RejectOnCooldown = "action.reject.cooldown";
        public const string RejectItemUnavailable = "action.reject.item_unavailable";

        // Estados bloqueados de la ficha de item activo (GDD "Ítems Activos" §7).
        public const string RejectNoValidTarget = "action.reject.no_valid_target";
        public const string RejectNoActiveItem = "action.reject.no_active_item";

        // Toast de Segundo Aliento (SecondWindFeedbackView): {0} = item, {1} = HP restante.
        public const string SecondWindTitle = "item.second_wind.title";
        public const string SecondWindBody = "item.second_wind.body";

        // Toast de item roto (ItemBrokeDownFeedbackView): {0} = item. Eco Menguante al llegar a x1.
        public const string ItemBrokeDownTitle = "item.broke_down.title";
        public const string ItemBrokeDownBody = "item.broke_down.body";

        // Toast de oro otorgado por un item (ItemGoldFeedbackView): {0} = item, {1} = oro.
        public const string ItemGoldGrantedBody = "item.gold_granted.body";

        // Ventana de decision de la ficha de item activo: tirada pendiente de aceptar
        // o re-tirar (ActiveItemChipView).
        public const string ActiveItemDecideHint = "hud.active_item.decide_hint";

        /// <summary>Todas las keys de esta clase, para validación en tests.</summary>
        public static IReadOnlyList<string> All { get; } = new[]
        {
            RejectTitle, RejectNoRange, RejectNoRolls, RejectUsed,
            RejectFullHealth, RejectNoDoor, RejectNotYourTurn, RejectNoPotion,
            RejectOnCooldown, RejectItemUnavailable,
            RejectNoValidTarget, RejectNoActiveItem,
            SecondWindTitle, SecondWindBody,
            ItemBrokeDownTitle, ItemBrokeDownBody,
            ItemGoldGrantedBody,
            ActiveItemDecideHint,
        };
    }
}
