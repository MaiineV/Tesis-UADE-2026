using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Puente entre Unity Animation Events y el sequencer de feedback. TECHNICAL.md §10.8.2.
    /// Se pega al GameObject del Animator (pawn o VFX) y el autoral engancha
    /// <see cref="PushFeedbackEvent"/> en un Animation Event del clip, pasando el key
    /// como parámetro string. Al llegar al frame, el key se publica en el bus de la
    /// secuencia activa (<see cref="FeedbackSequenceRuntime.Current"/>), destrabando
    /// los steps cuyo <c>StartMode</c> / <c>EndMode</c> sea <c>OnEvent</c>.
    /// </summary>
    /// <remarks>
    /// Publicar es puramente una señal de secuenciación — la animación nunca se
    /// interrumpe (invariante §10.3: "done" es marca de orden, no comando de stop).
    /// Sin secuencia activa el publish es no-op (§10.8.2).
    /// </remarks>
    public class AnimationFeedbackEvent : MonoBehaviour
    {
        /// <summary>
        /// Llamar desde un Animation Event con un parámetro string igual al event key deseado.
        /// </summary>
        public void PushFeedbackEvent(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning($"[AnimationFeedbackEvent] Animation Event sin key en '{name}'.");
                return;
            }

            FeedbackSequenceRuntime.Publish(key);
        }
    }
}
