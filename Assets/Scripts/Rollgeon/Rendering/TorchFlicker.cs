using UnityEngine;

namespace Rollgeon.Rendering
{
    /// <summary>
    /// Le da vida a una luz puntual/spot tipo antorcha: ruido Perlin sobre la
    /// intensidad (nunca se apaga del todo ni se dispara), más un jitter chico
    /// opcional de posición y temperatura de color, para que no se sienta como
    /// una luz estática con un número fijo.
    /// </summary>
    [RequireComponent(typeof(Light))]
    [AddComponentMenu("Rollgeon/Lighting/Torch Flicker")]
    public class TorchFlicker : MonoBehaviour
    {
        [Header("Intensity Noise")]
        [Tooltip("Intensidad base sobre la que oscila el ruido.")]
        public float BaseIntensity = 2f;
        [Tooltip("Cuánto se aleja la intensidad del valor base (+/-).")]
        [Range(0f, 2f)] public float IntensityAmplitude = 0.4f;
        [Tooltip("Velocidad del ruido — más alto = titila más rápido.")]
        public float NoiseSpeed = 1.5f;

        [Header("Range Noise (opcional)")]
        [Tooltip("Cuánto se aleja el Range del valor base (+/-), con el mismo ruido que la " +
                 "intensidad — la luz se agranda/achica en sync con el brillo. 0 = Range fijo.")]
        [Range(0f, 2f)] public float RangeAmplitude = 0f;

        [Header("Position Jitter (opcional)")]
        [Tooltip("Desplaza la luz un poco cada frame, como si la llama se moviera. 0 = quieta.")]
        [Range(0f, 0.2f)] public float PositionJitter = 0f;
        public float PositionJitterSpeed = 4f;

        [Header("Color Drift (opcional)")]
        [Tooltip("Si está activo, mezcla entre 2 colores con el mismo ruido de intensidad " +
                 "(color frío en los valles, cálido en los picos) en vez de un color fijo.")]
        public bool UseColorDrift = false;
        public Color ColorLow = new Color(0.85f, 0.35f, 0.1f);
        public Color ColorHigh = new Color(1f, 0.65f, 0.25f);

        private Light _light;
        private Vector3 _localPosition;
        private float _baseRange;
        // Semillas por-instancia (hash de la posición inicial) para que varias
        // antorchas en la misma sala no titilen sincronizadas con el mismo patrón.
        private float _seedIntensity;
        private float _seedJitterX;
        private float _seedJitterY;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _localPosition = transform.localPosition;
            _baseRange = _light.range;

            float hash = Mathf.Abs(Mathf.Sin(Vector3.Dot(transform.position, new Vector3(12.9898f, 78.233f, 37.719f))) * 43758.5453f);
            hash -= Mathf.Floor(hash);
            _seedIntensity = hash * 100f;
            _seedJitterX = hash * 57.31f;
            _seedJitterY = hash * 91.77f;
        }

        private void Update()
        {
            float t = Time.time * NoiseSpeed;

            // Perlin en [0,1] -> remapeado a [-1,1] para oscilar simétrico alrededor de BaseIntensity.
            float noise = Mathf.PerlinNoise(_seedIntensity + t, 0f) * 2f - 1f;
            float intensity01 = noise * 0.5f + 0.5f; // [0,1] normalizado, para el color drift
            _light.intensity = Mathf.Max(0f, BaseIntensity + noise * IntensityAmplitude);

            if (RangeAmplitude > 0f)
                _light.range = Mathf.Max(0.01f, _baseRange + noise * RangeAmplitude);

            if (UseColorDrift)
                _light.color = Color.Lerp(ColorLow, ColorHigh, intensity01);

            if (PositionJitter > 0f)
            {
                float jt = Time.time * PositionJitterSpeed;
                float jx = (Mathf.PerlinNoise(_seedJitterX + jt, 0f) * 2f - 1f) * PositionJitter;
                float jy = (Mathf.PerlinNoise(_seedJitterY + jt, 0f) * 2f - 1f) * PositionJitter;
                transform.localPosition = _localPosition + new Vector3(jx, jy, 0f);
            }
        }
    }
}
