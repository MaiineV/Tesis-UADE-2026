using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI.Bosses.Anotador;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Estela helada del Anotador (piso 2): congela las casillas que el boss <b>acaba de pisar</b>
    /// en su repliegue. Pisarlas no hace daño — cuesta el turno (stun 1) y derrite la casilla.
    /// Va inmediatamente <b>después</b> del nodo de repliegue en el Sequence del turno.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué existe.</b> La marca de fila cae sobre la fila del jugador y el boss no está
    /// obligado a salirse de ella, así que el camino corto hasta él suele estar entero adentro de
    /// la marca. La estela cobra ese camino corto en la moneda que más duele: quedarse quieto
    /// adentro de la fila marcada, donde los 30 conectan garantizado. El zigzag la esquiva gratis
    /// porque nunca camina por donde caminó el boss.
    /// </para>
    /// <para>
    /// <b>De dónde salen las casillas.</b> De <see cref="AnotadorIceStunBinder"/>, que graba el
    /// path real que publicó <c>IMovementService.OnEntityMoved</c>. No se reconstruye con
    /// <c>FindPath</c>: después de moverse la ocupancia cambió y el camino recalculado podría no
    /// ser el que caminó, congelando casillas mentirosas.
    /// </para>
    /// <para>
    /// <b>No abortar el turno.</b> "No me repliegué este turno" es el caso mayoritario (el boss solo
    /// se mueve si lo tienen a 3 casillas o menos) y devuelve <see cref="AIResult.Succeeded"/> como
    /// no-op transparente: un <see cref="AIResult.Failed"/> ahí cortaría el <see cref="AINode_Sequence"/>
    /// del turno y el boss perdería la marca de fila, que es su único ataque — el mismo bug que dejó
    /// quieto al Sunken Grand. Los <c>Failed</c> que quedan son de configuración (sin
    /// <see cref="Hazard"/>, sin <see cref="IHazardService"/>), y aun así el árbol lo cuelga de un
    /// <c>Selector[IceTrail, Wait]</c>.
    /// </para>
    /// <para>
    /// <b>Estela única.</b> Con <see cref="ReplacePreviousTrail"/> el repliegue de este turno mata la
    /// estela del turno pasado antes de publicar la nueva: dos estelas vivas superpondrían overlays y
    /// duplicarían las casillas congeladas de un boss que "deja una estela", en singular.
    /// </para>
    /// <para>
    /// <b>Sin presentación propia a propósito.</b> El hazard ya trae su <c>VFX_IceBurst</c>, así que
    /// las casillas se ven al congelarse. Del lado del jefe no hay gesto que agregar: la estela es
    /// consecuencia pasiva del repliegue —cae en el mismo tick, justo después de que se movió— y el
    /// rig del Anotador (<c>ChestMimic</c>) sólo tiene <c>Attack</c> y <c>Awaken</c>. Reproducir
    /// <c>Attack</c> acá haría leer un ataque que no existe y bloquearía el turno detrás de él.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_IceTrail : AIActionNode
    {
        [Tooltip("Definición del hielo. Debe tener Trigger = OnEnter, Damage = 0, " +
                 "ConsumeOnTrigger = true (la casilla se derrite al pisarla, así no hay cadenas de " +
                 "stun) y DurationRounds = 4. Ojo con ese último: la estela nace en el turno del " +
                 "jefe, cuando el jugador ya jugó el suyo (CNF-006), y la duración se descuenta en " +
                 "el wrap de ronda — DurationRounds = D deja D-1 rondas pisables, así que las 3 de " +
                 "la ficha piden 4. Ver HazardDefinitionSO.")]
        public HazardDefinitionSO Hazard;

        [Tooltip("Tope de casillas congeladas por repliegue. El repliegue camina como máximo " +
                 "MaxSteps del nodo de movimiento: para que 4 sea tope y no recorte, ese MaxSteps " +
                 "tiene que ser 4.")]
        [MinValue(1)]
        public int MaxTiles = 4;

        [Tooltip("Turnos de stun al pisar la estela. ApplyStun toma max(actual, nuevo): dos " +
                 "pisadas seguidas siguen siendo 1 turno.")]
        [MinValue(1)]
        public int StunTurns = 1;

        [Tooltip("Si true, la estela nueva reemplaza la del turno anterior (una sola estela viva).")]
        public bool ReplacePreviousTrail = true;

        /// <summary>Instancia viva publicada por este nodo. Por pelea: el árbol se clona al spawn.</summary>
        [NonSerialized] private Guid _liveTrailId;

        public override string NodeName => "Ice Trail (retreat tiles)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;
            if (Hazard == null)
            {
                Debug.LogError("[AINode_IceTrail] Sin HazardDefinitionSO asignada — no hay estela helada.");
                return AIResult.Failed;
            }

            var binder = AnotadorIceStunBinder.ResolveOrCreate();
            if (binder == null) return AIResult.Failed;

            // Sin repliegue este turno ⇒ no-op transparente (ver remarks: un Failed acá le come
            // la marca de fila al boss).
            if (!binder.TryConsumeWalkedTiles(context.SelfGuid, out var walked)) return AIResult.Succeeded;

            if (!ServiceLocator.TryGetService<IHazardService>(out var hazards) || hazards == null)
            {
                Debug.LogError("[AINode_IceTrail] IHazardService no registrado. " +
                               "Agregá HazardServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return AIResult.Failed;
            }

            var tiles = TrimToLast(walked, MaxTiles);
            if (tiles.Count == 0) return AIResult.Succeeded;

            if (ReplacePreviousTrail && _liveTrailId != Guid.Empty)
            {
                hazards.Deactivate(_liveTrailId);
                binder.ForgetTrail(_liveTrailId);
                _liveTrailId = Guid.Empty;
            }

            var instanceId = hazards.Activate(Hazard, tiles);
            if (instanceId == Guid.Empty) return AIResult.Failed;

            // Trackear DESPUÉS de activar: el binder necesita el id para reconocer sus propios
            // triggers y saber a quién no stunear (el dueño de la estela).
            binder.TrackTrail(instanceId, Hazard, context.SelfGuid, StunTurns);
            _liveTrailId = instanceId;
            return AIResult.Succeeded;
        }

        /// <summary>
        /// Últimas <paramref name="max"/> casillas del recorrido. Se recorta por el final y no por
        /// el principio: si alguna vez el repliegue camina más de <paramref name="max"/> pasos, las
        /// casillas que importan son las pegadas a su posición final — las que pisa quien lo
        /// persigue por el camino corto.
        /// </summary>
        private static List<GridCoord> TrimToLast(List<GridCoord> walked, int max)
        {
            if (walked == null) return new List<GridCoord>();
            int cap = max < 1 ? 1 : max;
            if (walked.Count <= cap) return walked;

            return walked.GetRange(walked.Count - cap, cap);
        }
    }
}
