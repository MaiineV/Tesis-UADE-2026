using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.HUD;
using Rollgeon.UI.HUD.Contract;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Camino drawer-style de <see cref="ContractDisplayView"/> (rework de selección de
    /// clase): con prefab de fila de contrato + settings cableados instancia
    /// <see cref="ContractComboRowView"/>; a medias o sin ellos cae a la tabla legacy.
    /// </summary>
    [TestFixture]
    public class ContractDisplayDrawerRowTests
    {
        private readonly List<Object> _spawned = new();
        private ContractDisplayView _view;

        [TearDown]
        public void TearDown()
        {
            // Bind suscribe a EventManager (estático) — sin este OnDisable el handler
            // sobreviviría al test y contaminaría a los siguientes.
            if (_view != null) InvokePrivate(_view, "OnDisable");
            _view = null;
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        [Test]
        public void should_use_drawer_style_rows_when_contract_prefab_and_settings_are_wired()
        {
            // Arrange
            var view = MakeView(out var container);
            SetPrivate(view, "_contractRowPrefab", MakeContractRowTemplate());
            SetPrivate(view, "_uiSettings", MakeSettings());
            var sheet = MakeSheet("combo.pair", "combo.trio");

            // Act
            view.Bind(sheet);

            // Assert
            Assert.AreEqual(2, container.childCount);
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                Assert.IsNotNull(child.GetComponent<ContractComboRowView>(),
                    $"la fila {i} debería ser drawer-style");
                Assert.IsNull(child.GetComponent<ComboRowView>(),
                    $"la fila {i} no debería ser legacy");
            }
        }

        [Test]
        public void should_fall_back_to_legacy_rows_when_settings_are_missing()
        {
            // Arrange — prefab drawer-style cableado pero sin settings: el camino nuevo no
            // puede activarse a medias.
            var view = MakeView(out var container);
            SetPrivate(view, "_contractRowPrefab", MakeContractRowTemplate());
            SetPrivate(view, "_rowPrefab", MakeLegacyRowTemplate());
            var sheet = MakeSheet("combo.pair");

            // Act
            view.Bind(sheet);

            // Assert
            Assert.AreEqual(1, container.childCount);
            Assert.IsNotNull(container.GetChild(0).GetComponent<ComboRowView>(),
                "sin settings la fila debe ser la legacy");
        }

        [Test]
        public void should_bind_the_localized_description_when_the_label_is_wired()
        {
            // Arrange — comboId desconocido: la tabla Content no lo tiene y el label debe
            // caer al texto autorado del SO.
            var row = MakeContractRowTemplate();
            var label = AddLabelChild(row.gameObject, "Description");
            SetPrivate(row, "_descriptionLabel", label);
            var combo = MakeCombo("combo.test_only", "Suma los dos dados iguales.");

            // Act
            row.Bind(combo, null, null);

            // Assert
            Assert.AreEqual("Suma los dos dados iguales.", label.text);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private ContractDisplayView MakeView(out Transform container)
        {
            var go = new GameObject("ContractDisplay", typeof(RectTransform));
            _spawned.Add(go);

            var containerGo = new GameObject("RowsContainer", typeof(RectTransform));
            containerGo.transform.SetParent(go.transform, worldPositionStays: false);
            container = containerGo.transform;

            _view = go.AddComponent<ContractDisplayView>();
            SetPrivate(_view, "_rowsContainer", container);
            return _view;
        }

        private ContractComboRowView MakeContractRowTemplate()
        {
            var go = new GameObject("ContractRowTemplate", typeof(RectTransform));
            _spawned.Add(go);
            return go.AddComponent<ContractComboRowView>();
        }

        private ComboRowView MakeLegacyRowTemplate()
        {
            var go = new GameObject("LegacyRowTemplate", typeof(RectTransform));
            _spawned.Add(go);
            return go.AddComponent<ComboRowView>();
        }

        private ContractSheetUiSettingsSO MakeSettings()
        {
            var settings = ScriptableObject.CreateInstance<ContractSheetUiSettingsSO>();
            _spawned.Add(settings);
            return settings;
        }

        private Rollgeon.Heroes.ContractSheet MakeSheet(params string[] comboIds)
        {
            var sheet = new Rollgeon.Heroes.ContractSheet();
            foreach (var comboId in comboIds)
                sheet.Combos.Add(MakeCombo(comboId, description: string.Empty));
            return sheet;
        }

        private Rollgeon.Combos.BaseComboSO MakeCombo(string comboId, string description)
        {
            var combo = ScriptableObject.CreateInstance<Rollgeon.Combos.Concretes.Combo_Par>();
            _spawned.Add(combo);
            SetPrivate(combo, "_comboId", comboId);
            SetPrivate(combo, "_description", description);
            return combo;
        }

        private TextMeshProUGUI AddLabelChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            return go.AddComponent<TextMeshProUGUI>();
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var info = FindField(target.GetType(), field);
            Assert.IsNotNull(info, $"campo {field} no encontrado en {target.GetType().Name}");
            info.SetValue(target, value);
        }

        private static FieldInfo FindField(System.Type type, string field)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var info = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                if (info != null) return info;
            }
            return null;
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
