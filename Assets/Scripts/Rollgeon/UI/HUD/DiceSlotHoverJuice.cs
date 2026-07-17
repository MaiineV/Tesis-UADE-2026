using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Hover feedback en un slot de dado lockeable: comunica "esto es clickeable"
    /// antes del primer click. Gateado por el <see cref="Button"/> del slot — un dado
    /// girando, bloqueado por boss o sin botón no reacciona. Players opcionales
    /// (scale sutil del root: el hover solo ocurre con el dado interactable, es decir
    /// fuera del spin/outro donde la motion toca el root).
    /// </summary>
    /// <remarks>
    /// Es también el dueño del ESTADO de hover del slot: le avisa a
    /// <see cref="DiceSlotView"/> para que pinte el sprite de hover del set. El estado vive
    /// acá y no allá porque acá está el gate correcto (dado interactable) y el reset del
    /// <see cref="OnDisable"/> — que no es opcional: uGUI no dispara
    /// <c>OnPointerExit</c> sobre un GameObject que se desactiva (<c>ExecuteEvents</c> sale
    /// temprano si <c>!activeInHierarchy</c>), y <c>DiceZoneView.ClearAll</c> apaga los slots.
    /// Sin este reset, confirmar con el mouse encima de un dado lo dejaría con el sprite de
    /// hover latcheado hasta el próximo enter.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Dice Slot Hover Juice")]
    public sealed class DiceSlotHoverJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Optional, Tooltip("Scale-up sutil al entrar (≈1.05 en 0.08s).")]
        private MMF_Player _hoverEnterPlayer;

        [SerializeField, Optional, Tooltip("Vuelta a reposo al salir.")]
        private MMF_Player _hoverExitPlayer;

        private Button _button;
        private DiceSlotView _view;
        private bool _hovering;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _view = GetComponent<DiceSlotView>();
        }

        private void OnEnable()
        {
            MmfJuice.CaptureRestPose(_hoverEnterPlayer);
            MmfJuice.CaptureRestPose(_hoverExitPlayer);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button == null || !_button.interactable) return;
            _hovering = true;
            _view?.SetHovered(true);
            Play(_hoverExitPlayer, stopOnly: true);
            Play(_hoverEnterPlayer);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_hovering) return;
            _hovering = false;
            _view?.SetHovered(false);
            Play(_hoverEnterPlayer, stopOnly: true);
            Play(_hoverExitPlayer);
        }

        private void OnDisable()
        {
            _hovering = false;
            _view?.SetHovered(false);
            Play(_hoverEnterPlayer, stopOnly: true);
            Play(_hoverExitPlayer, stopOnly: true);
        }

        private static void Play(MMF_Player player, bool stopOnly = false)
        {
            if (stopOnly) MmfJuice.Rest(player);
            else MmfJuice.Replay(player);
        }
    }
}
