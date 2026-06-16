using System;
using System.Collections;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Componente que se adjunta al GameObject del feedback y dispara <see cref="OnCompleted"/>
    /// cuando un particle system para (<c>stopAction = Callback</c>) o cuando un Animator state
    /// llega a <c>normalizedTime ≥ 1</c>. TECHNICAL.md §10.11.
    /// </summary>
    /// <remarks>
    /// Guard <c>_completed</c> garantiza single-fire. El listener no se destruye a sí mismo —
    /// eso es responsabilidad del dueño (el manager o el owner del objeto).
    /// </remarks>
    public sealed class FeedbackCallbackListener : MonoBehaviour
    {
        public event Action OnCompleted;

        private bool _completed;
        private Coroutine _routine;

        public bool IsCompleted => _completed;

        // ==================================================================
        // Particle end
        // ==================================================================
        public void ListenForParticleEnd()
        {
            var ps = GetComponent<ParticleSystem>() ?? GetComponentInChildren<ParticleSystem>();
            if (ps == null) return;
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.Callback;
        }

        private void OnParticleSystemStopped() => Complete();

        // ==================================================================
        // Animator state end
        // ==================================================================
        public void ListenForAnimatorStateEnd(Animator animator, string triggerName, float safetyTimeout)
        {
            if (animator == null) { Complete(); return; }
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(AnimatorEndRoutine(animator, triggerName, safetyTimeout));
        }

        /// <summary>Hook para Animation Events: el autoral agrega un evento que invoca este método.</summary>
        public void OnFeedbackAnimationComplete() => Complete();

        private IEnumerator AnimatorEndRoutine(Animator animator, string trigger, float timeout)
        {
            float deadline = Time.time + Mathf.Max(0.1f, timeout);
            // Espera 1 frame — da tiempo a que el SetTrigger llegue al state target.
            yield return null;
            while (!_completed && Time.time < deadline)
            {
                if (animator == null) break;
                if (!animator.IsInTransition(0))
                {
                    var info = animator.GetCurrentAnimatorStateInfo(0);
                    if (info.normalizedTime >= 1f) break;
                }
                yield return null;
            }
            Complete();
        }

        private void Complete()
        {
            if (_completed) return;
            _completed = true;
            OnCompleted?.Invoke();
        }
    }
}
