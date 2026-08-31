using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Giro cartoon de la corona de estrellas de stun (BUG-87): rotación continua
    /// en Y + bob suave. Vive en el root de <c>Resources/VFX_StunStars</c> (el
    /// modelo <c>Art/3D/Models/Items/Stars.fbx</c> como hijo); el ciclo de vida lo
    /// maneja <see cref="StunVfxBinder"/>.
    /// </summary>
    /// <remarks>
    /// Tiempo ESCALADO a propósito: el stun es estado de combate — si un hitstop
    /// congela el juego, las estrellas también se frenan (leerlas girando durante
    /// una pausa quedaría raro).
    /// </remarks>
    [AddComponentMenu("Rollgeon/Feedback/Stun Stars Spin")]
    public sealed class StunStarsSpin : MonoBehaviour
    {
        [SerializeField, Tooltip("Grados por segundo alrededor de Y.")]
        private float _degreesPerSecond = 120f;

        [SerializeField, Tooltip("Amplitud del bob vertical, en unidades locales. 0 = sin bob.")]
        private float _bobAmplitude = 0.05f;

        [SerializeField, Tooltip("Ciclos de bob por segundo.")]
        private float _bobFrequency = 1.5f;

        private Vector3 _restLocalPos;
        private float _elapsed;

        private void OnEnable()
        {
            _restLocalPos = transform.localPosition;
            _elapsed = 0f;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            transform.Rotate(0f, _degreesPerSecond * Time.deltaTime, 0f, Space.Self);

            if (_bobAmplitude > 0f)
            {
                float bob = Mathf.Sin(_elapsed * _bobFrequency * 2f * Mathf.PI) * _bobAmplitude;
                transform.localPosition = _restLocalPos + new Vector3(0f, bob, 0f);
            }
        }
    }
}
