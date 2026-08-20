using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Player;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Pila de fichas de vida del jugador con las fichas de escudo apiladas
    /// ENCIMA, y label "hp/max" debajo — con escudo el numerador pasa a
    /// hp+escudo teñido con el color de escudo de la paleta ("12/10" en
    /// #A3B3B1 con 10 de vida y 2 de escudo).
    /// </summary>
    /// <remarks>
    /// Mismo ciclo de vida que la vieja <c>HealthBarView</c>: suscripción real en
    /// OnEnable (vive en Canvas_PlayerStatus, siempre activa), <see cref="Bind"/>
    /// guarda el guid + fetch inicial. En cada evento se relee el estado
    /// autoritativo (Health/Shield del AttributesManager) y se diffea contra el
    /// modelo — el burst OnShieldChanged + DamageResolvedPayload de una
    /// absorción resuelve en dos animaciones parciales correctas o no-ops.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Health Chip Stack View")]
    public class HealthChipStackView : MonoBehaviour
    {
        private const string LogPrefix = "[HealthChipStackView] ";

        [Title("Health Chips — Widget refs")]
        [Required("Arrastrar el ChipStackView de la pila de vida.")]
        [SerializeField]
        private ChipStackView _stack;

        [Required("Arrastrar el TextMeshProUGUI (richText ON) debajo de la pila.")]
        [SerializeField]
        private TextMeshProUGUI _label;

        [Required("Arrastrar el ChipStackSettings asset.")]
        [SerializeField]
        private ChipStackSettingsSO _settings;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private int _maxHp = 1;
        private bool _hasData;
        private readonly List<int> _chipBuffer = new List<int>();

        // Pila fantasma: la silueta del máximo de vida siempre visible detrás de la
        // pila real, a baja opacidad — la vida que falta se lee sin mirar el label.
        private ChipStackView _ghostStack;
        private readonly List<int> _ghostBuffer = new List<int>();

        private Action<DamageResolvedPayload> _onDamageResolved;
        private Action<HealResolvedPayload> _onHealResolved;

        private void Awake()
        {
            ConfigureStack();
        }

        private void ConfigureStack()
        {
            if (_stack == null || _settings == null) return;
            _stack.Configure(_settings,
                new[] { _settings.HealthChip, _settings.ShieldChip },
                _settings.ChipSpacingY);
        }

        public void Bind(Guid playerGuid)
        {
            _playerGuid = playerGuid;
            ConfigureStack();
            if (!_bound) Subscribe();

            ResolveMaxHp();
            RefreshFromAuthoritative(animate: false);
        }

        public void Unbind()
        {
            // No-op: el ciclo de vida lo controla OnEnable/OnDisable (patrón de las
            // sub-views del HUD — ver HealthBarView.Unbind).
        }

        private void OnEnable()
        {
            ConfigureStack();
            Subscribe();
            ResolveMaxHp();
            RefreshFromAuthoritative(animate: false);
        }

        /// <summary>
        /// Reintento del estado inicial: al arrancar la run el BindAll del HUD
        /// corre antes de que el AttributesManager registre al jugador, el fetch
        /// falla silencioso y en exploración no hay eventos que re-pueblen la
        /// pila (recién el primer combate la llenaba). Se reintenta por frame
        /// hasta la primera lectura exitosa y después es no-op.
        /// </summary>
        private void Update()
        {
            if (_hasData) return;

            if (_playerGuid == Guid.Empty
                && ServiceLocator.TryGetService<IPlayerService>(out var ps) && ps != null
                && ps.PlayerGuid != Guid.Empty)
            {
                _playerGuid = ps.PlayerGuid;
            }

            if (_playerGuid == Guid.Empty) return;

            ResolveMaxHp();
            RefreshFromAuthoritative(animate: false);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_bound) return;

            _onDamageResolved = HandleDamageResolved;
            _onHealResolved = HandleHealResolved;

            TypedEvent<DamageResolvedPayload>.Subscribe(_onDamageResolved);
            TypedEvent<HealResolvedPayload>.Subscribe(_onHealResolved);
            EventManager.Subscribe(EventName.OnShieldChanged, HandleShieldChanged);
            _bound = true;
        }

        private void Unsubscribe()
        {
            if (!_bound) return;

            if (_onDamageResolved != null)
            {
                TypedEvent<DamageResolvedPayload>.Unsubscribe(_onDamageResolved);
                _onDamageResolved = null;
            }
            if (_onHealResolved != null)
            {
                TypedEvent<HealResolvedPayload>.Unsubscribe(_onHealResolved);
                _onHealResolved = null;
            }
            EventManager.UnSubscribe(EventName.OnShieldChanged, HandleShieldChanged);
            _bound = false;
        }

        private void HandleDamageResolved(DamageResolvedPayload payload)
        {
            if (payload.TargetGuid != _playerGuid) return;
            RefreshFromAuthoritative(animate: true);
        }

        private void HandleHealResolved(HealResolvedPayload payload)
        {
            if (payload.TargetGuid != _playerGuid) return;
            RefreshFromAuthoritative(animate: true);
        }

        private void HandleShieldChanged(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            if (!(args[0] is Guid entityGuid) || entityGuid != _playerGuid) return;
            RefreshFromAuthoritative(animate: true);
        }

        /// <summary>
        /// Relee (hp, escudo) del AttributesManager y aplica pila + label. Público
        /// solo vía eventos; para tests el estado se siembra en los servicios.
        /// </summary>
        private void RefreshFromAuthoritative(bool animate)
        {
            // Sin datos todavía → silencioso: el Update reintenta cada frame
            // hasta la primera lectura exitosa (loguear acá spamearía).
            if (_playerGuid == Guid.Empty) return;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null) return;
            if (!attrs.IsRegistered(_playerGuid)) return;

            var healthAttr = attrs.GetAttribute<Health>(_playerGuid);
            if (healthAttr == null) return;

            _hasData = true;
            // BUG-022: el max puede haber subido por un reward in-run — re-resolver siempre.
            ResolveMaxHp();
            int hp = Mathf.Max(0, healthAttr.Value);
            var shieldAttr = attrs.GetAttribute<Shield>(_playerGuid);
            int shield = Mathf.Max(0, shieldAttr?.Value ?? 0);

            Apply(hp, shield, animate);
        }

        private void Apply(int hp, int shield, bool animate)
        {
            if (_settings == null) return;

            // Sin tope visual: una ficha por cada HpPerChip puntos, la pila crece con
            // el recurso (el cap real lo pone el diseño del juego, no el HUD).
            ChipStackMath.BuildHealthChipIds(hp, shield, _chipBuffer);

            // Fantasma antes que la pila real: el root fantasma queda primer sibling
            // y las fichas reales (SetAsLastSibling) siempre dibujan encima.
            RefreshGhostStack();
            if (_stack != null) _stack.SetChips(_chipBuffer, animate);
            if (_label != null)
            {
                _label.text = ChipStackMath.FormatHealthLabel(hp, shield, _maxHp, _settings.ShieldHex);
            }
        }

        private void ResolveMaxHp()
        {
            int resolved = PlayerMaxHp.Resolve(_playerGuid);
            _maxHp = resolved > 0 ? resolved : 1;
        }

        /// <summary>
        /// Pila fantasma del máximo: siempre muestra ceil(max/HpPerChip) fichas de vida
        /// a baja opacidad. Solo cambia cuando cambia el máximo, y siempre instantáneo —
        /// sin drop, sin SFX, sin competir con las animaciones de la pila real.
        /// </summary>
        private void RefreshGhostStack()
        {
            EnsureGhostStack();
            if (_ghostStack == null) return;

            ChipStackMath.BuildHealthChipIds(_maxHp, 0, _ghostBuffer);
            _ghostStack.SetChips(_ghostBuffer, animate: false);
        }

        // Creada por código y no en el prefab: cuelga del mismo root que las fichas
        // reales (zero-size, mismo anchor bottom-center) para que cada fantasma
        // coincida 1:1 con la posición de la ficha real que la tapa.
        private void EnsureGhostStack()
        {
            if (_ghostStack != null || _stack == null || _settings == null) return;

            var go = new GameObject("GhostChips", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_stack.Root, worldPositionStays: false);
            rect.SetAsFirstSibling();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            var group = go.GetComponent<CanvasGroup>();
            group.alpha = _settings.GhostChipAlpha;
            group.interactable = false;
            group.blocksRaycasts = false;

            _ghostStack = go.AddComponent<ChipStackView>();
            _ghostStack.Configure(_settings, new[] { _settings.HealthChip }, _settings.ChipSpacingY,
                _settings.GhostChipTint);
        }
    }
}
