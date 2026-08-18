using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Movement;
using UnityEngine;

namespace Rollgeon.Combat.Status
{
    /// <summary>
    /// Glue de <b>cualquier</b> hielo del juego. Dos trabajos: traducir <c>OnHazardTriggered</c> en
    /// <see cref="IStunService.ApplyStun"/> para quien pisó un parche registrado acá (el hazard no
    /// sabe nada de stun: su <c>Damage</c> es 0), y grabar por entidad las casillas del último
    /// <see cref="IMovementService.OnEntityMoved"/>, que consume
    /// <see cref="Rollgeon.Combat.AI.Decisions.AINode_IceTrail"/>.
    /// </summary>
    /// <remarks>
    /// <b>El path hay que grabarlo en el momento.</b> El origen no sobrevive al movimiento, y un
    /// <c>FindPath</c> a posteriori corre sobre otra ocupancia: la estela congelaría casillas por
    /// las que el boss nunca pasó. De ahí el auto-install
    /// (<see cref="RuntimeInitializeOnLoadMethod"/>) en vez de una entry en
    /// <c>ServiceBootstrap.ExtraServices</c>: un binder creado en el primer tick del nodo llegaría
    /// tarde al primer repliegue.
    /// </remarks>
    public sealed class IceStunBinder : IDisposable
    {
        /// <summary>A quién NO stunear, y cuántos turnos cuesta pisar el parche.</summary>
        private readonly struct IceInfo
        {
            public readonly Guid OwnerGuid;
            public readonly int StunTurns;

            public IceInfo(Guid ownerGuid, int stunTurns)
            {
                OwnerGuid = ownerGuid;
                StunTurns = stunTurns;
            }
        }

        private readonly Dictionary<Guid, IceInfo> _patches = new Dictionary<Guid, IceInfo>();

        // Red de seguridad: si el binder se recreó entre runs y perdió los instanceId, una instancia
        // cuya Definition es la de un hielo conocido sigue siendo suya.
        private readonly Dictionary<HazardDefinitionSO, IceInfo> _byDefinition =
            new Dictionary<HazardDefinitionSO, IceInfo>();

        private readonly Dictionary<Guid, List<GridCoord>> _lastWalkedTiles =
            new Dictionary<Guid, List<GridCoord>>();

        // La instancia EXACTA a la que nos suscribimos: IMovementService es run-scoped y este binder
        // no, así que un `-=` resuelto del locator podría dejar viva la suscripción vieja.
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
        /// movimiento: ese servicio es run-scoped y puede no existir todavía.
        /// </summary>
        public static IceStunBinder ResolveOrCreate()
        {
            if (ServiceLocator.TryGetService<IceStunBinder>(out var existing) && existing != null)
            {
                existing.EnsureMovementSubscription();
                return existing;
            }

            var binder = new IceStunBinder();
            binder.Register();
            return binder;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall() => ResolveOrCreate();

        /// <summary>Suscribe handlers y se registra en el locator.</summary>
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

            ServiceLocator.AddService<IceStunBinder>(this, ServiceScope.Global);
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
        // API para los nodos
        // ======================================================================

        /// <summary><paramref name="ownerGuid"/> no se stunea; pisarlo cuesta
        /// <paramref name="stunTurns"/> turnos.</summary>
        public void TrackIce(Guid instanceId, HazardDefinitionSO definition, Guid ownerGuid, int stunTurns)
        {
            if (instanceId == Guid.Empty) return;

            int turns = stunTurns < 1 ? 1 : stunTurns;
            var info = new IceInfo(ownerGuid, turns);
            _patches[instanceId] = info;
            if (definition != null) _byDefinition[definition] = info;
        }

        /// <summary>Olvida un parche (lo reemplazó uno nuevo, o expiró).</summary>
        public void ForgetIce(Guid instanceId)
        {
            if (instanceId == Guid.Empty) return;
            _patches.Remove(instanceId);
        }

        /// <summary><c>true</c> si <paramref name="instanceId"/> es un parche trackeado.</summary>
        public bool IsTrackedIce(Guid instanceId) => _patches.ContainsKey(instanceId);

        /// <summary>
        /// Entrega y <b>descarta</b> las casillas del último movimiento de
        /// <paramref name="entity"/>, para que un turno sin movimiento no reutilice el camino previo.
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
            _patches.Clear();
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
        /// Por id trackeado y, si no está, por definición: la instancia todavía vive cuando
        /// <c>OnHazardTriggered</c> se dispara (la casilla se consume después del evento).
        /// </summary>
        private bool TryResolveIce(Guid instanceId, out IceInfo info)
        {
            if (_patches.TryGetValue(instanceId, out info)) return true;
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
            if (_patches.Count == 0 && _byDefinition.Count == 0) return;
            if (args == null || args.Length < 2) return;
            if (!(args[0] is Guid instanceId) || instanceId == Guid.Empty) return;
            if (!(args[1] is Guid entityGuid) || entityGuid == Guid.Empty) return;

            if (!TryResolveIce(instanceId, out var ice)) return;

            // Hoy no se ejecuta (todos los hielos son PlayerOnly y HazardService ya filtró al
            // dueño), pero es el único freno si alguien autora uno con HazardAffects.Everyone: ahí
            // el jefe vuelve a ser cobrable y se congelaría con su propio anillo.
            if (entityGuid == ice.OwnerGuid) return;

            if (!ServiceLocator.TryGetService<IStunService>(out var stun) || stun == null)
            {
                Debug.LogWarning("[IceStunBinder] IStunService no registrado — el hielo no stunea. " +
                                 "Agregá StunServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return;
            }

            stun.ApplyStun(entityGuid, ice.StunTurns);
        }

        private void OnHazardExpiredExternal(params object[] args)
        {
            if (args == null || args.Length == 0) return;
            if (!(args[0] is Guid instanceId)) return;
            _patches.Remove(instanceId);
        }

        private void OnTurnQueueBuiltExternal(params object[] args)
        {
            // Hook recurrente más barato para reintentar: IMovementService es run-scoped y puede
            // registrarse después que este binder.
            EnsureMovementSubscription();
        }

        private void OnScopeEndedExternal(params object[] args) => ResetAll();

        private void OnEntityMovedExternal(Guid entity, GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
        {
            if (entity == Guid.Empty) return;
            _lastWalkedTiles[entity] = EnteredTiles(from, to, path);
        }

        /// <summary>
        /// El path sin el origen. Tiene que dar el mismo conjunto que
        /// <c>HazardService.EnteredTiles</c>: la estela congela justo lo que escanea <c>OnEnter</c>.
        /// </summary>
        private static List<GridCoord> EnteredTiles(GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
        {
            var tiles = new List<GridCoord>();

            if (path == null || path.Count == 0)
            {
                // Un teleport no reporta path, pero el destino sí se pisó.
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
