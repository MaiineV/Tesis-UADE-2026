using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Threat
{
    /// <summary>Forma del área telegráfica.</summary>
    /// <remarks>
    /// Los índices viajan serializados en los <c>.asset</c> de los jefes: las formas nuevas se
    /// appendean al final. Reordenar o insertar en el medio le cambia la forma a jefes ya autorados.
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
        /// parámetro <c>size</c> (1..6). Los seis bloques <b>cubren la sala entera</b>: ninguna
        /// casilla caminable queda fuera de la numeración. Ver
        /// <see cref="ThreatAreaShape.ComputeRoomSector"/>.
        /// </summary>
        RoomSector,

        /// <summary>
        /// Toda la sala caminable MENOS el cuadrado (2·radio+1) centrado en el propio boss — La
        /// Banca del Tahúr con el pozo en 5: cobra en todos lados salvo La Mesa, su 3×3. Es el
        /// complemento de <see cref="SquareAroundSelf"/>, así que el centro también es la
        /// coordenada del boss. Ver <see cref="ThreatAreaShape.ComputeAllExceptSquareAroundSelf"/>.
        /// </summary>
        AllExceptSquareAroundSelf,

        /// <summary>
        /// Franja vertical centrada en el propio boss — la columna del Cajero. Misma matemática que
        /// <see cref="Column"/>, pero el centro es la coordenada del boss.
        /// </summary>
        ColumnAroundSelf,

        /// <summary>
        /// Partición genérica de la sala en una grilla de columnas × filas configurable. A
        /// diferencia de <see cref="RoomSector"/> (fijo en 3×2, con costura deliberada), ninguna
        /// casilla puede caer en dos celdas a la vez. Ni el jugador ni el boss son el centro.
        /// </summary>
        /// <remarks>
        /// Columnas, filas y el índice de celda no entran en el parámetro único <c>size</c> de
        /// <see cref="ThreatAreaShape.Compute"/>, así que esta shape no pasa por ahí: el caller
        /// llama directo a <see cref="ThreatAreaShape.ComputeGridPartition"/>.
        /// </remarks>
        GridPartition,

        /// <summary>
        /// Cono que sale del propio boss hacia el jugador y se abre una casilla por lado en cada
        /// paso de fondo — el fuego del Croupier. Comparte origen y cardinales con
        /// <see cref="DirectionalBand"/>, pero el ancho crece con la distancia: pegado al boss es
        /// una sola casilla. Ver <see cref="ThreatAreaShape.ComputeDirectionalCone"/>.
        /// </summary>
        DirectionalCone,
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
    /// <c>IsWalkable</c>). Código puro, sin estado.
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

                // Misma cuenta para las dos: lo único que cambia es quién es `center`, y eso lo
                // resolvió el caller vía AnchorsOnSelf antes de llegar acá.
                case ThreatShape.Column:
                case ThreatShape.ColumnAroundSelf:
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
                    // El índice del sector viaja en `size`; `center` no se usa: el sector no está
                    // centrado en nadie.
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
        public static bool AnchorsOnSelf(ThreatShape shape) =>
            shape == ThreatShape.SquareAroundSelf ||
            shape == ThreatShape.AllExceptSquareAroundSelf ||
            shape == ThreatShape.ColumnAroundSelf;

        /// <summary>
        /// Formas que salen del boss <b>hacia</b> el jugador. El caller tiene que resolver las dos
        /// posiciones y llamar al Compute propio de cada una: no pasan por <see cref="Compute"/>,
        /// que recibe un solo centro y las devolveria vacias sin avisar.
        /// </summary>
        public static bool NeedsSelfAndPlayer(ThreatShape shape) =>
            shape == ThreatShape.DirectionalBand ||
            shape == ThreatShape.DirectionalCone;

        /// <summary>
        /// Toda la sala caminable menos el cuadrado de radio <paramref name="radius"/> (1 ⇒ 3×3)
        /// centrado en <paramref name="self"/> — La Banca del Tahúr: cobra en todos lados salvo
        /// La Mesa, el 3×3 que el jefe arrastra consigo.
        /// </summary>
        /// <remarks>Sala sin bounds ⇒ vacío, igual que el resto de las shapes que enumeran la
        /// sala.</remarks>
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
        /// fila de bloques de arriba (izquierda → derecha), 4-5-6 la de abajo. Cada sector es el
        /// cruce de una banda de columnas (1 de 3) con una banda de filas (1 de 2), y las bandas
        /// de los dos ejes salen de la misma regla (<see cref="Band"/>).
        /// </summary>
        /// <remarks>
        /// Los seis bloques cubren la sala entera: una casilla que no pertenece a ningún sector no
        /// se prende fuego nunca. Costuras, no huecos: cada banda mide <c>ceil(extensión/bandas)</c>
        /// con la última anclada al borde lejano, así que con extensión no múltiplo las bandas se
        /// <i>solapan</i> en vez de dejar hueco. Sala sin bounds o índice fuera de 1..6 ⇒ vacío.
        /// </remarks>
        public static HashSet<GridCoord> ComputeRoomSector(IGridManager grid, int sector)
        {
            var result = new HashSet<GridCoord>();
            if (grid == null) return result;
            if (sector < 1 || sector > RoomSectorCount) return result;

            var tiles = new List<GridCoord>(RoomTiles(grid));
            if (tiles.Count == 0) return result;

            RoomBounds(tiles, out int minX, out int maxX, out int minY, out int maxY);

            int column = (sector - 1) % RoomSectorColumns;                 // 0 izq, 1 medio, 2 der
            int row = sector <= RoomSectorColumns ? 1 : 0;                 // 1 arriba, 0 abajo

            Band(minX, maxX, column, RoomSectorColumns, out int loX, out int hiX);
            Band(minY, maxY, row, RoomSectorRows, out int loY, out int hiY);

            foreach (var c in tiles)
            {
                if (c.X < loX || c.X > hiX) continue;
                if (c.Y < loY || c.Y > hiY) continue;
                result.Add(c);
            }

            return result;
        }

        /// <summary>
        /// Fila (o filas) de costura: las casillas que pertenecen a la vez a un bloque de arriba y
        /// a uno de abajo. Vacío si la altura de la sala es par —ahí las dos bandas encajan justas—
        /// o si la sala no tiene bounds reales.
        /// </summary>
        /// <remarks>
        /// Sale de las mismas <see cref="Band"/> que <see cref="ComputeRoomSector"/> para que las
        /// dos definiciones no puedan divergir.
        /// </remarks>
        public static HashSet<GridCoord> ComputeSeamRow(IGridManager grid)
        {
            var result = new HashSet<GridCoord>();
            if (grid == null) return result;

            var tiles = new List<GridCoord>(RoomTiles(grid));
            if (tiles.Count == 0) return result;

            RoomBounds(tiles, out _, out _, out int minY, out int maxY);

            Band(minY, maxY, 0, RoomSectorRows, out int lowerLo, out int lowerHi);
            Band(minY, maxY, 1, RoomSectorRows, out int upperLo, out int upperHi);

            int lo = System.Math.Max(lowerLo, upperLo);
            int hi = System.Math.Min(lowerHi, upperHi);

            foreach (var c in tiles)
                if (c.Y >= lo && c.Y <= hi) result.Add(c);

            return result;
        }

        /// <summary>Bandas de columnas del paño: 1-2-3 / 4-5-6 son tres columnas de bloques.</summary>
        public const int RoomSectorColumns = 3;

        /// <summary>Bandas de filas del paño: los de arriba y los de abajo.</summary>
        public const int RoomSectorRows = 2;

        /// <summary>
        /// Banda <paramref name="index"/> de <paramref name="count"/> sobre <c>[min,max]</c>.
        /// </summary>
        /// <remarks>
        /// Tamaño <c>ceil(extensión/count)</c> y la última banda anclada al borde lejano. Con una
        /// extensión que no divide justo, eso hace que las bandas se solapen en una costura en vez
        /// de dejar un hueco: la unión de las <paramref name="count"/> bandas es siempre
        /// <c>[min,max]</c> completo, que es la invariante de la que cuelga el jefe.
        /// </remarks>
        private static void Band(int min, int max, int index, int count, out int lo, out int hi)
        {
            int extent = max - min + 1;
            int size = (extent + count - 1) / count; // ceil(extent/count)

            bool last = index >= count - 1;
            lo = last ? max - size + 1 : min + index * size;
            hi = last ? max : lo + size - 1;

            // Clamp de las dos puntas: en salas más chicas que la cantidad de bandas el ancla del
            // borde lejano puede caerse afuera, y una banda vacía sería otra vez un hueco.
            if (lo < min) lo = min;
            if (lo > max) lo = max;
            if (hi > max) hi = max;
            if (hi < min) hi = min;
        }

        private static void RoomBounds(
            List<GridCoord> tiles, out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = int.MaxValue; maxX = int.MinValue;
            minY = int.MaxValue; maxY = int.MinValue;

            foreach (var c in tiles)
            {
                if (c.X < minX) minX = c.X;
                if (c.X > maxX) maxX = c.X;
                if (c.Y < minY) minY = c.Y;
                if (c.Y > maxY) maxY = c.Y;
            }
        }

        /// <summary>
        /// Partición genérica de la sala en <paramref name="columns"/> × <paramref name="rows"/>
        /// celdas: devuelve la celda <paramref name="cellIndex"/> (1-based, columna =
        /// <c>(cellIndex-1) % columns</c>, fila = <c>(cellIndex-1) / columns</c>, fila 0 la
        /// más cercana al borde de Y mínimo). Primitiva reusable para cualquier jefe que
        /// necesite "la sala partida en N×M sin doble-cobro" — ver <see cref="PartitionBand"/>
        /// para por qué no comparte la matemática de <see cref="ComputeRoomSector"/>.
        /// </summary>
        /// <remarks>
        /// Columnas/filas ≤ 0, índice fuera de <c>1..columns*rows</c>, o sala sin bounds
        /// reales ⇒ vacío, igual que el resto de las shapes que enumeran la sala.
        /// </remarks>
        public static HashSet<GridCoord> ComputeGridPartition(IGridManager grid, int columns, int rows, int cellIndex)
        {
            var result = new HashSet<GridCoord>();
            if (grid == null || columns <= 0 || rows <= 0) return result;

            int total = columns * rows;
            if (cellIndex < 1 || cellIndex > total) return result;

            var tiles = new List<GridCoord>(RoomTiles(grid));
            if (tiles.Count == 0) return result;

            RoomBounds(tiles, out int minX, out int maxX, out int minY, out int maxY);

            int column = (cellIndex - 1) % columns;
            int row = (cellIndex - 1) / columns;

            PartitionBand(minX, maxX, column, columns, out int loX, out int hiX);
            PartitionBand(minY, maxY, row, rows, out int loY, out int hiY);

            foreach (var c in tiles)
            {
                if (c.X < loX || c.X > hiX) continue;
                if (c.Y < loY || c.Y > hiY) continue;
                result.Add(c);
            }

            return result;
        }

        /// <summary>
        /// Banda <paramref name="index"/> de <paramref name="count"/> sobre <c>[min,max]</c>,
        /// sin la costura de <see cref="Band"/>.
        /// </summary>
        /// <remarks>
        /// El resto de la división se reparte entre las primeras <c>extent % count</c> bandas (una
        /// casilla extra cada una) en vez de concentrarlo en la última, así la unión de las
        /// <paramref name="count"/> bandas es <c>[min,max]</c> exacto — sin huecos ni costuras, a
        /// diferencia de <see cref="Band"/>.
        /// </remarks>
        private static void PartitionBand(int min, int max, int index, int count, out int lo, out int hi)
        {
            int extent = max - min + 1;
            int baseSize = extent / count;
            int remainder = extent % count;

            // Las primeras `remainder` bandas cargan la casilla extra; el resto mide `baseSize`.
            int size = index < remainder ? baseSize + 1 : baseSize;

            // Offset de las bandas anteriores: cada una de las que ya cargó el extra empuja el
            // arranque de esta un paso más que `baseSize`.
            int extraBefore = index < remainder ? index : remainder;
            lo = min + index * baseSize + extraBefore;
            hi = lo + size - 1; // Con size 0 (más bandas que extensión) queda hi < lo: banda vacía, no crashea.
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
        /// Cono anclado en <paramref name="self"/> y apuntado a <paramref name="player"/>: en el
        /// paso <c>n</c> el semi-ancho es <paramref name="apexHalfWidth"/> + n - 1, asi que con
        /// apex 0 sale 1-3-5-7 casillas. Igual que <see cref="ComputeDirectionalBand"/> arranca en
        /// el paso 1 —la casilla del boss no entra— y sólo mira los cuatro cardinales.
        /// </summary>
        public static HashSet<GridCoord> ComputeDirectionalCone(
            IGridManager grid, GridCoord self, GridCoord player, int apexHalfWidth, int depth)
        {
            var result = new HashSet<GridCoord>();
            if (grid == null) return result;

            int apex = apexHalfWidth < 0 ? 0 : apexHalfWidth;
            int d = depth < 1 ? 1 : depth;

            var dir = CardinalExtensions.FromDelta(self, player);
            var (stepX, stepY) = DirectionStep(dir);
            bool advancesOnX = stepX != 0;

            for (int step = 1; step <= d; step++)
            {
                int originX = self.X + stepX * step;
                int originY = self.Y + stepY * step;
                int hw = apex + step - 1;

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

        // Elige hasta count anclas del pool probando niveles de separación decrecientes (gap de 3
        // casillas → 2 → 1 → apenas sin solapar → libre) y se queda con el primero que las junte.
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

        // Greedy: baraja el pool y acepta anclas cuya distancia Chebyshev a TODAS las ya elegidas
        // sea >= minDist. Puede devolver menos de count si el pool no alcanza a esa separación.
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

        /// <summary>
        /// Casillas caminables reales de la sala. Si el grafo está poblado, usa sus nodos (maneja
        /// salas no rectangulares y orígenes arbitrarios). Si está vacío (stub "infinito"), no hay
        /// extensión que enumerar → vacío; las formas Row/Column/HalfRoom requieren una sala con
        /// bounds reales (siempre el caso en combate).
        /// </summary>
        public static IEnumerable<GridCoord> RoomTiles(IGridManager grid)
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
