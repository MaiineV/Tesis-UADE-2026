using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Threat
{
    /// <summary>Forma del área telegráfica. Cada Boss usa una distinta (Sistemas prerequisito Bosses §1).</summary>
    /// <remarks>
    /// Los índices viajan serializados en los <c>.asset</c> de los jefes: las formas nuevas se
    /// appendean al final. Reordenar o insertar en el medio le cambia la forma a jefes ya
    /// autorados sin tocar ningún asset.
    /// </remarks>
    public enum ThreatShape
    {
        /// <summary>Cuadrado (2·radio+1) centrado en el jugador. Boss 1 — cruz/área 3×3 (radio 1).</summary>
        SquareAroundPlayer,

        /// <summary>Franja horizontal: la(s) fila(s) del jugador. Boss 2 — franja.</summary>
        Row,

        /// <summary>Franja vertical: la(s) columna(s) del jugador. Boss 2 — franja.</summary>
        Column,

        /// <summary>Mitad de la sala donde está el jugador. Boss 3 — media sala.</summary>
        HalfRoom,

        /// <summary>
        /// Banda direccional que sale del propio boss hacia el jugador — Boss 1 (slash).
        /// A diferencia de las shapes de arriba, el origen es la coordenada del boss, no la
        /// del jugador. Ver <see cref="ThreatAreaShape.ComputeDirectionalBand"/>.
        /// </summary>
        DirectionalBand,

        /// <summary>
        /// Varios cuadrados independientes en posiciones erráticas de la sala — Boss 1
        /// (lluvia de zonas). Ni el jugador ni el boss son el centro; ver
        /// <see cref="ThreatAreaShape.ComputeScatteredSquares"/>.
        /// </summary>
        ScatteredSquares,

        /// <summary>
        /// Cuadrado (2·radio+1) centrado en el propio boss — Boss 1 (área alrededor). Misma
        /// matemática que <see cref="SquareAroundPlayer"/>, pero el centro es la coordenada
        /// del boss, no la del jugador.
        /// </summary>
        SquareAroundSelf,

        /// <summary>
        /// Uno de los seis sectores del paño del Croupier (3 columnas × 2 filas de bloques),
        /// numerados 1-2-3 arriba y 4-5-6 abajo. Ni el jugador ni el boss son el centro: el
        /// sector se elige por índice, que en <see cref="ThreatAreaShape.Compute"/> viaja en el
        /// parámetro <c>size</c> (1..6). La fila central de la sala nunca pertenece a ningún
        /// sector — es el pasillo. Ver <see cref="ThreatAreaShape.ComputeRoomSector"/>.
        /// </summary>
        RoomSector,

        /// <summary>
        /// Toda la sala caminable MENOS el cuadrado (2·radio+1) centrado en el propio boss — La
        /// Banca del Tahúr con el pozo en 5: cobra en todos lados salvo La Mesa, su 3×3. Es el
        /// complemento de <see cref="SquareAroundSelf"/>, así que el centro también es la
        /// coordenada del boss. Ver <see cref="ThreatAreaShape.ComputeAllExceptSquareAroundSelf"/>.
        /// </summary>
        AllExceptSquareAroundSelf,
    }

    /// <summary>Eje de corte para <see cref="ThreatShape.HalfRoom"/>.</summary>
    public enum HalfRoomAxis
    {
        /// <summary>Corte vertical → mitades izquierda/derecha (por X).</summary>
        Vertical,

        /// <summary>Corte horizontal → mitades inferior/superior (por Y).</summary>
        Horizontal,
    }

    /// <summary>
    /// Calcula el conjunto de casillas de un área telegráfica a partir de la posición del jugador
    /// y la forma elegida. Solo devuelve casillas que existen en la grilla (<c>InBounds</c> +
    /// <c>IsWalkable</c>). Es código puro — sin estado — para que los nodos de AI y los tests lo reusen.
    /// </summary>
    public static class ThreatAreaShape
    {
        /// <summary>
        /// Devuelve las casillas amenazadas. <paramref name="size"/> es el radio para
        /// <see cref="ThreatShape.SquareAroundPlayer"/>/<see cref="ThreatShape.SquareAroundSelf"/>
        /// (1 ⇒ 3×3) y el ancho (en casillas) de la franja para <see cref="ThreatShape.Row"/> /
        /// <see cref="ThreatShape.Column"/> (1 ⇒ la línea del jugador; 3 ⇒ ±1), y el radio del
        /// hueco para <see cref="ThreatShape.AllExceptSquareAroundSelf"/> (1 ⇒ hueco 3×3).
        /// Ignorado para <see cref="ThreatShape.HalfRoom"/>. <paramref name="center"/> es la
        /// coordenada del jugador salvo en las shapes que devuelven <c>true</c> en
        /// <see cref="AnchorsOnSelf"/>, donde es la del boss — la resuelve el caller (ver
        /// <see cref="Rollgeon.Combat.AI.Decisions.AINode_TelegraphMark"/>).
        /// </summary>
        public static HashSet<GridCoord> Compute(
            IGridManager grid, GridCoord center, ThreatShape shape, int size, HalfRoomAxis axis)
        {
            var result = new HashSet<GridCoord>();
            if (grid == null) return result;

            switch (shape)
            {
                case ThreatShape.SquareAroundPlayer:
                case ThreatShape.SquareAroundSelf:
                {
                    int r = size < 0 ? 0 : size;
                    for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        var c = new GridCoord(center.X + dx, center.Y + dy);
                        if (IsValidTile(grid, c)) result.Add(c);
                    }
                    break;
                }

                case ThreatShape.Row:
                {
                    int half = HalfBand(size);
                    foreach (var c in RoomTiles(grid))
                        if (System.Math.Abs(c.Y - center.Y) <= half) result.Add(c);
                    break;
                }

                case ThreatShape.Column:
                {
                    int half = HalfBand(size);
                    foreach (var c in RoomTiles(grid))
                        if (System.Math.Abs(c.X - center.X) <= half) result.Add(c);
                    break;
                }

                case ThreatShape.HalfRoom:
                {
                    AddHalfRoom(grid, center, axis, result);
                    break;
                }

                case ThreatShape.RoomSector:
                {
                    // El índice del sector viaja en `size` — mismo criterio que el resto de las
                    // shapes, donde ese parámetro ya significa cosas distintas según la forma
                    // (radio / ancho de franja / lado del cuadrado). `center` no se usa: el
                    // sector no está centrado en nadie.
                    result.UnionWith(ComputeRoomSector(grid, size));
                    break;
                }

                case ThreatShape.AllExceptSquareAroundSelf:
                {
                    result.UnionWith(ComputeAllExceptSquareAroundSelf(grid, center, size));
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// <c>true</c> si la shape se ancla en la coordenada del propio boss en vez de la del
        /// jugador.
        /// </summary>
        /// <remarks>
        /// El caller resuelve el <c>center</c> antes de llamar a <see cref="Compute"/>, pero el
        /// criterio es de la forma, no del nodo: vive acá para que agregar una shape anclada en el
        /// boss no dependa de acordarse de tocar cada call site.
        /// </remarks>
        public static bool AnchorsOnSelf(ThreatShape shape) =>
            shape == ThreatShape.SquareAroundSelf ||
            shape == ThreatShape.AllExceptSquareAroundSelf;

        /// <summary>
        /// Toda la sala caminable menos el cuadrado de radio <paramref name="radius"/> (1 ⇒ 3×3)
        /// centrado en <paramref name="self"/> — La Banca del Tahúr: cobra en todos lados salvo
        /// La Mesa, el 3×3 que el jefe arrastra consigo.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>El hueco se recorta solo.</b> No se proyecta un cuadrado para restarlo: se filtran
        /// las casillas de la sala por distancia Chebyshev al jefe. Contra una pared el hueco
        /// queda del tamaño que entre —en una esquina son 4 casillas en vez de 9— sin ningún caso
        /// especial, y la casilla del propio jefe nunca queda amenazada ni con radio 0.
        /// </para>
        /// <para>
        /// Los obstáculos no se pintan: <see cref="RoomTiles"/> ya devuelve solo casillas
        /// caminables. Sala sin bounds reales (grafo vacío) ⇒ vacío, igual que el resto de las
        /// shapes que necesitan enumerar la sala.
        /// </para>
        /// </remarks>
        public static HashSet<GridCoord> ComputeAllExceptSquareAroundSelf(
            IGridManager grid, GridCoord self, int radius)
        {
            var result = new HashSet<GridCoord>();
            if (grid == null) return result;

            int r = radius < 0 ? 0 : radius;
            foreach (var c in RoomTiles(grid))
                if (c.Chebyshev(self) > r) result.Add(c);

            return result;
        }

        /// <summary>Cantidad de sectores del paño (3 columnas × 2 filas de bloques).</summary>
        public const int RoomSectorCount = 6;

        /// <summary>
        /// Casillas del sector <paramref name="sector"/> (1..6) del paño del Croupier: 1-2-3 la
        /// fila de bloques de arriba (izquierda → derecha), 4-5-6 la de abajo. En la sala canónica
        /// de 11×7 cada sector mide 4×3.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>El pasillo no cae nunca.</b> La fila central de la sala (<c>(minY+maxY)/2</c>) queda
        /// fuera de los seis sectores por construcción: los bloques de arriba arrancan en
        /// <c>mid+1</c> y los de abajo terminan en <c>mid-1</c>. Es la invariante que sostiene el
        /// diseño del jefe — se queda parado ahí y llegar a pegarle nunca cuesta posición.
        /// </para>
        /// <para>
        /// <b>Columna de costura.</b> El ancho de banda es <c>ceil(ancho/3)</c> y la banda derecha
        /// se ancla al borde derecho (<c>maxX-ancho+1 .. maxX</c>) en vez de continuar a la banda
        /// del medio. Con un ancho que no es múltiplo de 3 —11, el caso real— eso deja las bandas
        /// en 0-3 / 4-7 / 7-10: la columna 7 pertenece a la vez al bloque del medio y al de la
        /// derecha. Es la única franja donde dos números cantados pegan los dos (24 en fase 2), y
        /// es determinístico, no un artefacto del redondeo: el solapamiento siempre cae en la
        /// costura derecha.
        /// </para>
        /// <para>
        /// Salas sin bounds reales (grafo vacío) o índices fuera de 1..6 devuelven vacío, igual que
        /// el resto de las shapes que necesitan extensión de sala.
        /// </para>
        /// </remarks>
        public static HashSet<GridCoord> ComputeRoomSector(IGridManager grid, int sector)
        {
            var result = new HashSet<GridCoord>();
            if (grid == null) return result;
            if (sector < 1 || sector > RoomSectorCount) return result;

            var tiles = new List<GridCoord>(RoomTiles(grid));
            if (tiles.Count == 0) return result;

            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var c in tiles)
            {
                if (c.X < minX) minX = c.X;
                if (c.X > maxX) maxX = c.X;
                if (c.Y < minY) minY = c.Y;
                if (c.Y > maxY) maxY = c.Y;
            }

            int width = maxX - minX + 1;
            int bandWidth = (width + 2) / 3; // ceil(width/3)
            int column = (sector - 1) % 3;   // 0 = izquierda, 1 = medio, 2 = derecha
            bool upperRow = sector <= 3;

            int loX = column < 2 ? minX + column * bandWidth : maxX - bandWidth + 1;
            int hiX = column < 2 ? loX + bandWidth - 1 : maxX;
            if (loX < minX) loX = minX;
            if (hiX > maxX) hiX = maxX;

            int midY = (minY + maxY) / 2;
            int loY = upperRow ? midY + 1 : minY;
            int hiY = upperRow ? maxY : midY - 1;

            foreach (var c in tiles)
            {
                if (c.X < loX || c.X > hiX) continue;
                if (c.Y < loY || c.Y > hiY) continue;
                result.Add(c);
            }

            return result;
        }

        /// <summary>
        /// Fila central de la sala — el pasillo que ningún sector cubre. Devuelve vacío si la sala
        /// no tiene bounds reales.
        /// </summary>
        /// <remarks>
        /// Expuesto porque el pasillo es una lectura de diseño en sí misma (once casillas seguras
        /// por construcción): lo consumen los tests de invariante y cualquier feedback que quiera
        /// pintarlo. Se calcula del mismo <c>midY</c> que <see cref="ComputeRoomSector"/> para que
        /// las dos definiciones no puedan divergir.
        /// </remarks>
        public static HashSet<GridCoord> ComputeCorridorRow(IGridManager grid)
        {
            var result = new HashSet<GridCoord>();
            if (grid == null) return result;

            var tiles = new List<GridCoord>(RoomTiles(grid));
            if (tiles.Count == 0) return result;

            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var c in tiles)
            {
                if (c.Y < minY) minY = c.Y;
                if (c.Y > maxY) maxY = c.Y;
            }

            int midY = (minY + maxY) / 2;
            foreach (var c in tiles)
                if (c.Y == midY) result.Add(c);

            return result;
        }

        /// <summary>
        /// Banda direccional que sale de <paramref name="self"/> (el boss) hacia
        /// <paramref name="player"/>: <paramref name="depth"/> pasos de profundidad en la
        /// dirección cardinal dominante (<see cref="Cardinal.FromDelta"/>), cada uno una
        /// banda perpendicular de <c>2·halfWidth+1</c> casillas centrada en el eje
        /// perpendicular del boss. Arranca pegada al boss (paso 1), nunca incluye su propia
        /// fila/columna en el eje de avance.
        /// </summary>
        public static HashSet<GridCoord> ComputeDirectionalBand(
            IGridManager grid, GridCoord self, GridCoord player, int halfWidth, int depth)
        {
            var result = new HashSet<GridCoord>();
            if (grid == null) return result;

            int hw = halfWidth < 0 ? 0 : halfWidth;
            int d = depth < 1 ? 1 : depth;

            var dir = CardinalExtensions.FromDelta(self, player);
            var (stepX, stepY) = DirectionStep(dir);
            bool advancesOnX = stepX != 0;

            for (int step = 1; step <= d; step++)
            {
                int originX = self.X + stepX * step;
                int originY = self.Y + stepY * step;

                for (int off = -hw; off <= hw; off++)
                {
                    var c = advancesOnX
                        ? new GridCoord(originX, originY + off)
                        : new GridCoord(originX + off, originY);
                    if (IsValidTile(grid, c)) result.Add(c);
                }
            }

            return result;
        }

        /// <summary>
        /// <paramref name="count"/> cuadrados de <paramref name="squareWidth"/>·<paramref name="squareWidth"/>
        /// casillas, anclados al azar (vía <paramref name="rng"/>) en el 50% central de la sala
        /// — ni el jugador ni el boss son el centro, y las zonas no aparecen pegadas a las
        /// paredes. Las anclas priorizan quedar separadas entre sí (nunca se tocan ni
        /// solapan) siempre que la sala tenga lugar; si no alcanza, degrada de forma
        /// escalonada (gap más chico → sin gap pero sin solapar → libre, puede solapar) antes
        /// de resignarse. Requiere una sala con bounds reales (como <see cref="ThreatShape.Row"/>/
        /// <see cref="ThreatShape.Column"/>/<see cref="ThreatShape.HalfRoom"/>); grafo vacío ⇒ vacío.
        /// </summary>
        public static HashSet<GridCoord> ComputeScatteredSquares(
            IGridManager grid, System.Random rng, int count, int squareWidth)
        {
            var result = new HashSet<GridCoord>();
            if (grid == null || rng == null || count <= 0) return result;

            int w = squareWidth < 1 ? 1 : squareWidth;
            var room = new List<GridCoord>(RoomTiles(grid));
            if (room.Count == 0) return result;

            var anchorPool = CenterAnchorPool(room, w);
            var anchors = PickSeparatedAnchors(anchorPool, rng, count, w);

            foreach (var anchor in anchors)
            {
                for (int dx = 0; dx < w; dx++)
                for (int dy = 0; dy < w; dy++)
                {
                    var c = new GridCoord(anchor.X + dx, anchor.Y + dy);
                    if (IsValidTile(grid, c)) result.Add(c);
                }
            }

            return result;
        }

        // Elige hasta count anclas del pool. Prueba niveles de separación decrecientes
        // (gap visible de 3 casillas → 2 → 1 → apenas sin solapar → libre) y se queda con el
        // primero que logre juntar count anclas — así el resultado se ve "prolijo" cuando
        // la sala da lugar, y solo se degrada a solapar si de verdad no entra.
        private static List<GridCoord> PickSeparatedAnchors(List<GridCoord> pool, System.Random rng, int count, int squareWidth)
        {
            foreach (var gap in new[] { 3, 2, 1, 0 })
            {
                var picked = TryPickWithMinDistance(pool, rng, count, squareWidth + gap);
                if (picked.Count == count) return picked;
            }

            // Último recurso: independientes, pueden solaparse.
            var free = new List<GridCoord>(count);
            for (int i = 0; i < count; i++) free.Add(pool[rng.Next(pool.Count)]);
            return free;
        }

        // Greedy: baraja el pool y va aceptando anclas cuya distancia Chebyshev a TODAS
        // las ya elegidas sea >= minDist (garantiza el gap en cualquier dirección, ya que
        // los cuadrados son... cuadrados). Puede devolver menos de count si el pool no
        // alcanza a ese nivel de separación — el caller decide si degradar el gap.
        private static List<GridCoord> TryPickWithMinDistance(List<GridCoord> pool, System.Random rng, int count, int minDist)
        {
            var shuffled = new List<GridCoord>(pool);
            Shuffle(shuffled, rng);

            var chosen = new List<GridCoord>(count);
            foreach (var candidate in shuffled)
            {
                bool farEnough = true;
                foreach (var picked in chosen)
                {
                    if (candidate.Chebyshev(picked) < minDist) { farEnough = false; break; }
                }
                if (!farEnough) continue;

                chosen.Add(candidate);
                if (chosen.Count == count) break;
            }
            return chosen;
        }

        private static void Shuffle(List<GridCoord> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // Recorta un margen del 25% por lado, dejando el 50% central de la sala como pool
        // de anclaje — así las zonas erráticas caen "en el medio del mapa", nunca pegadas
        // al borde. El ancla es la esquina inferior-izquierda del cuadrado (crece hacia
        // +X/+Y), así que además recortamos (squareWidth-1) del límite superior para que el
        // cuadrado entero quede adentro del pool central, sin sobresalir hacia el borde.
        // Sala/pool minúsculos donde el recorte vacía el pool ⇒ fallback en cascada.
        private static List<GridCoord> CenterAnchorPool(List<GridCoord> room, int squareWidth)
        {
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var c in room)
            {
                if (c.X < minX) minX = c.X;
                if (c.X > maxX) maxX = c.X;
                if (c.Y < minY) minY = c.Y;
                if (c.Y > maxY) maxY = c.Y;
            }

            int marginX = (maxX - minX + 1) / 4;
            int marginY = (maxY - minY + 1) / 4;
            int loX = minX + marginX, hiX = maxX - marginX;
            int loY = minY + marginY, hiY = maxY - marginY;

            int fit = squareWidth - 1;
            var fitted = new List<GridCoord>();
            foreach (var c in room)
                if (c.X >= loX && c.X <= hiX - fit && c.Y >= loY && c.Y <= hiY - fit) fitted.Add(c);
            if (fitted.Count > 0) return fitted;

            var unfitted = new List<GridCoord>();
            foreach (var c in room)
                if (c.X >= loX && c.X <= hiX && c.Y >= loY && c.Y <= hiY) unfitted.Add(c);

            return unfitted.Count > 0 ? unfitted : room;
        }

        private static (int dx, int dy) DirectionStep(Cardinal dir) => dir switch
        {
            Cardinal.North => (0, 1),
            Cardinal.South => (0, -1),
            Cardinal.East => (1, 0),
            Cardinal.West => (-1, 0),
            _ => (0, 1),
        };

        // El "ancho" de la franja es impar-céntrico: width 1 ⇒ banda 0 (solo la línea),
        // width 2/3 ⇒ banda 1 (±1), etc. half = (width-1)/2.
        private static int HalfBand(int width)
        {
            if (width <= 1) return 0;
            return (width - 1) / 2;
        }

        private static void AddHalfRoom(IGridManager grid, GridCoord center, HalfRoomAxis axis, HashSet<GridCoord> result)
        {
            var tiles = new List<GridCoord>(RoomTiles(grid));
            if (tiles.Count == 0) return;

            int min = int.MaxValue, max = int.MinValue;
            foreach (var c in tiles)
            {
                int v = axis == HalfRoomAxis.Vertical ? c.X : c.Y;
                if (v < min) min = v;
                if (v > max) max = v;
            }

            // Punto medio entero. El jugador cae en la mitad baja (<= mid) o alta (> mid).
            int mid = (min + max) / 2;
            int playerV = axis == HalfRoomAxis.Vertical ? center.X : center.Y;
            bool playerInLowHalf = playerV <= mid;

            foreach (var c in tiles)
            {
                int v = axis == HalfRoomAxis.Vertical ? c.X : c.Y;
                bool inLow = v <= mid;
                if (inLow == playerInLowHalf) result.Add(c);
            }
        }

        // Casillas reales de la sala. Si el grafo está poblado, usamos sus nodos (maneja
        // salas no rectangulares y orígenes arbitrarios). Si está vacío (stub "infinito"),
        // no hay extensión que enumerar → vacío; las formas Row/Column/HalfRoom requieren
        // una sala con bounds reales (siempre el caso en combate).
        private static IEnumerable<GridCoord> RoomTiles(IGridManager grid)
        {
            var graph = grid.Graph;
            if (graph == null || graph.IsEmpty) yield break;
            foreach (var c in graph.AllCoords())
                if (grid.IsWalkable(c)) yield return c;
        }

        private static bool IsValidTile(IGridManager grid, GridCoord c)
            => grid.InBounds(c) && grid.IsWalkable(c);
    }
}
