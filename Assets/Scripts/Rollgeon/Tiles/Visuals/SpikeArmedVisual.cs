using Rollgeon.Tiles.Visuals;
using UnityEngine;

namespace Rollgeon.Tiles.Visuals
{
    /// <summary>
    /// Sube y baja los pinchos según la celda esté armada o disparada.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sin esto un pincho bajado se ve idéntico a uno armado, y entonces la regla "el que se disparó
    /// queda bajado hasta el cierre de ronda" es invisible: el jugador lo pisa, no le pasa nada, y lee
    /// un bug en vez de una regla. El estado ya viajaba —
    /// <see cref="SpecialTileVisualBinding.OnArmed"/> y <see cref="SpecialTileVisualBinding.OnDisarmed"/>
    /// existen desde que se escribió el binding — pero ningún prefab de arte los escuchaba.
    /// </para>
    /// <para>
    /// Se engancha por código y no por el Inspector a propósito: el prefab de pinchos lo genera un
    /// instalador, así que un wiring de UnityEvent serializado se perdería en cada regeneración.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/Tiles/Spike Armed Visual")]
    [RequireComponent(typeof(SpecialTileVisualBinding))]
    public sealed class SpikeArmedVisual : MonoBehaviour
    {
        [Tooltip("Qué se hunde al dispararse. Vacío = este mismo transform.")]
        public Transform Spikes;

        [Tooltip("Cuánto baja el pincho disparado, en unidades de mundo.")]
        public float SunkDepth = 0.32f;

        [Tooltip("Segundos del movimiento. 0 = instantáneo.")]
        public float Seconds = 0.12f;

        private Vector3 _up;
        private Vector3 _down;
        private Coroutine _moving;

        private void Awake()
        {
            if (Spikes == null) Spikes = transform;

            _up = Spikes.localPosition;
            _down = _up + Vector3.down * SunkDepth;

            // El binding no sabe de pinchos: publica el estado y quien quiera lo escucha.
            var binding = GetComponent<SpecialTileVisualBinding>();
            binding.OnArmed.AddListener(Raise);
            binding.OnDisarmed.AddListener(Sink);
        }

        /// <summary>Arranca armado: es el estado de una celda recién puesta.</summary>
        private void OnEnable() => Apply(_up);

        private void Raise() => MoveTo(_up);

        private void Sink() => MoveTo(_down);

        private void MoveTo(Vector3 target)
        {
            if (!isActiveAndEnabled || Seconds <= 0f)
            {
                Apply(target);
                return;
            }

            if (_moving != null) StopCoroutine(_moving);
            _moving = StartCoroutine(Slide(target));
        }

        private void Apply(Vector3 target)
        {
            if (Spikes != null) Spikes.localPosition = target;
        }

        private System.Collections.IEnumerator Slide(Vector3 target)
        {
            var from = Spikes.localPosition;
            for (float t = 0f; t < Seconds; t += Time.deltaTime)
            {
                Apply(Vector3.Lerp(from, target, t / Seconds));
                yield return null;
            }

            Apply(target);
            _moving = null;
        }
    }
}
