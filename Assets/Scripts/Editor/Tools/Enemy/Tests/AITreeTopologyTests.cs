using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Editor.Tools.Enemy.AITree;
using Rollgeon.Entities;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    [TestFixture]
    public class AITreeTopologyTests
    {
        readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            // El undo stack es global al editor: lo que quede acá se cuela en el PerformUndo
            // del próximo test.
            Undo.ClearAll();
            foreach (var o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- IsTopologyMember --------------------------------------------

        [TestCase(typeof(AIDecisionNode))]
        [TestCase(typeof(AINode_Wait))]
        [TestCase(typeof(List<AIDecisionNode>))]
        [TestCase(typeof(List<AINode_Random.Option>))]
        public void IsTopologyMember_ChildrenAndChildLists_True(Type declared)
        {
            Assert.IsTrue(AITreeTopology.IsTopologyMember(declared));
        }

        [TestCase(typeof(int))]
        [TestCase(typeof(string))]
        [TestCase(typeof(List<int>))]
        [TestCase(typeof(BaseEnemyTargetSelector))]
        public void IsTopologyMember_PlainParameters_False(Type declared)
        {
            Assert.IsFalse(AITreeTopology.IsTopologyMember(declared));
        }

        /// <summary>
        /// Guardia contra el próximo compuesto olvidado: si un nodo declara un campo de hijos
        /// pero la topología no le da slots, el editor lo dibuja como hoja y sus hijos
        /// desaparecen del canvas (pasó con Alternate).
        /// </summary>
        [Test]
        public void EveryNodeWithTopologyFields_HasSlots()
        {
            var missing = new List<string>();
            foreach (var meta in AINodeRegistry.All)
            {
                bool declaresChildren = false;
                foreach (var f in meta.Type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (AITreeTopology.IsTopologyMember(f.FieldType)) { declaresChildren = true; break; }
                }
                if (!declaresChildren) continue;

                var instance = (AIDecisionNode)Activator.CreateInstance(meta.Type);
                if (AITreeTopology.SlotsOf(instance).Count == 0) missing.Add(meta.Type.Name);
            }

            Assert.IsEmpty(missing,
                "Nodos con campos de hijos que AITreeTopology no conoce: " + string.Join(", ", missing));
        }

        // ---- Commit (undo ordering) --------------------------------------

        EnemyDataSO NewEnemyWithSequence(out AINode_Sequence seq, out AINode_Wait leaf1, out AINode_Wait leaf2)
        {
            var so = ScriptableObject.CreateInstance<EnemyDataSO>();
            _spawned.Add(so);
            seq = new AINode_Sequence();
            leaf1 = new AINode_Wait();
            leaf2 = new AINode_Wait();
            seq.Children.Add(leaf1);
            seq.Children.Add(leaf2);
            so.AIRoot = seq;
            return so;
        }

        [Test]
        public void Commit_RecordsUndoBeforeMutating_SoUndoRestoresTopology()
        {
            var so = NewEnemyWithSequence(out var seq, out _, out var leaf2);
            var snap = AITreeSerializer.Load(so.AIRoot);
            snap.Nodes.Remove(leaf2);
            snap.Edges.RemoveAll(e => e.Child == leaf2);

            bool ok = AITreeSerializer.Commit(so, snap, "Edit AI Tree", out var errors);

            Assert.IsTrue(ok);
            Assert.IsEmpty(errors);
            Assert.AreEqual(1, seq.Children.Count);

            Undo.PerformUndo();

            // Odin repuebla AIRoot con instancias nuevas: comparar por forma, no por referencia.
            var restored = so.AIRoot as AINode_Sequence;
            Assert.IsNotNull(restored);
            Assert.AreEqual(2, restored.Children.Count,
                "RecordObject después de Save deja un 'antes' igual al 'después' y Ctrl+Z no revierte");
        }

        [Test]
        public void Commit_WithCycle_DoesNotTouchAsset()
        {
            var so = NewEnemyWithSequence(out var seq, out var leaf1, out _);
            var snap = AITreeSerializer.Load(so.AIRoot);
            snap.Edges.Add(new GraphSnapshot.Edge(leaf1, 0, seq)); // leaf1 → seq: ciclo

            bool ok = AITreeSerializer.Commit(so, snap, "Edit AI Tree", out var issues);

            Assert.IsFalse(ok);
            Assert.IsTrue(AITreeValidator.HasErrors(issues));
            Assert.AreSame(seq, so.AIRoot);
            Assert.AreEqual(2, seq.Children.Count);
        }

        [Test]
        public void Commit_WithWarningsOnly_WritesAsset()
        {
            var so = NewEnemyWithSequence(out var seq, out _, out var leaf2);
            var snap = AITreeSerializer.Load(so.AIRoot);
            snap.Edges.RemoveAll(e => e.Child == leaf2); // leaf2 queda suelto (info); seq sigue con 1 hijo

            bool ok = AITreeSerializer.Commit(so, snap, "Edit AI Tree", out var issues);

            Assert.IsTrue(ok);
            Assert.IsFalse(AITreeValidator.HasErrors(issues));
            Assert.AreEqual(1, seq.Children.Count);
            CollectionAssert.AreEqual(new[] { leaf2 }, so.AIDetachedNodes);
        }

        [Test]
        public void Commit_WritesDetachedRootsToAIDetachedNodes_AndLoadRoundTrips()
        {
            var so = NewEnemyWithSequence(out var seq, out _, out _);
            var snap = AITreeSerializer.Load(so.AIRoot);
            var loose = new AINode_Sequence();
            var looseLeaf = new AINode_Wait();
            snap.Nodes.Add(loose);
            snap.Nodes.Add(looseLeaf);
            snap.Edges.Add(new GraphSnapshot.Edge(loose, 0, looseLeaf));

            Assert.IsTrue(AITreeSerializer.Commit(so, snap, "Edit AI Tree", out _));

            CollectionAssert.AreEqual(new[] { loose }, so.AIDetachedNodes);
            var reloaded = AITreeSerializer.Load(so.AIRoot, so.AIDetachedNodes);
            Assert.AreEqual(5, reloaded.Nodes.Count);
            Assert.AreEqual(3, reloaded.Edges.Count);
            Assert.AreSame(seq, reloaded.Root);
        }

        [Test]
        public void Commit_UndoRestoresAIDetachedNodes()
        {
            var so = NewEnemyWithSequence(out _, out _, out _);
            var snap = AITreeSerializer.Load(so.AIRoot);
            snap.Nodes.Add(new AINode_Wait());

            Assert.IsTrue(AITreeSerializer.Commit(so, snap, "Edit AI Tree", out _));
            Assert.AreEqual(1, so.AIDetachedNodes.Count);

            Undo.PerformUndo();

            Assert.AreEqual(0, so.AIDetachedNodes.Count);
        }

        // ---- PortLabel ------------------------------------------------------

        [Test]
        public void PortLabel_FreeDynamicSlot_IsPlus()
        {
            var slot = AITreeTopology.SlotsOf(new AINode_Sequence())[0];
            Assert.AreEqual("+", AITreeTopology.PortLabel(slot, null));
        }

        [Test]
        public void PortLabel_ConnectedDynamicSlot_ShowsOrdinal()
        {
            var slot = AITreeTopology.SlotsOf(new AINode_Sequence())[0];
            Assert.AreEqual("2", AITreeTopology.PortLabel(slot, 2));
        }

        [Test]
        public void PortLabel_Random_ShowsOrdinalAndWeight()
        {
            var slot = AITreeTopology.SlotsOf(new AINode_Random())[0];
            Assert.AreEqual("1 · peso 2.5", AITreeTopology.PortLabel(slot, 1, 2.5f));
        }

        [Test]
        public void PortLabel_FixedSlot_IsSpanishLabel()
        {
            var slots = AITreeTopology.SlotsOf(new AINode_If());
            Assert.AreEqual("Entonces", AITreeTopology.PortLabel(slots[0], null));
            Assert.AreEqual("Si no", AITreeTopology.PortLabel(slots[1], 1));
            Assert.AreEqual("Then", slots[0].Name, "Name es identificador estable, no se traduce");
        }
    }
}
