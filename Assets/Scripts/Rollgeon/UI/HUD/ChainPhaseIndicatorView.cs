using System;
using Patterns;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    [AddComponentMenu("Rollgeon/UI/HUD/Chain Phase Indicator View")]
    public class ChainPhaseIndicatorView : MonoBehaviour
    {
        [Title("Chain Phase Indicator — Widget refs")]
        [SerializeField]
        [Tooltip("Label que muestra la fase actual. Formato: _textFormat.")]
        private TextMeshProUGUI _text;

        [SerializeField]
        [Tooltip("Formato del label. Default: 'Phase {0}/{1}'.")]
        private string _textFormat = "Phase {0}/{1}";

        [SerializeField]
        [Tooltip("GameObject raiz a mostrar/ocultar segun chain activo.")]
        private GameObject _container;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();

            _playerGuid = playerGuid;
            EventManager.Subscribe(EventName.OnChainPhaseStarted, OnChainPhaseStarted);
            EventManager.Subscribe(EventName.OnChainCompleted, OnChainCompleted);
            _bound = true;

            Hide();
        }

        public void Unbind()
        {
            if (!_bound) return;

            EventManager.UnSubscribe(EventName.OnChainPhaseStarted, OnChainPhaseStarted);
            EventManager.UnSubscribe(EventName.OnChainCompleted, OnChainCompleted);
            _bound = false;

            Hide();
        }

        public void Show(int phaseIndex, int totalPhases)
        {
            if (_text != null)
                _text.text = string.Format(_textFormat, phaseIndex + 1, totalPhases);

            if (_container != null)
                _container.SetActive(true);
        }

        public void Hide()
        {
            if (_container != null)
                _container.SetActive(false);
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        private void OnChainPhaseStarted(params object[] args)
        {
            if (args == null || args.Length < 3) return;
            if (!(args[0] is Guid sourceGuid) || sourceGuid != _playerGuid) return;

            int phaseIndex = (int)args[1];
            int totalPhases = (int)args[2];
            Show(phaseIndex, totalPhases);
        }

        private void OnChainCompleted(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (!(args[0] is Guid sourceGuid) || sourceGuid != _playerGuid) return;

            Hide();
        }
    }
}
