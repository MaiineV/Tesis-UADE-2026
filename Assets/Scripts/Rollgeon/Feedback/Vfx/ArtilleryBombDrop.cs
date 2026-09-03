using System;
using Patterns;
using Rollgeon.Combat.Threat;
using Rollgeon.Entities.Visuals;
using Rollgeon.Grid;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Feedback.Vfx
{
    /// <summary>
    /// Movimiento del "obús" de Ranged Artillery: nace en la posición del propio Artillery
    /// (spawneado con <see cref="SpawnPosition.AtSource"/>) y viaja en arco balístico cóncavo
    /// (parábola clásica — sube y baja) hasta el CENTRO del área telegrafiada, en
    /// <see cref="_duration"/> segundos.
    /// </summary>
    /// <remarks>
    /// El destino es el centro congelado de <see cref="LastThreatenedAreaCenter"/> — la celda del
    /// jugador AL MARCAR, no su posición actual — y no <see cref="IPlayerService.PlayerGuid"/> en
    /// vivo: si el jugador esquivó moviéndose fuera del área después del telegraph, el daño real
    /// (<c>AINode_ExecuteTelegraph.Resolve</c>) ya no le pega, y el obús que igual volara hasta su
    /// posición actual mentiría — parecería que sí conectó. Sin marca congelada disponible (VFX
    /// reusado fuera de ese flujo) cae a la posición del jugador como aproximación razonable.
    /// </remarks>
    /// <remarks>
    /// Puramente visual: no aplica daño — el daño real lo resuelve <c>AINode_ExecuteTelegraph</c>
    /// por su cuenta, en paralelo. Se auto-destruye al aterrizar; no depende de
    /// <c>ShouldDestroyOnParticleEnd</c> del feedback (no es un ParticleSystem).
    /// </remarks>
    /// <remarks>
    /// Resuelve el destino y el disparador de animación por su cuenta en vez de recibirlos del
    /// feedback: <c>FeedbackRequest</c> solo transporta UNA posición ya resuelta (el spawn), no un
    /// par origen/destino ni un <c>SourceGuid</c> — pedirle eso al pipeline de feedback genérico es
    /// una cirugía más grande de lo que este VFX puntual justifica. Como MonoBehaviour en escena,
    /// tiene acceso directo al ServiceLocator igual que cualquier otro sistema. El propio Artillery
    /// se identifica leyendo el OCUPANTE de la celda en la que este VFX nace — nace exactamente
    /// encima suyo (<c>SpawnPosition.AtSource</c>), así que es él.
    /// </remarks>
    public sealed class ArtilleryBombDrop : MonoBehaviour
    {
        [Tooltip("Segundos de vuelo desde que nace hasta que aterriza.")]
        [SerializeField, Min(0.05f)] private float _duration = 0.6f;

        [Tooltip("Altura del pico del arco.")]
        [SerializeField, Min(0f)] private float _arcHeight = 3f;

        [Tooltip("Trigger del Animator del Artillery a disparar al nacer — saca al rig del pose de " +
                 "carga (Charge_Loop) sin depender de AnimTrigger del feedback VFX, que el " +
                 "dispatcher ignora para entries Type=VFX.")]
        [SerializeField] private string _sourceAnimTrigger = "Attack";

        private float _elapsed;
        private Vector3 _start;
        private Vector3 _end;

        private void Awake()
        {
            _start = transform.position;

            var sourceGuid = ResolveSourceGuid();
            FireSourceAnimTrigger(sourceGuid);
            _end = ResolveTargetPosition(sourceGuid) ?? _start; // sin target resuelto: no vuela, cae en el lugar
        }

        /// <summary>El ocupante de la celda en la que este VFX nació — el propio Artillery.</summary>
        private Guid ResolveSourceGuid()
        {
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return Guid.Empty;
            var coord = grid.WorldToGrid(_start);
            return grid.TryGetOccupant(coord, out var occupant) ? occupant : Guid.Empty;
        }

        private void FireSourceAnimTrigger(Guid sourceGuid)
        {
            if (sourceGuid == Guid.Empty || string.IsNullOrEmpty(_sourceAnimTrigger)) return;
            if (!ServiceLocator.TryGetService<IEntityVisualService>(out var visuals) || visuals == null) return;
            if (visuals.TryGetPawn(sourceGuid, out var pawn) && pawn != null)
                pawn.TrySetTrigger(_sourceAnimTrigger); // no-op silencioso si el rig no tiene ese trigger
        }

        private static Vector3? ResolveTargetPosition(Guid sourceGuid)
        {
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return null;

            if (sourceGuid != Guid.Empty && LastThreatenedAreaCenter.TryGet(sourceGuid, out var center))
                return grid.GridToWorld(center);

            // Fallback: sin centro congelado (VFX disparado fuera del flujo de telegraph), la
            // posición actual del jugador es la mejor aproximación disponible.
            if (!ServiceLocator.TryGetService<IPlayerService>(out var player) || player == null) return null;
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
