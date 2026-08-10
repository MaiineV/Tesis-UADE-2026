using System;
using UnityEngine;

namespace Rollgeon.Entities.Portraits
{
    /// <summary>
    /// Lookup runtime guid → portrait <see cref="Sprite"/>. Fuente única para toda UI
    /// que necesite identificar visualmente a una entidad (turn order HUD hoy;
    /// bestiario / codex mañana).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Población.</b> Los enemigos y jefes se registran en el spawn pipeline
    /// (<c>DefaultEnemySpawnResolver.RegisterEnemyAtCoord</c>) desde
    /// <see cref="BaseEntitySO.Portrait"/> — un solo sprite les alcanza. El player NO
    /// se registra: la impl lo resuelve lazy vía
    /// <c>IPlayerService.CurrentHero.ResolveTurnOrderIcon()</c>, que prefiere el icono
    /// dedicado de la clase sobre su retrato de selección.
    /// </para>
    /// <para>
    /// <b>Lifecycle.</b> Run-scoped — <c>ClearScope(Run)</c> libera el registro al
    /// terminar la run; no hace falta desregistrar por <c>OnEntityDestroyed</c>
    /// (las entradas stale son refs a assets, sin leak).
    /// </para>
    /// </remarks>
    public interface IEntityPortraitResolver
    {
        /// <summary>Asocia un portrait al guid. <see cref="Guid.Empty"/> o sprite null = no-op.</summary>
        void Register(Guid entityId, Sprite portrait);

        /// <summary>Remueve la entrada del guid. Idempotente.</summary>
        void Unregister(Guid entityId);

        /// <summary>
        /// Resuelve el portrait del guid. <c>false</c> si no hay sprite usable —
        /// el caller conserva su visual default (fallback del prefab).
        /// </summary>
        bool TryGetPortrait(Guid entityId, out Sprite portrait);

        /// <summary>Vacía el registro explícito (el lazy player no se ve afectado).</summary>
        void Clear();
    }
}
