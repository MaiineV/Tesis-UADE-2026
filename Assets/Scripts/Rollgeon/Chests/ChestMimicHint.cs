using System.Collections;
using UnityEngine;

namespace Rollgeon.Chests
{
    /// <summary>
    /// Hint del Mimic dormido (GDD §18, "leve movimiento"): cada [min,max] segundos
    /// el modelo del cofre rockea unos grados y vuelve. Twitch procedural porque el
    /// rig del cofre no tiene clip de movimiento (Anim_Chest_Idle es pose estática).
    /// Lo agrega <see cref="ChestService"/> SOLO en cofres-mimic; muere con el pawn
    /// al despawnear (activación, apertura, expiry).
    /// </summary>
    public sealed class ChestMimicHint : MonoBehaviour
    {
        // Ciclos de la sinusoide amortiguada por twitch — 3 = rock corto y nervioso.
        private const float Cycles = 3f;

        // Defaults defensivos: los assets serializados ANTES de agregar los campos
        // de config quedan en default(T) (gotcha Odin), e Init ignora valores <= 0.
        private float _minSeconds = 6f;
        private float _maxSeconds = 12f;
        private float _duration = 0.4f;
        private float _angleDegrees = 4f;

        private Transform _model;
        private Quaternion _baseRotation;
        private Coroutine _loop;

        public void Init(float minSeconds, float maxSeconds, float duration, float angleDegrees)
        {
            if (minSeconds > 0f) _minSeconds = minSeconds;
            _maxSeconds = Mathf.Max(_minSeconds, maxSeconds);
            if (duration > 0f) _duration = duration;
            if (angleDegrees > 0f) _angleDegrees = angleDegrees;
        }

        /// <summary>
        /// Sinusoide amortiguada normalizada: 0 en los extremos, oscila en el medio.
        /// Pura para poder testearla en EditMode.
        /// </summary>
        public static float EvaluateAngle01(float t01)
        {
            t01 = Mathf.Clamp01(t01);
            return Mathf.Sin(t01 * Cycles * 2f * Mathf.PI) * (1f - t01);
        }

        private void OnEnable()
        {
            // Cosmético puro — en EditMode (tests) no hay frames que lo tickeen.
            if (!Application.isPlaying) return;

            // El modelo rigeado es el hijo del pawn; el root lo maneja EntityPawn
            // (snap/facing) y no hay que tocarlo.
            _model = transform.childCount > 0 ? transform.GetChild(0) : transform;
            _baseRotation = _model.localRotation;
            _loop = StartCoroutine(Run());
        }

        private void OnDisable()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
            if (_model != null) _model.localRotation = _baseRotation;
        }

        private IEnumerator Run()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(_minSeconds, _maxSeconds));

                float t = 0f;
                while (t < _duration)
                {
                    t += Time.deltaTime;
                    float roll = EvaluateAngle01(t / _duration) * _angleDegrees;
                    _model.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, roll);
                    yield return null;
                }
                _model.localRotation = _baseRotation;
            }
        }
    }
}
