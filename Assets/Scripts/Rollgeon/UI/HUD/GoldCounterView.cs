using System;
using Patterns;
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
            if (_text != null)
            {
                _text.text = string.Format(_textFormat, gold);
            }
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        // [STUB] Payload OnGoldChanged — verified against EventName.cs: [int current, int delta].
        //        Si en el futuro se agrega un Guid al payload (ej: multi-run), ajustar filtro.
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

            SetValue(current);
        }

        /// <summary>
        /// [SEED] Lectura one-shot del oro inicial (plan §2.4). No existe todavia un
        /// <c>IEconomyService</c> / <c>AttributesManager.GetGold</c> registrado — la
        /// UI queda en default ('0G') hasta el primer evento.
        /// </summary>
        // [STUB] OnPlayerStatsSnapshot — remove FetchInitialState when snapshot event exists.
        private void FetchInitialState()
        {
            // No hay IEconomyService upstream todavia. La UI se rellena con el primer
            // OnGoldChanged que dispare el publisher canonico.
        }
    }
}
