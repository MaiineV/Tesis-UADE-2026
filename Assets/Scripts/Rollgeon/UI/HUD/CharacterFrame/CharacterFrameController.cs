using System;
using PrimeTween;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.CharacterFrame
{
    /// <summary>
    /// La ruleta del marco de personaje: en reposo solo se ven el marco y la pasiva; al
    /// hover el anillo da una vuelta mientras los íconos de navegación salen del marco, y
    /// un click en el marco los pinnea (<see cref="TogglePin"/>).
    /// </summary>
    /// <remarks>
    /// Vive en el root del cluster y detecta hover por bubbling de uGUI: el puntero sobre
    /// cualquier raycast target descendiente (marco o íconos revelados) cuenta como hover,
    /// así pasar del marco a un ícono no cierra el reveal. La pasiva (StatusRow) quedó
    /// FUERA del cluster a propósito — su hover es de su tooltip, no de la ruleta — pero
    /// este controller la sigue moviendo por referencia para reacomodarla.
    /// <para>
    /// Una sola animación maestra: un tween sobre <c>_progress</c> (0..1) y
    /// <see cref="ApplyProgress"/> escribe rotación del anillo, posición+alpha de cada
    /// ícono (con ventanas de stagger) y la posición de la pasiva. El cierre es el mismo
    /// recorrido al revés, con la ruleta girando para el otro lado, y un retarget a mitad
    /// de animación arranca desde el progreso actual — nunca salta.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Character Frame Controller")]
    public class CharacterFrameController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Serializable]
        public class RevealElement
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Vector2 HiddenPos;
            public Vector2 ShownPos;
            [Range(0f, 1f)] public float WindowStart;
            [Range(0f, 1f)] public float WindowEnd = 1f;
        }

        [Title("Anillo")]
        [SerializeField, Required] private RectTransform _ring;
        [SerializeField, Required] private Image _ringImage;
        [SerializeField, Required] private Sprite _normalSprite;
        [SerializeField, Required] private Sprite _hoverSprite;
        [SerializeField, Required] private Sprite _pressedSprite;

        [Title("Íconos")]
        [SerializeField] private RevealElement[] _icons = Array.Empty<RevealElement>();

        [Title("Pasiva (nunca se oculta, solo se corre)")]
        [SerializeField] private RectTransform _statusRow;
        [SerializeField] private Vector2 _statusRowCollapsedPos;
        [SerializeField] private Vector2 _statusRowExpandedPos;

        [Title("Timing")]
        [SerializeField, MinValue(0f)] private float _openSeconds = 0.45f;
        [SerializeField] private Ease _ease = Ease.OutCubic;
        [Tooltip("Colchón antes de cerrar al perder el hover — cubre los gaps sin raycast " +
                 "target entre el marco y los íconos.")]
        [SerializeField, MinValue(0f)] private float _closeGraceSeconds = 0.18f;

        private bool _hovered;
        private bool _pinned;
        private float _progress;
        private Tween _anim;
        private Tween _closeGrace;

        /// <summary>Íconos fijados por click en el marco.</summary>
        public bool IsPinned => _pinned;

        /// <summary>Los íconos están (o están yendo a) visibles.</summary>
        public bool IsRevealed => CharacterFrameLogic.ShouldReveal(_hovered, _pinned);

        private void Awake()
        {
            // Estado de reposo: el installer autora el prefab ABIERTO para poder
            // inspeccionarlo; acá se snapea a cerrado antes del primer frame.
            SetProgress(CharacterFrameLogic.ShouldReveal(_hovered, _pinned) ? 1f : 0f);
            ApplySprite();
        }

        private void OnDisable()
        {
            CancelCloseGrace();
            if (_anim.isAlive) _anim.Complete();

            // uGUI no dispara PointerExit sobre un GO desactivado — mismo reset que
            // DiceSlotHoverJuice. El pin sí se conserva: es estado del jugador, no del puntero.
            _hovered = false;
            Reevaluate();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            CancelCloseGrace();
            Reevaluate();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            ApplySprite();

            if (_pinned) return;

            // El cierre espera el grace: cruzar un gap entre marco e ícono dispara
            // exit+enter y sin colchón la ruleta parpadearía.
            if (Application.isPlaying && _closeGraceSeconds > 0f && !DiceUiMotionPrefs.ReducedMotion)
            {
                CancelCloseGrace();
                _closeGrace = Tween.Delay(this, _closeGraceSeconds, self => self.Reevaluate());
            }
            else
            {
                Reevaluate();
            }
        }

        /// <summary>Alterna el pin. Lo invoca <see cref="CharacterFramePinRelay"/>.</summary>
        public void TogglePin()
        {
            _pinned = !_pinned;
            CancelCloseGrace();
            Reevaluate();
        }

        // ==================================================================
        // Animación maestra
        // ==================================================================

        private void Reevaluate()
        {
            ApplySprite();

            // Si el hover volvió durante el grace, el estado ya es "abierto" y esto
            // simplemente re-apunta al 1 en el que probablemente ya está.
            AnimateTo(CharacterFrameLogic.ShouldReveal(_hovered, _pinned) ? 1f : 0f);
        }

        private void AnimateTo(float target)
        {
            if (_anim.isAlive) _anim.Stop();
            if (Mathf.Approximately(_progress, target)) return;

            // Duración proporcional al tramo restante: un retarget a mitad de camino no
            // dura una vuelta entera.
            float duration = _openSeconds * Mathf.Abs(target - _progress);
            if (!Application.isPlaying || DiceUiMotionPrefs.ReducedMotion || duration <= 0f)
            {
                SetProgress(target);
                return;
            }

            _anim = Tween.Custom(_progress, target, duration, ease: _ease, useUnscaledTime: true,
                onValueChange: v => SetProgress(v));
        }

        private void SetProgress(float progress)
        {
            _progress = progress;
            ApplyProgress(progress);
        }

        /// <summary>
        /// El ÚNICO escritor del visual animado: rotación del anillo, íconos y pasiva
        /// salen todos del mismo progreso, así que terminan juntos por construcción.
        /// </summary>
        private void ApplyProgress(float progress)
        {
            if (_ring != null)
                _ring.localEulerAngles = new Vector3(0f, 0f, CharacterFrameLogic.SpinDegrees(progress));

            if (_icons != null)
            {
                for (int i = 0; i < _icons.Length; i++)
                {
                    var icon = _icons[i];
                    if (icon == null) continue;

                    float w = CharacterFrameLogic.Window(progress, icon.WindowStart, icon.WindowEnd);
                    if (icon.Rect != null)
                        icon.Rect.anchoredPosition = Vector2.LerpUnclamped(icon.HiddenPos, icon.ShownPos, w);
                    if (icon.Group != null)
                    {
                        icon.Group.alpha = w;
                        // Interactivo recién al estar 100% afuera: sin clicks ni tooltips
                        // fantasma sobre íconos a medio salir.
                        bool on = w >= 0.999f;
                        icon.Group.blocksRaycasts = on;
                        icon.Group.interactable = on;
                    }
                }
            }

            if (_statusRow != null)
                _statusRow.anchoredPosition =
                    Vector2.LerpUnclamped(_statusRowCollapsedPos, _statusRowExpandedPos, progress);
        }

        private void ApplySprite()
        {
            if (_ringImage == null) return;
            switch (CharacterFrameLogic.Resolve(_hovered, _pinned))
            {
                case CharacterFrameVisual.Pressed: _ringImage.sprite = _pressedSprite; break;
                case CharacterFrameVisual.Hover: _ringImage.sprite = _hoverSprite; break;
                default: _ringImage.sprite = _normalSprite; break;
            }
        }

        private void CancelCloseGrace()
        {
            if (_closeGrace.isAlive) _closeGrace.Stop();
        }
    }
}
