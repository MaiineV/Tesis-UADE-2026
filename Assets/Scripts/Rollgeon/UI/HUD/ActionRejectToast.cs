using PrimeTween;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Aviso flotante "Esta acción no puede ser realizada" + motivo, spawneado
    /// sobre el chip tocado. Armado por código (sin prefab): TMP + CanvasGroup
    /// bajo el canvas raíz del chip, sube y se desvanece, y se destruye solo.
    /// Un toast por vez — tocar otro chip (o el mismo) reemplaza al anterior en
    /// vez de apilar carteles.
    /// </summary>
    public static class ActionRejectToast
    {
        private const float RiseDistancePx = 46f;
        private const float LifeSeconds = 1.6f;
        private const float FadeStartFraction = 0.55f;
        private const float FontSize = 26f;

        private static GameObject _active;

        /// <param name="anchor">Rect del chip — el toast aparece encima suyo.</param>
        /// <param name="text">Texto ya localizado (título + motivo).</param>
        /// <param name="font">Fuente TMP; null = default de TMP. Pasar la del cost
        /// label del chip mantiene el estilo del juego sin cargar assets.</param>
        /// <param name="color">Color del texto; default = blanco.</param>
        public static void Show(RectTransform anchor, string text, TMP_FontAsset font, Color? color = null)
        {
            if (anchor == null || string.IsNullOrEmpty(text)) return;

            var canvas = anchor.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            // MISMO root que el tooltip, no el del chip: el overrideSorting de un canvas
            // anidado solo compite dentro de su root — colgado del root del chip (order 0)
            // el tooltip (root 60) lo tapaba por más 30020 que tuviera el toast.
            var rootCanvas = Rollgeon.UI.Tooltips.TooltipController.OverlayRootCanvas;
            if (rootCanvas == null) rootCanvas = canvas.rootCanvas;

            if (_active != null) Object.Destroy(_active);

            var go = new GameObject("[ActionRejectToast]", typeof(RectTransform), typeof(CanvasGroup));
            _active = go;
            var rect = (RectTransform)go.transform;
            rect.SetParent(rootCanvas.transform, worldPositionStays: false);
            rect.SetAsLastSibling();

            // Canvas propio POR ENCIMA del tooltip (30000): el hover del chip abre su
            // tooltip a la vez que el toast, y el motivo de "no podés" tapado por la
            // tooltip era ilegible. +20: también sobre la burbuja de estados (30001).
            var toastCanvas = go.AddComponent<Canvas>();
            toastCanvas.overrideSorting = true;
            toastCanvas.sortingOrder = Rollgeon.UI.Tooltips.TooltipController.OverlaySortingOrder + 20;

            // Encima del chip: borde superior del rect del chip + un colchón, en
            // píxeles de pantalla (ScreenSpaceOverlay: world == píxeles).
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            float topY = Mathf.Max(corners[1].y, corners[2].y);
            float centerX = (corners[0].x + corners[3].x) * 0.5f;
            rect.position = new Vector3(centerX, topY + 24f, 0f);
            rect.sizeDelta = new Vector2(480f, 80f);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            if (font != null) label.font = font;
            label.fontSize = FontSize;
            label.color = color ?? Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.text = text;
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color32(0, 0, 0, 220);

            var group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            if (!Application.isPlaying || DiceAnim.DiceUiMotionPrefs.ReducedMotion)
            {
                // Sin tweens: queda visible el tiempo de vida y se destruye.
                Object.Destroy(go, LifeSeconds);
                return;
            }

            Tween.Position(rect, rect.position + new Vector3(0f, RiseDistancePx, 0f),
                LifeSeconds, Ease.OutCubic, useUnscaledTime: true);
            // El toast se reemplaza a sí mismo (tocar otro chip destruye el anterior con
            // el fade a medio correr) y muere con el canvas al terminar el combate. En los
            // dos casos el target se destruye con el callback pendiente, y PrimeTween lo
            // loguea como error. Acá es cosmético: el callback solo destruye lo que ya se
            // está destruyendo, así que se silencia el aviso para este tween puntual.
            Tween.Alpha(group, 0f, LifeSeconds * (1f - FadeStartFraction), Ease.InQuad,
                    startDelay: LifeSeconds * FadeStartFraction, useUnscaledTime: true)
                .OnComplete(go, t => Object.Destroy(t), warnIfTargetDestroyed: false);
        }
    }
}
