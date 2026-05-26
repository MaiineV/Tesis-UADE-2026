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
    /// Sub-view del HUD que muestra información de la sala actual.
    /// <para>
    /// 2026-04-22 (§13.6): con el sistema de puertas físicas el botón
    /// "Proceed" dejó de tener sentido — la transición entre salas la
    /// dispara el player cruzando una puerta. El botón permanece en el
    /// prefab para compatibilidad del Inspector pero siempre queda
    /// deshabilitado y sin wiring.
    /// </para>
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Room Navigation View")]
    public class RoomNavigationView : MonoBehaviour
    {
        private const string LogPrefix = "[RoomNavigationView] ";

        [Title("Room Navigation — Widget refs")]
        [Required("Arrastrar el TextMeshProUGUI del nombre de room.")]
        [SerializeField]
        private TextMeshProUGUI _roomNameLabel;

        [Required("Arrastrar el TextMeshProUGUI del progreso (rooms cleared / total).")]
        [SerializeField]
        private TextMeshProUGUI _roomProgressLabel;

        [Required("Arrastrar el TextMeshProUGUI del tipo de room.")]
        [SerializeField]
        private TextMeshProUGUI _roomTypeLabel;

        [InfoBox("Deprecado tras §13.6 — se mantiene para no romper prefabs " +
                 "existentes pero queda disabled y sin onClick.")]
        [SerializeField]
        private Button _proceedButton;

        [Required("Arrastrar el Button de pausa.")]
        [SerializeField]
        private Button _pauseButton;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private IDungeonService _dungeon;
        private IExplorationController _exploration;

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();

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

            EventManager.Subscribe(EventName.OnRoomEntered, OnRoomEnteredHandler);
            EventManager.Subscribe(EventName.OnRoomCleared, OnRoomClearedHandler);
            EventManager.Subscribe(EventName.OnCombatTriggered, OnCombatTriggeredHandler);
            EventManager.Subscribe(EventName.OnExplorationStarted, OnExplorationStartedHandler);

            if (_pauseButton != null) _pauseButton.onClick.AddListener(OnPauseClicked);
            if (_proceedButton != null) _proceedButton.interactable = false;

            RefreshRoomInfo();
            _bound = true;
        }

        public void Unbind()
        {
            if (!_bound) return;

            EventManager.UnSubscribe(EventName.OnRoomEntered, OnRoomEnteredHandler);
            EventManager.UnSubscribe(EventName.OnRoomCleared, OnRoomClearedHandler);
            EventManager.UnSubscribe(EventName.OnCombatTriggered, OnCombatTriggeredHandler);
            EventManager.UnSubscribe(EventName.OnExplorationStarted, OnExplorationStartedHandler);

            if (_pauseButton != null) _pauseButton.onClick.RemoveListener(OnPauseClicked);

            _dungeon = null;
            _exploration = null;
            _bound = false;
        }

        /// <summary>
        /// Actualiza labels desde <see cref="IDungeonService"/>. El progreso
        /// ahora es "cleared / total" sobre el grafo completo del piso.
        /// </summary>
        public void RefreshRoomInfo()
        {
            if (_dungeon == null)
            {
                if (_roomNameLabel != null) _roomNameLabel.text = "???";
                if (_roomProgressLabel != null) _roomProgressLabel.text = "Rooms ?/?";
                if (_roomTypeLabel != null) _roomTypeLabel.text = "";
                if (_proceedButton != null) _proceedButton.interactable = false;
                return;
            }

            var room = _dungeon.CurrentRoom;
            var instances = _dungeon.GetAllRoomInstances();

            int total = instances != null ? instances.Count : 0;
            int cleared = 0;
            if (instances != null)
            {
                foreach (var ri in instances.Values)
                {
                    if (ri.State == RoomState.Cleared) cleared++;
                }
            }

            if (_roomNameLabel != null)
                _roomNameLabel.text = room?.DisplayName ?? "???";

            if (_roomProgressLabel != null)
                _roomProgressLabel.text = $"Rooms {cleared}/{total}";

            if (_roomTypeLabel != null)
                _roomTypeLabel.text = room?.Type.ToString() ?? "";

            if (_proceedButton != null)
                _proceedButton.interactable = false;
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

        private void OnRoomEnteredHandler(params object[] args) => RefreshRoomInfo();
        private void OnRoomClearedHandler(params object[] args) => RefreshRoomInfo();
        private void OnCombatTriggeredHandler(params object[] args) => RefreshRoomInfo();
        private void OnExplorationStartedHandler(params object[] args) => RefreshRoomInfo();

        private void OnDisable()
        {
            if (_bound) Unbind();
        }
    }
}
