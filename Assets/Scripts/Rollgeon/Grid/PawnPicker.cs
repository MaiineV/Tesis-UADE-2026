using Rollgeon.Entities.Visuals;
using UnityEngine;

namespace Rollgeon.Grid
{
    /// <summary>
    /// Resolución compartida de "qué hay bajo el cursor" para todo el targeting del
    /// mundo: click de tiles, drag&amp;drop de acciones y feedback del cursor.
    /// </summary>
    /// <remarks>
    /// <b>Por qué recorre TODOS los hits y no el primero</b>: los room prefabs traen
    /// ~325 colliders en layer Default (paredes, cubos, props decorativos). Con un
    /// <see cref="Physics.Raycast"/> de un solo hit, cualquiera de ellos delante del
    /// enemigo ganaba el raycast, el chequeo de <see cref="EntityPawn"/> daba null y
    /// el pick caía al plano del piso — que con la cámara isométrica devuelve la celda
    /// de ATRÁS del enemigo. De ahí el targeting errático reportado por QA: el mouse
    /// estaba encima de la unidad y seleccionaba otra celda.
    /// <para>
    /// Los colliders que no son pawn nunca bloquean el pick: sólo cuentan para elegir
    /// entre varios pawns (gana el más cercano a cámara = el que se ve adelante).
    /// </para>
    /// <para>
    /// <b>Máscara de layers</b>: los pawns viven en <see cref="PawnLayers"/> (Player /
    /// Entity) y cada overload con <c>layerMask</c> decide cuáles pueden capturar el rayo.
    /// Así el pick es contextual a la acción: un movimiento pasa máscara vacía y el click
    /// sobre el modelo de un enemigo cae al piso que hay debajo; un ataque pasa Entity y el
    /// cuerpo del héroe deja de tapar a los enemigos de atrás. Las máscaras son <c>int</c>
    /// y no <see cref="LayerMask"/> a propósito: con un <c>int</c> literal el compilador
    /// elegiría el overload de <c>float maxDistance</c> si el parámetro fuera el struct.
    /// </para>
    /// </remarks>
    public static class PawnPicker
    {
        public const float DefaultMaxDistance = 100f;

        // El hover pollea por frame mientras hay una selección activa: RaycastAll
        // allocaría un array nuevo cada uno. 32 alcanza de sobra para la profundidad
        // de una sala (pared + piso + props + pawns en línea).
        private static readonly RaycastHit[] Buffer = new RaycastHit[32];

        /// <summary>
        /// Pawn más cercano a cámara sobre el rayo, ignorando cualquier collider que
        /// no pertenezca a un pawn. False si no hay ninguno.
        /// </summary>
        public static bool TryPickPawn(Ray ray, out EntityPawn pawn, float maxDistance = DefaultMaxDistance)
            => TryPick(ray, out pawn, Physics.DefaultRaycastLayers, maxDistance);

        /// <summary>
        /// Igual que <see cref="TryPickPawn(Ray, out EntityPawn, float)"/> pero solo los
        /// colliders dentro de <paramref name="layerMask"/> pueden capturar el rayo.
        /// </summary>
        public static bool TryPickPawn(Ray ray, out EntityPawn pawn, int layerMask, float maxDistance = DefaultMaxDistance)
            => TryPick(ray, out pawn, layerMask, maxDistance);

        /// <summary>
        /// Instancia de <typeparamref name="T"/> más cercana a cámara sobre el rayo,
        /// buscada hacia arriba desde cada collider atravesado. Sirve también para
        /// interfaces (ej. <c>ICursorHoverable</c>).
        /// </summary>
        public static bool TryPick<T>(Ray ray, out T component, float maxDistance = DefaultMaxDistance)
            where T : class
            => TryPick(ray, out component, Physics.DefaultRaycastLayers, maxDistance);

        /// <summary>
        /// Igual que <see cref="TryPick{T}(Ray, out T, float)"/> restringido a los colliders
        /// de <paramref name="layerMask"/>. Con máscara vacía no hay query: nada puede ganar.
        /// </summary>
        public static bool TryPick<T>(Ray ray, out T component, int layerMask, float maxDistance = DefaultMaxDistance)
            where T : class
        {
            component = null;
            if (layerMask == 0) return false;

            int count = Physics.RaycastNonAlloc(ray, Buffer, maxDistance, layerMask);

            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = Buffer[i];
                if (hit.distance >= best) continue;

                var collider = hit.collider;
                if (collider == null) continue;

                var candidate = collider.GetComponentInParent<T>();
                if (candidate == null) continue;

                component = candidate;
                best = hit.distance;
            }

            return component != null;
        }

        /// <summary>
        /// Celda bajo el cursor: la del pawn apuntado si el rayo cruza su cuerpo,
        /// si no la del piso. Null si el rayo no cae en la grilla.
        /// </summary>
        public static GridCoord? ResolveCoord(Ray ray, IGridManager grid, float maxDistance = DefaultMaxDistance)
            => ResolveCoord(ray, grid, Physics.DefaultRaycastLayers, maxDistance);

        /// <summary>
        /// Celda bajo el cursor considerando solo los pawns cuyo collider está en
        /// <paramref name="pawnLayerMask"/>; el resto es transparente y el pick cae al
        /// piso. Null si el rayo no cae en la grilla.
        /// </summary>
        /// <remarks>
        /// El fallback intersecta el PLANO del piso, no los colliders de los tiles: el
        /// collider de cada tile es una caja de 1u de alto que sobresale 0.5 sobre el
        /// piso visible, así que desde la cámara en ángulo el tile de adelante tapaba
        /// al de atrás y esa celda quedaba imposible de seleccionar. La grilla es un
        /// único plano (<see cref="GridCoord"/> es 2D), así que intersectar contra el
        /// plano a la altura de <see cref="IGridManager.GridOrigin"/> da la celda
        /// correcta ignorando lo que haya en el medio.
        /// </remarks>
        public static GridCoord? ResolveCoord(Ray ray, IGridManager grid, int pawnLayerMask, float maxDistance = DefaultMaxDistance)
        {
            if (grid == null) return null;

            if (TryPickPawn(ray, out var pawn, pawnLayerMask, maxDistance)
                && grid.TryGetPosition(pawn.EntityGuid, out var pawnCoord))
            {
                // Footprint multi-celda (Fase C): devolver la celda cubierta más cercana al
                // punto bajo el cursor, no el ancla fija — si el ancla quedó fuera del rango
                // del ataque pero otra celda del rect no, el click tiene que valer. Para un
                // 1×1 la única celda cubierta ES el ancla (comportamiento de siempre).
                var footprint = grid.GetFootprint(pawn.EntityGuid);
                if (!GridFootprint.IsUnit(footprint))
                {
                    var floorPlane = new Plane(Vector3.up, grid.GridOrigin);
                    if (floorPlane.Raycast(ray, out float toFloor))
                    {
                        var cursorCell = grid.WorldToGrid(ray.GetPoint(toFloor));
                        var best = pawnCoord;
                        int bestDist = int.MaxValue;
                        foreach (var c in GridFootprint.Cells(pawnCoord, footprint))
                        {
                            int d = c.Manhattan(cursorCell);
                            if (d < bestDist) { bestDist = d; best = c; }
                        }
                        return best;
                    }
                }
                return pawnCoord;
            }

            var floor = new Plane(Vector3.up, grid.GridOrigin);
            if (!floor.Raycast(ray, out float enter)) return null;

            var coord = grid.WorldToGrid(ray.GetPoint(enter));
            return grid.InBounds(coord) ? coord : (GridCoord?)null;
        }
    }
}
