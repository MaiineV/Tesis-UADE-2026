using MoreMountains.Feedbacks;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Disparo seguro de <see cref="MMF_Player"/>s de juice. Necesario porque
    /// <c>StopFeedbacks</c> a mitad de un spring feedback re-basa su punto de reposo
    /// al valor desplazado del momento (<c>MMF_*Spring.CustomStopFeedback</c> hace
    /// <c>_targetValue = _currentValue</c>): sin un <c>RestoreInitialValues</c>, el
    /// target queda squashado/rotado permanente para el resto de la sesión (bug del
    /// "primer dado squashado", 2026-07-13).
    /// </summary>
    internal static class MmfJuice
    {
        /// <summary>
        /// Captura el reposo de los springs YA, con el target quieto, y pasa el player
        /// a modo Script para que su <c>Start()</c> no re-capture. El default
        /// (InitializationMode.Start) corre 1 frame después de activarse la zona — el
        /// slot 0 arranca a girar sin stagger ese mismo frame, así que su lock/reveal
        /// capturaban ~45° de spin y el squash de anticipación como "reposo" (bug del
        /// primer dado; los slots 1-4 zafaban por el stagger de 50ms+). Llamar desde
        /// OnEnable: la activación siempre precede al roll dentro del frame.
        /// </summary>
        public static void CaptureRestPose(MMF_Player player)
        {
            if (player == null || player.FeedbacksList == null) return;
            player.InitializationMode = MMFeedbacks.InitializationModes.Script;
            player.Initialization();
        }

        /// <summary>Replay limpio: corta lo que esté a medias, devuelve los springs a su reposo autorado y dispara.</summary>
        public static void Replay(MMF_Player player, float intensity = 1f)
        {
            if (player == null) return;
            if (player.IsPlaying) player.StopFeedbacks();
            // Guardeado por PlayCount adentro — no-op en players nunca reproducidos.
            player.RestoreInitialValues();
            if (intensity != 1f) player.PlayFeedbacks(player.transform.position, intensity);
            else player.PlayFeedbacks();
        }

        /// <summary>
        /// Frena y deja el target en su pose de reposo. Para teardowns: el
        /// <c>StopFeedbacksOnDisable</c> del propio player puede correr antes o después
        /// que el OnDisable del caller (orden no garantizado por Unity) — el restore
        /// incondicional limpia el re-baseo en ambos órdenes.
        /// </summary>
        public static void Rest(MMF_Player player)
        {
            if (player == null) return;
            if (player.IsPlaying) player.StopFeedbacks();
            player.RestoreInitialValues();
        }
    }
}
