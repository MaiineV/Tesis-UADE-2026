using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.HUD.Contract;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Manos de ejemplo del contrato y el estado abierto/cerrado del drawer.
    /// </summary>
    [TestFixture]
    public class ContractDrawerTests
    {
        private ContractSheetUiSettingsSO _settings;
        private readonly List<Object> _spawned = new();

        [SetUp]
        public void Setup()
        {
            _settings = ScriptableObject.CreateInstance<ContractSheetUiSettingsSO>();
            _settings.SetHands(new List<ComboExampleHand>
            {
                new ComboExampleHand
                {
                    ComboId = "combo.pair",
                    Faces = new[] { 5, 5, 2, 3, 4 },
                    Highlighted = new[] { true, true, false, false, false },
                },
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings != null) Object.DestroyImmediate(_settings);
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ------------------------------------------------------------------
        // Manos de ejemplo
        // ------------------------------------------------------------------

        [Test]
        public void should_find_the_hand_authored_for_a_combo()
        {
            // Arrange + Act
            var hand = _settings.FindHand("combo.pair");

            // Assert
            Assert.IsNotNull(hand);
            Assert.AreEqual(5, hand.Count);
            Assert.AreEqual(5, hand.FaceAt(0));
            Assert.IsTrue(hand.IsHighlighted(1), "el par va con marco");
            Assert.IsFalse(hand.IsHighlighted(2), "el resto va opaco");
        }

        [Test]
        public void should_return_null_for_a_combo_without_an_authored_hand()
        {
            // Arrange + Act + Assert — un combo nuevo sin mano no puede romper la tabla.
            Assert.IsNull(_settings.FindHand("combo.unknown"));
            Assert.IsNull(_settings.FindHand(null));
        }

        [Test]
        public void should_tolerate_a_half_authored_hand()
        {
            // Arrange — máscara más corta que las caras: pasa si alguien edita el asset a
            // mano y agrega un dado sin extender el otro array.
            var hand = new ComboExampleHand
            {
                ComboId = "combo.x",
                Faces = new[] { 1, 2, 3, 4, 5 },
                Highlighted = new[] { true },
            };

            // Act + Assert
            Assert.IsTrue(hand.IsHighlighted(0));
            Assert.IsFalse(hand.IsHighlighted(4), "fuera de la máscara = sin marco, no excepción");
            Assert.AreEqual(0, hand.FaceAt(9), "fuera de rango = 0, no excepción");
        }

        [Test]
        public void should_map_face_numbers_to_sprites_one_based()
        {
            // Arrange — d6_0 es la cara 1, así que el índice va corrido.
            var faces = new Sprite[6];
            for (int i = 0; i < 6; i++) faces[i] = MakeSprite($"d6_{i}");
            _settings.SetArt(faces, null);

            // Act + Assert
            Assert.AreSame(faces[0], _settings.FaceSprite(1));
            Assert.AreSame(faces[5], _settings.FaceSprite(6));
            Assert.IsNull(_settings.FaceSprite(0), "cara 0 no existe");
            Assert.IsNull(_settings.FaceSprite(7), "cara 7 tampoco");
        }

        // ------------------------------------------------------------------
        // Dado
        // ------------------------------------------------------------------

        [Test]
        public void should_dim_the_dice_that_do_not_form_the_combo()
        {
            // Arrange
            var die = MakeDie(out var face, out var frame);

            // Act
            die.Show(MakeSprite("cara"), MakeSprite("marco"), highlighted: false, dimmedAlpha: 0.45f);

            // Assert
            Assert.AreEqual(0.45f, face.color.a, 0.001f);
            Assert.IsFalse(frame.enabled, "sin marco si no forma el combo");
        }

        [Test]
        public void should_frame_the_dice_that_form_the_combo()
        {
            // Arrange
            var die = MakeDie(out var face, out var frame);

            // Act
            die.Show(MakeSprite("cara"), MakeSprite("marco"), highlighted: true, dimmedAlpha: 0.45f);

            // Assert
            Assert.AreEqual(1f, face.color.a, 0.001f);
            Assert.IsTrue(frame.enabled);
        }

        // ------------------------------------------------------------------
        // Drawer
        // ------------------------------------------------------------------

        [Test]
        public void should_start_closed()
        {
            // Arrange + Act
            var drawer = MakeDrawer(out _, out var backdrop);

            // Assert
            Assert.IsFalse(drawer.IsOpen);
            Assert.IsFalse(backdrop.gameObject.activeSelf, "el backdrop cerrado comería clicks del tablero");
        }

        [Test]
        public void should_open_and_close_on_toggle()
        {
            // Arrange
            var drawer = MakeDrawer(out _, out var backdrop);

            // Act
            drawer.Toggle();

            // Assert
            Assert.IsTrue(drawer.IsOpen);
            Assert.IsTrue(backdrop.gameObject.activeSelf);

            // Act
            drawer.Toggle();

            // Assert
            Assert.IsFalse(drawer.IsOpen);
            Assert.IsFalse(backdrop.gameObject.activeSelf);
        }

        [Test]
        public void should_ignore_a_second_open()
        {
            // Arrange — el ícono se puede martillar; abrir dos veces no debe re-animar.
            var drawer = MakeDrawer(out var panel, out _);
            drawer.Open();
            float xAfterFirst = panel.anchoredPosition.x;

            // Act
            drawer.Open();

            // Assert
            Assert.IsTrue(drawer.IsOpen);
            Assert.AreEqual(xAfterFirst, panel.anchoredPosition.x, 0.001f);
        }

        // ------------------------------------------------------------------
        // Orden por daño base
        // ------------------------------------------------------------------

        [Test]
        public void should_order_the_combos_by_base_damage_ascending()
        {
            // Arrange — el contrato los trae en orden de autoría; el drawer los ordena por
            // valor para que el jugador lea la escalera de daño de un vistazo.
            var sheet = new Rollgeon.Heroes.ContractSheet();
            var generala = MakeCombo<Rollgeon.Combos.Concretes.Combo_Generala>("combo.generala");
            var par = MakeCombo<Rollgeon.Combos.Concretes.Combo_Par>("combo.pair");
            var trio = MakeCombo<Rollgeon.Combos.Concretes.Combo_Trio>("combo.trio");
            sheet.Combos.AddRange(new Rollgeon.Combos.BaseComboSO[] { generala, par, trio });
            sheet.BaseDamageTable.AddRange(new[]
            {
                new Rollgeon.Heroes.ComboBaseDamageEntry { ComboId = "combo.generala", BaseDamage = 90 },
                new Rollgeon.Heroes.ComboBaseDamageEntry { ComboId = "combo.pair", BaseDamage = 8 },
                new Rollgeon.Heroes.ComboBaseDamageEntry { ComboId = "combo.trio", BaseDamage = 22 },
            });

            // Act
            var ordered = InvokeSort(sheet);

            // Assert
            CollectionAssert.AreEqual(
                new Rollgeon.Combos.BaseComboSO[] { par, trio, generala }, ordered);
        }

        [Test]
        public void should_survive_a_null_sheet()
        {
            // Arrange + Act — abrir el drawer en una escena sin héroe no puede romper.
            var ordered = InvokeSort(null);

            // Assert
            Assert.IsNotNull(ordered);
            Assert.AreEqual(0, ordered.Count);
        }

        private T MakeCombo<T>(string comboId) where T : Rollgeon.Combos.BaseComboSO
        {
            var combo = ScriptableObject.CreateInstance<T>();
            _spawned.Add(combo);
            var field = typeof(Rollgeon.Combos.BaseComboSO)
                .GetField("_comboId", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "campo _comboId no encontrado en BaseComboSO");
            field.SetValue(combo, comboId);
            return combo;
        }

        private static List<Rollgeon.Combos.BaseComboSO> InvokeSort(Rollgeon.Heroes.ContractSheet sheet)
        {
            var method = typeof(ContractDrawerView).GetMethod("SortedByBaseDamage",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "método SortedByBaseDamage no encontrado");
            return (List<Rollgeon.Combos.BaseComboSO>)method.Invoke(null, new object[] { sheet });
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private Sprite MakeSprite(string name)
        {
            var tex = new Texture2D(4, 4);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            _spawned.Add(tex);
            _spawned.Add(sprite);
            return sprite;
        }

        private ContractDieView MakeDie(out Image face, out Image frame)
        {
            var go = new GameObject("ContractDie", typeof(RectTransform));
            _spawned.Add(go);

            frame = AddImageChild(go, "Frame");
            face = AddImageChild(go, "Face");

            var die = go.AddComponent<ContractDieView>();
            SetPrivate(die, "_face", face);
            SetPrivate(die, "_frame", frame);
            return die;
        }

        private ContractDrawerView MakeDrawer(out RectTransform panel, out Button backdrop)
        {
            var go = new GameObject("ContractDrawer", typeof(RectTransform));
            _spawned.Add(go);

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(go.transform, worldPositionStays: false);
            panel = (RectTransform)panelGo.transform;

            var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backdropGo.transform.SetParent(go.transform, worldPositionStays: false);
            backdrop = backdropGo.AddComponent<Button>();

            var drawer = go.AddComponent<ContractDrawerView>();
            SetPrivate(drawer, "_panel", panel);
            SetPrivate(drawer, "_backdrop", backdrop);
            // AddComponent ya corrió Awake sin ver los refs; se re-dispara con el wiring
            // completo, igual que en ActionButtonVisualStateTests.
            InvokePrivate(drawer, "Awake");
            return drawer;
        }

        private static Image AddImageChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            return go.AddComponent<Image>();
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"campo {field} no encontrado en {target.GetType().Name}");
            info.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string method)
        {
            var info = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"método {method} no encontrado en {target.GetType().Name}");
            info.Invoke(target, null);
        }
    }
}
