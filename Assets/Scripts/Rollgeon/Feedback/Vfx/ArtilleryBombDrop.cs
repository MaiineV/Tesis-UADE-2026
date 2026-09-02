using Patterns;
using Rollgeon.Grid;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Feedback.Vfx
{
    /// <summary>
    /// Movimiento del "obús" de Ranged Artillery: nace en la posición del propio Artillery
    /// (spawneado con <see cref="SpawnPosition.AtSource"/>) y viaja en arco balístico cóncavo
    /// (parábola clásica — sube y baja) hasta la celda del jugador, en <see cref="_duration"/>
    /// segundos.
    /// </summary>
    /// <remarks>
    /// Puramente visual: no aplica daño ni sabe nada de combate — el daño real y las tiles de
    /// fuego los resuelve <c>AINode_IgniteArea</c> por su cuenta, en paralelo. Se auto-destruye
    /// al aterrizar; no depende de <c>ShouldDestroyOnParticleEnd</c> del feedback (no es un
    /// ParticleSystem).
    /// </remarks>
    /// <remarks>
    /// Resuelve el destino por su cuenta (<see cref="IPlayerService"/> + <see cref="IGridManager"/>)
    /// en vez de recibirlo del feedback: <c>FeedbackRequest</c> solo transporta UNA posición ya
    /// resuelta (el spawn), no un par origen/destino — pedirle eso al pipeline de feedback
    /// genérico es una cirugía más grande de lo que este VFX puntual justifica. Como MonoBehaviour
    /// en escena, tiene acceso directo al ServiceLocator igual que cualquier otro sistema.
    /// </remarks>
    public sealed class ArtilleryBombDrop : MonoBehaviour
    {
        [Tooltip("Segundos de vuelo desde que nace hasta que aterriza.")]
        [SerializeField, Min(0.05f)] private float _duration = 0.6f;

        [Tooltip("Altura del pico del arco.")]
        [SerializeField, Min(0f)] private float _arcHeight = 3f;

        private float _elapsed;
        private Vector3 _start;
        private Vector3 _end;

        private void Awake()
        {
            _start = transform.position;
            _end = ResolveTargetPosition() ?? _start; // sin target resuelto: no vuela, cae en el lugar
        }

        private static Vector3? ResolveTargetPosition()
        {
            if (!ServiceLocator.TryGetService<IPlayerService>(out var player) || player == null) return null;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return null;
            if (!grid.TryGetPosition(player.PlayerGuid, out var coord)) return null;
            return grid.GridToWorld(coord);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;

            var flat = Vector3.Lerp(_start, _end, t);
            // 4·h·t·(1-t) — parábola clásica: 0 en los dos extremos, pico h en t=0.5. Arco
            // balístico cóncavo real, no una caída vertical en el lugar.
            float height = 4f * _arcHeight * t * (1f - t);
            transform.position = flat + Vector3.up * height;

            if (t >= 1f)
            {
                transform.position = _end;
                Destroy(gameObject);
            }
        }
    }
}
