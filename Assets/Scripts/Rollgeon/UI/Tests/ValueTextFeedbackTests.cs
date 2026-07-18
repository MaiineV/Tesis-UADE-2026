using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    [TestFixture]
    public class ValueTextFeedbackTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        // ───── Intensity (escala por valor de combo) ──────────────────────────

        [Test]
        public void Intensity_ValueAtOrAboveMax_ReturnsMax()
        {
            Assert.AreEqual(10f, ValueTextFeedbackController.Intensity(20, 2f, 10f, 20f), 1e-4f);
            Assert.AreEqual(10f, ValueTextFeedbackController.Intensity(40, 2f, 10f, 20f), 1e-4f);
        }

        [Test]
        public void Intensity_ValueBelowMax_Interpolates()
        {
            // t = 10/20 = 0.5 → lerp(2,10,0.5) = 6
            Assert.AreEqual(6f, ValueTextFeedbackController.Intensity(10, 2f, 10f, 20f), 1e-4f);
        }

        [Test]
        public void Intensity_NonPositiveValue_ReturnsBaselineMin()
        {
            // Sin combo => amplitud baseline (min), no cero: la animación del tipo igual corre.
            Assert.AreEqual(2f, ValueTextFeedbackController.Intensity(0, 2f, 10f, 20f), 1e-4f);
            Assert.AreEqual(2f, ValueTextFeedbackController.Intensity(-5, 2f, 10f, 20f), 1e-4f);
        }

        // ───── Resolución de color/flavor por tipo ────────────────────────────

        [Test]
        public void TryGet_ReturnsTagAndColor_ForAuthoredType()
        {
            // Arrange
            var catalog = MakeCatalog(
                Entry(DiceBoardType.Default, Color.white, ""),
                Entry(DiceBoardType.Attack, Color.red, "shake"));

            // Act
            bool found = catalog.TryGet(DiceBoardType.Attack, out var skin);

            // Assert
            Assert.IsTrue(found);
            Assert.AreEqual(Color.red, skin.TextColor);
            Assert.AreEqual("shake", skin.Feedback.EffectTag);
        }

        [Test]
        public void TryGet_DegradesToDefault_WhenTypeMissing()
        {
            // Arrange — solo Default autorado; Attack no existe en el catálogo.
            var catalog = MakeCatalog(Entry(DiceBoardType.Default, Color.green, "wave"));

            // Act
            bool found = catalog.TryGet(DiceBoardType.Attack, out var skin);

            // Assert — hereda color/tag de Default.
            Assert.IsTrue(found);
            Assert.AreEqual(Color.green, skin.TextColor);
            Assert.AreEqual("wave", skin.Feedback.EffectTag);
        }

        // ───── Helpers ────────────────────────────────────────────────────────

        private DiceBoardSkinEntry Entry(DiceBoardType type, Color textColor, string effectTag)
        {
            var tex = Texture2D.whiteTexture;
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            _created.Add(sprite);
            return new DiceBoardSkinEntry
            {
                Type = type,
                Sprite = sprite, // TryGetExact exige Sprite != null para considerar la entry usable
                Tint = Color.white,
                ImageType = Image.Type.Sliced,
                TextColor = textColor,
                Feedback = new ValueTextFeedback { EffectTag = effectTag },
            };
        }

        private DiceBoardSkinCatalogSO MakeCatalog(params DiceBoardSkinEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<DiceBoardSkinCatalogSO>();
            catalog.Skins = new List<DiceBoardSkinEntry>(entries);
            _created.Add(catalog);
            return catalog;
        }
    }
}
