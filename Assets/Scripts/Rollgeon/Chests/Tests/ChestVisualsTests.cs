using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Items;
using UnityEngine;

namespace Rollgeon.Chests.Tests
{
    /// <summary>
    /// Resolución del visual por tier: prefab override vs global, y aplicación de
    /// materiales/tint por slot. El contrato clave es backward-compat: con los
    /// overrides en null el comportamiento debe ser el tint MPB de siempre.
    /// </summary>
    [TestFixture]
    public class ChestVisualsTests
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly List<Object> _cleanup = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _cleanup)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _cleanup.Clear();
        }

        private Material NewMaterial(string name)
        {
            var mat = new Material(Shader.Find("Sprites/Default")) { name = name };
            _cleanup.Add(mat);
            return mat;
        }

        private GameObject NewChestGO(out MeshRenderer renderer, string woodName = "Wood", string frameName = "Frame")
        {
            var go = new GameObject("ChestVisualsTest");
            _cleanup.Add(go);
            renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { NewMaterial(woodName), NewMaterial(frameName) };
            return go;
        }

        private static Color SlotColor(Renderer renderer, int slot)
        {
            var mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb, slot);
            return mpb.GetColor(BaseColorId);
        }

        // -----------------------------------------------------------------
        // ClassifySlot
        // -----------------------------------------------------------------

        [TestCase("Wood", ChestVisuals.Slot.Body)]
        [TestCase("MAT_ChestWood_Common", ChestVisuals.Slot.Body)]
        [TestCase("Body", ChestVisuals.Slot.Body)]
        [TestCase("Frame", ChestVisuals.Slot.Fittings)]
        [TestCase("fittings_metal", ChestVisuals.Slot.Fittings)]
        [TestCase("Lid", ChestVisuals.Slot.None)]
        [TestCase(null, ChestVisuals.Slot.None)]
        public void ClassifySlot_MatchesByNameCaseInsensitive(string materialName, ChestVisuals.Slot expected)
        {
            // Act
            var slot = ChestVisuals.ClassifySlot(materialName);

            // Assert
            Assert.AreEqual(expected, slot);
        }

        // -----------------------------------------------------------------
        // ResolvePrefab
        // -----------------------------------------------------------------

        [Test]
        public void ResolvePrefab_WithoutOverride_ReturnsGlobalPrefab()
        {
            // Arrange
            var config = ScriptableObject.CreateInstance<ChestConfigSO>();
            _cleanup.Add(config);
            var global = new GameObject("GlobalChest");
            _cleanup.Add(global);
            config.ChestPrefab = global;
            var tierDef = new ChestTierDef { Tier = ItemRarity.Common };

            // Act
            var resolved = ChestVisuals.ResolvePrefab(config, tierDef);

            // Assert
            Assert.AreSame(global, resolved);
        }

        [Test]
        public void ResolvePrefab_WithOverride_ReturnsTierPrefab()
        {
            // Arrange
            var config = ScriptableObject.CreateInstance<ChestConfigSO>();
            _cleanup.Add(config);
            var global = new GameObject("GlobalChest");
            _cleanup.Add(global);
            var perTier = new GameObject("LegendaryChest");
            _cleanup.Add(perTier);
            config.ChestPrefab = global;
            var tierDef = new ChestTierDef { Tier = ItemRarity.Legendary, ChestPrefabOverride = perTier };

            // Act
            var resolved = ChestVisuals.ResolvePrefab(config, tierDef);

            // Assert
            Assert.AreSame(perTier, resolved);
        }

        // -----------------------------------------------------------------
        // Apply — sin overrides = tint clásico
        // -----------------------------------------------------------------

        [Test]
        public void Apply_WithoutOverrides_TintsSlotsAndKeepsSharedMaterials()
        {
            // Arrange
            var go = NewChestGO(out var renderer);
            var originalMats = renderer.sharedMaterials;
            var fittings = new Color(0.2f, 0.3f, 0.4f, 1f);
            var tierDef = new ChestTierDef { BodyColor = Color.red };

            // Act
            ChestVisuals.Apply(go, tierDef, fittings);

            // Assert
            Assert.AreEqual(Color.red, SlotColor(renderer, 0), "Slot Wood debe tintarse con BodyColor.");
            Assert.AreEqual(fittings, SlotColor(renderer, 1), "Slot Frame debe tintarse con FittingsColor.");
            CollectionAssert.AreEqual(originalMats, renderer.sharedMaterials,
                "Sin overrides los materiales no deben reemplazarse.");
        }

        [Test]
        public void Apply_WithBodyOverride_ReplacesBodySlotAndStillTintsFittings()
        {
            // Arrange
            var go = NewChestGO(out var renderer);
            var originalFrame = renderer.sharedMaterials[1];
            var overrideMat = NewMaterial("MAT_GoldChest");
            var fittings = new Color(0.2f, 0.3f, 0.4f, 1f);
            var tierDef = new ChestTierDef { BodyColor = Color.red, BodyMaterialOverride = overrideMat };

            // Act
            ChestVisuals.Apply(go, tierDef, fittings);

            // Assert
            Assert.AreSame(overrideMat, renderer.sharedMaterials[0], "Slot Wood debe recibir el material override.");
            Assert.AreSame(originalFrame, renderer.sharedMaterials[1], "Slot Frame no debe reemplazarse.");
            Assert.AreEqual(default(Color), SlotColor(renderer, 0), "El slot overrideado no debe tintarse.");
            Assert.AreEqual(fittings, SlotColor(renderer, 1), "Slot Frame debe seguir tintándose.");
        }

        [Test]
        public void Apply_WithBothOverrides_ReplacesBothSlotsWithoutTint()
        {
            // Arrange
            var go = NewChestGO(out var renderer);
            var bodyMat = NewMaterial("MAT_GoldChest");
            var fittingsMat = NewMaterial("MAT_SilverTrim");
            var tierDef = new ChestTierDef
            {
                BodyColor = Color.red,
                BodyMaterialOverride = bodyMat,
                FittingsMaterialOverride = fittingsMat,
            };

            // Act
            ChestVisuals.Apply(go, tierDef, Color.blue);

            // Assert
            Assert.AreSame(bodyMat, renderer.sharedMaterials[0]);
            Assert.AreSame(fittingsMat, renderer.sharedMaterials[1]);
            Assert.AreEqual(default(Color), SlotColor(renderer, 0));
            Assert.AreEqual(default(Color), SlotColor(renderer, 1));
        }

        // -----------------------------------------------------------------
        // Apply — fallback por nombre de renderer (PF_ChestPlaceholder)
        // -----------------------------------------------------------------

        [Test]
        public void Apply_LegacyRendererNames_TintsWholeRenderer()
        {
            // Arrange — materiales sin nombre matcheable, renderer llamado 'Body'.
            var root = new GameObject("ChestRoot");
            _cleanup.Add(root);
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform);
            var renderer = body.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { NewMaterial("Default") };
            var tierDef = new ChestTierDef { BodyColor = Color.green };

            // Act
            ChestVisuals.Apply(root, tierDef, Color.blue);

            // Assert — tint whole-renderer (sin índice de slot).
            var mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);
            Assert.AreEqual(Color.green, mpb.GetColor(BaseColorId));
            Assert.AreEqual(Color.green, mpb.GetColor(ColorId));
        }
    }
}
