using System.Collections.Generic;
using System.IO;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Construye una sala propia por jefe (<c>Rollgeon → Bosses → Build Boss Rooms</c>): clona la sala
    /// base del piso, le pone los blockers del plano de <c>docs/design/bosses-seis-refinados.html</c>,
    /// mueve el spawn del jefe a su casilla y hornea el <see cref="NavGraph"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Un prefab por jefe y no una variante: el <see cref="NavGraph"/> se hornea en el editor y queda
    /// serializado, y un blocker no decora — mata el nodo y corta sus aristas. Terreno distinto ⇒
    /// grafo distinto ⇒ prefab distinto.
    /// </para>
    /// <para>
    /// Idempotente por reconstrucción: cada corrida parte de la sala base y reescribe el output
    /// entero sobre el mismo path, que preserva el GUID. <b>Pero no byte a byte</b> —
    /// <c>SaveAsPrefabAsset</c> renumera los fileIDs internos en cada escritura, así que un rebuild
    /// sin cambios reales igual aparece como diff enorme en git. Para saber si la sala cambió hay
    /// que mirar el contenido, no el tamaño del diff. El precio es que lo editado a mano en una sala
    /// derivada se pierde: la decoración compartida va en la sala base.
    /// </para>
    /// <para>
    /// Las tres reglas de autoría —jefe alcanzable, sala conexa, spawn libre— se chequean contra el
    /// grafo horneado y no contra el plano: el plano es una grilla ideal de 11×7 y la sala real es
    /// 11×11 con muebles propios.
    /// </para>
    /// </remarks>
    public static class BossRoomBuilder
    {
        private const string LogPrefix = "[BossRoomBuilder] ";

        // ======================================================================
        // Grilla del plano
        // ======================================================================

        /// <summary>Ancho del plano del documento, en celdas.</summary>
        public const int PlanWidth = 11;

        /// <summary>
        /// Alto del plano del documento, en celdas. Es 11 y no 7: las tres salas base
        /// hornean x -5..5 e y -5..5 (101 nodos caminables en pisos 1-2, 95 en el 3).
        /// El documento declaraba 11x7 y sus cuentas de area quedaban optimistas.
        /// </summary>
        public const int PlanHeight = 11;

        /// <summary>
        /// Layer (altura en celdas) de los blockers. 0 = apoyados en el piso, igual que los barriles y
        /// la mesa de pool que ya trae la sala base.
        /// </summary>
        public const int BlockerLayer = 0;

        /// <summary>
        /// Mínimo de casillas caminables pegadas al jefe. El jugador pega a distancia 1: encerrarlo es
        /// prohibir la pelea, y dejarle una sola es convertir la sala en un cuello de botella de un
        /// tile que ninguna de las seis fichas pide.
        /// </summary>
        public const int MinBossAdjacency = 2;

        /// <summary>Nombre del grupo que agrupa los props del plano dentro de la sala derivada.</summary>
        public const string BlockerGroupName = "BossRoomBlockers";

        // ======================================================================
        // Salas base
        // ======================================================================

        /// <summary>
        /// <c>RoomSO</c> de boss compartido, del que los seis derivados copian <c>ShellIcon</c>,
        /// <c>GridSize</c> y <c>EnemyPool</c> para no divergir del resto del wiring.
        /// </summary>
        private const string SharedBossRoomSOPath = "Assets/Rollgeon/Rooms/Room_Boss01.asset";

        private const string FloorOneBaseRoom = "Assets/Prefabs/Rooms/FloorOne/Boss_Room01.prefab";
        private const string FloorTwoBaseRoom = "Assets/Prefabs/Rooms/FloorTwo/Boss_Room_FloorTwo01.prefab";
        private const string FloorThreeBaseRoom = "Assets/Prefabs/Rooms/FloorThree/Boss_Room_FloorThree.prefab";

        // ======================================================================
        // Props de obstáculo
        // ======================================================================

        // Placeholders elegidos entre lo que ya existe y ya se usa como blocker en estas mismas salas.
        // El arte que pide el documento (mármol bajo, mostrador con reja de latón, escritorios con
        // lámpara) no está modelado: cuando esté, se cambia el path acá y se re-corre el menú.

        /// <summary>Tragamonedas — el único prop que el documento pide y que ya existe tal cual.</summary>
        private const string SlotMachineProp = "Assets/Prefabs/Props/slotv02.prefab";

        /// <summary>
        /// Mesa: hace de escritorio (Anotador). <b>Mide 1.805 × 1.009 × 3.017</b> — tres casillas de
        /// profundidad, así que una hilera se ve como un muro macizo. El Cajero salió de acá por eso.
        /// </summary>
        private const string TableProp = "Assets/Prefabs/Props/Tablev02.prefab";

        /// <summary>
        /// Caja de fichas: el mostrador del Cajero. Mide 0.978 × 0.510 × 1.107, o sea una casilla de
        /// huella. Se le corrige sólo la altura (<c>PropScaleAxes</c> del plano): con 0.510 supera la
        /// banda de walk clearance del bake por un centímetro, y así deja de bloquear a la primera.
        /// </summary>
        private const string ChipCrateProp = "Assets/Prefabs/Props/CajaFichasv01.prefab";

        /// <summary>Barril: placeholder de las columnas hasta que haya una columna modelada.</summary>
        private const string BarrelProp = "Assets/Prefabs/Props/barrilv01.prefab";

        // ======================================================================
        // Los seis planos
        // ======================================================================

        /// <summary>
        /// Un plano por jefe, en coordenadas del documento: 11 × 7, origen arriba-izquierda, <c>y</c>
        /// creciendo hacia abajo. La traducción a la grilla de la sala la hace
        /// <see cref="PlanToRoom"/>.
        /// </summary>
        public static readonly BossRoomPlan[] Plans =
        {
            new BossRoomPlan
            {
                BossName = "Croupier",
                Floor = 1,
                BaseRoomPath = FloorOneBaseRoom,
                OutputRoomPath = "Assets/Prefabs/Rooms/FloorOne/Boss_Room_Croupier.prefab",
                OutputRoomSOPath = "Assets/Rollgeon/Rooms/Room_Boss_Croupier.asset",
                PropPrefabPath = BarrelProp,
                BossPlanCell = new Vector2Int(5, 5),
                // La sala estaba pelada, y contra un kiter eso la vuelve una pista de atletismo:
                // el jefe huye en línea recta y al jugador cuerpo a cuerpo no le queda nada que
                // usar para cortarle el paso. Doce barriles sueltos en látiz 3x3 —x/y en {2,5,8},
                // sin el centro (5,5), que es su spawn— encarecen el movimiento en toda la sala
                // sin abrir un solo rincón muerto: un obstáculo de una casilla se rodea por los
                // dos lados, cosa que un mueble grande no permite. Las cuatro esquinas llevan el
                // suyo porque si no son refugio gratis fuera del alcance del fuego.
                BlockerPlanCells = new[]
                {
                    new Vector2Int(2, 2), new Vector2Int(5, 2), new Vector2Int(8, 2),
                    new Vector2Int(2, 5), new Vector2Int(8, 5),
                    new Vector2Int(2, 8), new Vector2Int(5, 8), new Vector2Int(8, 8),
                    new Vector2Int(0, 0), new Vector2Int(10, 0),
                    new Vector2Int(0, 10), new Vector2Int(10, 10),
                },
                // La mesa de pool del noreste se va sólo de esta sala, igual que en el Cajero: vive
                // en las tres salas base y borrarla allá se la saca a todos los jefes del piso.
                // Libera además sus casillas, que la base tenía bloqueadas.
                RemoveBaseObjectNames = new[] { "Poolv04" },
            },
            new BossRoomPlan
            {
                // Piso 2 y no 1: con cuatro blancos, un turno y la jugada correcta siendo "no matar",
                // cruza dos palancas en vez de enseñar una — y su jackpot pega el 60% de la vida. El
                // piso 1 queda con el Croupier solo, que enseña una palanca por vez.
                BossName = "Bandida",
                Floor = 2,
                BaseRoomPath = FloorTwoBaseRoom,
                OutputRoomPath = "Assets/Prefabs/Rooms/FloorTwo/Boss_Room_Bandida.prefab",
                OutputRoomSOPath = "Assets/Rollgeon/Rooms/Room_Boss_Bandida.asset",
                PropPrefabPath = SlotMachineProp,
                // Contra la pared izquierda: es una máquina atornillada, no camina.
                BossPlanCell = new Vector2Int(0, 5),
                // Tres bancos de tragamonedas que abren las calles verticales. El cuarto —arriba a
                // la derecha— no se autora: ahí la sala base ya tiene su mueble de 2×3, que bloquea
                // igual. Autorar encima sería un prop duplicado sobre una celda que ya no es piso.
                // El banco de abajo va en x=3-4 y no en x=4-5 como el de arriba: (5,8) del plano cae
                // en la sala (0,-3), que es la casilla de spawn del jugador — el jugador aparecía
                // dentro de una tragamonedas. Corrido una casilla, la calle vertical sigue abierta y
                // el spawn queda libre.
                BlockerPlanCells = new[]
                {
                    new Vector2Int(4, 2), new Vector2Int(5, 2),
                    new Vector2Int(3, 8), new Vector2Int(4, 8),
                    new Vector2Int(8, 8), new Vector2Int(9, 8),
                },
            },
            new BossRoomPlan
            {
                BossName = "Cajero",
                Floor = 2,
                BaseRoomPath = FloorTwoBaseRoom,
                OutputRoomPath = "Assets/Prefabs/Rooms/FloorTwo/Boss_Room_Cajero.prefab",
                OutputRoomSOPath = "Assets/Rollgeon/Rooms/Room_Boss_Cajero.asset",
                PropPrefabPath = ChipCrateProp,
                // Del lado de arriba del mostrador: elegir puerta te compromete con un lado.
                BossPlanCell = new Vector2Int(5, 2),
                // El mostrador va en la fila 5, la única que cruza la sala sin tocar recorte ni
                // mueble, como dos cajas de dos casillas. Quedan tres pasos (x=0-1, x=4-6, x=9-10) y
                // el del medio es el que se lee como puerta. Las puntas van SIN prop: son los
                // tiles-frente de las puertas Oeste y Este y taparlas sella la sala.
                BlockerPlanCells = new[]
                {
                    new Vector2Int(2, 5),
                    new Vector2Int(3, 5),
                    new Vector2Int(7, 5),
                    new Vector2Int(8, 5),
                },
                // La mesa de pool del noreste se va sólo de esta sala. Vive en las tres salas base, así
                // que borrarla allá se la saca a todos los jefes del piso; acá es una decisión de la
                // sala del Cajero. Libera además sus casillas, que la base tenía bloqueadas.
                RemoveBaseObjectNames = new[] { "Poolv04" },
                // La caja de fichas ya mide una casilla en X y en Z (0.978 × 1.107): la huella no se
                // toca. Lo único que se corrige es la altura — 0.510 pasa la banda de walk clearance
                // del bake (NavGraphBaker.WalkClearance = 0.5) por un centímetro, y un prop que
                // bloquea por un centímetro deja de bloquear con cualquier cambio de piso. ×2 la deja
                // en ~1.02, con margen y sin deformarle la planta.
                PropScaleAxes = new Vector3(1f, 2f, 1f),
            },
            new BossRoomPlan
            {
                BossName = "Anotador",
                Floor = 2,
                BaseRoomPath = FloorTwoBaseRoom,
                OutputRoomPath = "Assets/Prefabs/Rooms/FloorTwo/Boss_Room_Anotador.prefab",
                OutputRoomSOPath = "Assets/Rollgeon/Rooms/Room_Boss_Anotador.asset",
                PropPrefabPath = TableProp,
                BossPlanCell = new Vector2Int(5, 5),
                // Cuatro escritorios de 2×1 que dejan libre el corredor central — el camino corto que
                // su estela de hielo va a tapar. Los de la derecha van en x6-7 y no x8-9: ahí está el
                // mueble de la sala base.
                BlockerPlanCells = new[]
                {
                    new Vector2Int(1, 2), new Vector2Int(2, 2),
                    new Vector2Int(6, 2), new Vector2Int(7, 2),
                    new Vector2Int(1, 8), new Vector2Int(2, 8),
                    new Vector2Int(6, 8), new Vector2Int(7, 8),
                },
            },
            new BossRoomPlan
            {
                BossName = "Generala",
                Floor = 3,
                BaseRoomPath = FloorThreeBaseRoom,
                OutputRoomPath = "Assets/Prefabs/Rooms/FloorThree/Boss_Room_Generala.prefab",
                OutputRoomSOPath = "Assets/Rollgeon/Rooms/Room_Boss_Generala.asset",
                PropPrefabPath = null,
                BossPlanCell = new Vector2Int(5, 5),
                // Vacío a propósito: sus cinco dados son el terreno, y son móviles. Un obstáculo fijo
                // competiría con ellos por la misma lectura. Igual necesita prefab propio: su sala es
                // la única del piso 3 sin blockers, y el grafo horneado tiene que decirlo.
                BlockerPlanCells = new Vector2Int[0],
            },
            new BossRoomPlan
            {
                BossName = "Tahur",
                Floor = 3,
                BaseRoomPath = FloorThreeBaseRoom,
                OutputRoomPath = "Assets/Prefabs/Rooms/FloorThree/Boss_Room_Tahur.prefab",
                OutputRoomSOPath = "Assets/Rollgeon/Rooms/Room_Boss_Tahur.asset",
                PropPrefabPath = BarrelProp,
                BossPlanCell = new Vector2Int(5, 5),
                // Cuatro columnas que encarecen el eje vertical, justo donde el Castigo y La Mesa
                // pierden intersección: peleálo de costado. Filas 3 y 7 y no 2 y 8: en el piso 3 los
                // recortes de esquina se comen (3,8), y una columna sobre pared no encarece nada.
                BlockerPlanCells = new[]
                {
                    new Vector2Int(3, 3), new Vector2Int(7, 3),
                    new Vector2Int(3, 7), new Vector2Int(7, 7),
                },
            },
        };

        // ======================================================================
        // Menú
        // ======================================================================

        [MenuItem("Rollgeon/Bosses/Build Boss Rooms")]
        public static void BuildBossRooms()
        {
            int built = 0;
            var failures = new List<string>();

            foreach (var plan in Plans)
            {
                if (Build(plan, failures)) built++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(LogPrefix + $"{built}/{Plans.Length} sala(s) de jefe construida(s).");

            if (failures.Count > 0)
            {
                // Uno por línea y no un solo error con saltos: la consola de Unity muestra sólo la
                // primera línea en la lista, así que un bloque multi-línea obliga a clickear el error
                // para ver qué falló — y por la misma razón se pierde entero si alguien lee el log
                // desde afuera del editor.
                Debug.LogError(LogPrefix + $"{failures.Count} problema(s) de autoría:");
                foreach (var failure in failures) Debug.LogError(LogPrefix + "• " + failure);
            }
        }

        // ======================================================================
        // Coordenadas
        // ======================================================================

        /// <summary>
        /// Traduce una celda del plano del documento a la grilla de la sala.
        /// </summary>
        /// <remarks>
        /// El plano es 11 × 7 con <c>y</c> hacia abajo; la sala real es 11 × 11 centrada en (0,0). El
        /// plano se centra: su celda central (5,3) es la (0,0) de la sala, y el eje <c>y</c> se da
        /// vuelta porque en la grilla <c>+Y</c> es <c>+Z</c> del mundo — el "arriba" del dibujo. Las
        /// dos filas de más arriba y de más abajo de la sala (y = ±4, ±5) quedan fuera del plano y
        /// llegan como estén en la sala base.
        /// </remarks>
        public static GridCoord PlanToRoom(Vector2Int planCell)
        {
            return new GridCoord(planCell.x - PlanWidth / 2, PlanHeight / 2 - planCell.y);
        }

        /// <summary>Centro world de una celda. Misma cuenta que el Room Editor al pintar un tile.</summary>
        public static Vector3 CellCenter(RoomLayout layout, GridCoord cell, int layer)
        {
            var origin = layout.GetOrigin();
            float tileSize = Mathf.Max(layout.TileSize, 0.01f);
            return new Vector3(
                origin.x + (cell.X + 0.5f) * tileSize,
                origin.y + (layer + 0.5f) * tileSize,
                origin.z + (cell.Y + 0.5f) * tileSize);
        }

        /// <summary>Inversa de <see cref="CellCenter"/>. Misma cuenta que <c>GridManager.WorldToGrid</c>.</summary>
        public static GridCoord WorldToCell(RoomLayout layout, Vector3 world)
        {
            var local = world - layout.GetOrigin();
            float tileSize = Mathf.Max(layout.TileSize, 0.01f);
            return new GridCoord(
                Mathf.FloorToInt(local.x / tileSize),
                Mathf.FloorToInt(local.z / tileSize));
        }

        // ======================================================================
        // Construcción de una sala
        // ======================================================================

        private static bool Build(BossRoomPlan plan, List<string> failures)
        {
            if (!ValidatePlan(plan, failures)) return false;

            var contents = PrefabUtility.LoadPrefabContents(plan.BaseRoomPath);
            if (contents == null)
            {
                failures.Add($"{plan.BossName}: no se pudo abrir la sala base '{plan.BaseRoomPath}'.");
                return false;
            }

            try
            {
                var layout = contents.GetComponent<RoomLayout>();
                if (layout == null)
                {
                    failures.Add($"{plan.BossName}: '{plan.BaseRoomPath}' no tiene RoomLayout.");
                    return false;
                }

                // Antes del horneado: los muebles de la base bloquean, así que sacar uno tiene que
                // reflejarse en el grafo. Después del bake el prop desaparecería de la pantalla pero
                // sus casillas seguirían sin ser caminables — el peor de los dos mundos.
                RemoveBaseObjects(contents, plan);

                // El grafo de la sala base, antes de tocar nada: es lo que dice qué celdas del plano
                // ya vienen ocupadas por los muebles propios de la sala.
                var baseGraph = NavGraphBaker.Bake(contents, layout.BakeSettings);
                if (baseGraph.IsEmpty)
                {
                    failures.Add($"{plan.BossName}: la sala base '{plan.BaseRoomPath}' hornea 0 nodos. " +
                                 "Sin piso caminable no hay nada que bloquear.");
                    return false;
                }

                var baseWalkable = WalkableSet(baseGraph);

                // Línea de base de puertas: la sala compartida puede llegar con findings propios, y
                // mezclarlos con los nuestros haría imposible ver cuál causó un blocker del plano.
                layout.NavGraph = baseGraph;
                var baselineDoorFindings = new HashSet<string>(RoomDoorBakeValidator.ValidateRoom(layout));

                var group = ResetBlockerGroup(contents);

                var placed = new List<GridCoord>();
                foreach (var planCell in plan.BlockerPlanCells)
                {
                    var cell = PlanToRoom(planCell);

                    // Ya bloqueada por la sala base (la mesa de pool de piso 1/2, un barril de esquina):
                    // el plano ya se cumple ahí y meter un segundo prop encima sólo apila geometría.
                    if (!baseWalkable.Contains(cell))
                    {
                        Debug.LogWarning(LogPrefix + $"{plan.BossName}: la celda {cell} " +
                                         $"(plano {planCell.x},{planCell.y}) ya está bloqueada en la sala base — " +
                                         "prop del plano omitido.");
                        continue;
                    }

                    if (PlaceBlocker(plan, layout, group, cell) != null) placed.Add(cell);
                }

                ReportPlanCellsEatenByBase(plan, baseWalkable);

                var bossCell = PlanToRoom(plan.BossPlanCell);
                if (!MoveBossSpawn(plan, layout, bossCell, failures)) return false;

                layout.NavGraph = NavGraphBaker.Bake(contents, layout.BakeSettings);

                var playerCell = layout.PlayerSpawnPoint != null
                    ? WorldToCell(layout, layout.PlayerSpawnPoint.position)
                    : (GridCoord?)null;

                var plannedBlockers = new List<GridCoord>();
                foreach (var planCell in plan.BlockerPlanCells) plannedBlockers.Add(PlanToRoom(planCell));

                foreach (var finding in ValidateRoomRules(layout.NavGraph, bossCell, playerCell, plannedBlockers))
                    failures.Add($"{plan.BossName}: {finding}");

                // Las puertas las valida el mismo chequeo que corre el rebaker: un tile-frente que se
                // cayó del grafo deja la sala sin cruce, y eso lo puede provocar un blocker del plano.
                foreach (var finding in RoomDoorBakeValidator.ValidateRoom(layout))
                {
                    if (baselineDoorFindings.Contains(finding)) continue;
                    failures.Add($"{plan.BossName}: {finding}");
                }

                if (!Save(contents, plan, failures)) return false;

                Debug.Log(LogPrefix + $"{plan.BossName} (piso {plan.Floor}) → '{plan.OutputRoomPath}': " +
                          $"{placed.Count}/{plan.BlockerPlanCells.Length} blocker(s), jefe en {bossCell}, " +
                          $"{layout.NavGraph.NodeCount} nodos y {layout.NavGraph.Edges.Count} aristas.");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static bool ValidatePlan(BossRoomPlan plan, List<string> failures)
        {
            if (plan.OutputRoomPath == plan.BaseRoomPath)
            {
                failures.Add($"{plan.BossName}: el output apunta a la sala base '{plan.BaseRoomPath}'. " +
                             "Eso pisaría la sala compartida de todo el piso.");
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(plan.BaseRoomPath) == null)
            {
                failures.Add($"{plan.BossName}: no existe la sala base '{plan.BaseRoomPath}'.");
                return false;
            }

            foreach (var cell in plan.BlockerPlanCells)
            {
                if (cell.x >= 0 && cell.x < PlanWidth && cell.y >= 0 && cell.y < PlanHeight) continue;
                failures.Add($"{plan.BossName}: blocker ({cell.x},{cell.y}) fuera del plano " +
                             $"{PlanWidth}×{PlanHeight}.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Deja el grupo de props del plano vacío y devuelve su transform. Existe por defensa: cada
        /// corrida arranca de la sala base, así que el grupo no debería estar — si está, es que alguien
        /// apuntó el output a una sala ya derivada.
        /// </summary>
        /// <summary>
        /// Borra los muebles de <see cref="BossRoomPlan.RemoveBaseObjectNames"/> de la copia de la sala
        /// base. Corre antes del horneado, así las casillas que ocupaban salen caminables.
        /// </summary>
        /// <remarks>
        /// Un nombre que no aparece es un warning y no un fallo: el plano describe la sala que se
        /// quiere, y que el mueble ya no esté en la base es exactamente el estado buscado. Pero se
        /// avisa igual — un nombre mal escrito se ve idéntico a un mueble ya borrado, y en silencio
        /// dejaría el plano diciendo que saca algo que nunca sacó.
        /// </remarks>
        private static void RemoveBaseObjects(GameObject roomRoot, BossRoomPlan plan)
        {
            if (plan.RemoveBaseObjectNames == null) return;

            foreach (var name in plan.RemoveBaseObjectNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                var target = roomRoot.transform.Find(name);
                if (target == null)
                {
                    Debug.LogWarning(LogPrefix + $"{plan.BossName}: no hay ningún hijo '{name}' en " +
                                     $"'{plan.BaseRoomPath}' — nada que borrar. ¿Cambió de nombre?");
                    continue;
                }

                Object.DestroyImmediate(target.gameObject);
                Debug.Log(LogPrefix + $"{plan.BossName}: borrado '{name}' de la sala base " +
                                      "(sus casillas quedan caminables).");
            }
        }

        private static Transform ResetBlockerGroup(GameObject roomRoot)
        {
            var existing = roomRoot.transform.Find(BlockerGroupName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var group = new GameObject(BlockerGroupName);
            group.transform.SetParent(roomRoot.transform, worldPositionStays: false);
            group.transform.localPosition = Vector3.zero;
            group.transform.localRotation = Quaternion.identity;
            group.transform.localScale = Vector3.one;
            return group.transform;
        }

        private static GameObject PlaceBlocker(
            BossRoomPlan plan, RoomLayout layout, Transform group, GridCoord cell)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(plan.PropPrefabPath);
            if (prefab == null)
            {
                Debug.LogError(LogPrefix + $"{plan.BossName}: falta el prop " +
                               $"'{plan.PropPrefabPath}' — la celda {cell} queda caminable.");
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab, group) as GameObject;
            if (instance == null)
            {
                Debug.LogError(LogPrefix + $"{plan.BossName}: no se pudo instanciar " +
                               $"'{plan.PropPrefabPath}' en {cell}.");
                return null;
            }

            instance.name = $"Blocker_{cell.X}_{cell.Y}";
            instance.transform.SetPositionAndRotation(
                CellCenter(layout, cell, BlockerLayer), Quaternion.Euler(plan.PropEuler));

            // Multiplicar y no asignar: la escala autorada del prop es parte de su arte (la mesa viene
            // a 1.5 × 1.8), y PropScale es la palanca de tuning encima de eso. PropScaleAxes corrige
            // el eje que desborda la casilla sin tocar los otros — ver sus remarks.
            var scale = instance.transform.localScale * plan.PropScale;
            instance.transform.localScale = new Vector3(
                scale.x * plan.PropScaleAxes.x,
                scale.y * plan.PropScaleAxes.y,
                scale.z * plan.PropScaleAxes.z);

            var marker = instance.GetComponent<TileMarker>();
            if (marker == null) marker = instance.AddComponent<TileMarker>();
            marker.Coord = cell;
            marker.Layer = BlockerLayer;
            // Footprint de una celda: un prop por casilla del plano. Un prop largo con footprint 9×1
            // para el mostrador leería igual, pero ataría el bloqueo al pivot y a la rotación del arte.
            marker.Footprint = Vector3Int.one;
            marker.FootprintOffset = Vector3Int.zero;
            marker.Type = TileType.Decoration;
            marker.IsBlocker = true;

            return instance;
        }

        /// <summary>
        /// Mueve el spawn del jefe a su casilla del plano. Es el único lugar donde se decide dónde
        /// pelea: el resolver de combate saca la celda del jefe de
        /// <c>WorldToGrid(EnemySpawnPoints[0].position)</c>.
        /// </summary>
        private static bool MoveBossSpawn(
            BossRoomPlan plan, RoomLayout layout, GridCoord bossCell, List<string> failures)
        {
            Transform spawn = null;
            if (layout.EnemySpawnPoints != null)
            {
                foreach (var candidate in layout.EnemySpawnPoints)
                {
                    if (candidate == null) continue;
                    spawn = candidate;
                    break;
                }
            }

            if (spawn == null)
            {
                failures.Add($"{plan.BossName}: la sala base no tiene EnemySpawnPoints — el jefe no " +
                             "tiene dónde aparecer y su casilla del plano no se puede aplicar.");
                return false;
            }

            spawn.position = CellCenter(layout, bossCell, BlockerLayer);
            return true;
        }

        /// <summary>
        /// Avisa qué celdas que el plano quiere caminables ya se come el mobiliario de la sala base.
        /// Va como warning y no como error: es una divergencia entre el plano (dibujado sobre una
        /// grilla limpia) y la sala real, y resolverla es sacar muebles — decisión de diseño, no del
        /// builder.
        /// </summary>
        private static void ReportPlanCellsEatenByBase(BossRoomPlan plan, HashSet<GridCoord> baseWalkable)
        {
            var planned = new HashSet<GridCoord>();
            foreach (var planCell in plan.BlockerPlanCells) planned.Add(PlanToRoom(planCell));

            var eaten = new List<GridCoord>();
            for (int y = 0; y < PlanHeight; y++)
            {
                for (int x = 0; x < PlanWidth; x++)
                {
                    var cell = PlanToRoom(new Vector2Int(x, y));
                    if (planned.Contains(cell)) continue;
                    if (baseWalkable.Contains(cell)) continue;
                    eaten.Add(cell);
                }
            }

            if (eaten.Count == 0) return;

            Debug.LogWarning(LogPrefix + $"{plan.BossName}: {eaten.Count} celda(s) que el plano quiere " +
                             $"caminables ya están bloqueadas por el mobiliario de la sala base: " +
                             $"{string.Join(", ", eaten)}. La pelea se juega con menos mapa del dibujado.");
        }

        private static bool Save(GameObject contents, BossRoomPlan plan, List<string> failures)
        {
            EnsureFolder(Path.GetDirectoryName(plan.OutputRoomPath));

            contents.name = Path.GetFileNameWithoutExtension(plan.OutputRoomPath);

            // Sobre un path existente reescribe el contenido preservando el GUID: por eso no se borra
            // el asset viejo primero — eso sí rompería las referencias de los RoomSO.
            var saved = PrefabUtility.SaveAsPrefabAsset(contents, plan.OutputRoomPath, out bool success);
            if (!success || saved == null)
            {
                failures.Add($"{plan.BossName}: falló el guardado de '{plan.OutputRoomPath}'.");
                return false;
            }

            return SaveRoomSO(plan, saved, failures);
        }

        /// <summary>
        /// Crea o actualiza el <c>RoomSO</c> que envuelve al prefab de la sala. Es lo que referencia el
        /// <c>WeightedBoss</c> del pool, así que se edita in-place cuando ya existe en vez de borrarlo y
        /// recrearlo — un GUID nuevo dejaría el campo <c>Room</c> del pool en null sin avisar.
        /// </summary>
        /// <remarks>
        /// El <c>ShellIcon</c> sale de la sala de boss compartida a propósito: si cada jefe trajera el
        /// suyo, el minimapa revelaría cuál te tocó antes de entrar. Que se vea o no es una decisión de
        /// diseño aparte; el default es no cambiar lo que hay.
        /// </remarks>
        private static bool SaveRoomSO(BossRoomPlan plan, GameObject roomPrefab, List<string> failures)
        {
            if (string.IsNullOrEmpty(plan.OutputRoomSOPath)) return true;

            EnsureFolder(Path.GetDirectoryName(plan.OutputRoomSOPath));

            var template = AssetDatabase.LoadAssetAtPath<RoomSO>(SharedBossRoomSOPath);
            if (template == null)
            {
                failures.Add($"{plan.BossName}: no existe '{SharedBossRoomSOPath}' para copiarle el " +
                             "ShellIcon y el EnemyPool.");
                return false;
            }

            var so = AssetDatabase.LoadAssetAtPath<RoomSO>(plan.OutputRoomSOPath);
            bool isNew = so == null;
            if (isNew) so = ScriptableObject.CreateInstance<RoomSO>();

            so.RoomId = $"CombatBoss{plan.BossName}";
            so.DisplayName = $"Boss · {plan.BossName}";
            so.Type = RoomType.Boss;
            so.RoomPrefab = roomPrefab;
            so.GridSize = template.GridSize;
            so.ShellIcon = template.ShellIcon;
            so.EnemyPool = template.EnemyPool;
            so.ForcePossibleSetups = false;

            if (isNew) AssetDatabase.CreateAsset(so, plan.OutputRoomSOPath);
            else EditorUtility.SetDirty(so);

            return true;
        }

        // ======================================================================
        // Reglas de autoría (puras — testeables sin assets)
        // ======================================================================

        /// <summary>
        /// Las tres reglas de autoría más el chequeo de que los blockers del plano realmente hayan
        /// caído del grafo. Devuelve un finding por violación; vacío = sala válida.
        /// </summary>
        /// <param name="playerCell">
        /// <c>null</c> si la sala no tiene <c>PlayerSpawnPoint</c> — es un finding en sí mismo.
        /// </param>
        public static List<string> ValidateRoomRules(
            NavGraph graph,
            GridCoord bossCell,
            GridCoord? playerCell,
            IReadOnlyList<GridCoord> plannedBlockers)
        {
            var findings = new List<string>();

            // Un grafo vacío hace que HasNode devuelva true para cualquier celda ("sin restricciones"),
            // así que las tres reglas pasarían por accidente.
            if (graph == null || graph.IsEmpty)
            {
                findings.Add("el NavGraph horneado quedó vacío — la sala no tiene piso caminable.");
                return findings;
            }

            var walkable = WalkableSet(graph);

            if (plannedBlockers != null)
            {
                foreach (var cell in plannedBlockers)
                {
                    if (!walkable.Contains(cell)) continue;
                    findings.Add($"el blocker del plano en {cell} sigue caminable después del bake. " +
                                 "El prop no llega a la banda de walk clearance: revisar su altura o su " +
                                 "PropScale.");
                }
            }

            // (a) El jugador pega a distancia 1: sin casillas pegadas al jefe no hay pelea.
            if (!walkable.Contains(bossCell))
            {
                findings.Add($"la casilla del jefe {bossCell} no es caminable.");
            }
            else
            {
                int adjacency = 0;
                foreach (var edge in graph.GetNeighbors(bossCell))
                    if (walkable.Contains(edge.To)) adjacency++;

                if (adjacency < MinBossAdjacency)
                {
                    findings.Add($"el jefe en {bossCell} tiene {adjacency} casilla(s) adyacente(s) " +
                                 $"caminable(s), mínimo {MinBossAdjacency}.");
                }
            }

            // (b) Una isla de piso es mapa que el jugador ve y no puede usar.
            var start = walkable.Contains(bossCell) ? bossCell : graph.Nodes[0].Coord;
            var isolated = UnreachableFrom(graph, walkable, start);
            if (isolated.Count > 0)
            {
                findings.Add($"{isolated.Count} casilla(s) de piso aisladas del resto de la sala: " +
                             $"{Describe(isolated)}.");
            }

            // (c) Entrar a la sala y aparecer dentro de un mueble.
            if (!playerCell.HasValue)
            {
                findings.Add("la sala no tiene PlayerSpawnPoint — no se puede validar el spawn del jugador.");
            }
            else if (!walkable.Contains(playerCell.Value))
            {
                findings.Add($"la casilla de spawn del jugador {playerCell.Value} quedó bloqueada.");
            }

            return findings;
        }

        private static HashSet<GridCoord> WalkableSet(NavGraph graph)
        {
            var set = new HashSet<GridCoord>();
            foreach (var node in graph.Nodes) set.Add(node.Coord);
            return set;
        }

        /// <summary>
        /// Nodos que el BFS del grafo no alcanza desde <paramref name="start"/>. Camina las aristas
        /// horneadas — el mismo grafo que camina el movimiento en combate — y no la vecindad-4 teórica:
        /// dos celdas pegadas pueden no tener arista.
        /// </summary>
        private static List<GridCoord> UnreachableFrom(
            NavGraph graph, HashSet<GridCoord> walkable, GridCoord start)
        {
            var seen = new HashSet<GridCoord> { start };
            var queue = new Queue<GridCoord>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var edge in graph.GetNeighbors(current))
                {
                    if (!seen.Add(edge.To)) continue;
                    queue.Enqueue(edge.To);
                }
            }

            var unreachable = new List<GridCoord>();
            foreach (var cell in walkable)
                if (!seen.Contains(cell)) unreachable.Add(cell);
            return unreachable;
        }

        private static string Describe(List<GridCoord> cells)
        {
            const int maxShown = 8;
            var shown = new List<string>();
            for (int i = 0; i < cells.Count && i < maxShown; i++) shown.Add(cells[i].ToString());
            if (cells.Count > maxShown) shown.Add($"… (+{cells.Count - maxShown})");
            return string.Join(", ", shown);
        }

        // ======================================================================
        // Helpers de AssetDatabase
        // ======================================================================

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;

            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }

    // ==========================================================================
    // Plano
    // ==========================================================================

    /// <summary>El terreno de un jefe. Ver <see cref="BossRoomBuilder"/>.</summary>
    public sealed class BossRoomPlan
    {
        /// <summary>Nombre corto del jefe — sale en los logs y en el nombre del prefab.</summary>
        public string BossName;

        /// <summary>Piso al que pertenece. Sólo informativo (el pool del piso lo arma otro instalador).</summary>
        public int Floor;

        /// <summary>Sala compartida del piso, que se clona en cada corrida.</summary>
        public string BaseRoomPath;

        /// <summary>Sala propia del jefe. Se reescribe sobre este path, que preserva el GUID.</summary>
        public string OutputRoomPath;

        /// <summary>
        /// <c>RoomSO</c> que envuelve al prefab. Es lo que el <c>WeightedBoss</c> del pool referencia,
        /// así que su GUID también tiene que sobrevivir a los rebuilds.
        /// </summary>
        public string OutputRoomSOPath;

        /// <summary>Prop que se instancia en cada celda bloqueada. <c>null</c> = plano sin blockers.</summary>
        public string PropPrefabPath;

        /// <summary>Celda del jefe, en coordenadas del plano (11 × 7, y hacia abajo).</summary>
        public Vector2Int BossPlanCell;

        /// <summary>Celdas bloqueadas, en coordenadas del plano.</summary>
        public Vector2Int[] BlockerPlanCells = new Vector2Int[0];

        /// <summary>
        /// Muebles de la sala base a borrar en <b>esta</b> sala, por nombre de hijo directo de la raíz.
        /// Vacío = la sala base se respeta tal cual.
        /// </summary>
        /// <remarks>
        /// Palanca de sala, no de encuadre: los muebles de la base bloquean, así que borrar uno
        /// libera sus casillas. Se aplica antes del horneado. Existe para poder sacar un mueble de
        /// una sola sala sin tocar la base, que es compartida por todos los jefes del piso.
        /// </remarks>
        public string[] RemoveBaseObjectNames = new string[0];

        /// <summary>Rotación world del prop. Palanca de encuadre visual: no cambia qué se bloquea.</summary>
        public Vector3 PropEuler = Vector3.zero;

        /// <summary>Factor sobre la escala autorada del prop. Palanca de encuadre visual.</summary>
        public float PropScale = 1f;

        /// <summary>
        /// Factor <b>por eje</b> sobre la escala autorada, encima de <see cref="PropScale"/>. Sirve
        /// para props cuyo arte no es cuadrado y desbordan la casilla en un solo eje.
        /// </summary>
        /// <remarks>
        /// <see cref="PropScale"/> no alcanza porque es uniforme: encoger el eje que desborda también
        /// baja la altura, y por debajo de la banda de walk clearance del bake el prop deja de
        /// bloquear. Medir con <c>Rollgeon → Bosses → Dump Prop Bounds</c>.
        /// </remarks>
        public Vector3 PropScaleAxes = Vector3.one;
    }
}
