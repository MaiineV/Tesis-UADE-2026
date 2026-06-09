using System;
using System.Collections.Generic;
using System.Linq;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Estado persistente de "ataque telegráfico" entre turnos (Sistemas prerequisito Bosses §1).
    /// Guarda, por fuente (el Boss), el conjunto de casillas marcadas en el turno N que van a
    /// recibir daño al inicio del turno N+1 del Boss. El highlight visual lo hace
    /// <see cref="ITileHighlightService"/>; este servicio solo retiene el <b>estado lógico</b>
    /// (qué casillas, cuánto daño, qué tipo) para poder ejecutarlo el turno siguiente.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por fuente.</b> La key es el <c>Guid</c> de la entidad que marcó el área — así dos
    /// bosses (o un boss + adds) no se pisan. En la práctica del FP hay un solo boss por combate.
    /// </para>
    /// <para>
    /// <b>Lifecycle.</b> Run-scoped vía limpieza en <c>OnCombatEnd</c> / <c>OnRunEnd</c>
    /// (igual que <c>ComboBlockService</c>). No persiste a save.
    /// </para>
    /// </remarks>
    public interface IThreatenedAreaService
    {
        /// <summary>
        /// Marca un área amenazada para <paramref name="sourceGuid"/>. Sobrescribe cualquier
        /// marca previa de esa misma fuente. No-op si <paramref name="tiles"/> es null/vacío
        /// o <paramref name="sourceGuid"/> es <see cref="Guid.Empty"/>.
        /// </summary>
        void Mark(Guid sourceGuid, IEnumerable<GridCoord> tiles, int damage, AttackKind kind);

        /// <summary><c>true</c> si <paramref name="sourceGuid"/> tiene un área marcada pendiente.</summary>
        bool HasPending(Guid sourceGuid);

        /// <summary>
        /// Lee (sin consumir) las casillas marcadas por <paramref name="sourceGuid"/>.
        /// Devuelve un set vacío si no hay nada pendiente. Usado por la UI / VFX / tests.
        /// </summary>
        IReadOnlyCollection<GridCoord> GetPendingTiles(Guid sourceGuid);

        /// <summary>
        /// Consume el área pendiente de <paramref name="sourceGuid"/>: la saca del estado y
        /// devuelve <c>true</c> + sus datos vía <paramref name="pending"/>. Devuelve <c>false</c>
        /// si no había nada pendiente. El caller (nodo de ejecución) decide a quién golpea.
        /// </summary>
        bool TryConsume(Guid sourceGuid, out ThreatenedArea pending);

        /// <summary>Descarta el área pendiente de <paramref name="sourceGuid"/> sin ejecutarla.</summary>
        void Clear(Guid sourceGuid);

        /// <summary>Descarta todas las áreas pendientes. Usado en <c>OnCombatEnd</c> / <c>OnRunEnd</c>.</summary>
        void ClearAll();
    }

    /// <summary>Snapshot inmutable de un área amenazada pendiente.</summary>
    public readonly struct ThreatenedArea
    {
        public readonly Guid SourceGuid;
        public readonly IReadOnlyCollection<GridCoord> Tiles;
        public readonly int Damage;
        public readonly AttackKind Kind;

        public ThreatenedArea(Guid sourceGuid, IReadOnlyCollection<GridCoord> tiles, int damage, AttackKind kind)
        {
            SourceGuid = sourceGuid;
            Tiles = tiles;
            Damage = damage;
            Kind = kind;
        }

        /// <summary><c>true</c> si <paramref name="coord"/> está dentro del área marcada.</summary>
        public bool Contains(GridCoord coord) => Tiles != null && Tiles.Contains(coord);
    }
}
