using System;
using System.Collections.Generic;
using Patterns;
using PrimeTween;
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

        [Title("Feedback — energía insuficiente")]
        [SerializeField, Tooltip("Color del flash del número cuando una acción no se puede pagar. " +
                 "Default = #D1365A, el rojo de UI de la paleta.")]
        private Color _insufficientColor = new Color(0.820f, 0.212f, 0.353f, 1f);

        [SerializeField, Tooltip("Duración del flash rojo del número (ida y vuelta).")]
        private float _insufficientFlashDuration = 0.18f;

        [SerializeField, Range(0f, 0.5f), Tooltip("Punch de escala del número al rechazar.")]
        private float _insufficientPunch = 0.3f;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private bool _hasData;
        private readonly List<int> _chipBuffer = new List<int>();

        private Color _labelBaseColor = Color.white;
        private Vector3 _labelRestScale = Vector3.one;
        private Tween _insufficientFlash;
        private Tween _insufficientPunchTween;

        private void Awake()
        {
            if (_label != null)
            {
                _labelBaseColor = _label.color;
                _labelRestScale = _label.transform.localScale;
            }
            ConfigureStack();
        }

        /// <summary>
        /// Feedback de "no te alcanza": sacude la pila y hace flashear el número en rojo.
        /// Lo dispara <see cref="PlayerActionButtonsView"/> cuando el jugador intenta usar
        /// una acción impagable — la pila es la respuesta a "¿por qué no puedo?".
        /// No-op con reduced motion (el shake de la pila ya se auto-gatea).
        /// </summary>
        public void PlayInsufficient()
        {
            if (_stack != null) _stack.Shake();

            if (_label == null || !Application.isPlaying || DiceAnim.DiceUiMotionPrefs.ReducedMotion) return;
            if (_insufficientFlash.isAlive) return; // spam de rechazos: un flash por vez

            // Yoyo en vez de dos tweens encadenados: se auto-restaura al color base
            // aunque lo interrumpa un cambio de escena. Apply() reescribe el texto en
            // cada cambio de energía pero nunca el color — si el flash quedara a medias,
            // el número se quedaría rojo para siempre y nada lo repararía.
            _insufficientFlash = Tween.Color(_label, _insufficientColor,
                _insufficientFlashDuration,
                cycles: 2, cycleMode: CycleMode.Yoyo, useUnscaledTime: true);

            if (_insufficientPunch <= 0f) return;
            // El Label de energía no tiene hijos, así que escalarlo es seguro.
            _insufficientPunchTween = Tween.PunchScale(_label.transform,
                strength: Vector3.one * _insufficientPunch,
                duration: _insufficientFlashDuration * 2f,
                useUnscaledTime: true);
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

            // Red de seguridad: si el flash queda a medias al apagarse el HUD, el número
            // volvería a encenderse rojo. Lo devolvemos a su color de autoría a mano.
            if (_insufficientFlash.isAlive) _insufficientFlash.Stop();
            if (_insufficientPunchTween.isAlive) _insufficientPunchTween.Stop();
            if (_label != null)
            {
                _label.color = _labelBaseColor;
                _label.transform.localScale = _labelRestScale;
            }
        }

        private void Subscribe()
        {
            if (_bound) return;
            EventManager.Subscribe(EventName.OnPlayerEnergyChanged, HandleEnergyChanged);
            TypedEvent<InsufficientEnergyPayload>.Subscribe(HandleInsufficientEnergy);
            _bound = true;
        }

        private void Unsubscribe()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnPlayerEnergyChanged, HandleEnergyChanged);
            TypedEvent<InsufficientEnergyPayload>.Unsubscribe(HandleInsufficientEnergy);
            _bound = false;
        }

        private void HandleInsufficientEnergy(InsufficientEnergyPayload payload)
        {
            // Antes de que el Bind resuelva, _playerGuid está vacío: ahí el único
            // jugador posible es el del payload, así que no filtramos de más.
            if (_playerGuid != Guid.Empty && payload.PlayerGuid != _playerGuid) return;
            PlayInsufficient();
        }

        /// <summary>
        /// Reintento del estado inicial: al arrancar la run el BindAll del HUD
        /// puede correr antes de que el jugador/servicio existan y el fetch
        /// falla silencioso — la pila quedaba vacía hasta el primer combate.
        /// Se reintenta por frame hasta la primera lectura exitosa.
        /// </summary>
        private void Update()
        {
            if (_hasData) return;

            if (_playerGuid == Guid.Empty
                && ServiceLocator.TryGetService<Rollgeon.Player.IPlayerService>(out var ps) && ps != null
                && ps.PlayerGuid != Guid.Empty)
            {
                _playerGuid = ps.PlayerGuid;
            }

            if (_playerGuid == Guid.Empty) return;
            FetchInitialState();
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

            _hasData = true;
            Apply(current, max, animate: true);
        }

        private void FetchInitialState()
        {
            // Silencioso sin datos: el Update reintenta (loguear acá spamearía).
            if (_playerGuid == Guid.Empty) return;
            if (!ServiceLocator.TryGetService<IEnergyService>(out var energy) || energy == null) return;

            int max = energy.GetMax(_playerGuid);
            if (max <= 0) return; // ruleset/energía aún no inicializados

            _hasData = true;
            Apply(energy.GetCurrent(_playerGuid), max, animate: false);
        }

        private void Apply(int current, int max, bool animate)
        {
            if (_settings == null) return;

            // Sin tope visual: una ficha por punto de energía.
            _chipBuffer.Clear();
            for (int i = 0; i < current; i++) _chipBuffer.Add(0);

            if (_stack != null) _stack.SetChips(_chipBuffer, animate);
            if (_label != null) _label.text = ChipStackMath.FormatEnergyLabel(current, max);
        }
    }
}
