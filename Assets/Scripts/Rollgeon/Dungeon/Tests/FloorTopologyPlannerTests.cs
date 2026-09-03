using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Regresión del planner de topología. Cubre dos modos de falla observados en
    /// el preview del Floor Editor (seed 12345, "Piso 1"):
    /// 1. Un slot Enchantment con count>0 no se colocaba (estaba fuera de
    ///    <c>specialOrder</c>): su count inflaba el target pero la cell caía al
    ///    fallback de Combat, así que las salas de encantamiento nunca aparecían.
    /// 2. Caracterización: si el pool de un slot contiene una sala de OTRO tipo, la
    ///    cell hereda el tipo de la sala pooled (el planner confía en el pool tal
    ///    cual). Esto producía "dos starts" cuando el slot Shop quedó cableado a la
    ///    sala Start por error de datos.
    /// Suma también BUG-064: la boss room tiene que quedar separada del spawn por
    /// ≥2 salas Combat (antes podía darse spawn → tienda → boss).
    /// </summary>
    [TestFixture]
    public class FloorTopologyPlannerTests
    {
        private readonly List<Object> _created = new();

        private RoomSO Room(string id, RoomType type)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = id;
            room.DisplayName = id;
            room.Type = type;
            _created.Add(room);
            return room;
        }

        private RoomTypeSlot Slot(RoomType type, int fixedCount, RoomSO pooled)
        {
            return new RoomTypeSlot
            {
                Type = type,
                Count = new RoomCountSpec { Mode = RoomCountMode.Fixed, Fixed = fixedCount },
                Pool = new List<RoomSO> { pooled },
            };
        }

        private RoomTypeSlot SlotRandom(RoomType type, int min, int max, RoomSO pooled)
        {
            return new RoomTypeSlot
            {
                Type = type,
                Count = new RoomCountSpec { Mode = RoomCountMode.Random, Min = min, Max = max },
                Pool = new List<RoomSO> { pooled },
            };
        }

        private static readonly Vector2Int[] TestSteps =
        {
            new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0),
        };

        /// <summary>
        /// BFS independiente de la implementación del planner — no reusa
        /// <c>FloorTopologyPlanner.ComputeGraphDistances</c> a propósito, para que el test
        /// verifique el resultado y no simplemente re-ejecute la misma lógica.
        /// </summary>
        private static int GraphDistance(HashSet<Vector2Int> cellSet, Vector2Int from, Vector2Int to)
        {
            var dist = new Dictionary<Vector2Int, int> { [from] = 0 };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (cur == to) return dist[cur];
                foreach (var step in TestSteps)
                {
                    var next = cur + step;
                    if (!cellSet.Contains(next) || dist.ContainsKey(next)) continue;
                    dist[next] = dist[cur] + 1;
                    queue.Enqueue(next);
                }
            }
            return int.MaxValue; // no debería pasar en un piso conexo
        }

        /// <summary>
        /// BUG-064 — verificación fuerte del invariante: ¿existe algún camino start→boss que
        /// cruce a lo sumo 1 sala Combat? BFS con estado (celda, combatesUsados), podando
        /// caminos que ya gastaron su único combate permitido. El fight de la boss room en sí
        /// no cuenta — el presupuesto es sobre lo que se cruza ANTES de llegar.
        /// </summary>
        private static bool BossReachableUsingAtMostOneCombat(
            FloorTopologyPlanner.Plan plan, Vector2Int start, Vector2Int boss)
        {
            var cellSet = new HashSet<Vector2Int>(plan.Cells);
            var types = plan.Types;
            var best = new Dictionary<Vector2Int, int> { [start] = 0 };
            var queue = new Queue<(Vector2Int cell, int used)>();
            queue.Enqueue((start, 0));

            while (queue.Count > 0)
            {
                var (cell, used) = queue.Dequeue();
                if (cell == boss) return true;

                foreach (var step in TestSteps)
                {
                    var next = cell + step;
                    if (!cellSet.Contains(next)) continue;

                    int nextUsed = used;
                    if (next != boss && types.TryGetValue(next, out var t) && t == RoomType.Combat)
                        nextUsed++;
                    if (nextUsed > 1) continue; // ya gastó el único combate permitido

                    if (best.TryGetValue(next, out var prevBest) && prevBest <= nextUsed) continue;
                    best[next] = nextUsed;
                    queue.Enqueue((next, nextUsed));
                }
            }
            return false;
        }

        private FloorLayoutSO Layout(params RoomTypeSlot[] slots)
        {
            var layout = ScriptableObject.CreateInstance<FloorLayoutSO>();
            layout.Slots = new List<RoomTypeSlot>(slots);
            _created.Add(layout);
            return layout;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        [Test]
        public void Generate_EnchantmentSlotWithCount_PlacesEnchantmentRoom()
        {
            // Arrange — Start + Boss + Combat + Enchantment, todos pooled con su tipo.
            var layout = Layout(
                Slot(RoomType.Start, 1, Room("start", RoomType.Start)),
                Slot(RoomType.Boss, 1, Room("boss", RoomType.Boss)),
                Slot(RoomType.Combat, 1, Room("combat", RoomType.Combat)),
                Slot(RoomType.Enchantment, 1, Room("ench", RoomType.Enchantment)));

            // Act
            var plan = FloorTopologyPlanner.Generate(layout, seed: 12345);

            // Assert — exactamente una cell de tipo Enchantment (antes del fix: 0).
            Assert.AreEqual(1, plan.Types.Values.Count(t => t == RoomType.Enchantment),
                "Un slot Enchantment con count=1 debe colocar exactamente una sala Enchantment.");
            Assert.IsFalse(plan.Warnings.Any(w => w.Contains("Enchantment")),
                "No debería quedar cupo de Enchantment sin colocar.");
        }

        [Test]
        public void Generate_RingEatsAllFreeCells_GrowsTopologyAndPlacesAllSpecials()
        {
            // Arrange — piso mínimo donde el anillo del boss (BUG-064) se come todas
            // las celdas libres: sin el growth, las especiales degradaban a warning y
            // la run salía sin sala de encantamiento.
            var layout = Layout(
                Slot(RoomType.Start, 1, Room("start", RoomType.Start)),
                Slot(RoomType.Boss, 1, Room("boss", RoomType.Boss)),
                Slot(RoomType.Shop, 1, Room("shop", RoomType.Shop)),
                Slot(RoomType.Potion, 1, Room("potion", RoomType.Potion)),
                Slot(RoomType.Enchantment, 1, Room("ench", RoomType.Enchantment)));

            for (int seed = 0; seed < 50; seed++)
            {
                // Act
                var plan = FloorTopologyPlanner.Generate(layout, seed);

                // Assert — cada especial colocada exactamente una vez, para todo seed.
                Assert.AreEqual(1, plan.Types.Values.Count(t => t == RoomType.Shop),
                    $"seed {seed}: Shop no quedó colocada exactamente una vez.");
                Assert.AreEqual(1, plan.Types.Values.Count(t => t == RoomType.Potion),
                    $"seed {seed}: Potion no quedó colocada exactamente una vez.");
                Assert.AreEqual(1, plan.Types.Values.Count(t => t == RoomType.Enchantment),
                    $"seed {seed}: Enchantment no quedó colocada exactamente una vez.");
            }
        }

        [Test]
        public void Generate_SlotPoolHasWrongType_CellInheritsPooledRoomType()
        {
            // Arrange — el slot Shop quedó (por error de datos) pooled con una sala Start.
            var layout = Layout(
                Slot(RoomType.Start, 1, Room("start", RoomType.Start)),
                Slot(RoomType.Boss, 1, Room("boss", RoomType.Boss)),
                Slot(RoomType.Shop, 1, Room("mislabeled", RoomType.Start)));

            // Act
            var plan = FloorTopologyPlanner.Generate(layout, seed: 12345);

            // Assert — ninguna Shop; la cell hereda Start → "dos starts".
            Assert.AreEqual(0, plan.Types.Values.Count(t => t == RoomType.Shop),
                "Con el pool mal cableado, ninguna cell sale como Shop.");
            Assert.AreEqual(2, plan.Types.Values.Count(t => t == RoomType.Start),
                "La cell Shop hereda el tipo de la sala pooled (Start), dando dos cells Start.");
        }

        /// <summary>
        /// BUG-064 — regresión del bug original ("spawn → tienda → boss"): barrido de seeds
        /// contra un layout tipo <c>FloorLayout.asset</c> (Combat 4..6, Shop 1, Enchantment 1,
        /// Boss 1, Start 1). Por seed valida: (a) el boss queda a distancia de GRAFO ≥3 del
        /// start (o el piso queda explícitamente warneado como degenerado); (b) ningún camino
        /// start→boss cruza ≤1 sala Combat; (c) las especiales que se colocan caen fuera del
        /// anillo reservado del boss (si no entran por falta de cupo, hay warning explícito).
        /// </summary>
        [Test]
        public void Generate_SeedSweep_BossIsSeparatedByAtLeastTwoCombatRooms()
        {
            // Arrange — layout tipo FloorLayout.asset (Combat 4..6, Shop 1, Enchantment 1,
            // Boss 1, Start 1). Seeds 0..199 fijos: pseudo-random determinístico, no estado
            // externo — property-based sweep contra el mismo layout en cada corrida.
            const int seedCount = 200;
            var layout = Layout(
                Slot(RoomType.Start, 1, Room("start", RoomType.Start)),
                Slot(RoomType.Boss, 1, Room("boss", RoomType.Boss)),
                SlotRandom(RoomType.Combat, 4, 6, Room("combat", RoomType.Combat)),
                Slot(RoomType.Shop, 1, Room("shop", RoomType.Shop)),
                Slot(RoomType.Enchantment, 1, Room("ench", RoomType.Enchantment)));

            int degenerateCount = 0;

            for (int seed = 0; seed < seedCount; seed++)
            {
                // Act
                var plan = FloorTopologyPlanner.Generate(layout, seed);
                var cellSet = new HashSet<Vector2Int>(plan.Cells);
                var start = Vector2Int.zero;

                // Assert
                var bossCells = plan.Types.Where(kv => kv.Value == RoomType.Boss)
                    .Select(kv => kv.Key).ToList();
                Assert.AreEqual(1, bossCells.Count, $"seed {seed}: esperaba exactamente 1 boss.");
                var bossCell = bossCells[0];
                int distToBoss = GraphDistance(cellSet, start, bossCell);

                // (c) — especiales que SÍ se colocaron quedan fuera del anillo reservado del
                // boss. No se exige que ambas existan siempre: en un piso chico (8-10 salas)
                // un anillo grande puede agotar el cupo, y eso ya queda cubierto por el warning
                // "{Tipo}: pedía N, cupieron M." del specialOrder loop (comportamiento previo,
                // sin tocar) — degradar ahí no es un fallo del invariante de BUG-064.
                var shopCells = plan.Types.Where(kv => kv.Value == RoomType.Shop)
                    .Select(kv => kv.Key).ToList();
                var enchCells = plan.Types.Where(kv => kv.Value == RoomType.Enchantment)
                    .Select(kv => kv.Key).ToList();
                Assert.LessOrEqual(shopCells.Count, 1, $"seed {seed}: no debería haber más de 1 Shop.");
                Assert.LessOrEqual(enchCells.Count, 1, $"seed {seed}: no debería haber más de 1 Enchantment.");
                if (shopCells.Count == 0)
                    Assert.IsTrue(plan.Warnings.Any(w => w.StartsWith("Shop:")),
                        $"seed {seed}: Shop no se colocó y no hay warning explicando por qué.");
                if (enchCells.Count == 0)
                    Assert.IsTrue(plan.Warnings.Any(w => w.StartsWith("Enchantment:")),
                        $"seed {seed}: Enchantment no se colocó y no hay warning explicando por qué.");
                foreach (var specialCell in shopCells.Concat(enchCells))
                {
                    Assert.Greater(GraphDistance(cellSet, bossCell, specialCell), 2,
                        $"seed {seed}: la sala especial {specialCell} cayó dentro del anillo reservado del boss.");
                }

                if (distToBoss < FloorTopologyPlanner.MinBossGraphDistance)
                {
                    // Piso chico donde no hubo margen — tiene que quedar warneado explícito,
                    // no fallar en silencio ni tirar excepción.
                    degenerateCount++;
                    Assert.IsTrue(plan.Warnings.Any(w => w.StartsWith("Boss:") && w.Contains("distancia")),
                        $"seed {seed}: boss a distancia {distToBoss} sin warning.");
                    continue;
                }

                // (a)+(b) — invariante fuerte: ningún camino start→boss cruza ≤1 Combat.
                Assert.IsFalse(BossReachableUsingAtMostOneCombat(plan, start, bossCell),
                    $"seed {seed}: existe un camino start→boss que cruza ≤1 sala Combat.");
            }

            // Sanity del sweep: con 7-9 salas casi siempre hay margen para dist≥3. Si TODOS
            // los seeds degeneraron, el layout de prueba está mal armado, no es una falla real.
            Assert.Less(degenerateCount, seedCount,
                "Todos los seeds degeneraron — revisar el layout de prueba, no debería pasar con 7-9 salas.");
        }

        /// <summary>
        /// BUG-064 — piso degenerado: 3 celdas en línea (mínimo permitido por
        /// <see cref="FloorTopologyPlanner.MinRoomCount"/>). No hay margen para separar el
        /// boss del start por ≥2 combates; el planner tiene que warnear y no explotar, no
        /// fingir que el invariante se cumplió. Se llama a <c>AssignTemplates</c> directo
        /// con cells fijas para no depender del RNG de <c>GenerateTopology</c>.
        /// </summary>
        [Test]
        public void AssignTemplates_DegenerateLineOfThreeCells_WarnsAndDoesNotThrow()
        {
            // Arrange — piso forzado en línea recta de 3 celdas (start–combat–boss), cells
            // fijas para no depender del RNG de GenerateTopology.
            var layout = Layout(
                Slot(RoomType.Start, 1, Room("start", RoomType.Start)),
                Slot(RoomType.Boss, 1, Room("boss", RoomType.Boss)),
                Slot(RoomType.Combat, 1, Room("combat", RoomType.Combat)));

            var cells = new List<Vector2Int> { Vector2Int.zero, new Vector2Int(1, 0), new Vector2Int(2, 0) };
            var resolved = new Dictionary<RoomType, int>
            {
                { RoomType.Start, 1 }, { RoomType.Boss, 1 }, { RoomType.Combat, 1 },
            };
            var warnings = new List<string>();

            // Act
            Dictionary<Vector2Int, RoomSO> assignments = null;
            Assert.DoesNotThrow(() =>
                assignments = FloorTopologyPlanner.AssignTemplates(cells, layout, resolved, new System.Random(1), warnings));

            // Assert
            Assert.AreEqual(3, assignments.Count, "Las 3 celdas del piso degenerado deben quedar asignadas igual.");
            Assert.IsTrue(warnings.Any(w => w.StartsWith("Boss:") && w.Contains("distancia")),
                "Un piso de 3 celdas en línea no puede garantizar distancia ≥3 al boss; tiene que warnear.");
        }
    }
}
