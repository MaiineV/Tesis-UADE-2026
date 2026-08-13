using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Items;
using Rollgeon.PreConditions;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Polymorphic.Tests
{
    /// <summary>
    /// Pins the Odin path contract. Every drawer addresses data by path string, and a wrong path
    /// fails <i>silently</i> — it renders "(field not found)" and the author just sees a missing
    /// field. These tests are the only thing standing between a typo and a tool that quietly
    /// edits nothing.
    /// </summary>
    public sealed class PolymorphicAuthoringContextTests
    {
        readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            // Editor-tool tests share the global undo stack; leaving entries on it leaks into the
            // next test's Undo.PerformUndo.
            Undo.ClearAll();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        ItemSO NewItem()
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "test.item";
            _spawned.Add(item);
            return item;
        }

        // ---- path resolution -----------------------------------------------

        [Test]
        public void At_ShallowPaths_ResolveOnItemSO()
        {
            var item = NewItem();
            item.Type = ItemType.Active;
            item.OnActivate = new EffectData();
            item.OnActivate.PreConditions.Add(new PCComposite());
            item.OnActivate.Effects.Add(new EffHeal());

            using (var ctx = new PolymorphicAuthoringContext(item))
            {
                Assert.IsNotNull(ctx.At("OnActivate"), "OnActivate");
                Assert.IsNotNull(ctx.At("OnActivate.Label"), "OnActivate.Label");
                Assert.IsNotNull(ctx.At("OnActivate.PreConditions.$0"), "PreConditions.$0");
                Assert.IsNotNull(ctx.At("OnActivate.Effects.$0"), "Effects.$0");
            }
        }

        [Test]
        public void At_PassiveHookPaths_ResolveOnItemSO()
        {
            var item = NewItem();
            item.Type = ItemType.Passive;
            var hook = new PassiveItemHook();
            hook.Effect.Effects.Add(new EffHeal());
            item.PassiveHooks.Add(hook);

            using (var ctx = new PolymorphicAuthoringContext(item))
            {
                Assert.IsNotNull(ctx.At("PassiveHooks.$0"), "PassiveHooks.$0");
                Assert.IsNotNull(ctx.At("PassiveHooks.$0.Effect"), "hook.Effect");
                Assert.IsNotNull(ctx.At("PassiveHooks.$0.Effect.Effects.$0"), "hook.Effect.Effects.$0");
            }
        }

        /// <summary>
        /// The deep shape: EffChain nests EffectData through Phases. Nothing could be authored in
        /// here before, so these paths had never been exercised.
        /// </summary>
        [Test]
        public void At_ChainPaths_ResolveThroughNestedEffectData()
        {
            var item = NewItem();
            item.Type = ItemType.Active;
            item.OnActivate = new EffectData();

            var chain = new EffChain();
            chain.Phases.Add(new ChainPhase());
            chain.Phases[0].Effects.Effects.Add(new EffDealDamage());
            chain.Phases[0].Effects.PreConditions.Add(new PCComposite());
            item.OnActivate.Effects.Add(chain);

            using (var ctx = new PolymorphicAuthoringContext(item))
            {
                Assert.IsNotNull(ctx.At("OnActivate.Effects.$0"), "the chain itself");
                Assert.IsNotNull(ctx.At("OnActivate.Effects.$0.Phases.$0"), "phase");
                Assert.IsNotNull(ctx.At("OnActivate.Effects.$0.Phases.$0.Effects"), "phase's EffectData");
                Assert.IsNotNull(ctx.At("OnActivate.Effects.$0.Phases.$0.Effects.PreConditions.$0"), "nested precondition");
                Assert.IsNotNull(ctx.At("OnActivate.Effects.$0.Phases.$0.Effects.Effects.$0"), "nested effect");
            }
        }

        [Test]
        public void At_UnknownPath_ReturnsNull_RatherThanThrowing()
        {
            var item = NewItem();
            using (var ctx = new PolymorphicAuthoringContext(item))
                Assert.IsNull(ctx.At("NoSuchField.$3.Nope"));
        }

        // ---- identity ------------------------------------------------------

        [Test]
        public void FindPathTo_RoundTrips_ToTheSameInstance()
        {
            var item = NewItem();
            item.Type = ItemType.Active;
            item.OnActivate = new EffectData();
            var heal = new EffHeal();
            item.OnActivate.Effects.Add(new EffAddShield());
            item.OnActivate.Effects.Add(heal);

            using (var ctx = new PolymorphicAuthoringContext(item))
            {
                string path = ctx.FindPathTo(heal);

                Assert.IsNotNull(path, "the instance is reachable, so it must have a path");
                Assert.AreSame(heal, ctx.At(path).ValueEntry.WeakSmartValue);
                Assert.IsTrue(ctx.PathPointsTo(path, heal));
            }
        }

        [Test]
        public void PathPointsTo_AfterTheListShifts_IsFalse()
        {
            var item = NewItem();
            item.Type = ItemType.Active;
            item.OnActivate = new EffectData();
            var first = new EffAddShield();
            var second = new EffHeal();
            item.OnActivate.Effects.Add(first);
            item.OnActivate.Effects.Add(second);

            using (var ctx = new PolymorphicAuthoringContext(item))
            {
                string secondPath = ctx.FindPathTo(second);
                item.OnActivate.Effects.Remove(first);
                ctx.Bind(item); // rebuild the tree the way a real edit would

                Assert.IsFalse(ctx.PathPointsTo(secondPath, second),
                    "$1 now holds a different element — a cached path must not be trusted");
                Assert.AreEqual("OnActivate.Effects.$0", ctx.FindPathTo(second));
            }
        }

        [Test]
        public void FindPathTo_UnreachableInstance_ReturnsNull()
        {
            var item = NewItem();
            using (var ctx = new PolymorphicAuthoringContext(item))
                Assert.IsNull(ctx.FindPathTo(new EffHeal()));
        }

        // ---- §13.6.1 round-trip --------------------------------------------

        /// <summary>
        /// The invariant every one of these tools depends on: concrete subtypes must survive
        /// serialization. If this breaks, an authored EffHeal comes back as a bare IEffect.
        /// </summary>
        [Test]
        public void Polymorphism_SurvivesOdinRoundTrip()
        {
            var source = new EffectData();
            source.Effects.Add(new EffHeal());
            source.PreConditions.Add(new PCComposite());

            // Fully qualified: UnityEditor.SerializationUtility exists too, and it is not this.
            var copy = Sirenix.Serialization.SerializationUtility.CreateCopy(source) as EffectData;

            Assert.IsNotNull(copy);
            Assert.IsInstanceOf<EffHeal>(copy.Effects[0], "effect kept its concrete type");
            Assert.IsInstanceOf<PCComposite>(copy.PreConditions[0], "precondition kept its concrete type");
        }

        // ---- colas de colección anidadas ------------------------------------

        /// <summary>
        /// La regresión del "+ que no hace nada": Odin ENCOLA el add de una lista en el
        /// resolver de esa propiedad, y <c>PropertyTree.ApplyChanges</c> solo vacía los
        /// resolvers del nivel raíz. <c>ctx.ApplyChanges</c> debe vaciar también los anidados
        /// — sin eso, el + de ComboIds / PersistentModifiers en las tools no aplica jamás.
        /// </summary>
        [Test]
        public void ApplyChanges_FlushesQueuedAdd_OnNestedStringList()
        {
            var item = NewItem();
            item.Type = ItemType.Passive;
            var hook = new PassiveItemHook { Kind = PassiveHookKind.ComboPlayed };
            item.PassiveHooks.Add(hook);

            using (var ctx = new PolymorphicAuthoringContext(item))
            {
                ctx.UpdateTree();
                var resolver = (ICollectionResolver)ctx.At("PassiveHooks.$0.ComboFilter.ComboIds").ChildResolver;
                resolver.QueueAdd(new object[] { "combo.par" });

                ctx.ApplyChanges();

                Assert.AreEqual(1, hook.ComboFilter.ComboIds.Count);
                Assert.AreEqual("combo.par", hook.ComboFilter.ComboIds[0]);
            }
        }

        [Test]
        public void ApplyChanges_FlushesQueuedAdd_OnPersistentModifiers()
        {
            var item = NewItem();
            item.Type = ItemType.Passive;
            var hook = new PassiveItemHook();
            item.PassiveHooks.Add(hook);

            using (var ctx = new PolymorphicAuthoringContext(item))
            {
                ctx.UpdateTree();
                var resolver = (ICollectionResolver)ctx.At("PassiveHooks.$0.PersistentModifiers").ChildResolver;
                resolver.QueueAdd(new object[] { new PersistentModifierDef() });

                ctx.ApplyChanges();

                Assert.AreEqual(1, hook.PersistentModifiers.Count);
            }
        }

        [Test]
        public void ApplyChanges_FlushedQueue_RaisesChanged_SoPanelsRepaint()
        {
            var item = NewItem();
            item.Type = ItemType.Passive;
            var hook = new PassiveItemHook();
            item.PassiveHooks.Add(hook);

            using (var ctx = new PolymorphicAuthoringContext(item))
            {
                int changed = 0;
                ctx.Changed += () => changed++;
                ctx.UpdateTree();
                var resolver = (ICollectionResolver)ctx.At("PassiveHooks.$0.ComboFilter.ComboIds").ChildResolver;
                resolver.QueueAdd(new object[] { "combo.par" });

                ctx.ApplyChanges();

                Assert.AreEqual(1, changed);
            }
        }

        [Test]
        public void ApplyChanges_WithoutQueuedChanges_DoesNotRaiseChanged()
        {
            var item = NewItem();
            item.Type = ItemType.Passive;
            item.PassiveHooks.Add(new PassiveItemHook());

            using (var ctx = new PolymorphicAuthoringContext(item))
            {
                int changed = 0;
                ctx.Changed += () => changed++;
                ctx.UpdateTree();
                ctx.At("PassiveHooks.$0.ComboFilter.ComboIds"); // resolver creado, cola vacía

                ctx.ApplyChanges();

                Assert.AreEqual(0, changed,
                    "el flush corre en cada pasada del panel — sin cola no debe notificar ni ensuciar");
            }
        }

        // ---- undo -----------------------------------------------------------

        [Test]
        public void Mutate_RecordsUndo_SoTheEditCanBeReverted()
        {
            var item = NewItem();
            item.Type = ItemType.Active;
            item.OnActivate = new EffectData { Label = "before" };

            using (var ctx = new PolymorphicAuthoringContext(item))
            {
                ctx.Mutate("Rename", () => item.OnActivate.Label = "after");
                Assert.AreEqual("after", item.OnActivate.Label);

                Undo.PerformUndo();

                Assert.AreEqual("before", item.OnActivate.Label,
                    "whole-object undo is the only kind available on an Odin blob");
            }
        }

        [Test]
        public void Mutate_RaisesChanged_SoPanelsRepaint()
        {
            var item = NewItem();
            int changed = 0;

            using (var ctx = new PolymorphicAuthoringContext(item))
            {
                ctx.Changed += () => changed++;
                ctx.Mutate("Touch", () => item.ItemId = "x");

                Assert.AreEqual(1, changed,
                    "GenericMenu callbacks fire outside the IMGUI cycle; without this the panel " +
                    "wouldn't redraw until the mouse moved");
            }
        }
    }
}
