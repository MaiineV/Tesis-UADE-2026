using System;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Exploration;
using Rollgeon.UI.Screens;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view del HUD que muestra informacion de la room actual y permite
    /// avanzar a la siguiente. Se suscribe a eventos de exploracion en
    /// <see cref="Bind"/> y actualiza labels + botones.
    /// </summary>
    /// <remarks>
    /// Plan §4 / UI#0011d. Sin <c>Update()</c> — pura reaccion a eventos.
    /// El boton de pausa es stub hasta UI#0014c.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Room Navigation View")]
    public class RoomNavigationView : MonoBehaviour
    {
        private const string LogPrefix = "[RoomNavigationView] ";

        [Title("Room Navigation — Widget refs")]
        [Required("Arrastrar el TextMeshProUGUI del nombre de room.")]
        [SerializeField]
        private TextMeshProUGUI _roomNameLabel;

        [Required("Arrastrar el TextMeshProUGUI del progreso (Room X/Y).")]
        [SerializeField]
        private TextMeshProUGUI _roomProgressLabel;

        [Required("Arrastrar el TextMeshProUGUI del tipo de room.")]
        [SerializeField]
        private TextMeshProUGUI _roomTypeLabel;

        [Required("Arrastrar el Button de avanzar room.")]
        [SerializeField]
        private Button _proceedButton;

        [Required("Arrastrar el Button de pausa.")]
        [SerializeField]
        private Button _pauseButton;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private IDungeonService _dungeon;
        private IExplorationController _exploration;

        /// <summary>
        /// Engancha la sub-view al jugador. Suscribe handlers al bus y
        /// refresca la UI. Idempotente: si ya estaba bound, primero hace
        /// <see cref="Unbind"/>.
        /// </summary>
        public void Bind(Guid playerGuid)
        {
            if (_bound)
            {
                Unbind();
            }

            if (ServiceLocator.TryGetService<IDungeonService>(out var dungeon))
            {
                _dungeon = dungeon;
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IDungeonService no registrado. Labels en fallback.", this);
            }

            if (ServiceLocator.TryGetService<IExplorationController>(out var exploration))
            {
                _exploration = exploration;
            }
            else
            {
                Debug.LogWarning(LogPrefix + "IExplorationController no registrado. Proceed deshabilitado.", this);
            }

            EventManager.Subscribe(EventName.OnRoomEntered, OnRoomEnteredHandler);
            EventManager.Subscribe(EventName.OnCombatTriggered, OnCombatTriggeredHandler);
            EventManager.Subscribe(EventName.OnFloorCleared, OnFloorClearedHandler);
            EventManager.Subscribe(EventName.OnExplorationStarted, OnExplorationStartedHandler);

            if (_proceedButton != null) _proceedButton.onClick.AddListener(OnProceedClicked);
            if (_pauseButton != null) _pauseButton.onClick.AddListener(OnPauseClicked);

            RefreshRoomInfo();
            _bound = true;
        }

        /// <summary>Desuscribe del bus y limpia estado. Idempotente.</summary>
        public void Unbind()
        {
            if (!_bound) return;

            EventManager.UnSubscribe(EventName.OnRoomEntered, OnRoomEnteredHandler);
            EventManager.UnSubscribe(EventName.OnCombatTriggered, OnCombatTriggeredHandler);
            EventManager.UnSubscribe(EventName.OnFloorCleared, OnFloorClearedHandler);
            EventManager.UnSubscribe(EventName.OnExplorationStarted, OnExplorationStartedHandler);

            if (_proceedButton != null) _proceedButton.onClick.RemoveListener(OnProceedClicked);
            if (_pauseButton != null) _pauseButton.onClick.RemoveListener(OnPauseClicked);

            _dungeon = null;
            _exploration = null;
            _bound = false;
        }

        /// <summary>
        /// Actualiza labels y estado del boton a partir de <see cref="IDungeonService"/>.
        /// Si el servicio no esta disponible, muestra textos de fallback.
        /// </summary>
        public void RefreshRoomInfo()
        {
            if (_dungeon == null)
            {
                if (_roomNameLabel != null) _roomNameLabel.text = "???";
                if (_roomProgressLabel != null) _roomProgressLabel.text = "Room ?/?";
                if (_roomTypeLabel != null) _roomTypeLabel.text = "";
                if (_proceedButton != null) _proceedButton.interactable = false;
                return;
            }

            var room = _dungeon.CurrentRoom;

            if (_roomNameLabel != null)
                _roomNameLabel.text = room?.DisplayName ?? "???";

            if (_roomProgressLabel != null)
                _roomProgressLabel.text = $"Room {_dungeon.CurrentRoomIndex + 1}/{_dungeon.RoomCount}";

            if (_roomTypeLabel != null)
                _roomTypeLabel.text = room?.Type.ToString() ?? "";

            if (_proceedButton != null)
            {
                _proceedButton.interactable = _exploration != null
                    && _exploration.IsExploring
                    && room?.Type != RoomType.Combat
                    && room?.Type != RoomType.Boss;
            }
        }

        private void OnProceedClicked()
        {
            if (_exploration == null)
            {
                Debug.LogWarning(LogPrefix + "Proceed clicked but IExplorationController is null.", this);
                return;
            }

            _exploration.AdvanceRoom();
        }

        private void OnPauseClicked()
        {
            if (!ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                Debug.LogWarning(LogPrefix + "IScreenManager not registered — can't open pause.", this);
                return;
            }

            screens.PushOverlay<PauseMenuOverlay>();
        }

        private void OnRoomEnteredHandler(params object[] args)
        {
            RefreshRoomInfo();
        }

        private void OnCombatTriggeredHandler(params object[] args)
        {
            if (_proceedButton != null) _proceedButton.interactable = false;
        }

        private void OnFloorClearedHandler(params object[] args)
        {
            if (_proceedButton != null) _proceedButton.interactable = false;
            if (_roomProgressLabel != null) _roomProgressLabel.text = "Floor Cleared!";
        }

        private void OnExplorationStartedHandler(params object[] args)
        {
            RefreshRoomInfo();
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }
    }
}
