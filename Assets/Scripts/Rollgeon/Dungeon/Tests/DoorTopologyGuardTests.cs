using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Dungeon.Components;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Guard de coherencia grafo↔puertas (PUL-011). El bug original: la generación conecta
    /// salas por adyacencia sin validar que el prefab tenga puerta en esa dirección, así que
    /// el minimapa mostraba una conexión sin paso físico (Start_Room01 sin puerta Este).
    /// </summary>
    [TestFixture]
    public class DoorTopologyGuardTests
    {
        // Grafo A(0,0) ↔ B(1,0): A-Este/B-Oeste.
        private static Dictionary<Guid, RoomInstance> BuildPair(out Guid aId, out Guid bId)
        {
            aId = Guid.NewGuid();
            bId = Guid.NewGuid();
            var a = new RoomInstance { InstanceId = aId };
            var b = new RoomInstance { InstanceId = bId };
            a.Connections[DoorDirection.East] = bId;
            b.Connections[DoorDirection.West] = aId;
            return new Dictionary<Guid, RoomInstance> { [aId] = a, [bId] = b };
        }

        [Test]
        public void ComputeDoorlessEdges_BothSidesHaveDoors_NoPrune()
        {
            var g = BuildPair(out _, out _);

            var edges = DoorTopologyGuard.ComputeDoorlessEdges(g, (inst, dir) => true);

            Assert.IsEmpty(edges, "Con puertas en ambos lados la conexión es válida.");
        }

        [Test]
        public void ComputeDoorlessEdges_MissingDoorOnOneSide_ReturnsEdge()
        {
            var g = BuildPair(out var aId, out var bId);

            // A no tiene puerta Este (el caso Start_Room01); todo lo demás sí.
            Func<RoomInstance, DoorDirection, bool> hasDoor =
                (inst, dir) => !(inst.InstanceId == aId && dir == DoorDirection.East);

            var edges = DoorTopologyGuard.ComputeDoorlessEdges(g, hasDoor);

            Assert.AreEqual(1, edges.Count, "La conexión sin puerta debe reportarse una sola vez.");
        }

        [Test]
        public void ComputeDoorlessEdges_AsymmetricNeighborMissingReciprocal_ReturnsEdge()
        {
            var g = BuildPair(out var aId, out var bId);

            // A tiene Este pero B no tiene Oeste: puerta asimétrica (no se puede volver).
            Func<RoomInstance, DoorDirection, bool> hasDoor =
                (inst, dir) => !(inst.InstanceId == bId && dir == DoorDirection.West);

            var edges = DoorTopologyGuard.ComputeDoorlessEdges(g, hasDoor);

            Assert.AreEqual(1, edges.Count);
        }

        [Test]
        public void ComputeDoorlessEdges_DedupesByPair_NotPerDirection()
        {
            var g = BuildPair(out _, out _);

            // Ningún lado tiene puerta: A→B y B→A fallan, pero es UNA sola arista.
            var edges = DoorTopologyGuard.ComputeDoorlessEdges(g, (inst, dir) => false);

            Assert.AreEqual(1, edges.Count, "Una arista compartida no debe reportarse dos veces.");
        }

        [Test]
        public void ReachableFrom_PrunedEdge_LeavesNeighborUnreachable()
        {
            var g = BuildPair(out var aId, out var bId);
            // Simular la poda: quitar la conexión de ambos lados.
            g[aId].Connections.Remove(DoorDirection.East);
            g[bId].Connections.Remove(DoorDirection.West);

            var reachable = DoorTopologyGuard.ReachableFrom(aId, g);

            Assert.Contains(aId, reachable.ToList());
            Assert.IsFalse(reachable.Contains(bId), "Tras podar, B quedó aislada de A.");
        }

        [Test]
        public void ReachableFrom_ConnectedGraph_ReachesAll()
        {
            var g = BuildPair(out var aId, out var bId);

            var reachable = DoorTopologyGuard.ReachableFrom(aId, g);

            Assert.AreEqual(2, reachable.Count);
        }

        [Test]
        public void ReachableFrom_UnknownStart_ReturnsEmpty()
        {
            var g = BuildPair(out _, out _);

            var reachable = DoorTopologyGuard.ReachableFrom(Guid.NewGuid(), g);

            Assert.IsEmpty(reachable);
        }
    }
}
