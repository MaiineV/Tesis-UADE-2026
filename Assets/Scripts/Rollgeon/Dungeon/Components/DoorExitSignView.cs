using PrimeTween;
using UnityEngine;

namespace Rollgeon.Dungeon.Components
{
    /// <summary>
    /// Cartel de salida sobre la puerta de fin de piso. Vive en el root del DoorBoss.prefab
    /// junto al <see cref="DoorController"/>, que lo notifica desde <c>SetState</c> (mismo
    /// patrón directo que el tooltip gate — inmune al gotcha de SetState corriendo antes
    /// del Awake en GOs inactivos). El cartel solo aplica a la puerta exit designada por
    /// <c>MarkBossExitDoor</c> y recién cuando abre (boss derrotado): drop-in desde arriba
    /// + bob infinito para señalar al player que la salida es esa puerta. Las DoorBoss que
    /// llevan HACIA la boss room comparten el prefab pero nunca son IsExit — cartel oculto.
    /// </summary>
    [AddComponentMenu("Rollgeon/Dungeon/Door Exit Sign View")]
    public sealed class DoorExitSignView : MonoBehaviour
    {
        [Tooltip("El hijo ExitSign del DoorBoss.prefab — inactivo por default, este view lo administra.")]
        [SerializeField] private GameObject _sign;

        [Header("Drop-in")]
        [Tooltip("Altura extra (local) desde la que cae el cartel al aparecer.")]
        [SerializeField] private float _dropHeight = 1.25f;
        [SerializeField] private float _dropDuration = 0.5f;
        [SerializeField] private Ease _dropEase = Ease.OutBack;

        [Header("Bob continuo")]
        [Tooltip("Amplitud (local) de la oscilación vertical infinita.")]
        [SerializeField] private float _bobAmplitude = 0.08f;
        [Tooltip("Duración de un ciclo completo (subir y bajar).")]
        [SerializeField] private float _bobPeriod = 1.6f;

        private Vector3 _baseLocalPos;
        private bool _baseCaptured;
        private bool _shown;

        private Tween _dropTween;
        private Tween _bobTween;

#if UNITY_EDITOR
        public const string EditorSignField = nameof(_sign);
        public GameObject EditorSign => _sign;
#endif

        /// <summary>
        /// Sincroniza el cartel con el estado de la puerta. Idempotente: los Sync repetidos
        /// del DungeonManager no re-disparan la animación mientras el estado no cambie.
        /// </summary>
        public void Apply(bool isExit, DoorVisualState state)
        {
            bool show = isExit && state == DoorVisualState.Open;
            if (show == _shown) return;

            _shown = show;
            if (show) Show();
            else Hide();
        }

        private void OnEnable()
        {
            // Re-entrada a la boss room cleared: el fog of war reactiva el root
            // (RefreshRoomVisibility) y el drop-in se re-dispara para volver a señalar.
            if (_shown) Show();
        }

        private void OnDisable()
        {
            StopTweens();
            ResetSignPosition();
        }

        private void Show()
        {
            if (_sign == null) return;

            CaptureBasePosition();
            StopTweens();
            _sign.SetActive(true);

            // Los installers/tests corren en EditMode — ahí solo el estado final, sin juice.
            if (!Application.isPlaying)
            {
                _sign.transform.localPosition = _baseLocalPos;
                return;
            }

            var t = _sign.transform;
            t.localPosition = _baseLocalPos + Vector3.up * _dropHeight;
            _dropTween = Tween.LocalPositionY(t, _baseLocalPos.y, _dropDuration, _dropEase)
                .OnComplete(this, self => self.StartBob());
        }

        private void StartBob()
        {
            if (_sign == null) return;
            _bobTween = Tween.LocalPositionY(_sign.transform,
                _baseLocalPos.y + _bobAmplitude, _bobPeriod * 0.5f,
                Ease.InOutSine, cycles: -1, CycleMode.Yoyo);
        }

        private void Hide()
        {
            if (_sign == null) return;
            StopTweens();
            ResetSignPosition();
            _sign.SetActive(false);
        }

        private void CaptureBasePosition()
        {
            if (_baseCaptured || _sign == null) return;
            _baseLocalPos = _sign.transform.localPosition;
            _baseCaptured = true;
        }

        private void StopTweens()
        {
            if (_dropTween.isAlive) _dropTween.Stop();
            if (_bobTween.isAlive) _bobTween.Stop();
        }

        private void ResetSignPosition()
        {
            if (_baseCaptured && _sign != null)
                _sign.transform.localPosition = _baseLocalPos;
        }
    }
}
