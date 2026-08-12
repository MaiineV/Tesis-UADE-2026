using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Reroll invertido (Balatro) — el descarte consume la selección al arrancar el
    /// reroll, pero el reveal debe seguir sabiendo qué dados volaron: los que se
    /// quedan no se re-revelan (ni re-spinean en Classic) y los que cambiaron de
    /// cara se revelan siempre, estén o no en la máscara (grab-to-reroll 2D).
    /// </summary>
    [TestFixture]
    public class DiceZoneRerollMaskTests
    {
        private GameObject _go;
        private DiceZoneView _zone;
        private DiceSlotView[] _slots;
        private Guid _playerGuid;
        private bool _savedKeepSelected;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            // El branch de HandleRerollStarted depende del modo persistido en
            // PlayerPrefs: pin al default (invertido) y restore en TearDown.
            _savedKeepSelected = Rollgeon.Dice.RerollSelectionPrefs.KeepSelected;
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = false;

            _playerGuid = Guid.NewGuid();

            _go = new GameObject("DiceZone", typeof(RectTransform));
            _zone = _go.AddComponent<DiceZoneView>();

            _slots = new DiceSlotView[3];
            var rects = new List<RectTransform>(3);
            for (int i = 0; i < 3; i++)
            {
                var slotGo = new GameObject($"Slot{i}", typeof(RectTransform));
                slotGo.transform.SetParent(_go.transform, false);
                _slots[i] = slotGo.AddComponent<DiceSlotView>();
                rects.Add((RectTransform)slotGo.transform);
            }
            AssignPrivate(_zone, "_diceSlots", rects);

            _zone.Bind(_playerGuid);
        }

        [TearDown]
        public void TearDown()
        {
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = _savedKeepSelected;
            _zone.Unbind();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private void Roll(params int[] faces)
            => EventManager.Trigger(EventName.OnDiceRolled, _playerGuid, (IReadOnlyList<int>)faces);

        private void SelectDie(int index) => _slots[index].OnToggled.Invoke();

        [Test]
        public void should_consume_selection_and_reveal_selected_die_when_reroll_runs()
        {
            // Arrange — mano inicial con el dado 0 seleccionado para re-tirar.
            Roll(1, 2, 3);
            SelectDie(0);
            Assert.IsTrue(_zone.GetHeldStates()[0], "Precondición: dado 0 seleccionado.");

            // Act — arranca el reroll (consume la selección) y llega el resultado:
            // el dado 0 cambió de cara, los otros dos conservan la suya.
            EventManager.Trigger(EventName.OnRerollStarted, _playerGuid, 2);
            Roll(6, 2, 3);

            // Assert
            CollectionAssert.AreEqual(new[] { false, false, false }, _zone.GetHeldStates(),
                "El reroll consume la selección — la tirada nueva arranca sin holds.");
            Assert.AreEqual(6, _slots[0].CurrentFace, "El seleccionado toma la cara nueva.");
            Assert.AreEqual(2, _slots[1].CurrentFace);
            Assert.AreEqual(3, _slots[2].CurrentFace);
        }

        [Test]
        public void should_reveal_unselected_die_when_its_face_changed_anyway()
        {
            // Arrange — el dado 0 está seleccionado, pero el resultado trae cambiada
            // la cara del dado 1 (grab-to-reroll 2D: lo agarrado puede diferir de lo
            // seleccionado). El safety net de cara-distinta debe revelarlo igual.
            Roll(1, 2, 3);
            SelectDie(0);

            // Act
            EventManager.Trigger(EventName.OnRerollStarted, _playerGuid, 2);
            Roll(1, 5, 3);

            // Assert
            Assert.AreEqual(1, _slots[0].CurrentFace);
            Assert.AreEqual(5, _slots[1].CurrentFace,
                "Un dado con cara nueva se revela aunque no estuviera en la máscara de selección.");
            Assert.AreEqual(3, _slots[2].CurrentFace);
        }

        [Test]
        public void should_reveal_full_hand_on_a_fresh_roll_after_clear()
        {
            // Arrange — una máscara pendiente no debe sobrevivir a un clear forzado
            // (fin de turno / retreat): la mano nueva se revela entera.
            Roll(1, 2, 3);
            SelectDie(0);
            EventManager.Trigger(EventName.OnRerollStarted, _playerGuid, 2);
            _zone.ClearAll();

            // Act — mano nueva.
            Roll(4, 4, 4);

            // Assert
            Assert.AreEqual(4, _slots[0].CurrentFace);
            Assert.AreEqual(4, _slots[1].CurrentFace);
            Assert.AreEqual(4, _slots[2].CurrentFace);
        }

        // -------------------------------------------------------------------
        // Modo clásico (RerollSelectionPrefs.KeepSelected): la selección marca los
        // dados que SE QUEDAN — los holds persisten entre rerolls y la máscara
        // stasheada es el complemento.
        // -------------------------------------------------------------------

        [Test]
        public void classic_should_persist_holds_and_reveal_unselected_when_reroll_runs()
        {
            // Arrange — dado 0 lockeado: debe sobrevivir al reroll con su cara.
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = true;
            Roll(1, 2, 3);
            SelectDie(0);

            // Act — vuelan los NO seleccionados (1 y 2).
            EventManager.Trigger(EventName.OnRerollStarted, _playerGuid, 2);
            Roll(1, 5, 6);

            // Assert
            CollectionAssert.AreEqual(new[] { true, false, false }, _zone.GetHeldStates(),
                "En clásico los holds persisten tras el reroll.");
            Assert.AreEqual(1, _slots[0].CurrentFace, "El lockeado conserva su cara.");
            Assert.AreEqual(5, _slots[1].CurrentFace);
            Assert.AreEqual(6, _slots[2].CurrentFace);
        }

        [Test]
        public void classic_grab_mode_should_still_consume_selection_when_reroll_runs()
        {
            // Arrange — en grab-mode 2D lo que vuela es lo AGARRADO, no la selección:
            // se mantiene el comportamiento invertido (stash + clear) aunque el modo
            // clásico esté activo.
            Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = true;
            var settings = ScriptableObject.CreateInstance<Rollgeon.Dice.Throw.DiceThrowSettingsSO>();
            settings.DefaultMode = Rollgeon.Dice.Throw.DiceThrowMode.TwoD;
            var throwSvc = new Rollgeon.Dice.Throw.DiceThrowService(settings);
            ServiceLocator.AddService<Rollgeon.Dice.Throw.IDiceThrowService>(throwSvc);
            try
            {
                Roll(1, 2, 3);
                SelectDie(0);

                // Act
                EventManager.Trigger(EventName.OnRerollStarted, _playerGuid, 2);

                // Assert — la selección se consumió como en invertido.
                CollectionAssert.AreEqual(new[] { false, false, false }, _zone.GetHeldStates(),
                    "En grab-mode el reroll consume la selección aunque rija el modo clásico.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Campo '{fieldName}' no encontrado en {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
