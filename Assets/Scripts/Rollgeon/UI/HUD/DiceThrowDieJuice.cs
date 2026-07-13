using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Juice por-dado de los dados arrojables (2D y 3D): players MMF autorados en el
    /// prefab del dado. El presenter lo llama DIRECTO — es dueño del lifetime del
    /// ghost y nunca invoca después del Destroy; los eventos por índice quedan para
    /// la capa de zona (<see cref="DiceThrowJuice"/>, que no puede retener views).
    /// Campos opcionales: sin wiring, no-op.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Dice Throw Die Juice")]
    public sealed class DiceThrowDieJuice : MonoBehaviour
    {
        [SerializeField, Optional, Tooltip("Pop al agarrar (scale bump chico).")]
        private MMF_Player _pickupPopPlayer;

        [SerializeField, Optional, Tooltip("Squash al rebotar contra un borde/pared.")]
        private MMF_Player _bounceSquashPlayer;

        [SerializeField, Optional, Tooltip("Pop al asentarse mostrando la cara.")]
        private MMF_Player _settlePopPlayer;

        [SerializeField, Optional, Tooltip("Variante crit (cara alta). Sin wiring cae al settle pop.")]
        private MMF_Player _critPopPlayer;

        [SerializeField, Tooltip("Cara desde la que el settle usa la variante crit.")]
        private int _critFace = 6;

        [SerializeField, Tooltip("Cara 'alta' — el settle pop escala su intensidad desde acá.")]
        private int _highFace = 4;

        [SerializeField]
        private float _highFaceIntensity = 1.3f;

        private void OnEnable()
        {
            // Instanciado en runtime: capturar el reposo YA, con el dado quieto, o el
            // player capturaría un frame de vuelo/drop-in como "reposo" (el bug del
            // primer dado squashado — ver MmfJuice).
            MmfJuice.CaptureRestPose(_pickupPopPlayer);
            MmfJuice.CaptureRestPose(_bounceSquashPlayer);
            MmfJuice.CaptureRestPose(_settlePopPlayer);
            MmfJuice.CaptureRestPose(_critPopPlayer);
        }

        public void PlayPickup()
        {
            if (DiceAnim.DiceUiMotionPrefs.ReducedMotion) return;
            MmfJuice.Replay(_pickupPopPlayer);
        }

        /// <summary>Squash de impacto. <paramref name="intensity"/> ~0.5 rebote suave, 1 impacto fuerte.</summary>
        public void PlayBounce(float intensity)
        {
            if (DiceAnim.DiceUiMotionPrefs.ReducedMotion) return;
            MmfJuice.Replay(_bounceSquashPlayer, intensity);
        }

        public void PlaySettle(int face)
        {
            if (DiceAnim.DiceUiMotionPrefs.ReducedMotion) return;
            if (face >= _critFace && _critPopPlayer != null)
            {
                MmfJuice.Replay(_critPopPlayer);
                return;
            }
            MmfJuice.Replay(_settlePopPlayer, face >= _highFace ? _highFaceIntensity : 1f);
        }
    }
}
