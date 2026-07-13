using PrimeTween;
using UnityEngine;

namespace Rollgeon.UI.HUD.DiceAnim
{
    /// <summary>
    /// Tuning de las animaciones del panel de dados legacy (modo Classic): spin del
    /// roll, raise del hold y outro del confirm. Cualquier duración &lt;= 0 aplica el
    /// cambio instantáneo (convención de la casa — permite tests EditMode y sirve de
    /// kill switch). Los modos 2D/3D no usan este SO (sus presenters ya animan).
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/UI/Dice UI Animation Settings",
        fileName = "DiceUiAnimationSettings")]
    public class DiceUiAnimationSettingsSO : ScriptableObject
    {
        [Header("Roll — Spin en el lugar")]
        [Tooltip("Duración del giro antes de revelar el resultado.")]
        public float SpinSeconds = 0.5f;

        [Tooltip("Intervalo base entre caras random durante el giro.")]
        public float SpinTickSeconds = 0.06f;

        [Tooltip("Desaceleración del ciclado de caras: 1 = ritmo constante, " +
                 ">1 = arranca rápido y frena hacia el reveal.")]
        [Min(1f)] public float SpinDecelerationPower = 2f;

        [Tooltip("Delay entre slots para que no revelen todos juntos.")]
        public float SpinStaggerSeconds = 0.05f;

        [Tooltip("Vueltas completas (Z) que da el sprite durante el giro.")]
        public float SpinTurns = 2f;

        public Ease SpinEase = Ease.OutCubic;

        [Tooltip("Cara máxima mostrada como preview durante el giro (d6 = 6). Si la " +
                 "cara real supera esto, el rango se extiende hasta la cara real.")]
        [Min(2)] public int PreviewFaceMax = 6;

        [Tooltip("Altura (px) del salto parabólico durante el giro — el dado 'rueda en " +
                 "el aire'. 0 = sin salto.")]
        public float SpinJumpHeight = 14f;

        [Header("Lock — Raise del dado holdeado")]
        [Tooltip("Cuánto se eleva el dado al holdearse (px del canvas).")]
        public float RaiseOffsetY = 18f;

        public float RaiseSeconds = 0.12f;

        [Tooltip("Duración de la bajada al des-holdear.")]
        public float LowerSeconds = 0.10f;

        public Ease RaiseEase = Ease.OutCubic;

        [Tooltip("Amplitud (px) del bob flotante del dado holdeado. 0 = sin bob.")]
        public float HoldBobAmplitude = 3f;

        [Tooltip("Período (segundos) de un ciclo completo del bob.")]
        public float HoldBobSeconds = 1.8f;

        [Header("Confirm — Throw al centro de la mesa (holdeados)")]
        public float ThrowSeconds = 0.35f;

        [Tooltip("Delay entre dados lanzados.")]
        public float ThrowStaggerSeconds = 0.05f;

        [Tooltip("Escala final al llegar al centro.")]
        public float ThrowEndScale = 0.6f;

        [Tooltip("Fracción final del vuelo en la que ocurre el fade (0.5 = última mitad).")]
        [Range(0f, 1f)] public float ThrowFadePortion = 0.5f;

        public Ease ThrowEase = Ease.InCubic;

        [Tooltip("Altura (px) del arco parabólico del vuelo. 0 = línea recta.")]
        public float ThrowArcHeight = 40f;

        [Tooltip("Grados de rotación Z durante el vuelo. 0 = sin rotación.")]
        public float ThrowSpinDegrees = 180f;

        [Header("Confirm — Descarte (no usados)")]
        public float DiscardSeconds = 0.25f;

        [Tooltip("Escala final del scale-down del descarte.")]
        public float DiscardEndScale = 0.7f;

        [Tooltip("Caída (px) hacia abajo durante el descarte — gravedad fake. 0 = en el lugar.")]
        public float DiscardFallDistance = 30f;

        [Tooltip("Rotación Z (grados) durante la caída del descarte.")]
        public float DiscardSpinDegrees = -35f;

        public Ease DiscardEase = Ease.InQuad;

        [Header("Outro")]
        [Tooltip("Pausa tras el último tween antes de limpiar los slots.")]
        public float OutroHoldSeconds = 0.05f;

        /// <summary>Snapshot plano para el choreographer (testeable sin SO).</summary>
        public DiceAnimTimings ToTimings() => new DiceAnimTimings
        {
            SpinSeconds = SpinSeconds,
            SpinTickSeconds = SpinTickSeconds,
            SpinDecelerationPower = SpinDecelerationPower,
            SpinStaggerSeconds = SpinStaggerSeconds,
            SpinTurns = SpinTurns,
            PreviewFaceMax = PreviewFaceMax,
            RaiseOffsetY = RaiseOffsetY,
            RaiseSeconds = RaiseSeconds,
            LowerSeconds = LowerSeconds,
            ThrowSeconds = ThrowSeconds,
            ThrowStaggerSeconds = ThrowStaggerSeconds,
            ThrowEndScale = ThrowEndScale,
            ThrowFadePortion = ThrowFadePortion,
            DiscardSeconds = DiscardSeconds,
            DiscardEndScale = DiscardEndScale,
            OutroHoldSeconds = OutroHoldSeconds,
        };
    }
}
