using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.Editor.Tools.Polymorphic.Graph;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Items;
using Rollgeon.PreConditions;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Polymorphic.Tests
{
    /// <summary>
    /// The graph is a projection, so it is pure code and worth testing — the canvas that renders it
    /// is not.
    /// </summary>
    public sealed class BlockGraphTests
    {
        readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        ItemSO NewActiveItem()
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "test.item";
            item.Type = ItemType.Active;
            item.OnActivate = new EffectData();
            _spawned.Add(item);
            return item;
        }

        /// <summary>
        /// Busca por el nombre del tipo, no por el título.
        /// </summary>
        /// <remarks>
        /// El <c>Title</c> es texto de display y cambia a propósito — un nodo de efecto ahora dice
        /// "Chain", no "EffChain", porque el grafo existe para que se lea qué hace el ítem. Un test
        /// que ancla en el título se rompe cada vez que alguien mejora una etiqueta, sin que haya
        /// pasado nada malo. El <c>Subtitle</c> es el nombre del tipo y sí es estructura.
        /// </remarks>
        static BlockGraphNode FindBySubtitle(BlockGraphModel.Result model, string subtitle)
        {
            foreach (var n in model.AllNodes) if (n.Subtitle == subtitle) return n;
            return null;
        }

        // ---- model ----------------------------------------------------------

        [Test]
        public void Build_NullAsset_ReturnsEmptyResult()
        {
            var model = BlockGraphModel.Build(null);

            Assert.IsNull(model.Root);
            Assert.IsEmpty(model.AllNodes);
        }

        [Test]
        public void Build_ActiveItem_ProjectsRootThenGroupThenEffect()
        {
            var item = NewActiveItem();
            item.OnActivate.Label = "Heal";
            item.OnActivate.Effects.Add(new EffHeal());

            var model = BlockGraphModel.Build(item);

            Assert.AreEqual(BlockNodeKind.Root, model.Root.Kind);
            Assert.AreEqual(1, model.Root.Children.Count);

            var group = model.Root.Children[0];
            Assert.AreEqual(BlockNodeKind.Group, group.Kind);
            Assert.AreEqual("Heal", group.Title, "an EffectData titles itself with its Label");
            Assert.AreEqual("OnActivate", group.Path);

            var effect = group.Children[0];
            Assert.AreEqual(BlockNodeKind.Effect, effect.Kind);
            Assert.AreEqual("OnActivate.Effects.$0", effect.Path);
        }

        [Test]
        public void Build_KeepsEffectOrder_BecauseOrderIsExecutionOrder()
        {
            var item = NewActiveItem();
            item.OnActivate.Effects.Add(new EffAddShield());
            item.OnActivate.Effects.Add(new EffHeal());
            item.OnActivate.Effects.Add(new EffDealDamage());

            var model = BlockGraphModel.Build(item);
            var group = model.Root.Children[0];

            Assert.AreEqual("EffAddShield", group.Children[0].Subtitle);
            Assert.AreEqual("EffHeal", group.Children[1].Subtitle);
            Assert.AreEqual("EffDealDamage", group.Children[2].Subtitle);
        }

        [Test]
        public void Build_Chain_HangsPhasesOffTheParent()
        {
            var item = NewActiveItem();
            var chain = new EffChain();
            chain.Phases.Add(new ChainPhase());
            chain.Phases.Add(new ChainPhase());
            chain.Phases[0].Effects.Effects.Add(new EffDealDamage());
            chain.Phases[1].Effects.Effects.Add(new EffAddShield());
            item.OnActivate.Effects.Add(chain);

            var model = BlockGraphModel.Build(item);

            var chainNode = FindBySubtitle(model, "EffChain");
            Assert.IsNotNull(chainNode, "the chain is a node of its own");
            Assert.AreEqual("Chain", chainNode.Title, "el título es el nombre legible del efecto");
            Assert.AreEqual(2, chainNode.Children.Count, "one child per phase");
            Assert.AreEqual("Phase 1", chainNode.Children[0].Title);
            Assert.AreEqual("Phase 2", chainNode.Children[1].Title);

            var innerEffect = chainNode.Children[0].Children[0].Children[0];
            Assert.AreEqual("EffDealDamage", innerEffect.Subtitle);
            Assert.AreEqual("OnActivate.Effects.$0.Phases.$0.Effects.Effects.$0", innerEffect.Path);
        }

        [Test]
        public void Build_ColumnsIncreaseWithDepth_SoTheFlowReadsLeftToRight()
        {
            var item = NewActiveItem();
            item.OnActivate.Effects.Add(new EffHeal());

            var model = BlockGraphModel.Build(item);

            Assert.AreEqual(0, model.Root.Column);
            Assert.AreEqual(1, model.Root.Children[0].Column);
            Assert.AreEqual(2, model.Root.Children[0].Children[0].Column);
        }

        [Test]
        public void Build_Preconditions_AreTheirOwnKind()
        {
            var item = NewActiveItem();
            item.OnActivate.PreConditions.Add(new PCComposite());

            var model = BlockGraphModel.Build(item);
            var group = model.Root.Children[0];

            Assert.AreEqual(BlockNodeKind.Condition, group.Children[0].Kind);
        }

        // ---- layout ----------------------------------------------------------

        [Test]
        public void Compute_IsDeterministic_ForTheSameModel()
        {
            var item = NewActiveItem();
            item.OnActivate.Effects.Add(new EffHeal());
            item.OnActivate.Effects.Add(new EffAddShield());

            var first = BlockGraphLayout.Compute(BlockGraphModel.Build(item));
            var second = BlockGraphLayout.Compute(BlockGraphModel.Build(item));

            Assert.AreEqual(first.Count, second.Count);
            foreach (var kv in first)
            {
                var match = FindPositionByPath(second, kv.Key.Path);
                Assert.AreEqual(kv.Value, match, $"'{kv.Key.Path}' must land in the same place every time");
            }
        }

        static Vector2 FindPositionByPath(Dictionary<BlockGraphNode, Vector2> positions, string path)
        {
            foreach (var kv in positions) if (kv.Key.Path == path) return kv.Value;
            return new Vector2(float.NaN, float.NaN);
        }

        [Test]
        public void Compute_PlacesEveryNode()
        {
            var item = NewActiveItem();
            item.OnActivate.Effects.Add(new EffHeal());

            var model = BlockGraphModel.Build(item);
            var positions = BlockGraphLayout.Compute(model);

            Assert.AreEqual(model.AllNodes.Count, positions.Count);
        }

        [Test]
        public void Compute_NeverOverlapsWithinAColumn()
        {
            var item = NewActiveItem();
            for (int i = 0; i < 6; i++) item.OnActivate.Effects.Add(new EffHeal());
            var chain = new EffChain();
            chain.Phases.Add(new ChainPhase());
            chain.Phases[0].Effects.Effects.Add(new EffAddShield());
            item.OnActivate.Effects.Add(chain);

            var positions = BlockGraphLayout.Compute(BlockGraphModel.Build(item));

            var byColumn = new Dictionary<int, List<float>>();
            foreach (var kv in positions)
            {
                if (!byColumn.TryGetValue(kv.Key.Column, out var ys))
                    byColumn[kv.Key.Column] = ys = new List<float>();
                ys.Add(kv.Value.y);
            }

            foreach (var kv in byColumn)
            {
                kv.Value.Sort();
                for (int i = 1; i < kv.Value.Count; i++)
                    Assert.GreaterOrEqual(
                        kv.Value[i] - kv.Value[i - 1], BlockGraphLayout.NODE_HEIGHT,
                        $"two boxes overlap in column {kv.Key}");
            }
        }

        /// <summary>
        /// The layout's row pitch has to be at least the node's real rendered height, or boxes
        /// overlap on screen while the maths says they don't. BlockNodeView pins its height to this
        /// constant precisely so the two can't drift; this guards the constant staying sane.
        /// </summary>
        [Test]
        public void NodeHeight_LeavesRoomForTitleSubtitleAndPorts()
        {
            Assert.GreaterOrEqual(BlockGraphLayout.NODE_HEIGHT, 90f,
                "a GraphView node renders a title bar, a subtitle, a kind tag and port rows");
            Assert.Greater(BlockGraphLayout.V_SPACING, 0f);
        }

        // ---- structural edits from the canvas -------------------------------

        [Test]
        public void Build_RecordsWhereEachNodeCameFrom_SoItCanBeRemoved()
        {
            var item = NewActiveItem();
            item.OnActivate.Effects.Add(new EffHeal());

            var model = BlockGraphModel.Build(item);
            var effect = model.Root.Children[0].Children[0];

            Assert.IsTrue(effect.CanRemove);
            Assert.AreEqual(0, effect.SourceIndex, "it's element 0 of the Effects list");
            Assert.AreEqual("Effects", effect.SourceMember.Value.Name);
            Assert.AreSame(item.OnActivate, effect.Owner, "removal mutates the group's list");
        }

        [Test]
        public void Build_Root_CannotBeRemoved()
        {
            var model = BlockGraphModel.Build(NewActiveItem());

            Assert.IsFalse(model.Root.CanRemove);
            Assert.IsNull(model.Root.Parent);
        }

        /// <summary>
        /// Removal goes by reference, not by index — a multi-select deletes several blocks in one
        /// pass and each removal shifts the indices after it. This pins that a stored index is never
        /// what identifies the element.
        /// </summary>
        [Test]
        public void Build_MiddleElement_RemovesByReferenceNotIndex()
        {
            var item = NewActiveItem();
            var first = new EffAddShield();
            var middle = new EffHeal();
            var last = new EffDealDamage();
            item.OnActivate.Effects.Add(first);
            item.OnActivate.Effects.Add(middle);
            item.OnActivate.Effects.Add(last);

            var model = BlockGraphModel.Build(item);
            var middleNode = model.Root.Children[0].Children[1];
            Assert.AreSame(middle, middleNode.Value);

            // Simulate deleting the first one, which is what shifts every later index.
            item.OnActivate.Effects.Remove(first);

            // A stale SourceIndex of 1 now points at `last`; by reference it still finds `middle`.
            item.OnActivate.Effects.Remove((IEffect)middleNode.Value);

            CollectionAssert.AreEqual(new IEffect[] { last }, item.OnActivate.Effects);
        }

        [Test]
        public void Build_SourceIndex_IsMinusOneForSingleSlots()
        {
            var item = NewActiveItem();
            item.OnActivate.Effects.Add(new EffHeal());

            var model = BlockGraphModel.Build(item);
            var group = model.Root.Children[0];

            Assert.AreEqual(-1, group.SourceIndex, "OnActivate is a single slot, not a list element");
            Assert.AreSame(item, group.Owner);
        }

        [Test]
        public void Compute_ColumnsAreEvenlySpacedByDepth()
        {
            var item = NewActiveItem();
            item.OnActivate.Effects.Add(new EffHeal());

            var positions = BlockGraphLayout.Compute(BlockGraphModel.Build(item));

            foreach (var kv in positions)
                Assert.AreEqual(
                    kv.Key.Column * (BlockGraphLayout.NODE_WIDTH + BlockGraphLayout.H_SPACING),
                    kv.Value.x);
        }
    }
}
