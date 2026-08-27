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
        public void Commit_WithValidationErrors_DoesNotTouchAsset()
        {
            var so = NewEnemyWithSequence(out var seq, out _, out _);
            var snap = AITreeSerializer.Load(so.AIRoot);
            var orphan = new AINode_Wait();
            snap.Nodes.Add(orphan);

            bool ok = AITreeSerializer.Commit(so, snap, "Edit AI Tree", out var errors);

            Assert.IsFalse(ok);
            CollectionAssert.IsNotEmpty(errors);
            Assert.AreSame(seq, so.AIRoot);
            Assert.AreEqual(2, seq.Children.Count);
        }
    }
}
