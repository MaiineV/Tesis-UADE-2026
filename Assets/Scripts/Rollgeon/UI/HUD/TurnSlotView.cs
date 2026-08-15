using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Widget de un slot individual en la <see cref="TurnQueueView"/>. Representa a
    /// un actor del round (player o enemy) con su portrait, numero de orden y dos
    /// overlays opcionales (activo / destruido).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Plan §3.2 / §4.3. Sin suscripciones a eventos — el <see cref="TurnQueueView"/>
    /// maneja el bus y llama a los setters publicos de este slot.
    /// </para>
    /// <para>
    /// <b>Prefab setup</b>: Image (portrait) + TMP (label) + 2 GameObject children
    /// (ActiveHighlight, DestroyedOverlay). Ver setup doc §8.2.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Turn Slot View")]
    public class TurnSlotView : MonoBehaviour
    {
        [Title("Turn Slot — Widget refs")]
        [SerializeField]
        [Tooltip("Portrait del actor. Se setea via SetPortrait(Sprite).")]
        private Image _portrait;

        [SerializeField]
        [Tooltip("Label opcional con el orden (1,2,3). Se setea en Bind.")]
        private TextMeshProUGUI _label;

        [SerializeField]
        [Tooltip("Overlay que se muestra cuando este actor tiene el turno.")]
        private GameObject _activeHighlight;

        [SerializeField]
        [Tooltip("Overlay que se muestra cuando el actor fue destruido mid-round.")]
        private GameObject _destroyedOverlay;

        [Title("Turn Slot — Frame (borde/fondo por estado)")]
        [SerializeField]
        [Tooltip("Image del borde/fondo detrás del portrait. Los cablea el installer " +
                 "Rollgeon → Turn Queue → Setup Frames.")]
        private Image _frame;

        [SerializeField, Tooltip("UI-Sheet-sheet_10 — player y enemigos comunes sin el turno.")]
        private Sprite _frameIdle;

        [SerializeField, Tooltip("UI-Sheet-sheet_11 — player con el turno activo.")]
        private Sprite _framePlayerActive;

        [SerializeField, Tooltip("UI-Sheet-sheet_15 — enemigo común con el turno activo.")]
        private Sprite _frameEnemyActive;

        [SerializeField, Tooltip("UI-Sheet-sheet_16 — boss sin el turno.")]
        private Sprite _frameBossIdle;

        [SerializeField, Tooltip("UI-Sheet-sheet_12 — boss con el turno activo.")]
        private Sprite _frameBossActive;

        [ShowInInspector, ReadOnly]
        private Guid _slotGuid;

        [ShowInInspector, ReadOnly]
        private bool _isPlayer;

        [ShowInInspector, ReadOnly]
        private bool _isBoss;

        [ShowInInspector, ReadOnly]
        private int _displayIndex;

        private CanvasGroup _canvasGroup;

        /// <summary>Guid del actor que este slot representa.</summary>
        public Guid SlotGuid => _slotGuid;

        /// <summary><c>true</c> si el slot es del player (marker visual opcional).</summary>
        public bool IsPlayer => _isPlayer;

        /// <summary>RectTransform del slot — el carrusel lo posiciona/escala a mano.</summary>
        public RectTransform Rect => (RectTransform)transform;

        /// <summary>
        /// CanvasGroup del root, para fades y dimming del carrusel. Se agrega por
        /// código si el prefab no lo trae (los slots viejos no lo tenían).
        /// </summary>
        public CanvasGroup Group
        {
            get
            {
                if (_canvasGroup == null)
                {
                    _canvasGroup = GetComponent<CanvasGroup>();
                    if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
                return _canvasGroup;
            }
        }

        /// <summary>
        /// Popula el slot. Reset de overlays (ambos hidden) + asignacion de label.
        /// </summary>
        public void Bind(Guid slotGuid, bool isPlayer, int displayIndex, bool isBoss = false)
        {
            _slotGuid = slotGuid;
            _isPlayer = isPlayer;
            _isBoss = isBoss;
            _displayIndex = displayIndex;

            if (_label != null)
            {
                // Mostramos 1-based para humanos; displayIndex ya viene 0-based del caller.
                _label.text = (displayIndex + 1).ToString();
            }

            SetActive(false);
            SetDestroyed(false);
        }

        /// <summary>
        /// Setea el texto del label de orden. Vacío/null lo oculta — en el carrusel
        /// centrado los números fijos 1..N ya no aplican y solo los próximos llevan
        /// su orden relativo (+1/+2).
        /// </summary>
        public void SetLabel(string text)
        {
            if (_label == null) return;
            bool visible = !string.IsNullOrEmpty(text);
            _label.text = visible ? text : string.Empty;
            _label.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Togglea el overlay "actor activo" + el frame del slot. Sin tintes de
        /// color — el estado se comunica con los sprites de borde/fondo del sheet.
        /// </summary>
        public void SetActive(bool isActive)
        {
            if (_activeHighlight != null)
            {
                _activeHighlight.SetActive(isActive);
            }
            ApplyFrame(isActive);
        }

        private void ApplyFrame(bool isActive)
        {
            if (_frame == null) return;
            var sprite = ResolveFrame(isActive);
            if (sprite != null) _frame.sprite = sprite;
        }

        // Mapa de frames: player/enemigo común comparten el idle (_10); el boss
        // tiene idle propio (_16); activos: player _11, enemigo _15, boss _12.
        private Sprite ResolveFrame(bool isActive)
        {
            if (_isBoss) return isActive ? _frameBossActive : _frameBossIdle;
            if (!isActive) return _frameIdle;
            return _isPlayer ? _framePlayerActive : _frameEnemyActive;
        }

        /// <summary>Togglea el overlay "destruido".</summary>
        public void SetDestroyed(bool destroyed)
        {
            if (_destroyedOverlay != null)
            {
                _destroyedOverlay.SetActive(destroyed);
            }
        }

        private Sprite _defaultPortrait;
        private bool _defaultPortraitCaptured;

        /// <summary>
        /// Setea el portrait. Lo llama <see cref="TurnQueueView"/> resolviendo
        /// <c>IEntityPortraitResolver</c>. Con <c>null</c> restaura el sprite default
        /// del prefab — necesario ahora que el carrusel recicla slots entre actores.
        /// </summary>
        public void SetPortrait(Sprite portrait)
        {
            if (_portrait == null) return;

            if (!_defaultPortraitCaptured)
            {
                _defaultPortrait = _portrait.sprite;
                _defaultPortraitCaptured = true;
            }

            _portrait.sprite = portrait != null ? portrait : _defaultPortrait;
        }
    }
}
