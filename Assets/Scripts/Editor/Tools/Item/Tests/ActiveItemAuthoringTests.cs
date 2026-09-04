using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dice;
using Rollgeon.EditorTools.Localization;
using Rollgeon.Items;
using Rollgeon.Items.Active;
using Rollgeon.Shop;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item.Tests
{
    /// <summary>
    /// Cubre <see cref="ActiveItemAuthoring.CreateOrSkip"/> (Feature#0085 §A6): escritura
    /// completa (asset + catálogo + pool + loc), idempotencia y que una estructura inválida
    /// no bloquea la creación — solo lo avisa en el reporte (el GDD prohíbe "no pasa nada",
    /// no prohíbe crear el item mientras se termina de autorar).
    /// </summary>
    /// <remarks>
    /// A diferencia de <see cref="ItemAuthoringTests"/> (que evita tocar el catálogo/pool
    /// vivos del proyecto), acá el catálogo y el pool son instancias en memoria propias del
    /// test — <see cref="ActiveItemAuthoring.CreateOrSkip"/> los recibe por parámetro, así
    /// que no hace falta la gimnasia de asset real para poder inspeccionarlos después.
    /// Localización es la única escritura que cae sobre la tabla <c>Content</c> real del
    /// proyecto — se limpia explícitamente en <see cref="TearDown"/>.
    /// </remarks>
    public sealed class ActiveItemAuthoringTests
    {
        const string TestFolder = "Assets/Rollgeon/Items/__ActiveItemAuthoringTests";

        ItemCatalogSO _catalog;
        ShopPoolSO _pool;
        readonly List<string> _itemIdsToCleanUpLocalization = new List<string>();

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.CreateFolder("Assets/Rollgeon/Items", "__ActiveItemAuthoringTests");

            _catalog = ScriptableObject.CreateInstance<ItemCatalogSO>();
            _pool = ScriptableObject.CreateInstance<ShopPoolSO>();
            _itemIdsToCleanUpLocalization.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var itemId in _itemIdsToCleanUpLocalization)
                ContentLocalizationBridge.RemoveEntityKeys(itemId, "Test Cleanup");

            // RemoveEntityKeys solo marca dirty: sin este guardado las tablas Content_*
            // quedan en disco con las entradas de prueba (CreateOrSkip si guarda).
            if (_itemIdsToCleanUpLocalization.Count > 0)
                AssetDatabase.SaveAssets();

            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_pool);

            if (AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.DeleteAsset(TestFolder);
        }

        static ActiveItemCreationSpec MakeSpec(string itemId, string displayName)
        {
            return new ActiveItemCreationSpec
            {
                ItemId = itemId,
                DisplayName = displayName,
                DescriptionEs = "Descripción de prueba en español.",
                DescriptionEn = "Test description in English.",
                Rarity = ItemRarity.Rare,
                BasePrice = 42,
                Die = DiceType.D6,
                Resolution = ActiveItemResolution.Gradient,
                TargetFolder = TestFolder,
            };
        }

        [Test]
        public void CreateOrSkip_NewSpec_WritesAssetCatalogPoolAndLocalization()
        {
            // Arrange
            var spec = MakeSpec("test.active.probe.gradient", "Test Active Probe");
            _itemIdsToCleanUpLocalization.Add(spec.ItemId);

            // Act
            var item = ActiveItemAuthoring.CreateOrSkip(spec, _catalog, _pool, out var report);

            // Assert — campos del modelo nuevo
            Assert.IsNotNull(item);
            Assert.AreEqual(spec.ItemId, item.ItemId);
            Assert.AreEqual(ItemType.Active, item.Type);
            Assert.IsTrue(item.UsesActiveSlot);
            Assert.AreEqual(DiceType.D6, item.ActiveDie);
            Assert.AreEqual(ActiveItemResolution.Gradient, item.ActiveResolution);
            Assert.AreEqual(ItemRarity.Rare, item.Rarity);

            // Assert — asset en disco
            var path = AssetDatabase.GetAssetPath(item);
            Assert.IsTrue(path.StartsWith(TestFolder), $"Asset path '{path}' debe vivir en la carpeta pedida.");

            // Assert — catálogo
            Assert.AreEqual(item, _catalog.GetById(spec.ItemId));

            // Assert — shop pool
            Assert.IsTrue(ItemShopPriceBridge.IsInPool(_pool, item));
            Assert.IsTrue(ItemShopPriceBridge.TryGetPrice(_pool, item, out var price));
            Assert.AreEqual(42, price);

            // Assert — localización: nombre idéntico ES/EN, descripción traducida
            var esEntry = ContentLocalizationBridge.Read(spec.ItemId, "es");
            var enEntry = ContentLocalizationBridge.Read(spec.ItemId, "en");
            Assert.AreEqual("Test Active Probe", esEntry.Name);
            Assert.AreEqual("Test Active Probe", enEntry.Name);
            Assert.AreEqual("Descripción de prueba en español.", esEntry.Description);
            Assert.AreEqual("Test description in English.", enEntry.Description);

            Assert.IsTrue(report.Contains("creado"), $"El reporte debe confirmar la creación: '{report}'");
        }

        [Test]
        public void CreateOrSkip_IdAlreadyInCatalog_SkipsAndReturnsExistingItem()
        {
            // Arrange — un item ya vive en el catálogo con ese id (simula una segunda corrida
            // del seed sobre un catálogo que ya lo tiene).
            var existing = ScriptableObject.CreateInstance<ItemSO>();
            existing.ItemId = "test.active.probe.existing";
            _catalog.EditorAdd(existing);

            var spec = MakeSpec("test.active.probe.existing", "Should Not Overwrite");

            try
            {
                // Act
                var result = ActiveItemAuthoring.CreateOrSkip(spec, _catalog, _pool, out var report);

                // Assert — devuelve el existente, no crea uno nuevo, y lo dice en el reporte.
                Assert.AreSame(existing, result);
                Assert.IsTrue(report.Contains("salteado"), $"El reporte debe avisar el skip: '{report}'");
                Assert.IsFalse(ItemShopPriceBridge.IsInPool(_pool, existing),
                    "Skip no debe dar de alta el item en el pool — no hubo creación.");
            }
            finally
            {
                Object.DestroyImmediate(existing);
            }
        }

        [Test]
        public void CreateOrSkip_InvalidBandStructure_StillCreatesButReportsValidationFailure()
        {
            // Arrange — Binary sobre un D3 (caras impares): ActiveItemBands.Validate rechaza
            // esta combinación, pero el GDD no prohíbe crear el item mientras se autora —
            // solo prohíbe que una banda quede sin efecto en runtime.
            var spec = MakeSpec("test.active.probe.invalid", "Test Invalid Structure");
            spec.Die = DiceType.D3;
            spec.Resolution = ActiveItemResolution.Binary;
            _itemIdsToCleanUpLocalization.Add(spec.ItemId);

            // Act
            var item = ActiveItemAuthoring.CreateOrSkip(spec, _catalog, _pool, out var report);

            // Assert — se crea igual (asset + catálogo), pero el reporte surfacea el error.
            Assert.IsNotNull(item);
            Assert.AreEqual(item, _catalog.GetById(spec.ItemId));
            Assert.IsFalse(ActiveItemBands.Validate(item, out var expectedError));
            Assert.IsTrue(report.Contains(expectedError),
                $"El reporte debe incluir el motivo de la validación fallida: '{report}'");
        }
    }
}
