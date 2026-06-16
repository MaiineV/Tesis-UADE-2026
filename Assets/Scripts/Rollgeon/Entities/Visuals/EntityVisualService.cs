using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Movement;
using UnityEngine;

namespace Rollgeon.Entities.Visuals
{
    /// <summary>
    /// Implementación default de <see cref="IEntityVisualService"/>.
    /// </summary>
    /// <remarks>
    /// Se suscribe a <see cref="IMovementService.OnEntityMoved"/> y posiciona el
    /// pawn en <c>GridToWorld(to)</c>. El FP usa teleport — tweens se pueden
    /// layerear después reemplazando <see cref="EntityPawn.SetWorldPosition"/>.
    /// </remarks>
    public sealed class EntityVisualService : IEntityVisualService, IDisposable
    {
        private readonly Dictionary<Guid, EntityPawn> _byGuid = new Dictionary<Guid, EntityPawn>();
        private readonly IGridManager _grid;
        private readonly IMovementService _movement;
        private readonly Transform _parent;
        private bool _subscribed;
        private Action<DamageResolvedPayload> _onDamageResolved;

        public EntityVisualService(
            IGridManager grid,
            IMovementService movement,
            Transform parent = null)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _movement = movement;
            _parent = parent;

            if (_movement != null)
            {
                _movement.OnEntityMoved += OnEntityMoved;
                _subscribed = true;
            }

            // Facing al atacar: el source rota hacia el target en el frame en que el daño
            // se resuelve. Suscribimos al TypedEvent porque DamagePipeline lo dispara
            // siempre, sin importar quién originó el ataque.
            _onDamageResolved = OnDamageResolved;
            TypedEvent<DamageResolvedPayload>.Subscribe(_onDamageResolved);
        }

        public void Dispose()
        {
            if (_subscribed && _movement != null)
            {
                _movement.OnEntityMoved -= OnEntityMoved;
                _subscribed = false;
            }
            if (_onDamageResolved != null)
            {
                TypedEvent<DamageResolvedPayload>.Unsubscribe(_onDamageResolved);
                _onDamageResolved = null;
            }
            DespawnAll();
        }

        public EntityPawn SpawnHero(Guid guid, ClassHeroSO hero, GridCoord coord)
        {
            if (hero == null) throw new ArgumentNullException(nameof(hero));
            if (hero.VisualPrefab == null)
            {
                Debug.LogError($"[EntityVisualService] ClassHeroSO '{hero.name}' no tiene VisualPrefab asignado.");
                return null;
            }
            return SpawnInternal(guid, hero.VisualPrefab, coord, EntityPawn.PawnKind.Hero);
        }

        public EntityPawn SpawnEnemy(Guid guid, EnemyDataSO data, GridCoord coord)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.VisualPrefab == null)
            {
                Debug.LogError($"[EntityVisualService] EnemyDataSO '{data.name}' no tiene VisualPrefab asignado.");
                return null;
            }
            return SpawnInternal(guid, data.VisualPrefab, coord, EntityPawn.PawnKind.Enemy);
        }

        public void Despawn(Guid guid)
        {
            if (!_byGuid.TryGetValue(guid, out var pawn)) return;
            _byGuid.Remove(guid);
            if (pawn != null) DestroyGO(pawn.gameObject);
        }

        public void DespawnAll()
        {
            foreach (var pawn in _byGuid.Values)
            {
                if (pawn != null) DestroyGO(pawn.gameObject);
            }
            _byGuid.Clear();
        }

        private static void DestroyGO(GameObject go)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(go);
            else
                UnityEngine.Object.DestroyImmediate(go);
        }

        public bool TryGetPawn(Guid guid, out EntityPawn pawn) =>
            _byGuid.TryGetValue(guid, out pawn);

        public IEnumerator WaitForMoveComplete(Guid entityId)
        {
            if (!_byGuid.TryGetValue(entityId, out var pawn) || pawn == null) return null;
            if (!pawn.IsMoving) return null;
            return pawn.WaitUntilMoveComplete();
        }

        public Vector3? TryGetWorldPosition(Guid entityId)
        {
            if (_byGuid.TryGetValue(entityId, out var pawn) && pawn != null)
                return pawn.transform.position;
            return null;
        }

        // ---- Internals ---------------------------------------------------

        private EntityPawn SpawnInternal(Guid guid, GameObject prefab, GridCoord coord, EntityPawn.PawnKind kind)
        {
            if (guid == Guid.Empty) throw new ArgumentException("guid cannot be Guid.Empty", nameof(guid));
            if (_byGuid.ContainsKey(guid)) Despawn(guid);

            var go = UnityEngine.Object.Instantiate(prefab, _parent);
            var pawn = go.GetComponent<EntityPawn>();
            if (pawn == null) pawn = go.AddComponent<EntityPawn>();
            pawn.Bind(guid, kind);
            pawn.SnapToGrid(_grid, coord);

            // §10 feedback pipeline: el pawn queda descubierto por IPawnRegistry via este binding.
            var binding = go.GetComponent<Rollgeon.Feedback.PawnRegistryBinding>();
            if (binding == null) binding = go.GetComponentInChildren<Rollgeon.Feedback.PawnRegistryBinding>(true);
            if (binding != null) binding.SetGuid(guid);

            _byGuid[guid] = pawn;
            return pawn;
        }

        private void OnEntityMoved(Guid guid, GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
        {
            if (!_byGuid.TryGetValue(guid, out var pawn) || pawn == null) return;

            // Si tenemos path detallado (≥2 nodos) animamos casilla-a-casilla — el FaceCoord
            // por step lo hace AnimatePath internamente. Pasamos también el movement service
            // para que la corutina pueda recalcular el path si otra entidad bloquea un tile
            // mientras animamos. Si no hay path, fallback al snap+facing directo.
            if (path != null && path.Count >= 2)
            {
                pawn.AnimatePath(_grid, path, movement: _movement);
            }
            else
            {
                pawn.FaceCoord(from, to);
                pawn.SnapToGrid(_grid, to);
            }
        }

        private void OnDamageResolved(DamageResolvedPayload payload)
        {
            // El atacante rota hacia el target. Si alguno no tiene pawn registrado o no
            // tienen posición en grid, no hacemos nada — el facing por movimiento ya
            // suele dejar al atacante mirando bien si vino caminando hacia el target.
            // Wrappeamos en try/catch para que una excepción del facing no rompa la
            // cadena de TypedEvent (otros subscribers como FloatingDamageSpawner deben
            // seguir recibiendo el payload aunque acá fallemos).
            try
            {
                if (payload.SourceGuid == Guid.Empty || payload.TargetGuid == Guid.Empty) return;
                if (!_byGuid.TryGetValue(payload.SourceGuid, out var sourcePawn) || sourcePawn == null) return;
                if (_grid == null) return;
                if (!_grid.TryGetPosition(payload.SourceGuid, out var sourceCoord)) return;
                if (!_grid.TryGetPosition(payload.TargetGuid, out var targetCoord)) return;
                sourcePawn.FaceCoord(sourceCoord, targetCoord);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EntityVisualService] OnDamageResolved facing failed: {ex}");
            }
        }
    }
}
