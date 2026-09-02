using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.PreConditions;
using Rollgeon.Upgrades.Dice.Triggers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Editor.Tools.Enchantment.Tests
{
    /// <summary>
    /// Cero disco: todo con <c>CreateInstance</c> y las overloads puras, igual que
    /// <c>ItemQueryTests</c>. El catálogo y el pool también son instancias en memoria.
    /// </summary>
    public class EnchantmentQueryTests
    {
        readonly List<Object> _created = new();

        T Create<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _created.Add(so);
            return so;
        }

        EnchantmentSO MakeEnchantment(string id, string displayName, EnchantmentCategory category)
        {
            var ench = Create<EnchantmentSO>();
            ench.EditorSetUpgradeId(id);
            ench.EditorSetDisplayName(displayName);
            ench.EditorSetCategory(category);
            return ench;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        // ---- agrupación por categoría ----------------------------------------------

        [Test]
        public void GetByCategory_GroupsAndSortsByDisplayName()
        {
            // Arrange
            var b = MakeEnchantment("ench.b", "Bravo", EnchantmentCategory.Caos);
            var a = MakeEnchantment("ench.a", "Alfa", EnchantmentCategory.Caos);
            var c = MakeEnchantment("ench.c", "Charlie", EnchantmentCategory.Recursos);

            // Act
            var groups = EnchantmentQuery.GetByCategory(new[] { b, c, a });

            // Assert
            Assert.AreEqual(2, groups.Count);
            var caos = groups.First(g => g.Category == EnchantmentCategory.Caos);
            CollectionAssert.AreEqual(new[] { a, b }, caos.Enchantments);
            var recursos = groups.First(g => g.Category == EnchantmentCategory.Recursos);
            CollectionAssert.AreEqual(new[] { c }, recursos.Enchantments);
        }

        // ---- efectos ----------------------------------------------------------------

        [Test]
        public void GetEffectTypes_WalksTriggersAndNestedChains()
        {
            // Arrange — un trigger con EffHeal directo y un EffChain que anida EffAddComboBonus.
            var ench = MakeEnchantment("ench.x", "X", EnchantmentCategory.Control);
            var chain = new EffChain
            {
                Phases = new List<ChainPhase>
                {
                    new()
                    {
                        Effects = new EffectData
                        {
                            Effects = new List<IEffect> { new EffAddComboBonus() },
                        },
                    },
                },
            };
            ench.EditorAddTrigger(new ExecuteEffectsOnDiceEvent
            {
                Event = EnchantmentHookEvent.ComboPlayed,
                Effects = new List<EffectData>
                {
                    new() { Effects = new List<IEffect> { new EffHeal(), chain } },
                },
            });

            // Act
            var types = EnchantmentQuery.GetEffectTypes(ench);

            // Assert
            CollectionAssert.Contains(types, typeof(EffHeal));
            CollectionAssert.Contains(types, typeof(EffChain));
            CollectionAssert.Contains(types, typeof(EffAddComboBonus));
        }

        // ---- salud del catálogo -----------------------------------------------------

        [Test]
        public void CheckCatalogHealth_DuplicateIds_AreFlaggedAsErrors()
        {
            // Arrange
            var a = MakeEnchantment("ench.dup", "A", EnchantmentCategory.Caos);
            var b = MakeEnchantment("ench.dup", "B", EnchantmentCategory.Caos);
            var catalog = Create<EnchantmentCatalogSO>();
            catalog.EditorAdd(a);
            catalog.EditorAdd(b);
            var pool = MakePoolWith((a, 1f), (b, 1f));

            // Act
            var findings = EnchantmentQuery.CheckCatalogHealth(new[] { a, b }, catalog, pool);

            // Assert — un error por cada asset del par duplicado.
            var dupErrors = findings.Where(f =>
                f.Severity == EnchantmentQuery.FindingSeverity.Error && f.Message.Contains("duplicado")).ToList();
            Assert.AreEqual(2, dupErrors.Count);
        }

        [Test]
        public void CheckCatalogHealth_OutsideCatalogAndPool_AreFlagged()
        {
            // Arrange — el caso Codicioso: asset sano pero inalcanzable en el juego.
            var orphan = MakeEnchantment("ench.huerfano", "Huérfano", EnchantmentCategory.Recursos);
            var catalog = Create<EnchantmentCatalogSO>();
            var pool = MakePoolWith();

            // Act
            var findings = EnchantmentQuery.CheckCatalogHealth(new[] { orphan }, catalog, pool);

            // Assert
            Assert.IsTrue(findings.Any(f =>
                f.Severity == EnchantmentQuery.FindingSeverity.Error && f.Message.Contains("EnchantmentCatalog")));
            Assert.IsTrue(findings.Any(f =>
                f.Severity == EnchantmentQuery.FindingSeverity.Warning && f.Message.Contains("pool del altar")));
        }

        [Test]
        public void CheckCatalogHealth_WeightZero_IsInfoNotWarning()
        {
            // Arrange
            var disabled = MakeEnchantment("ench.apagado", "Apagado", EnchantmentCategory.Control);
            var catalog = Create<EnchantmentCatalogSO>();
            catalog.EditorAdd(disabled);
            var pool = MakePoolWith((disabled, 0f));

            // Act
            var findings = EnchantmentQuery.CheckCatalogHealth(new[] { disabled }, catalog, pool);

            // Assert
            Assert.IsTrue(findings.Any(f =>
                f.Severity == EnchantmentQuery.FindingSeverity.Info && f.Message.Contains("peso 0")));
            Assert.IsFalse(findings.Any(f => f.Message.Contains("no se ofrece nunca")));
        }

        [Test]
        public void CheckCatalogHealth_CategoryNone_IsAnError()
        {
            // Arrange
            var ench = MakeEnchantment("ench.sin_cat", "Sin Cat", EnchantmentCategory.None);
            var catalog = Create<EnchantmentCatalogSO>();
            catalog.EditorAdd(ench);
            var pool = MakePoolWith((ench, 1f));

            // Act
            var findings = EnchantmentQuery.CheckCatalogHealth(new[] { ench }, catalog, pool);

            // Assert
            Assert.IsTrue(findings.Any(f =>
                f.Severity == EnchantmentQuery.FindingSeverity.Error && f.Message.Contains("categoría")));
        }

        [Test]
        public void CheckCatalogHealth_DoesNothingEnchantment_IsFlagged_ButFaceFilterOnlyIsNot()
        {
            // Arrange
            var empty = MakeEnchantment("ench.vacio", "Vacío", EnchantmentCategory.Control);
            var filterOnly = MakeEnchantment("ench.filtro", "Filtro", EnchantmentCategory.Control);
            filterOnly.EditorSetFaceFilter(new Rollgeon.Upgrades.Dice.Filters.ParityFilter());
            var catalog = Create<EnchantmentCatalogSO>();
            catalog.EditorAdd(empty);
            catalog.EditorAdd(filterOnly);
            var pool = MakePoolWith((empty, 1f), (filterOnly, 1f));

            // Act
            var findings = EnchantmentQuery.CheckCatalogHealth(new[] { empty, filterOnly }, catalog, pool);

            // Assert
            Assert.IsTrue(findings.Any(f => f.Message.Contains("Vacío") && f.Message.Contains("no hace nada")));
            Assert.IsFalse(findings.Any(f => f.Message.Contains("Filtro") && f.Message.Contains("no hace nada")));
        }

        [Test]
        public void CheckCatalogHealth_DirectApplyEffectInComboMatched_IsAnError()
        {
            // Arrange — BUG-017: preview re-dispara por toggle de hold, un apply directo es farmeable.
            var farmeable = MakeEnchantment("ench.farm", "Farm", EnchantmentCategory.Recursos);
            farmeable.EditorAddTrigger(new ExecuteEffectsOnDiceEvent
            {
                Event = EnchantmentHookEvent.ComboMatched,
                Effects = new List<EffectData> { new() { Effects = new List<IEffect> { new EffHeal() } } },
            });

            var sano = MakeEnchantment("ench.sano", "Sano", EnchantmentCategory.Ataque);
            sano.EditorAddTrigger(new ExecuteEffectsOnDiceEvent
            {
                Event = EnchantmentHookEvent.ComboMatched,
                Effects = new List<EffectData>
                {
                    new() { Effects = new List<IEffect> { new EffAddComboBonus() } },
                },
            });

            var catalog = Create<EnchantmentCatalogSO>();
            catalog.EditorAdd(farmeable);
            catalog.EditorAdd(sano);
            var pool = MakePoolWith((farmeable, 1f), (sano, 1f));

            // Act
            var findings = EnchantmentQuery.CheckCatalogHealth(new[] { farmeable, sano }, catalog, pool);

            // Assert — EffHeal en preview es error; EffAddComboBonus (scratch-writer) no.
            Assert.IsTrue(findings.Any(f =>
                f.Severity == EnchantmentQuery.FindingSeverity.Error && f.Message.Contains("EffHeal")));
            Assert.IsFalse(findings.Any(f => f.Message.Contains("EffAddComboBonus")));
        }

        [Test]
        public void CheckCatalogHealth_CarrierFaceWithoutCarrierGate_IsAnError()
        {
            // Arrange
            var sinGate = MakeEnchantment("ench.sin_gate", "Sin Gate", EnchantmentCategory.Ataque);
            sinGate.EditorAddTrigger(new ExecuteEffectsOnDiceEvent
            {
                Event = EnchantmentHookEvent.ComboPlayed,
                RequireCarrierParticipates = false,
                Effects = new List<EffectData>
                {
                    new()
                    {
                        PreConditions = new List<Rollgeon.PreConditions.BasePreCondition> { new PcCarrierFace() },
                        Effects = new List<IEffect> { new EffAddComboBonus() },
                    },
                },
            });

            var catalog = Create<EnchantmentCatalogSO>();
            catalog.EditorAdd(sinGate);
            var pool = MakePoolWith((sinGate, 1f));

            // Act
            var findings = EnchantmentQuery.CheckCatalogHealth(new[] { sinGate }, catalog, pool);

            // Assert
            Assert.IsTrue(findings.Any(f =>
                f.Severity == EnchantmentQuery.FindingSeverity.Error
                && f.Message.Contains("RequireCarrierParticipates")));
        }

        [Test]
        public void CheckCatalogHealth_UnknownComboId_IsAnError()
        {
            // Arrange
            var ench = MakeEnchantment("ench.combo_falso", "Combo Falso", EnchantmentCategory.Control);
            var trigger = new ExecuteEffectsOnDiceEvent
            {
                Event = EnchantmentHookEvent.ComboPlayed,
                Effects = new List<EffectData>
                {
                    new() { Effects = new List<IEffect> { new EffAddComboBonus() } },
                },
            };
            trigger.Filter.Mode = ComboFilterMode.ComboIds;
            trigger.Filter.ComboIds = new List<string> { "combo.que_no_existe" };
            ench.EditorAddTrigger(trigger);

            var catalog = Create<EnchantmentCatalogSO>();
            catalog.EditorAdd(ench);
            var pool = MakePoolWith((ench, 1f));

            // Act
            var findings = EnchantmentQuery.CheckCatalogHealth(new[] { ench }, catalog, pool);

            // Assert
            Assert.IsTrue(findings.Any(f =>
                f.Severity == EnchantmentQuery.FindingSeverity.Error && f.Message.Contains("combo.que_no_existe")));
        }

        // ---- localización -----------------------------------------------------------

        [Test]
        public void CheckLocalizationHealth_MissingLocale_IsFlagged()
        {
            // Arrange — lookup inyectado: es tiene textos, en no existe.
            var ench = MakeEnchantment("ench.loc", "Loc", EnchantmentCategory.Control);
            EnchantmentLocalizationBridge.Entry Lookup(string id, string locale) =>
                locale == "es" ? new EnchantmentLocalizationBridge.Entry("Loc", "desc") : default;

            // Act
            var findings = EnchantmentQuery.CheckLocalizationHealth(
                new[] { ench }, new[] { "es", "en" }, Lookup);

            // Assert
            Assert.AreEqual(1, findings.Count);
            StringAssert.Contains("'en'", findings[0].Message);
        }

        [Test]
        public void CheckLocalizationHealth_SameTextInBothLocales_IsFlagged()
        {
            // Arrange — lo que test_localization_no_key_repeats_the_spanish_text_in_english rechaza.
            var ench = MakeEnchantment("ench.igual", "Igual", EnchantmentCategory.Control);
            EnchantmentLocalizationBridge.Entry Lookup(string id, string locale) =>
                new("Mismo Nombre", "misma desc");

            // Act
            var findings = EnchantmentQuery.CheckLocalizationHealth(
                new[] { ench }, new[] { "es", "en" }, Lookup);

            // Assert
            Assert.AreEqual(2, findings.Count, "un aviso por el nombre y otro por la descripción");
            Assert.IsTrue(findings.All(f => f.Message.Contains("falta traducir")));
        }

        [Test]
        public void CheckLocalizationHealth_TranslatedTexts_ProduceNoFindings()
        {
            // Arrange
            var ench = MakeEnchantment("ench.ok", "Ok", EnchantmentCategory.Control);
            EnchantmentLocalizationBridge.Entry Lookup(string id, string locale) =>
                locale == "es" ? new EnchantmentLocalizationBridge.Entry("Nombre", "desc es")
                               : new EnchantmentLocalizationBridge.Entry("Name", "desc en");

            // Act
            var findings = EnchantmentQuery.CheckLocalizationHealth(
                new[] { ench }, new[] { "es", "en" }, Lookup);

            // Assert
            Assert.IsEmpty(findings);
        }

        // ---- métricas ---------------------------------------------------------------

        [Test]
        public void GetMetrics_ReadsWeightAndTriggerEventsFromThePool()
        {
            // Arrange
            var ench = MakeEnchantment("ench.metrica", "Métrica", EnchantmentCategory.Ataque);
            ench.EditorAddTrigger(new ExecuteEffectsOnDiceEvent { Event = EnchantmentHookEvent.ComboPlayed });
            var pool = MakePoolWith((ench, 2.5f));

            // Act
            var metrics = EnchantmentQuery.GetMetrics(ench, pool);

            // Assert
            Assert.IsTrue(metrics.InPool);
            Assert.AreEqual(2.5f, metrics.Weight);
            CollectionAssert.AreEqual(new[] { EnchantmentHookEvent.ComboPlayed }, metrics.TriggerEvents);
            CollectionAssert.AreEqual(new[] { EnchantmentQuery.AnyComboSentinel }, metrics.ComboIds);
        }

        // ---- helpers ----------------------------------------------------------------

        EnchantmentPoolSO MakePoolWith(params (EnchantmentSO ench, float weight)[] entries)
        {
            var pool = Create<EnchantmentPoolSO>();
            foreach (var (ench, weight) in entries)
                pool.Entries.Add(new WeightedEnchantment { Enchantment = ench, Weight = weight });
            return pool;
        }
    }
}
