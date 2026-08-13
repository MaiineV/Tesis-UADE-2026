using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
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
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Boss Bar View")]
    public class BossBarView : MonoBehaviour
    {
        private const string LogPrefix = "[BossBarView] ";

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

        [ShowInInspector, ReadOnly]
        private Guid _bossGuid;

        private int _maxHp;
        private float _lastRatio = 1f;
        private bool _shown;

        private Action<BossEncounterStartedPayload> _onBossStarted;
        private Action<DamageResolvedPayload> _onDamageResolved;
        private Action<HealResolvedPayload> _onHealResolved;

        private void Awake()
        {
            // Arranca apagada; el canvas queda vivo para poder escuchar el inicio del jefe.
            if (_root != null)
                _root.SetActive(false);
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
