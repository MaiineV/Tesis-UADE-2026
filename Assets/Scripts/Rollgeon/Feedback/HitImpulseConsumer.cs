using System.Collections;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Componente visual que consume vectores <see cref="Rollgeon.Entities.Behaviors.ImpulseBehaviorValue"/>
    /// aplicándolos como desplazamiento temporal del transform. TECHNICAL.md §9.2 (HitImpulse).
    /// </summary>
    /// <remarks>
    /// <para>Se pega al root visual del pawn. El <see cref="FeedbackManager"/> lo busca via
    /// <c>GetComponent</c>/<c>GetComponentInChildren</c> en el transform registrado en
    /// <see cref="IPawnRegistry"/>, y le pasa el vector al aplicar un feedback del tipo
    /// <c>BehaviorValue</c> con key <c>HitImpulse</c>.</para>
    /// <para>La animación default es un "knockback breve": desplaza el transform hacia el
    /// vector y lo devuelve a la posición original en <see cref="_returnSeconds"/>. Si el
    /// pawn ya está corriendo un knockback, el nuevo impulso reemplaza al anterior.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HitImpulseConsumer : MonoBehaviour
    {
        [SerializeField, Min(0.05f), Tooltip("Duración del knockback (push + return).")]
        private float _returnSeconds = 0.18f;

        [SerializeField, Range(0f, 2f), Tooltip("Multiplicador aplicado al vector del impulso.")]
        private float _strength = 1f;

        [SerializeField, Tooltip("Si true, el knockback respeta la escala del parent.")]
        private bool _localSpace = true;

        private Coroutine _routine;
        private Vector3 _restPosition;
        private bool _hasRest;

        public void ApplyImpulse(Vector3 impulse)
        {
            if (impulse == Vector3.zero) return;
            if (!_hasRest)
            {
                _restPosition = _localSpace ? transform.localPosition : transform.position;
                _hasRest = true;
            }
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(KnockbackRoutine(impulse * _strength));
        }

        private IEnumerator KnockbackRoutine(Vector3 impulse)
        {
            float half = _returnSeconds * 0.5f;
            float t = 0f;

            // push
            while (t < half)
            {
                t += Time.deltaTime;
                SetOffset(Vector3.Lerp(Vector3.zero, impulse, t / half));
                yield return null;
            }

            // return
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                SetOffset(Vector3.Lerp(impulse, Vector3.zero, t / half));
                yield return null;
            }

            SetOffset(Vector3.zero);
            _routine = null;
        }

        private void SetOffset(Vector3 offset)
        {
            if (_localSpace) transform.localPosition = _restPosition + offset;
            else transform.position = _restPosition + offset;
        }

        private void OnDisable()
        {
            if (_hasRest)
            {
                if (_localSpace) transform.localPosition = _restPosition;
                else transform.position = _restPosition;
            }
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }
    }
}
