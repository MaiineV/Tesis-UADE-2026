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

        [Title("Turn Slot — Cosmetics")]
        [SerializeField]
        [Tooltip("Color del highlight cuando el actor esta activo. Tinta el portrait.")]
        private Color _highlightColor = new Color(1f, 0.87f, 0.27f, 1f);

        [SerializeField]
        [Tooltip("Color del portrait cuando el actor NO esta activo.")]
        private Color _idleColor = Color.white;

        [ShowInInspector, ReadOnly]
        private Guid _slotGuid;

        [ShowInInspector, ReadOnly]
        private bool _isPlayer;

        [ShowInInspector, ReadOnly]
        private int _displayIndex;

        /// <summary>Guid del actor que este slot representa.</summary>
        public Guid SlotGuid => _slotGuid;

        /// <summary><c>true</c> si el slot es del player (marker visual opcional).</summary>
        public bool IsPlayer => _isPlayer;

        /// <summary>
        /// Popula el slot. Reset de overlays (ambos hidden) + asignacion de label.
        /// </summary>
        public void Bind(Guid slotGuid, bool isPlayer, int displayIndex)
        {
            _slotGuid = slotGuid;
            _isPlayer = isPlayer;
            _displayIndex = displayIndex;

            if (_label != null)
            {
                // Mostramos 1-based para humanos; displayIndex ya viene 0-based del caller.
                _label.text = (displayIndex + 1).ToString();
            }

            SetActive(false);
            SetDestroyed(false);
        }

        /// <summary>Togglea el overlay "actor activo" + color del portrait.</summary>
        public void SetActive(bool isActive)
        {
            if (_activeHighlight != null)
            {
                _activeHighlight.SetActive(isActive);
            }
            if (_portrait != null)
            {
                _portrait.color = isActive ? _highlightColor : _idleColor;
            }
        }

        /// <summary>Togglea el overlay "destruido".</summary>
        public void SetDestroyed(bool destroyed)
        {
            if (_destroyedOverlay != null)
            {
                _destroyedOverlay.SetActive(destroyed);
            }
        }

        /// <summary>Hook opcional para que un futuro <c>IEntityPortraitResolver</c> setee el sprite.</summary>
        public void SetPortrait(Sprite portrait)
        {
            if (_portrait != null && portrait != null)
            {
                _portrait.sprite = portrait;
            }
        }
    }
}
