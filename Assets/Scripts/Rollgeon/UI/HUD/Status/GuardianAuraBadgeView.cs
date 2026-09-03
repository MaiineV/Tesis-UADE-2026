using System.Collections;
using Rollgeon.Entities.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Badge del aura del Guardian (Support del GDD) junto a la barra de vida del aliado
    /// protegido: ícono + número SUPERPUESTOS en el mismo rect, igual tratamiento que
    /// <see cref="WorldSpaceHealthBar"/> le da al HP — no el badge chico de esquina que usa el
    /// resto de los estados (<see cref="StatusEffectIconView"/>, pensado para contadores de
    /// turno, no para un número que hay que leer de un vistazo).
    /// </summary>
    /// <remarks>
    /// Se cuelga como hijo del propio <c>Canvas</c> de la barra de vida (mismo GameObject que
    /// lleva <see cref="WorldSpaceHealthBar"/>) en vez de armar su propio Canvas/CanvasScaler:
    /// hereda el mismo RenderMode/escala world-space que el número de HP, así el texto sale con
    /// la misma nitidez sin reinventar nada — pedido explícito del usuario ("por qué no hacés lo
    /// mismo que con la vida").
    /// </remarks>
    public sealed class GuardianAuraBadgeView : MonoBehaviour
    {
        /// <summary>Id de catálogo del ícono — mismo que resuelve <see cref="StatusIconCatalogSO"/>.</summary>
        public const string StateId = "status.guardian_aura";

        // Mismo tamaño de rect (world units) que los bloques de LifeFill/HealthText del vecino,
        // para que el badge se lea del mismo porte que el número de vida.
        private static readonly Vector2 BlockSize = new Vector2(0.44f, 0.63f);

        private RectTransform _rect;
        private TextMeshProUGUI _label;

        public static GuardianAuraBadgeView Create(Transform healthBarCanvas, Sprite icon)
        {
            if (healthBarCanvas == null) return null;

            var go = new GameObject("GuardianAuraBadge");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(healthBarCanvas, worldPositionStays: false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            // Pivot bottom, igual que LifeBackground/LifeFill/Frame (no 0.5,0.5): con ese pivot
            // "anchoredPos.y = -0.30" es exactamente el mismo ancla que usa el marco de vida, así
            // el badge queda a la misma altura del frame en vez de colgar más abajo.
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = BlockSize;
            rect.anchoredPosition = new Vector2(BlockSize.x * 1.35f, -0.30f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;

            var iconGo = new GameObject("Icon");
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.SetParent(rect, worldPositionStays: false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.sizeDelta = Vector2.zero;
            iconRect.anchoredPosition = Vector2.zero;
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.enabled = icon != null;

            // Mismo stretch (0,0)-(1,1) que el ícono, no un ancla/pivot propio: así el número
            // queda garantizado centrado ENCIMA del escudo, sin depender de que dos convenciones
            // de ancla distintas coincidan por casualidad (el bug de "separados" era esto).
            var textGo = new GameObject("Number");
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.SetParent(rect, worldPositionStays: false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            var label = textGo.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = false;
            label.fontSize = 0.35f; // mismo tamaño (world units) que HealthText del vecino
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.outlineWidth = 0.25f;
            label.outlineColor = Color.black;
            label.raycastTarget = false;

            // Misma tipografía que el número de vida, para que lean como la misma familia visual.
            var hpText = healthBarCanvas.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
            if (hpText != null && hpText.font != null) label.font = hpText.font;

            var view = go.AddComponent<GuardianAuraBadgeView>();
            view._rect = rect;
            view._label = label;

            go.SetActive(false);
            return view;
        }

        public void Show(int amount)
        {
            if (_label != null) _label.text = amount.ToString();
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>Punch de escala en el instante exacto del golpe reducido.</summary>
        public void Pulse()
        {
            if (!gameObject.activeInHierarchy) return;
            StartCoroutine(PulseRoutine());
        }

        // Punch chico y simétrico: sube 35% y vuelve, 0.12s por tramo — perceptible sin
        // interrumpir la lectura del número, que no cambia durante el pulso.
        private IEnumerator PulseRoutine()
        {
            if (_rect == null) yield break;
            var baseScale = _rect.localScale;
            var peak = baseScale * 1.35f;
            const float half = 0.12f;

            float t = 0f;
            while (t < half)
            {
                if (_rect == null) yield break;
                t += Time.deltaTime;
                _rect.localScale = Vector3.Lerp(baseScale, peak, t / half);
                yield return null;
            }

            t = 0f;
            while (t < half)
            {
                if (_rect == null) yield break;
                t += Time.deltaTime;
                _rect.localScale = Vector3.Lerp(peak, baseScale, t / half);
                yield return null;
            }

            if (_rect != null) _rect.localScale = baseScale;
        }
    }
}
