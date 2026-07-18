using NUnit.Framework;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Swap de sprite por cantidad del slot de poción: PotionSheet_0 con ≥1,
    /// PotionSheet_1 con 0; sin sprites cableados el ícono no se toca.
    /// </summary>
    [TestFixture]
    public class ActiveItemSlotViewPotionSpriteTests
    {
        private GameObject _go;
        private ActiveItemSlotView _slot;
        private Image _icon;
        private Texture2D _texture;
        private Sprite _full;
        private Sprite _empty;
        private Sprite _original;

        [SetUp]
        public void Setup()
        {
            _texture = new Texture2D(4, 4);
            _full = MakeSprite("full");
            _empty = MakeSprite("empty");
            _original = MakeSprite("original");

            // Inactivo: Awake (EnsureClickable/EnsureCountLabel) no corre — el
            // fixture solo prueba SetCount.
            _go = new GameObject("PocionSlot");
            _go.SetActive(false);
            _slot = _go.AddComponent<ActiveItemSlotView>();

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(_go.transform, false);
            _icon = iconGo.AddComponent<Image>();
            _icon.sprite = _original;

            AssignPrivate(_slot, "_icon", _icon);
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_full != null) Object.DestroyImmediate(_full);
            if (_empty != null) Object.DestroyImmediate(_empty);
            if (_original != null) Object.DestroyImmediate(_original);
            if (_texture != null) Object.DestroyImmediate(_texture);
        }

        [Test]
        public void SetCount_PositiveWithSpritesWired_ShowsFullSprite()
        {
            AssignPrivate(_slot, "_iconWhenCountPositive", _full);
            AssignPrivate(_slot, "_iconWhenCountZero", _empty);

            _slot.SetCount(1);

            Assert.AreSame(_full, _icon.sprite);
        }

        [Test]
        public void SetCount_ZeroWithSpritesWired_ShowsEmptySprite()
        {
            AssignPrivate(_slot, "_iconWhenCountPositive", _full);
            AssignPrivate(_slot, "_iconWhenCountZero", _empty);

            _slot.SetCount(0);

            Assert.AreSame(_empty, _icon.sprite);
        }

        [Test]
        public void SetCount_WithoutCountSprites_LeavesIconUntouched()
        {
            _slot.SetCount(3);

            Assert.AreSame(_original, _icon.sprite,
                "Sin sprites por cantidad cableados (ej. ArcoSlot), el ícono no debe cambiar.");
        }

        // ---------------- helpers ----------------

        private Sprite MakeSprite(string name)
        {
            var sprite = Sprite.Create(_texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            return sprite;
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
