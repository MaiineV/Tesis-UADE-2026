using System;
using System.Collections;
using Rollgeon.Grid;
using Rollgeon.Heroes;

namespace Rollgeon.Entities.Visuals
{
    /// <summary>
    /// Capa visual: spawnea / despawnea GameObjects de entidades y los mantiene
    /// sincronizados con la grilla lógica. Run-scope. TECHNICAL.md §17.§I + §13.3.
    /// </summary>
    /// <remarks>
    /// También implementa <see cref="IEntityPositionResolver"/> para que el
    /// <c>FloatingDamageSpawner</c> pueda anclar números al pawn de cada entidad.
    /// </remarks>
    public interface IEntityVisualService : IEntityPositionResolver
    {
        EntityPawn SpawnHero(Guid guid, ClassHeroSO hero, GridCoord coord);
        EntityPawn SpawnEnemy(Guid guid, EnemyDataSO data, GridCoord coord);

        /// <summary>
        /// Spawnea un prop de sala con entidad (cofre): mismo pipeline que un enemigo
        /// (pawn + PawnRegistryBinding + posición) pero desde un prefab directo, sin
        /// <see cref="EnemyDataSO"/>.
        /// </summary>
        EntityPawn SpawnProp(Guid guid, UnityEngine.GameObject prefab, GridCoord coord);
        void Despawn(Guid guid);
        void DespawnAll();
        bool TryGetPawn(Guid guid, out EntityPawn pawn);
        IEnumerator WaitForMoveComplete(Guid entityId);
    }
}
