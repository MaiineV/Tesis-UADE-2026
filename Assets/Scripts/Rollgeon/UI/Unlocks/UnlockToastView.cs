using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Meta;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.Unlocks
{
    /// <summary>
    /// Notificación no intrusiva de desbloqueo (#164): un panel chico en esquina de
    /// pantalla que aparece unos segundos cuando se cumple una condición durante la
    /// run. Consume <c>TypedEvent&lt;UnlockAchievedPayload&gt;</c>; si llegan varios
    /// unlocks seguidos los encola y los muestra de a uno.
    /// </summary>
    /// <remarks>
    /// [SETUP] Vive como hijo SIEMPRE ACTIVO del Canvas (gameplay y/o main menu);
    /// solo <see cref="_panelRoot"/> se activa/desactiva. Ver
    /// <c>docs/setup/0164_MetaProgression.md</c>.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Unlocks/Unlock Toast View")]
    public class UnlockToastView : MonoBehaviour
    {
        [Title("Unlock Toast")]
        [Required("Arrastrar el GameObject raíz del panel (arranca inactivo).")]
        [SerializeField] private GameObject _panelRoot;

        [Required("Arrastrar el TMP del título (ej. '¡Desbloqueado!').")]
        [SerializeField] private TextMeshProUGUI _titleLabel;

        [Required("Arrastrar el TMP del nombre del elemento desbloqueado.")]
        [SerializeField] private TextMeshProUGUI _bodyLabel;

        [Tooltip("Segundos que el toast queda visible por unlock.")]
        [MinValue(0.5f)]
        [SerializeField] private float _displaySeconds = 3f;

        [Tooltip("Pausa entre toasts encolados.")]
        [MinValue(0f)]
        [SerializeField] private float _gapSeconds = 0.25f;

        private readonly Queue<UnlockAchievedPayload> _queue = new Queue<UnlockAchievedPayload>();
        private Coroutine _drain;

        private void OnEnable()
        {
            TypedEvent<UnlockAchievedPayload>.Subscribe(OnUnlockAchieved);
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        private void OnDisable()
        {
            TypedEvent<UnlockAchievedPayload>.Unsubscribe(OnUnlockAchieved);
            if (_drain != null)
            {
                StopCoroutine(_drain);
                _drain = null;
            }
            _queue.Clear();
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        private void OnUnlockAchieved(UnlockAchievedPayload payload)
        {
            _queue.Enqueue(payload);
            _drain ??= StartCoroutine(DrainQueue());
        }

        private IEnumerator DrainQueue()
        {
            while (_queue.Count > 0)
            {
                var payload = _queue.Dequeue();

                if (_titleLabel != null) _titleLabel.text = "¡Desbloqueado!";
                if (_bodyLabel != null)
                {
                    _bodyLabel.text = string.IsNullOrEmpty(payload.DisplayName)
                        ? payload.TargetId
                        : payload.DisplayName;
                }

                if (_panelRoot != null) _panelRoot.SetActive(true);
                yield return new WaitForSeconds(_displaySeconds);
                if (_panelRoot != null) _panelRoot.SetActive(false);

                if (_queue.Count > 0) yield return new WaitForSeconds(_gapSeconds);
            }
            _drain = null;
        }
    }
}
