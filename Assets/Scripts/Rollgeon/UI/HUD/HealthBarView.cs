using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Player;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    [AddComponentMenu("Rollgeon/UI/HUD/Health Bar View")]
    public class HealthBarView : MonoBehaviour
    {
        private const string LogPrefix = "[HealthBarView] ";

        [Title("Health Bar — Widget refs")]
        [Required("Arrastrar la Image (tipo Filled) del widget de HP.")]
        [SerializeField]
        [Tooltip("Image de HP con tipo Filled (Horizontal). fillAmount refleja HP ratio.")]
        private Image _fillImage;

        [Required("Arrastrar el TextMeshProUGUI del widget.")]
        [SerializeField]
        [Tooltip("Label de HP. Formato controlado por _textFormat.")]
        private TextMeshProUGUI _text;

        [SerializeField]
        [Tooltip("Formato del label. Default: '{0}/{1}'. Ej: '{0} HP', 'HP {0}/{1}'.")]
        private string _textFormat = "{0}/{1}";

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private int _maxHp;

        private Action<DamageResolvedPayload> _onDamageResolved;
        private Action<HealResolvedPayload> _onHealResolved;

        public void Bind(Guid playerGuid)
        {
            _playerGuid = playerGuid;
            if (!_bound) Subscribe();

            ResolveMaxHp();
            FetchInitialState();
        }

        public void Unbind()
        {
            // No-op: el ciclo de vida lo controla OnEnable/OnDisable. Sin esto, cuando
            // el HUD de exploration se desactiva al pushear el de combate y se vuelve
            // a activar, los eventos no se re-suscriben y la barra queda stale.
        }

        private void Subscribe()
        {
            if (_bound) return;

            _onDamageResolved = HandleDamageResolved;
            _onHealResolved = HandleHealResolved;

            TypedEvent<DamageResolvedPayload>.Subscribe(_onDamageResolved);
            TypedEvent<HealResolvedPayload>.Subscribe(_onHealResolved);
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
            _bound = false;
        }

        private void OnEnable()
        {
            Subscribe();
            ResolveMaxHp();
            FetchInitialState();
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
            Unsubscribe();
        }

        private void HandleDamageResolved(DamageResolvedPayload payload)
        {
            if (payload.TargetGuid != _playerGuid) return;
            ReadAndRefresh();
        }

        private void HandleHealResolved(HealResolvedPayload payload)
        {
            if (payload.TargetGuid != _playerGuid) return;
            ReadAndRefresh();
        }

        private void ReadAndRefresh()
        {
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
                return;

            int current = attrs.GetAttributeValue<Health, int>(_playerGuid);
            SetValue(current, _maxHp);
        }

        private void ResolveMaxHp()
        {
            _maxHp = 1;
            if (!ServiceLocator.TryGetService<IPlayerService>(out var ps) || ps == null) return;
            if (ps.CurrentHero == null) return;
            _maxHp = ps.CurrentHero.BaseMaxHp > 0 ? ps.CurrentHero.BaseMaxHp : 1;
        }

        private void FetchInitialState()
        {
            if (_playerGuid == Guid.Empty) return;

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
            {
                Debug.Log(LogPrefix + "AttributesManager no registrado todavia — UI queda default hasta primer evento.", this);
                return;
            }

            int current = attrs.GetAttributeValue<Health, int>(_playerGuid);
            SetValue(current, _maxHp);
        }
    }
}
