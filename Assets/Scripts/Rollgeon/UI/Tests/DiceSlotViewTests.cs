using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.HUD;
using Rollgeon.Upgrades.Dice;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Visual de encantamiento del slot de combate: holo para bendiciones, material
    /// maldito para <see cref="CapCursed"/>, default sin encantamiento.
    /// </summary>
    /// <remarks>
    /// EditMode no corre lifecycle, pero <c>SetEnchantVisual</c> no depende de
    /// <c>Awake</c> — solo escribe el material del background.
    /// </remarks>
    [TestFixture]
    public class DiceSlotViewTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private DiceSlotView MakeSlot(out Image background, Material holo, Material cursed)
        {
            var go = new GameObject("DiceSlot", typeof(RectTransform));
            _spawned.Add(go);

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer));
            bgGo.transform.SetParent(go.transform, worldPositionStays: false);
            background = bgGo.AddComponent<Image>();

            var slot = go.AddComponent<DiceSlotView>();
            SetPrivate(slot, "_background", background);
            SetPrivate(slot, "_enchantMaterial", holo);
            SetPrivate(slot, "_cursedMaterial", cursed);
            return slot;
        }

        private Material MakeMaterial()
        {
            var mat = new Material(Shader.Find("UI/Default"));
            _spawned.Add(mat);
            return mat;
        }

        private EnchantmentSO MakeEnchantment(bool cursed)
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            _spawned.Add(ench);
            if (cursed)
                SetPrivate(ench, "_capabilities",
                    new List<IEnchantmentCapability> { new CapCursed() });
            return ench;
        }

        [Test]
        public void should_show_the_cursed_material_only_on_a_cursed_die()
        {
            // Arrange
            var holo = MakeMaterial();
            var cursed = MakeMaterial();
            var slot = MakeSlot(out var background, holo, cursed);

            // Act + Assert
            slot.SetEnchantVisual(MakeEnchantment(cursed: true));
            Assert.AreSame(cursed, background.material);

            slot.SetEnchantVisual(MakeEnchantment(cursed: false));
            Assert.AreSame(holo, background.material, "un encantamiento bueno mantiene el holo");

            slot.SetEnchantVisual(null);
            Assert.AreNotSame(cursed, background.material, "sin encantamiento vuelve al default");
            Assert.AreNotSame(holo, background.material, "sin encantamiento vuelve al default");
        }

        [Test]
        public void should_fall_back_to_the_holo_material_when_the_cursed_one_is_missing()
        {
            // Arrange — prefab mal wireado: mejor holo que un dado que parece sin encantar.
            var holo = MakeMaterial();
            var slot = MakeSlot(out var background, holo, cursed: null);

            // Act
            slot.SetEnchantVisual(MakeEnchantment(cursed: true));

            // Assert
            Assert.AreSame(holo, background.material);
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
