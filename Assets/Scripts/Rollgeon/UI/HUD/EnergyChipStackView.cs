using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.EnergyLib;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Pila de fichas de energía del jugador (una ficha por punto) con label
    /// "actual/max" debajo. Reemplazo visual de la vieja <c>EnergyBarView</c> —
    /// mismos eventos, misma fuente de datos.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Energy Chip Stack View")]
    public class EnergyChipStackView : MonoBehaviour
    {
        private const string LogPrefix = "[EnergyChipStackView] ";

        [Title("Energy Chips — Widget refs")]
        [Required("Arrastrar el ChipStackView de la pila de energía.")]
        [SerializeField]
        private ChipStackView _stack;

        [Required("Arrastrar el TextMeshProUGUI debajo de la pila.")]
        [SerializeField]
        private TextMeshProUGUI _label;

        [Required("Arrastrar el ChipStackSettings asset.")]
        [SerializeField]
        private ChipStackSettingsSO _settings;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private readonly List<int> _chipBuffer = new List<int>();

        private void Awake()
        {
            ConfigureStack();
        }

        private void ConfigureStack()
        {
            if (_stack == null || _settings == null) return;
            _stack.Configure(_settings, new[] { _settings.EnergyChip }, _settings.ChipSpacingY);
        }

        public void Bind(Guid playerGuid)
        {
            _playerGuid = playerGuid;
            ConfigureStack();
            if (!_bound) Subscribe();
            FetchInitialState();
        }

        public void Unbind()
        {
            // No-op: ciclo de vida por OnEnable/OnDisable (patrón sub-views HUD).
        }

        private void OnEnable()
        {
            ConfigureStack();
            Subscribe();
            FetchInitialState();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_bound) return;
            EventManager.Subscribe(EventName.OnPlayerEnergyChanged, HandleEnergyChanged);
            _bound = true;
        }

        private void Unsubscribe()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnPlayerEnergyChanged, HandleEnergyChanged);
            _bound = false;
        }

        private void HandleEnergyChanged(params object[] args)
        {
            if (args == null || args.Length < 3)
            {
                Debug.LogWarning(LogPrefix + "OnPlayerEnergyChanged args malformed (len < 3).", this);
                return;
            }
            if (!(args[0] is Guid guid) || guid != _playerGuid) return;
            if (!(args[1] is int current) || !(args[2] is int max)) return;

            Apply(current, max, animate: true);
        }

        private void FetchInitialState()
        {
            if (_playerGuid == Guid.Empty) return;
            if (!ServiceLocator.TryGetService<IEnergyService>(out var energy) || energy == null)
            {
                Debug.Log(LogPrefix + "IEnergyService no registrado todavia — UI queda default hasta primer evento.", this);
                return;
            }

            Apply(energy.GetCurrent(_playerGuid), energy.GetMax(_playerGuid), animate: false);
        }

        private void Apply(int current, int max, bool animate)
        {
            if (_settings == null) return;

            int visible = ChipStackMath.ClampVisible(current, _settings.MaxVisibleChips);
            _chipBuffer.Clear();
            for (int i = 0; i < visible; i++) _chipBuffer.Add(0);

            if (_stack != null) _stack.SetChips(_chipBuffer, animate);
            if (_label != null) _label.text = ChipStackMath.FormatEnergyLabel(current, max);
        }
    }
}
