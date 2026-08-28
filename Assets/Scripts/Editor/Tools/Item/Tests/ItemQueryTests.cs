using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Editor.Tools.Item;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Items;
using Rollgeon.Shop;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item.Tests
{
    /// <summary>
    /// EditMode coverage for <see cref="ItemQuery"/>. Uses the pure <c>IEnumerable&lt;ItemSO&gt;</c>
    /// overloads (item-editor-spec.md, Fase 2/A4) so nothing here touches the project's real
    /// <c>ItemSO</c> assets — everything is built in memory with <c>ScriptableObject.CreateInstance</c>
    /// and destroyed in <see cref="TearDown"/>.
    /// </summary>
    public class ItemQueryTests
    {
        readonly List<Object> _created = new();

        T Create<T>() where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            _created.Add(obj);
            return obj;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        // ---- Family grouping ----------------------------------------------------

        [Test]
        public void GetFamilies_OrdersVariantsByVariantIndex_RegardlessOfInputOrder()
        {
            var tier2 = Create<ItemSO>(); tier2.FamilyId = "boots"; tier2.VariantIndex = 2; tier2.ItemId = "boots.tier2";
            var tier0 = Create<ItemSO>(); tier0.FamilyId = "boots"; tier0.VariantIndex = 0; tier0.ItemId = "boots.tier0";
            var tier1 = Create<ItemSO>(); tier1.FamilyId = "boots"; tier1.VariantIndex = 1; tier1.ItemId = "boots.tier1";

            // Deliberately out of variant order to prove the grouping sorts, not just preserves input order.
            var families = ItemQuery.GetFamilies(new[] { tier2, tier0, tier1 });

            Assert.AreEqual(1, families.Count);
            var boots = families.Single();
            Assert.AreEqual("boots", boots.FamilyId);
            CollectionAssert.AreEqual(new[] { tier0, tier1, tier2 }, boots.Variants);
        }

        [Test]
        public void GetFamilies_ExcludesLooseItems_GetLooseItemsReturnsThem()
        {
            var grouped = Create<ItemSO>(); grouped.FamilyId = "boots"; grouped.VariantIndex = 0; grouped.ItemId = "boots.tier0";
            var loose = Create<ItemSO>(); loose.FamilyId = string.Empty; loose.ItemId = "standalone";

            var items = new[] { grouped, loose };

            var families = ItemQuery.GetFamilies(items);
            Assert.AreEqual(1, families.Count);
            Assert.IsFalse(families.Single().Variants.Contains(loose));

            var looseItems = ItemQuery.GetLooseItems(items);
            CollectionAssert.AreEqual(new[] { loose }, looseItems);
        }

        // ---- Effect type collection ----------------------------------------------

        [Test]
        public void GetEffectTypes_CollectsTypesFromNestedEffChainPhases()
        {
            var item = Create<ItemSO>();
            item.ItemId = "test.nested";
            item.Type = ItemType.Passive;

            var nestedPhaseEffects = new EffectData { Effects = new List<IEffect> { new EffHeal() } };
            var chain = new EffChain { Phases = new List<ChainPhase> { new ChainPhase { Effects = nestedPhaseEffects } } };

            item.PassiveHooks = new List<PassiveItemHook>
            {
                new PassiveItemHook
                {
                    Kind = PassiveHookKind.EventBus,
                    TriggerEvent = EventName.OnTurnStarted,
                    Effect = new EffectData { Effects = new List<IEffect> { new EffModifyGold(), chain } },
                },
            };

            var types = ItemQuery.GetEffectTypes(item);

            Assert.Contains(typeof(EffModifyGold), types.ToList());
            Assert.Contains(typeof(EffChain), types.ToList());
            Assert.Contains(typeof(EffHeal), types.ToList(), "Effect nested inside an EffChain phase should be walked, not just top-level effects.");
            Assert.IsTrue(ItemQuery.ImplementsEffect<EffModifyGold>(item));
            Assert.IsFalse(ItemQuery.ImplementsEffect<EffAddShield>(item));
        }

        [Test]
        public void GetEffectTypes_ReturnsEmpty_ForItemWithNoHooks()
        {
            var item = Create<ItemSO>();
            item.ItemId = "test.empty";
            item.Type = ItemType.Passive;

            Assert.IsEmpty(ItemQuery.GetEffectTypes(item));
        }

        // ---- Catalog health --------------------------------------------------------

        [Test]
        public void CheckCatalogHealth_FlagsDuplicateIds()
        {
            var a = Create<ItemSO>(); a.ItemId = "dup.id"; a.DisplayName = "A";
            var b = Create<ItemSO>(); b.ItemId = "dup.id"; b.DisplayName = "B";
            var unique = Create<ItemSO>(); unique.ItemId = "unique.id"; unique.DisplayName = "C";

            var pool = Create<ShopPoolSO>(); // empty pool — keeps the "not in pool" findings out of scope for this assertion set

            var findings = ItemQuery.CheckCatalogHealth(new[] { a, b, unique }, pool);

            var dupFindings = findings.Where(f => f.Severity == ItemQuery.FindingSeverity.Error
                                                   && f.Message.Contains("duplicado")).ToList();
            Assert.AreEqual(2, dupFindings.Count, "Both assets sharing the duplicated id should be flagged.");
            CollectionAssert.AreEquivalent(new Object[] { a, b }, dupFindings.Select(f => f.Asset).ToList());
            Assert.IsFalse(findings.Any(f => f.Asset == unique && f.Message.Contains("duplicado")));
        }

        [Test]
        public void CheckCatalogHealth_FlagsEmptyItemId()
        {
            var noId = Create<ItemSO>(); // ItemId left default (empty)
            var pool = Create<ShopPoolSO>();

            var findings = ItemQuery.CheckCatalogHealth(new[] { noId }, pool);

            Assert.IsTrue(findings.Any(f => f.Asset == noId
                                             && f.Severity == ItemQuery.FindingSeverity.Error
                                             && f.Message.Contains("ItemId")));
        }

        [Test]
        public void CheckCatalogHealth_FlagsPassiveHookWithNoEffectsAndNoPersistentModifiers()
        {
            var item = Create<ItemSO>();
            item.ItemId = "test.emptyhook";
            item.Type = ItemType.Passive;
            item.PassiveHooks = new List<PassiveItemHook>
            {
                new PassiveItemHook
                {
                    Kind = PassiveHookKind.EventBus,
                    TriggerEvent = EventName.OnTurnStarted,
                    Effect = new EffectData(), // no effects
                    PersistentModifiers = new List<PersistentModifierDef>(), // no modifiers either
                },
            };
            var pool = Create<ShopPoolSO>();

            var findings = ItemQuery.CheckCatalogHealth(new[] { item }, pool);

            Assert.IsTrue(findings.Any(f => f.Asset == item
                                             && f.Severity == ItemQuery.FindingSeverity.Warning
                                             && f.Message.Contains("sin efectos ni modificadores")));
        }

        /// <summary>
        /// Un hook que solo lleva modificadores persistentes no usa su evento: los aplica
        /// <c>ApplyPersistentModifiers</c> al entrar el ítem al inventario, sin mirarlo. Avisar ahí
        /// mandaría a "arreglar" ítems que andan — pasó con Botas Ligeras y Coraza Reforzada.
        /// </summary>
        [Test]
        public void CheckCatalogHealth_HookWithOnlyPersistentModifiers_IsNotFlaggedForItsEvent()
        {
            var item = Create<ItemSO>();
            item.ItemId = "test.modsonly";
            item.Type = ItemType.Passive;
            item.PassiveHooks = new List<PassiveItemHook>
            {
                new PassiveItemHook
                {
                    Kind = PassiveHookKind.EventBus,
                    TriggerEvent = EventName.OnRunStart,
                    PersistentModifiers = new List<PersistentModifierDef> { new PersistentModifierDef() },
                },
            };
            var pool = Create<ShopPoolSO>();

            var findings = ItemQuery.CheckCatalogHealth(new[] { item }, pool);

            Assert.IsFalse(findings.Any(f => f.Message.Contains("nunca se va a ejecutar")),
                "el evento es decorativo en un hook que solo lleva modificadores");
        }

        static EffectData EffectWith(BaseEffect effect)
        {
            var data = new EffectData();
            data.Effects.Add(effect);
            return data;
        }

        /// <summary>
        /// Un evento fuera de <see cref="ItemTriggerCatalog"/> no rompe nada visible: el ítem
        /// simplemente no dispara nunca, y eso antes se descubría jugando.
        /// </summary>
        [Test]
        public void CheckCatalogHealth_FlagsHookOnAnEventNoItemCanHear()
        {
            var item = Create<ItemSO>();
            item.ItemId = "test.deadhook";
            item.Type = ItemType.Passive;
            item.PassiveHooks = new List<PassiveItemHook>
            {
                new PassiveItemHook
                {
                    Kind = PassiveHookKind.EventBus,
                    TriggerEvent = EventName.OnSceneLoaded,
                    Effect = EffectWith(new EffAddShield()),
                },
            };
            var pool = Create<ShopPoolSO>();

            var findings = ItemQuery.CheckCatalogHealth(new[] { item }, pool);

            Assert.IsTrue(findings.Any(f => f.Asset == item
                                             && f.Severity == ItemQuery.FindingSeverity.Error
                                             && f.Message.Contains("nunca se va a ejecutar")));
        }

        [Test]
        public void CheckCatalogHealth_HookOnACatalogEvent_IsNotFlagged()
        {
            var item = Create<ItemSO>();
            item.ItemId = "test.livehook";
            item.Type = ItemType.Passive;
            item.PassiveHooks = new List<PassiveItemHook>
            {
                new PassiveItemHook
                {
                    Kind = PassiveHookKind.EventBus,
                    TriggerEvent = EventName.OnDamageIncoming,
                    Subject = PassiveHookSubject.Target,
                    Effect = EffectWith(new EffAddShield()),
                },
            };
            var pool = Create<ShopPoolSO>();

            var findings = ItemQuery.CheckCatalogHealth(new[] { item }, pool);

            Assert.IsFalse(findings.Any(f => f.Message.Contains("nunca se va a ejecutar")),
                "'cuando te pegan' está en el catálogo — no puede reportarse como muerto");
        }
    }
}
