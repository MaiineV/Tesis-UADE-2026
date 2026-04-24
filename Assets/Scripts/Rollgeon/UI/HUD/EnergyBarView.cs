using System;
using Patterns;
using Rollgeon.Combat.Energy;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view del HUD que muestra la barra de Energia del jugador. Se suscribe a
    /// <see cref="EventName.OnPlayerEnergyChanged"/> en <see cref="Bind"/>, filtra
    /// por <c>playerGuid</c>, y actualiza <see cref="_fillImage"/> + <see cref="_text"/>.
    /// </summary>
    /// <remarks>
    /// Plan §4.4. FetchInitialState lee via <see cref="IEnergyService"/> (T100a).
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Energy Bar View")]
    public class EnergyBarView : MonoBehaviour
    {
        private const string LogPrefix = "[EnergyBarView] ";

        [Title("Energy Bar — Widget refs")]
        [Required("Arrastrar la Image (tipo Filled) del widget de energia.")]
        [SerializeField]
        [Tooltip("Image de energia con tipo Filled (Horizontal). fillAmount refleja ratio.")]
        private Image _fillImage;

        [Required("Arrastrar el TextMeshProUGUI del widget.")]
        [SerializeField]
        private TextMeshProUGUI _text;

        [SerializeField]
        [Tooltip("Formato del label. Default: '{0}/{1}'. Ej: '{0}', 'ENG {0}/{1}'.")]
        private string _textFormat = "{0}/{1}";

        [InfoBox("Actualiza solo por eventos (OnPlayerEnergyChanged). No polling.")]
        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();

            _playerGuid = playerGuid;
            EventManager.Subscribe(EventName.OnPlayerEnergyChanged, HandleEnergyChanged);
            _bound = true;

            FetchInitialState();
        }

        public void Unbind()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnPlayerEnergyChanged, HandleEnergyChanged);
            _bound = false;
        }

        public void SetValue(int current, int max)
        {
            if (_fillImage != null)
            {
                _fillImage.fillAmount = max > 0 ? (float)current / max : 0f;
            }
            if (_text != null)
            {
                _text.text = string.Format(_textFormat, current, max);
            }
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        private void HandleEnergyChanged(params object[] args)
        {
            if (args == null || args.Length < 3)
            {
                Debug.LogWarning(LogPrefix + "OnPlayerEnergyChanged args malformed (len < 3).", this);
                return;
            }
            if (!(args[0] is Guid guid))
            {
                Debug.LogWarning(LogPrefix + "OnPlayerEnergyChanged args[0] is not Guid.", this);
                return;
            }
            if (guid != _playerGuid) return;

            if (!(args[1] is int current) || !(args[2] is int max))
            {
                Debug.LogWarning(LogPrefix + "OnPlayerEnergyChanged args[1]/[2] not int.", this);
                return;
            }

            SetValue(current, max);
        }

        /// <summary>
        /// [SEED] Lectura one-shot de energia inicial via <see cref="IEnergyService"/>
        /// (plan §2.4, excepcion a §17.D.4). Si el servicio no esta registrado al
        /// momento del Bind, queda default hasta el primer OnPlayerEnergyChanged.
        /// </summary>
        // [STUB] OnPlayerStatsSnapshot — remove FetchInitialState when snapshot event exists.
        private void FetchInitialState()
        {
            if (_playerGuid == Guid.Empty) return;

            if (!ServiceLocator.TryGetService<IEnergyService>(out var energy) || energy == null)
            {
                Debug.Log(LogPrefix + "IEnergyService no registrado todavia — UI queda default hasta primer evento.", this);
                return;
            }

            int current = energy.GetCurrent(_playerGuid);
            int max = energy.GetMax(_playerGuid);
            SetValue(current, max);
        }
    }
}
