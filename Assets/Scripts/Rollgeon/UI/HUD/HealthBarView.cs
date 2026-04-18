using System;
using Patterns;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view del HUD que muestra la barra de HP del jugador. Se suscribe a
    /// <see cref="EventName.OnPlayerHealthChanged"/> en <see cref="Bind"/>, filtra
    /// por <c>playerGuid</c> (plan §2.3), y actualiza <see cref="_slider"/> +
    /// <see cref="_text"/> con <c>current/max</c>.
    /// </summary>
    /// <remarks>
    /// Plan §4.3. Sin <c>Update()</c> ni polling — pura reaccion a eventos.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Health Bar View")]
    public class HealthBarView : MonoBehaviour
    {
        private const string LogPrefix = "[HealthBarView] ";

        [Title("Health Bar — Widget refs")]
        [Required("Arrastrar el Slider del widget (ver instructivo §8.2).")]
        [SerializeField]
        [Tooltip("Slider de HP. El Handle suele estar deshabilitado; solo se anima Fill.")]
        private Slider _slider;

        [Required("Arrastrar el TextMeshProUGUI del widget.")]
        [SerializeField]
        [Tooltip("Label de HP. Formato controlado por _textFormat.")]
        private TextMeshProUGUI _text;

        [SerializeField]
        [Tooltip("Formato del label. Default: '{0}/{1}'. Ej: '{0} HP', 'HP {0}/{1}'.")]
        private string _textFormat = "{0}/{1}";

        [InfoBox("Esta view NO hace polling. Actualiza solo por eventos " +
                 "(OnPlayerHealthChanged) y una lectura inicial opcional en Bind.")]
        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        /// <summary>
        /// Engancha la sub-view al jugador <paramref name="playerGuid"/>. Suscribe
        /// el handler al bus y pinta el estado inicial leyendo servicios una
        /// vez (ver <c>[SEED]</c>). Idempotente: si ya estaba bound, primero hace
        /// <see cref="Unbind"/>.
        /// </summary>
        public void Bind(Guid playerGuid)
        {
            if (_bound)
            {
                Unbind();
            }

            _playerGuid = playerGuid;
            EventManager.Subscribe(EventName.OnPlayerHealthChanged, HandleHealthChanged);
            _bound = true;

            FetchInitialState();
        }

        /// <summary>Desuscribe del bus. Idempotente.</summary>
        public void Unbind()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnPlayerHealthChanged, HandleHealthChanged);
            _bound = false;
        }

        /// <summary>
        /// Pinta la UI con <paramref name="current"/>/<paramref name="max"/>. Publico
        /// para tests y para re-pintar manualmente desde otras capas.
        /// </summary>
        public void SetValue(int current, int max)
        {
            if (_slider != null)
            {
                _slider.value = max > 0 ? (float)current / max : 0f;
            }
            if (_text != null)
            {
                _text.text = string.Format(_textFormat, current, max);
            }
        }

        private void OnDisable()
        {
            // Safety net: si la GO se desactiva sin un Unbind explicito, limpiar.
            if (_bound) Unbind();
        }

        private void HandleHealthChanged(params object[] args)
        {
            // §17.D.5 — handlers tolerantes: log + early return si el payload viene malformado.
            if (args == null || args.Length < 3)
            {
                Debug.LogWarning(LogPrefix + "OnPlayerHealthChanged args malformed (len < 3).", this);
                return;
            }
            if (!(args[0] is Guid guid))
            {
                Debug.LogWarning(LogPrefix + "OnPlayerHealthChanged args[0] is not Guid.", this);
                return;
            }
            if (guid != _playerGuid) return; // plan §2.3 — filtrar por entidad.

            if (!(args[1] is int current) || !(args[2] is int max))
            {
                Debug.LogWarning(LogPrefix + "OnPlayerHealthChanged args[1]/[2] not int.", this);
                return;
            }

            SetValue(current, max);
        }

        /// <summary>
        /// [SEED] Lectura one-shot de estado inicial en Bind (plan §2.4, excepcion a
        /// §17.D.4). No es polling: se ejecuta una unica vez cuando la sub-view se
        /// engancha al jugador. Futuro upgrade: <c>OnPlayerStatsSnapshot</c> emitido
        /// por <c>IPlayerService.SetPlayer</c> elimina esta lectura.
        /// </summary>
        // [STUB] OnPlayerStatsSnapshot — remove FetchInitialState when snapshot event exists.
        private void FetchInitialState()
        {
            // El dominio no expone todavia un getter (guid, HP) — no hay Stats/Health.cs.
            // Cuando Foundation de Health aterrice, leer via AttributesManager aca.
            // Por ahora, dejamos la UI en default hasta el primer OnPlayerHealthChanged.
        }
    }
}
