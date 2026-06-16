using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.Entities.Visuals
{
    [AddComponentMenu("Rollgeon/Entities/World Space Health Bar")]
    public sealed class WorldSpaceHealthBar : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Image con tipo Filled (Horizontal). fillAmount refleja HP ratio.")]
        private Image _fillImage;

        [SerializeField]
        [Tooltip("Texto numerico de HP. Null = sin texto.")]
        private TextMeshProUGUI _hpText;

        [SerializeField]
        [Tooltip("Formato del texto. {0} = current, {1} = max.")]
        private string _textFormat = "{0}/{1}";

        [SerializeField]
        [Tooltip("Root de la barra. Se desactiva cuando la entidad muere.")]
        private GameObject _barRoot;

        [SerializeField]
        [Tooltip("Offset local respecto al pawn (Y = altura sobre la cabeza).")]
        private Vector3 _offset = new Vector3(0f, 2f, 0f);

        private Guid _entityGuid;
        private int _maxHp;
        private bool _bound;

        private Action<DamageResolvedPayload> _onDamageResolved;
        private Action<HealResolvedPayload> _onHealResolved;

        public void Initialize(Guid entityGuid, int currentHp, int maxHp)
        {
            if (_bound) Teardown();

            _entityGuid = entityGuid;
            _maxHp = maxHp > 0 ? maxHp : 1;

            _onDamageResolved = HandleDamageResolved;
            _onHealResolved = HandleHealResolved;

            TypedEvent<DamageResolvedPayload>.Subscribe(_onDamageResolved);
            TypedEvent<HealResolvedPayload>.Subscribe(_onHealResolved);
            EventManager.Subscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);

            _bound = true;

            SetBarVisible(true);
            RefreshFill(currentHp);
        }

        public void Teardown()
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
            EventManager.UnSubscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);

            _bound = false;
        }

        private void OnDisable()
        {
            if (_bound) Teardown();
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null)
                transform.forward = cam.transform.forward;

            transform.localPosition = _offset;
        }

        private void HandleDamageResolved(DamageResolvedPayload payload)
        {
            if (payload.TargetGuid != _entityGuid) return;
            ReadAndRefresh();
        }

        private void HandleHealResolved(HealResolvedPayload payload)
        {
            if (payload.TargetGuid != _entityGuid) return;
            ReadAndRefresh();
        }

        private void HandleEntityDestroyed(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (!(args[0] is Guid guid)) return;
            if (guid != _entityGuid) return;

            SetBarVisible(false);
        }

        private void ReadAndRefresh()
        {
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
                return;

            int current = attrs.GetAttributeValue<Health, int>(_entityGuid);
            RefreshFill(current);
        }

        private void RefreshFill(int current)
        {
            float ratio = (float)current / _maxHp;
            if (_fillImage != null)
                _fillImage.fillAmount = ratio;
            if (_hpText != null)
                _hpText.text = string.Format(_textFormat, current, _maxHp);
        }

        private void SetBarVisible(bool visible)
        {
            if (_barRoot != null)
                _barRoot.SetActive(visible);
        }
    }
}
