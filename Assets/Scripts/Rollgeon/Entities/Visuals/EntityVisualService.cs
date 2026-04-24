using System;
using System.Collections.Generic;
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
        private readonly GameObject _heroPrefab;
        private readonly GameObject _enemyPrefab;
        private readonly GameObject _bossPrefab;
        private readonly Transform _parent;
        private bool _subscribed;

        public EntityVisualService(
            IGridManager grid,
            IMovementService movement,
            GameObject heroPrefab,
            GameObject enemyPrefab,
            GameObject bossPrefab,
            Transform parent = null)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _movement = movement;
            _heroPrefab = heroPrefab;
            _enemyPrefab = enemyPrefab;
            _bossPrefab = bossPrefab;
            _parent = parent;

            if (_movement != null)
            {
                _movement.OnEntityMoved += OnEntityMoved;
                _subscribed = true;
            }
        }

        public void Dispose()
        {
            if (_subscribed && _movement != null)
            {
                _movement.OnEntityMoved -= OnEntityMoved;
                _subscribed = false;
            }
            DespawnAll();
        }

        public EntityPawn SpawnHero(Guid guid, ClassHeroSO hero, GridCoord coord)
        {
            var prefab = _heroPrefab ?? CreatePrimitiveFallback(PrimitiveType.Capsule, Color.cyan);
            return SpawnInternal(guid, prefab, coord, EntityPawn.PawnKind.Hero);
        }

        public EntityPawn SpawnEnemy(Guid guid, EnemyDataSO data, GridCoord coord)
        {
            var isBoss = data != null && data.BaseHP >= 80; // heurística simple para FP
            var prefab = isBoss
                ? (_bossPrefab ?? _enemyPrefab ?? CreatePrimitiveFallback(PrimitiveType.Cube, Color.magenta))
                : (_enemyPrefab ?? CreatePrimitiveFallback(PrimitiveType.Capsule, Color.red));
            var kind = isBoss ? EntityPawn.PawnKind.Boss : EntityPawn.PawnKind.Enemy;
            return SpawnInternal(guid, prefab, coord, kind);
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

        private GameObject CreatePrimitiveFallback(PrimitiveType type, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = $"{type}_Placeholder";
            go.hideFlags = HideFlags.DontSave;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial.color = color;
            go.SetActive(false); // used only as a prefab source — instantiate activates
            return go;
        }

        private void OnEntityMoved(Guid guid, GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
        {
            if (_byGuid.TryGetValue(guid, out var pawn) && pawn != null)
            {
                pawn.SnapToGrid(_grid, to);
            }
        }
    }
}
