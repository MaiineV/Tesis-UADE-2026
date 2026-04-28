using System;
using Patterns;
using Rollgeon.Economy;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view del HUD que muestra el contador de oro del jugador. Se suscribe a
    /// <see cref="EventName.OnGoldChanged"/> en <see cref="Bind"/> y pinta el valor
    /// actual en <see cref="_text"/>.
    /// </summary>
    /// <remarks>
    /// Plan §4.5. Payload real del evento: <c>[int current, int delta]</c> (sin Guid;
    /// ver <see cref="EventName.OnGoldChanged"/> — el oro es global del run). Por
    /// eso <see cref="Bind"/> acepta <see cref="Guid"/> por consistencia con las
    /// otras sub-views pero no lo usa para filtrar.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Gold Counter View")]
    public class GoldCounterView : MonoBehaviour
    {
        private const string LogPrefix = "[GoldCounterView] ";

        [Title("Gold Counter — Widget refs")]
        [Required("Arrastrar el TextMeshProUGUI del widget.")]
        [SerializeField]
        private TextMeshProUGUI _text;

        [SerializeField]
        [Tooltip("Formato del label. Default: '{0}G'. Ej: '{0}', '{0} oro', '$ {0}'.")]
        private string _textFormat = "{0}G";

        [InfoBox("Actualiza solo por OnGoldChanged. No polling.")]
        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        /// <summary>
        /// Engancha la sub-view. <paramref name="playerGuid"/> guardado por consistencia
        /// con otras sub-views pero no filtra (el evento es global).
        /// </summary>
        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();

            _playerGuid = playerGuid;
            EventManager.Subscribe(EventName.OnGoldChanged, HandleGoldChanged);
            _bound = true;

            Debug.Log(LogPrefix + $"Bound. _text={(_text != null ? "set" : "NULL")} gameObject.active={gameObject.activeInHierarchy}", this);
            FetchInitialState();
        }

        public void Unbind()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnGoldChanged, HandleGoldChanged);
            _bound = false;
        }

        public void SetValue(int gold)
        {
            if (_text == null)
            {
                Debug.LogWarning(LogPrefix + $"SetValue({gold}) — _text es NULL (no cableado en Inspector). UI no actualiza.", this);
                return;
            }
            _text.text = string.Format(_textFormat, gold);
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        private void HandleGoldChanged(params object[] args)
        {
            if (args == null || args.Length < 1)
            {
                Debug.LogWarning(LogPrefix + "OnGoldChanged args malformed (len < 1).", this);
                return;
            }
            if (!(args[0] is int current))
            {
                Debug.LogWarning(LogPrefix + "OnGoldChanged args[0] is not int.", this);
                return;
            }

            Debug.Log(LogPrefix + $"OnGoldChanged received current={current}", this);
            SetValue(current);
        }

        /// <summary>
        /// Pulla el oro actual del <see cref="IEconomyService"/> al bindear, para que
        /// la UI muestre el valor correcto sin depender de que el primer
        /// <c>OnGoldChanged</c> haya disparado antes (ej. cuando el HUD se pushea
        /// después del registro del servicio).
        /// </summary>
        private void FetchInitialState()
        {
            if (ServiceLocator.TryGetService<IEconomyService>(out var economy) && economy != null)
            {
                SetValue(economy.CurrentGold);
            }
        }
    }
}
