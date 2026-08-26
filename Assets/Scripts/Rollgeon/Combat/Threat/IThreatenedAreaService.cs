using System;
using System.Collections.Generic;
using System.Linq;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Estado persistente de "ataque telegráfico" entre turnos: guarda, por fuente, el conjunto de
    /// casillas marcadas que van a recibir daño cuando esa fuente lo ejecute. Solo retiene el
    /// <b>estado lógico</b> (qué casillas, cuánto daño, qué tipo); el highlight visual lo hace
    /// <see cref="ITileHighlightService"/>.
    /// </summary>
    /// <remarks>
    /// Una marca por fuente, y no se arregla mergeando: quien detona consume por fuente y cobra el
    /// <c>Damage</c> de lo que consumió, así que dos áreas fundidas en una entrada se resuelven como
    /// un solo golpe con un solo número. Nada limpia el estado por turno ni por ronda — sólo el que
    /// lo consume o el fin de combate/run —, y de eso depende un aviso que se sostiene más de un
    /// turno.
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

        /// <summary>
        /// Lee el área pendiente de <paramref name="sourceGuid"/> <b>sin</b> consumirla: lo mismo
        /// que devuelve <see cref="TryConsume"/>, pero dejándola puesta.
        /// </summary>
        /// <remarks>
        /// <see cref="GetPendingTiles"/> ya lee sin consumir pero no trae el número, y el número
        /// es justamente lo que el aviso promete. Quien lo leyera con <see cref="TryConsume"/> se
        /// lo cobraría a sí mismo y el jefe se quedaría sin su ataque.
        /// </remarks>
        bool TryPeek(Guid sourceGuid, out ThreatenedArea pending);

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
