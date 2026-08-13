using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Status;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Movement;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Anotador
{
    /// <summary>
    /// Glue del hielo del Anotador (piso 2). Dos trabajos que nadie más puede hacer:
    /// <list type="number">
    ///   <item><description><b>Grabar el repliegue.</b> Escucha
    ///   <see cref="IMovementService.OnEntityMoved"/> y guarda, por entidad, las casillas que
    ///   <i>pisó</i> en su último movimiento. <see cref="Decisions.AINode_IceTrail"/> las consume
    ///   para congelar exactamente el camino que el boss caminó, no una reconstrucción.</description></item>
    ///   <item><description><b>Traducir hazard → stun.</b> Escucha <c>OnHazardTriggered</c> y
    ///   aplica <see cref="IStunService.ApplyStun"/> a quien pisó una estela <b>propia</b>. El
    ///   hazard no sabe nada de stun (su <c>Damage</c> es 0) — la estela paga en turnos, no en
    ///   HP.</description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué el path no se puede reconstruir después.</b> <see cref="IMovementService.Move"/>
    /// resuelve el camino y lo publica en el evento; una vez ejecutado, el origen ya no existe en
    /// ninguna parte. Un <c>FindPath(origen, destino)</c> a posteriori tampoco sirve: la ocupancia
    /// cambió, así que podría devolver otro camino y la estela congelaría casillas por las que el
    /// boss nunca pasó. De ahí que el grabador tenga que estar escuchando <b>antes</b> del
    /// repliegue, y de ahí el auto-install de abajo.
    /// </para>
    /// <para>
    /// <b>Auto-install.</b> El binder se instala solo en runtime
    /// (<see cref="RuntimeInitializeOnLoadMethod"/>) en vez de pedir una entry en
    /// <c>ServiceBootstrap.ExtraServices</c>: si esperara a que el nodo lo cree en su primer tick,
    /// ese primer tick ya sería <i>después</i> del primer repliegue y el boss perdería la estela de
    /// su primer turno. Sin trails trackeadas el costo es un dict vacío y un handler que retorna
    /// en la primera línea.
    /// </para>
    /// <para>
    /// <b>El boss no se congela a sí mismo.</b> Cada trail recuerda su dueño y los triggers de ese
    /// guid se ignoran: un turno después el repliegue puede cruzar su propia estela y auto-stunearse
    /// se leería como bug (y le regalaría al jugador un turno gratis). El derretido de esa casilla
    /// lo decide <see cref="HazardDefinitionSO.ConsumeOnTrigger"/> y ocurre dentro de
    /// <see cref="HazardService"/> — pero el repliegue publica una estela nueva sobre las mismas
    /// casillas en el mismo tick, así que el hielo no desaparece.
    /// </para>
    /// <para>
    /// <b>Sin cadenas de stun.</b> Dos disparos seguidos no suman:
    /// <see cref="IStunService.ApplyStun"/> es <c>max(actual, nuevo)</c> y la casilla pisada se
    /// derrite. El binder no agrega lógica de acumulación a propósito.
    /// </para>
    /// </remarks>
    public sealed class AnotadorIceStunBinder : IDisposable
    {
        /// <summary>Datos con los que una estela viva se cobra: a quién NO stunear y cuántos turnos.</summary>
        private readonly struct TrailInfo
        {
            public readonly Guid OwnerGuid;
            public readonly int StunTurns;

            public TrailInfo(Guid ownerGuid, int stunTurns)
            {
                OwnerGuid = ownerGuid;
                StunTurns = stunTurns;
            }
        }

        private readonly Dictionary<Guid, TrailInfo> _trails = new Dictionary<Guid, TrailInfo>();

        // Red de seguridad por definición: si el binder se recreó (ServiceLocator.Clear entre runs)
        // y perdió los instanceId, una instancia cuya Definition es la del hielo sigue siendo suya.
        private readonly Dictionary<HazardDefinitionSO, TrailInfo> _byDefinition =
            new Dictionary<HazardDefinitionSO, TrailInfo>();

        private readonly Dictionary<Guid, List<GridCoord>> _lastWalkedTiles =
            new Dictionary<Guid, List<GridCoord>>();

        // Mismo criterio que HazardService: guardamos la instancia EXACTA a la que nos suscribimos.
        // IMovementService es run-scoped y este binder no, así que un `-=` resuelto del locator
        // podría apuntar a la instancia nueva y dejar la suscripción vieja viva para siempre.
        private IMovementService _movementSubscribedTo;

        private EventManager.EventReceiver _onHazardTriggeredHandler;
        private EventManager.EventReceiver _onHazardExpiredHandler;
        private EventManager.EventReceiver _onTurnQueueBuiltHandler;
        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        // ======================================================================
        // Lifecycle
        // ======================================================================

        /// <summary>
        /// Devuelve el binder registrado, creándolo si hace falta, y reintenta la suscripción a
        /// movimiento (el servicio es run-scoped: puede no existir todavía la primera vez).
        /// Mismo idiom que <c>ThreatTelegraphOverlay.ResolveOrCreate</c>.
        /// </summary>
        public static AnotadorIceStunBinder ResolveOrCreate()
        {
            if (ServiceLocator.TryGetService<AnotadorIceStunBinder>(out var existing) && existing != null)
            {
                existing.EnsureMovementSubscription();
                return existing;
            }

            var binder = new AnotadorIceStunBinder();
            binder.Register();
            return binder;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall() => ResolveOrCreate();

        /// <summary>
        /// Suscribe handlers y se registra en el locator. Público para que los tests EditMode lo
        /// instancien a mano (mismo patrón que <see cref="HazardService.Register"/>).
        /// </summary>
        public void Register()
        {
            _onHazardTriggeredHandler = OnHazardTriggeredExternal;
            _onHazardExpiredHandler = OnHazardExpiredExternal;
            _onTurnQueueBuiltHandler = OnTurnQueueBuiltExternal;
            _onCombatEndHandler = OnScopeEndedExternal;
            _onRunEndHandler = OnScopeEndedExternal;

            EventManager.Subscribe(EventName.OnHazardTriggered, _onHazardTriggeredHandler);
            EventManager.Subscribe(EventName.OnHazardExpired, _onHazardExpiredHandler);
            EventManager.Subscribe(EventName.OnTurnQueueBuilt, _onTurnQueueBuiltHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);

            ServiceLocator.AddService<AnotadorIceStunBinder>(this, ServiceScope.Global);
            EnsureMovementSubscription();
        }

        public void Dispose()
        {
            if (_onHazardTriggeredHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnHazardTriggered, _onHazardTriggeredHandler);
                _onHazardTriggeredHandler = null;
            }
            if (_onHazardExpiredHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnHazardExpired, _onHazardExpiredHandler);
                _onHazardExpiredHandler = null;
            }
            if (_onTurnQueueBuiltHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnTurnQueueBuilt, _onTurnQueueBuiltHandler);
                _onTurnQueueBuiltHandler = null;
            }
            if (_onCombatEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
                _onCombatEndHandler = null;
            }
            if (_onRunEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEndHandler);
                _onRunEndHandler = null;
            }

            ResetAll();
        }

        // ======================================================================
        // API para el nodo
        // ======================================================================

        /// <summary>
        /// Registra una estela recién activada: <paramref name="instanceId"/> es suya, su dueño
        /// (<paramref name="ownerGuid"/>) no se stunea, y pisarla cuesta
        /// <paramref name="stunTurns"/> turnos.
        /// </summary>
        public void TrackTrail(Guid instanceId, HazardDefinitionSO definition, Guid ownerGuid, int stunTurns)
        {
            if (instanceId == Guid.Empty) return;

            int turns = stunTurns < 1 ? 1 : stunTurns;
            var info = new TrailInfo(ownerGuid, turns);
            _trails[instanceId] = info;
            if (definition != null) _byDefinition[definition] = info;
        }

        /// <summary>Olvida una estela (la reemplazó una nueva, o expiró).</summary>
        public void ForgetTrail(Guid instanceId)
        {
            if (instanceId == Guid.Empty) return;
            _trails.Remove(instanceId);
        }

        /// <summary><c>true</c> si <paramref name="instanceId"/> es una estela trackeada.</summary>
        public bool IsTrackedTrail(Guid instanceId) => _trails.ContainsKey(instanceId);

        /// <summary>
        /// Entrega y descarta las casillas que <paramref name="entity"/> pisó en su último
        /// movimiento (sin el origen, en orden de recorrido). Se descartan al leerlas para que un
        /// turno sin repliegue no reutilice el camino del turno anterior.
        /// </summary>
        public bool TryConsumeWalkedTiles(Guid entity, out List<GridCoord> tiles)
        {
            if (entity != Guid.Empty && _lastWalkedTiles.TryGetValue(entity, out tiles))
            {
                _lastWalkedTiles.Remove(entity);
                return tiles != null && tiles.Count > 0;
            }

            tiles = null;
            return false;
        }

        // ======================================================================
        // Internals
        // ======================================================================

        private void ResetAll()
        {
            UnsubscribeMovement();
            _trails.Clear();
            _byDefinition.Clear();
            _lastWalkedTiles.Clear();
        }

        private void EnsureMovementSubscription()
        {
            if (_movementSubscribedTo != null) return;
            if (!ServiceLocator.TryGetService<IMovementService>(out var movement) || movement == null) return;

            movement.OnEntityMoved += OnEntityMovedExternal;
            _movementSubscribedTo = movement;
        }

        private void UnsubscribeMovement()
        {
            if (_movementSubscribedTo == null) return;

            _movementSubscribedTo.OnEntityMoved -= OnEntityMovedExternal;
            _movementSubscribedTo = null;
        }

        /// <summary>
        /// Resuelve el trail de un instanceId: primero por id trackeado y, si no está, por su
        /// definición (la instancia todavía vive cuando <c>OnHazardTriggered</c> se dispara —
        /// <see cref="HazardService"/> consume la casilla después del evento).
        /// </summary>
        private bool TryResolveTrail(Guid instanceId, out TrailInfo info)
        {
            if (_trails.TryGetValue(instanceId, out info)) return true;
            if (_byDefinition.Count == 0) return false;
            if (!ServiceLocator.TryGetService<IHazardService>(out var hazards) || hazards == null) return false;

            foreach (var instance in hazards.ActiveInstances())
            {
                if (instance.InstanceId != instanceId) continue;
                if (instance.Definition == null) return false;
                return _byDefinition.TryGetValue(instance.Definition, out info);
            }

            return false;
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void OnHazardTriggeredExternal(params object[] args)
        {
            if (_trails.Count == 0 && _byDefinition.Count == 0) return;
            if (args == null || args.Length < 2) return;
            if (!(args[0] is Guid instanceId) || instanceId == Guid.Empty) return;
            if (!(args[1] is Guid entityGuid) || entityGuid == Guid.Empty) return;

            if (!TryResolveTrail(instanceId, out var trail)) return;
            if (entityGuid == trail.OwnerGuid) return;

            if (!ServiceLocator.TryGetService<IStunService>(out var stun) || stun == null)
            {
                Debug.LogWarning("[AnotadorIceStunBinder] IStunService no registrado — la estela " +
                                 "helada no stunea. Agregá StunServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return;
            }

            stun.ApplyStun(entityGuid, trail.StunTurns);
        }

        private void OnHazardExpiredExternal(params object[] args)
        {
            if (args == null || args.Length == 0) return;
            if (!(args[0] is Guid instanceId)) return;
            _trails.Remove(instanceId);
        }

        private void OnTurnQueueBuiltExternal(params object[] args)
        {
            // Hook recurrente más barato para reintentar la suscripción: IMovementService es
            // run-scoped y puede registrarse después que este binder (mismo reintento que
            // HazardService hace acá).
            EnsureMovementSubscription();
        }

        private void OnScopeEndedExternal(params object[] args) => ResetAll();

        private void OnEntityMovedExternal(Guid entity, GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
        {
            if (entity == Guid.Empty) return;
            _lastWalkedTiles[entity] = EnteredTiles(from, to, path);
        }

        /// <summary>
        /// Casillas en las que la entidad <i>entró</i>, en orden de recorrido — el path sin el
        /// origen. Calcado de <c>HazardService.EnteredTiles</c> a propósito: la estela tiene que
        /// congelar exactamente el mismo conjunto que después el trigger <c>OnEnter</c> escanea.
        /// </summary>
        private static List<GridCoord> EnteredTiles(GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
        {
            var tiles = new List<GridCoord>();

            if (path == null || path.Count == 0)
            {
                // Un reposicionamiento instantáneo no reporta path, pero el destino sí se pisó.
                if (!to.Equals(from)) tiles.Add(to);
                return tiles;
            }

            for (int i = 0; i < path.Count; i++)
            {
                if (!path[i].Equals(from)) tiles.Add(path[i]);
            }
            return tiles;
        }
    }
}
