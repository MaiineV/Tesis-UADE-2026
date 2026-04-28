using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    [AddComponentMenu("Rollgeon/UI/HUD/Shield Bar View")]
    public class ShieldBarView : MonoBehaviour
    {
        [Title("Shield Bar — Widget refs")]
        [SerializeField]
        [Tooltip("Image de shield con tipo Filled (Horizontal). Null si solo se usa texto.")]
        private Image _fillImage;

        [SerializeField]
        [Tooltip("Label de shield. Formato controlado por _textFormat.")]
        private TextMeshProUGUI _text;

        [SerializeField]
        [Tooltip("Formato del label. Default: '{0}'. Ej: 'Shield {0}'.")]
        private string _textFormat = "{0}";

        [SerializeField]
        [Tooltip("GameObject raiz a mostrar/ocultar segun shield > 0.")]
        private GameObject _container;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();

            _playerGuid = playerGuid;
            EventManager.Subscribe(EventName.OnShieldChanged, OnShieldChanged);
            _bound = true;

            FetchInitialState();
        }

        public void Unbind()
        {
            if (!_bound) return;

            EventManager.UnSubscribe(EventName.OnShieldChanged, OnShieldChanged);
            _bound = false;
        }

        public void SetValue(int current)
        {
            if (_fillImage != null)
                _fillImage.fillAmount = Mathf.Clamp01(current / 100f);

            if (_text != null)
                _text.text = string.Format(_textFormat, current);

            if (_container != null)
                _container.SetActive(current > 0);
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        private void OnShieldChanged(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            if (!(args[0] is Guid entityGuid) || entityGuid != _playerGuid) return;

            int current = (int)args[1];
            SetValue(current);
        }

        private void FetchInitialState()
        {
            if (_playerGuid == Guid.Empty) return;

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
            {
                SetValue(0);
                return;
            }

            var shieldAttr = attrs.GetAttribute<Shield>(_playerGuid);
            SetValue(shieldAttr?.Value ?? 0);
        }
    }
}
