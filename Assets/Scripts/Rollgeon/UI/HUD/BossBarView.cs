using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Balance;
using Rollgeon.Combat.Weakness;
using Rollgeon.Combos;
using Rollgeon.Entities.Portraits;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Barra de vida de jefe en pantalla (screen-space). A diferencia de
    /// <see cref="Rollgeon.Entities.Visuals.WorldSpaceHealthBar"/> (sobre la cabeza del pawn),
    /// esta es una barra grande de HUD con el nombre del jefe y un contador numérico, pensada
    /// para dar presencia al encuentro.
    /// <para>
    /// Vive persistente bajo <c>ScreenHost</c> con el canvas activo pero <see cref="_root"/>
    /// apagado. Se enciende al recibir <see cref="BossEncounterStartedPayload"/> (lo emite
    /// <c>CombatHandoffService</c> en salas <c>RoomType.Boss</c>), sigue el <c>Health</c> del
    /// jefe vía los canales tipados de daño/heal, y se apaga al morir el jefe
    /// (<see cref="EventName.OnEntityDestroyed"/>) o al terminar el combate
    /// (<see cref="EventName.OnCombatEnd"/>).
    /// </para>
    /// <para>
    /// Al lado del contador vive el <b>badge de debilidad</b> (icono del combo + multiplicador):
    /// la fila "La debilidad del jefe" de la tabla de reglas invisibles de
    /// <c>docs/design/bosses-seis-refinados.html</c>. El destello al conectarla ya lo hace
    /// <see cref="BossBarJuice"/>; el badge es la mitad persistente, la que se lee ANTES de tirar.
    /// Cableado en <c>docs/setup/boss-weakness-badge.md</c>.
    /// </para>
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Boss Bar View")]
    public class BossBarView : MonoBehaviour
    {
        private const string LogPrefix = "[BossBarView] ";

        /// <summary>Formato del multiplicador cuando el campo del prefab quedó vacío.</summary>
        public const string DefaultWeaknessFormat = "x{0:0.##}";

        [Title("Boss Bar — refs")]
        [Required("Root visual de la barra. Se prende/apaga; arranca inactivo.")]
        [SerializeField]
        [Tooltip("GameObject que se activa al entrar a un jefe y se desactiva al terminar.")]
        private GameObject _root;

        [Required("Image (tipo Filled Horizontal) del fill de HP.")]
        [SerializeField]
        [Tooltip("Image con tipo Filled (Horizontal). fillAmount refleja HP ratio.")]
        private Image _fillImage;

        [Required("Label numérico de HP.")]
        [SerializeField]
        [Tooltip("Contador numérico de HP. Formato controlado por _textFormat.")]
        private TextMeshProUGUI _hpText;

        [Required("Label del nombre del jefe.")]
        [SerializeField]
        [Tooltip("Nombre del jefe, poblado al encender la barra.")]
        private TextMeshProUGUI _nameText;

        [SerializeField, Optional]
        [Tooltip("Retrato del jefe (opcional). Se resuelve por guid; sin sprite se esconde.")]
        private Image _portrait;

        [SerializeField]
        [Tooltip("Formato del contador. {0} = current, {1} = max.")]
        private string _textFormat = "{0}/{1}";

        [SerializeField, Optional]
        [Tooltip("Companion de juice (opcional). Sin ref, la barra snapea sin animación.")]
        private BossBarJuice _juice;

        [Title("Debilidad — badge")]
        [SerializeField, Optional]
        [Tooltip("Contenedor del badge de debilidad (icono + multiplicador). Se prende sólo si el " +
                 "jefe tiene debilidad registrada. Sin ref, se prenden/apagan icono y label sueltos.")]
        private GameObject _weaknessRoot;

        [SerializeField, Optional]
        [Tooltip("Icono del combo al que el jefe es débil. Sale de BaseComboSO.Icon vía el " +
                 "ComboCatalogSO; sin sprite autorado se esconde y el label dice el nombre del combo.")]
        private Image _weaknessIcon;

        [SerializeField, Optional]
        [Tooltip("Label del multiplicador de debilidad (formato en _weaknessFormat).")]
        private TextMeshProUGUI _weaknessText;

        [SerializeField]
        [Tooltip("Formato del multiplicador. {0} = multiplicador resuelto. ASCII a propósito: " +
                 "m6x11plus SDF no tiene glifo para '×' — cambialo si el font asset se extiende.")]
        private string _weaknessFormat = DefaultWeaknessFormat;

        [ShowInInspector, ReadOnly]
        private Guid _bossGuid;

        private int _maxHp;
        private float _lastRatio = 1f;
        private bool _shown;

        /// <summary>
        /// Sin <see cref="RulesetSO"/> registrado (escenas de tooling, tests) el badge cae al mismo
        /// default que el config de balance en vez de a 1: un ×1 diría que pegarle ahí no paga, que
        /// es lo contrario de lo que el jefe tiene autorado.
        /// </summary>
        private static readonly float FallbackWeaknessMultiplier = new WeaknessConfig().DefaultMultiplier;

        private Action<BossEncounterStartedPayload> _onBossStarted;
        private Action<DamageResolvedPayload> _onDamageResolved;
        private Action<HealResolvedPayload> _onHealResolved;

        /// <summary>Combo al que el jefe es débil ahora mismo. <c>null</c> = sin debilidad.</summary>
        [ShowInInspector, ReadOnly]
        public string WeaknessComboId { get; private set; }

        /// <summary>Multiplicador que muestra el badge. <c>1</c> cuando no hay debilidad.</summary>
        [ShowInInspector, ReadOnly]
        public float WeaknessMultiplier { get; private set; } = 1f;

        private void Awake()
        {
            // Arranca apagada; el canvas queda vivo para poder escuchar el inicio del jefe.
            if (_root != null)
                _root.SetActive(false);

            // El badge se apaga aunque lo hayan dejado prendido en el prefab: sin jefe bindeado
            // no hay debilidad que mostrar.
            RenderWeakness();
        }

        private void OnEnable()
        {
            _onBossStarted = HandleBossStarted;
            TypedEvent<BossEncounterStartedPayload>.Subscribe(_onBossStarted);
        }

        private void OnDisable()
        {
            if (_onBossStarted != null)
            {
                TypedEvent<BossEncounterStartedPayload>.Unsubscribe(_onBossStarted);
                _onBossStarted = null;
            }
            Hide();
        }

        private void HandleBossStarted(BossEncounterStartedPayload payload)
        {
            Show(payload.BossGuid, payload.DisplayName);
        }

        /// <summary>Enciende la barra, la bindea al jefe y la deja al máximo.</summary>
        public void Show(Guid bossGuid, string displayName)
        {
            _bossGuid = bossGuid;

            // El jefe entra a full: el Health actual al encender ES el máximo. Evita
            // plomería de tier — no hay curación por encima de este valor en el encuentro.
            _maxHp = ReadCurrentHp(out int current);
            if (_maxHp <= 0) _maxHp = current > 0 ? current : 1;

            if (_nameText != null)
                _nameText.text = displayName;

            ApplyPortrait(bossGuid);
            ApplyWeakness();

            if (_root != null)
                _root.SetActive(true);

            SubscribeCombat();

            _lastRatio = Ratio(current);
            UpdateText(current);
            if (_juice != null)
                _juice.PlayIntro(_lastRatio); // fija el fill + anima la entrada (no-op fuera de Play)
            else
                SetFillImmediate(_lastRatio);
        }

        /// <summary>Apaga la barra y corta el binding.</summary>
        public void Hide()
        {
            if (_juice != null)
                _juice.StopAndRestore();
            UnsubscribeCombat();
            _bossGuid = Guid.Empty;
            ApplyWeakness();
            if (_root != null)
                _root.SetActive(false);
        }

        private void SubscribeCombat()
        {
            if (_shown) return;

            _onDamageResolved = HandleDamageResolved;
            _onHealResolved = HandleHealResolved;

            TypedEvent<DamageResolvedPayload>.Subscribe(_onDamageResolved);
            TypedEvent<HealResolvedPayload>.Subscribe(_onHealResolved);
            EventManager.Subscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);
            EventManager.Subscribe(EventName.OnCombatEnd, HandleCombatEnd);
            // La debilidad no es fija: La Generala la reasigna en fase 2 (AINode_AdoptWeakness).
            // Repintar en cada arranque de turno la deja actualizada ANTES de que el jugador
            // elija su mano, que es cuando el dato sirve.
            EventManager.Subscribe(EventName.OnTurnStarted, HandleTurnStarted);

            _shown = true;
        }

        private void UnsubscribeCombat()
        {
            if (!_shown) return;

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
            EventManager.UnSubscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);
            EventManager.UnSubscribe(EventName.OnCombatEnd, HandleCombatEnd);
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleTurnStarted);

            _shown = false;
        }

        private void HandleDamageResolved(DamageResolvedPayload payload)
        {
            if (payload.TargetGuid != _bossGuid) return;

            ReadCurrentHp(out int current);
            float to = Ratio(current);
            UpdateText(current);

            if (_juice != null)
                _juice.PlayDamage(_lastRatio, to, payload);
            else
                SetFillImmediate(to);

            _lastRatio = to;
        }

        private void HandleHealResolved(HealResolvedPayload payload)
        {
            if (payload.TargetGuid != _bossGuid) return;

            ReadCurrentHp(out int current);
            float to = Ratio(current);
            UpdateText(current);

            if (_juice != null)
                _juice.PlayHeal(_lastRatio, to);
            else
                SetFillImmediate(to);

            _lastRatio = to;
        }

        private void HandleEntityDestroyed(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (!(args[0] is Guid guid)) return;
            if (guid != _bossGuid) return;

            // Muerte del jefe: fade/flash de salida y recién ahí ocultar. Sin juice (o
            // fuera de Play), oculta directo.
            if (_juice != null && Application.isPlaying)
                _juice.PlayDeath(Hide);
            else
                Hide();
        }

        private void HandleCombatEnd(params object[] args)
        {
            Hide();
        }

        private void HandleTurnStarted(params object[] args)
        {
            ApplyWeakness();
        }

        /// <summary>
        /// Relee la debilidad del jefe del <see cref="IWeaknessRegistry"/> y repinta el badge. El
        /// registry es la fuente viva: lo puebla el spawn desde <c>EnemyDataSO.WeaknessComboId</c> y
        /// lo puede reescribir la IA mid-combate, así que leer el <c>EnemyDataSO</c> daría el dato
        /// de autoría y no el vigente.
        /// </summary>
        /// <remarks>
        /// Mismo contrato que <see cref="ApplyPortrait"/>: servicios opcionales, refs opcionales,
        /// nada de esto puede tirar en escenas de tooling ni en tests.
        /// </remarks>
        private void ApplyWeakness()
        {
            WeaknessComboId = null;
            WeaknessMultiplier = 1f;

            if (_bossGuid != Guid.Empty
                && ServiceLocator.TryGetService<IWeaknessRegistry>(out var registry)
                && registry != null
                && registry.TryGet(_bossGuid, out var weakness)
                && !string.IsNullOrEmpty(weakness.comboId))
            {
                WeaknessComboId = weakness.comboId;
                WeaknessMultiplier = weakness.mult > 0f ? weakness.mult : DefaultWeaknessMultiplier();
            }

            RenderWeakness();
        }

        private void RenderWeakness()
        {
            bool hasWeakness = !string.IsNullOrEmpty(WeaknessComboId);
            if (_weaknessRoot != null)
                _weaknessRoot.SetActive(hasWeakness);

            var combo = hasWeakness ? ResolveCombo(WeaknessComboId) : null;
            var icon = combo != null ? combo.Icon : null;

            if (_weaknessIcon != null)
            {
                _weaknessIcon.sprite = icon;
                _weaknessIcon.enabled = icon != null;
            }

            if (_weaknessText == null) return;

            if (!hasWeakness)
            {
                _weaknessText.text = string.Empty;
                _weaknessText.enabled = false;
                return;
            }

            _weaknessText.enabled = true;
            string format = string.IsNullOrEmpty(_weaknessFormat) ? DefaultWeaknessFormat : _weaknessFormat;
            string multiplier = string.Format(format, WeaknessMultiplier);
            // El pipeline de arte de combos puede dejar el icono sin autorar: un "x1,5" pelado no
            // dice a QUÉ le pega, así que sin sprite el nombre del combo entra al label.
            _weaknessText.text = icon != null
                ? multiplier
                : $"{ComboLabel(combo)} {multiplier}";
        }

        private string ComboLabel(BaseComboSO combo)
            => combo != null
                ? Rollgeon.Localization.LocalizedContent.Name(combo.ComboId, combo.DisplayName)
                : WeaknessComboId;

        private static BaseComboSO ResolveCombo(string comboId)
            => ServiceLocator.TryGetService<ComboCatalogSO>(out var catalog) && catalog != null
                ? catalog.GetById(comboId)
                : null;

        private static float DefaultWeaknessMultiplier()
        {
            ServiceLocator.TryGetService<RulesetSO>(out var ruleset);
            return ruleset != null && ruleset.Weakness != null
                ? ruleset.Weakness.DefaultMultiplier
                : FallbackWeaknessMultiplier;
        }

        /// <summary>
        /// Pone el retrato del jefe resuelto por <see cref="IEntityPortraitResolver"/>. Sin Image
        /// cableada, sin servicio o sin sprite para el guid, esconde la Image en vez de dejar el
        /// cuadro blanco del default de uGUI.
        /// </summary>
        /// <remarks>
        /// Mismo contrato que <see cref="TurnQueueView"/>: el resolver es run-scoped y opcional
        /// (los tests y las escenas de tooling corren sin él), así que nada de esto puede tirar.
        /// </remarks>
        private void ApplyPortrait(Guid bossGuid)
        {
            if (_portrait == null) return;

            ServiceLocator.TryGetService<IEntityPortraitResolver>(out var portraits);
            if (portraits != null
                && bossGuid != Guid.Empty
                && portraits.TryGetPortrait(bossGuid, out var sprite)
                && sprite != null)
            {
                _portrait.sprite = sprite;
                _portrait.enabled = true;
                return;
            }

            _portrait.sprite = null;
            _portrait.enabled = false;
        }

        private float Ratio(int current) => _maxHp > 0 ? Mathf.Clamp01((float)current / _maxHp) : 0f;

        private void UpdateText(int current)
        {
            if (_hpText != null)
                _hpText.text = string.Format(_textFormat, Mathf.Max(0, current), _maxHp);
        }

        private void SetFillImmediate(float ratio)
        {
            if (_fillImage != null)
                _fillImage.fillAmount = Mathf.Clamp01(ratio);
        }

        /// <summary>Lee el Health actual del jefe desde el AttributesManager. 0 si no hay servicio.</summary>
        private int ReadCurrentHp(out int current)
        {
            current = 0;
            if (_bossGuid == Guid.Empty) return 0;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
            {
                Debug.Log(LogPrefix + "AttributesManager no registrado — barra en default hasta primer evento.", this);
                return 0;
            }
            current = attrs.GetAttributeValue<Health, int>(_bossGuid);
            return current;
        }
    }
}
