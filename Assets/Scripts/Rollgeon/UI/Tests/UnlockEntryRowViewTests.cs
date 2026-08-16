using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.Unlocks;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Enmascarado y color del nombre en la card de desbloqueos: bloqueado = "???"
    /// en tinta oscura (la card naranja no banca texto claro), desbloqueado = nombre
    /// real en el color claro autorado.
    /// </summary>
    [TestFixture]
    public class UnlockEntryRowViewTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        [Test]
        public void should_mask_the_name_in_the_locked_color_when_locked()
        {
            // Arrange
            var row = MakeRow(out var nameLabel, Color.white, Color.black);

            // Act
            row.Bind("Berserker", "pista", locked: true);

            // Assert
            Assert.AreEqual("???", nameLabel.text);
            Assert.AreEqual(Color.black, nameLabel.color);
        }

        [Test]
        public void should_show_the_name_in_the_unlocked_color_when_unlocked()
        {
            // Arrange — el color claro debe volver si la misma fila se re-bindea
            // desbloqueada (pooling / refresh de localización).
            var row = MakeRow(out var nameLabel, Color.white, Color.black);
            row.Bind("Berserker", "pista", locked: true);

            // Act
            row.Bind("Berserker", "descripción completa", locked: false);

            // Assert
            Assert.AreEqual("Berserker", nameLabel.text);
            Assert.AreEqual(Color.white, nameLabel.color);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private UnlockEntryRowView MakeRow(out TextMeshProUGUI nameLabel,
            Color unlockedColor, Color lockedColor)
        {
            var go = new GameObject("UnlockEntry", typeof(RectTransform));
            _spawned.Add(go);

            var labelGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer));
            labelGo.transform.SetParent(go.transform, worldPositionStays: false);
            nameLabel = labelGo.AddComponent<TextMeshProUGUI>();

            var row = go.AddComponent<UnlockEntryRowView>();
            SetPrivate(row, "_nameLabel", nameLabel);
            SetPrivate(row, "_unlockedNameColor", unlockedColor);
            SetPrivate(row, "_lockedNameColor", lockedColor);
            return row;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"campo {field} no encontrado en {target.GetType().Name}");
            info.SetValue(target, value);
        }
    }
}
