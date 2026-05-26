using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Run;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Tests del modo "builder" de <see cref="BuildSelectionScreen"/> (Fase 2 —
    /// pool por clase + construccion interactiva). El fixture cablea los
    /// SerializedFields nuevos (_poolOfferingsContainer, _poolOfferingPrefab,
    /// _bagCounterLabel) y un hero con DiceBagPool.
    /// </summary>
    [TestFixture]
    public class BuildSelectionScreenBuilderTests
    {
        private GameObject _screenGO;
        private BuildSelectionScreen _screen;
        private Button _confirmButton;
        private Button _backButton;
        private Transform _diceContainer;
        private Transform _poolContainer;
        private GameObject _poolPrefabGO;
        private GameObject _diceSlotPrefabGO;
        private ClassHeroSO _hero;
        private DiceBagPoolSO _pool;

        [SetUp]
        public void SetUp()
        {
            PendingRunRequest.Clear();

            _screenGO = new GameObject("BuildSelectionScreen");
            _screenGO.SetActive(false);
            _screen = _screenGO.AddComponent<BuildSelectionScreen>();

            _confirmButton = AttachButton("ConfirmButton");
            _backButton = AttachButton("BackButton");

            var heroNameLabel = AttachTMP("HeroNameLabel");
            var heroDescLabel = AttachTMP("HeroDescLabel");
            var fallbackLabel = AttachTMP("FallbackLabel");
            var bagCounterLabel = AttachTMP("BagCounterLabel");

            // Container para los slots de la bolsa armada.
            var diceContainerGO = new GameObject("DiceContainer");
            diceContainerGO.transform.SetParent(_screenGO.transform, false);
            _diceContainer = diceContainerGO.transform;

            // Container para las filas del pool.
            var poolContainerGO = new GameObject("PoolContainer");
            poolContainerGO.transform.SetParent(_screenGO.transform, false);
            _poolContainer = poolContainerGO.transform;

            // Prefab de DiceSlotView (preview).
            _diceSlotPrefabGO = new GameObject("DiceSlotPrefab");
            _diceSlotPrefabGO.SetActive(false);
            var diceSlotPrefab = _diceSlotPrefabGO.AddComponent<DiceSlotView>();

            // Prefab de PoolOfferingRow (sin hijos visuales — el componente solo).
            _poolPrefabGO = new GameObject("PoolOfferingPrefab");
            _poolPrefabGO.SetActive(false);
            var poolPrefab = _poolPrefabGO.AddComponent<PoolOfferingRow>();

            // Hero + Pool.
            _hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _hero.EntityId = "hero.warrior";
            _hero.DisplayName = "Guerrero";

            _pool = ScriptableObject.CreateInstance<DiceBagPoolSO>();
            _pool.RequiredBagSize = 5;
            _pool.Offerings = new List<DicePoolEntry>
            {
                new DicePoolEntry { Type = DiceType.D6, MaxInBag = 5 },
                new DicePoolEntry { Type = DiceType.D8, MaxInBag = 2 },
            };
            _hero.DiceBagPool = _pool;

            AssignPrivate(_screen, "_heroNameLabel", heroNameLabel);
            AssignPrivate(_screen, "_heroDescriptionLabel", heroDescLabel);
            AssignPrivate(_screen, "_diceContainer", _diceContainer);
            AssignPrivate(_screen, "_diceSlotPrefab", diceSlotPrefab);
            AssignPrivate(_screen, "_diceBagFallbackLabel", fallbackLabel);
            AssignPrivate(_screen, "_confirmButton", _confirmButton);
            AssignPrivate(_screen, "_backButton", _backButton);
            AssignPrivate(_screen, "_poolOfferingsContainer", _poolContainer);
            AssignPrivate(_screen, "_poolOfferingPrefab", poolPrefab);
            AssignPrivate(_screen, "_bagCounterLabel", bagCounterLabel);
        }

        [TearDown]
        public void TearDown()
        {
            PendingRunRequest.Clear();
            if (_screenGO != null) UnityEngine.Object.DestroyImmediate(_screenGO);
            if (_poolPrefabGO != null) UnityEngine.Object.DestroyImmediate(_poolPrefabGO);
            if (_diceSlotPrefabGO != null) UnityEngine.Object.DestroyImmediate(_diceSlotPrefabGO);
            if (_hero != null) UnityEngine.Object.DestroyImmediate(_hero);
            if (_pool != null) UnityEngine.Object.DestroyImmediate(_pool);
        }

        // ---------------- Tests ----------------

        [Test]
        public void OnPushed_WithPool_InstantiatesOnePoolRowPerOffering()
        {
            Push();

            var rows = GetPoolRows();
            Assert.AreEqual(2, rows.Count, "Debe haber una PoolOfferingRow por DicePoolEntry.");
            Assert.AreEqual(DiceType.D6, rows[0].Type);
            Assert.AreEqual(DiceType.D8, rows[1].Type);
        }

        [Test]
        public void OnPushed_WithPool_ConfirmStartsDisabled()
        {
            Push();
            Assert.IsFalse(_confirmButton.interactable, "Confirm debe iniciar deshabilitado en builder mode.");
        }

        [Test]
        public void AddDice_FillsBagAndEnablesConfirmAt5()
        {
            Push();
            for (int i = 0; i < 5; i++)
                InvokeAdd(DiceType.D6);

            Assert.AreEqual(5, GetCurrentBag().Count);
            Assert.IsTrue(_confirmButton.interactable, "Confirm debe habilitarse al llegar a RequiredBagSize.");
        }

        [Test]
        public void AddDice_RejectsSixthDie()
        {
            Push();
            for (int i = 0; i < 5; i++)
                InvokeAdd(DiceType.D6);

            // Sexto intento — debe ignorarse (la bolsa ya esta llena).
            InvokeAdd(DiceType.D8);

            var bag = GetCurrentBag();
            Assert.AreEqual(5, bag.Count, "No debe permitir mas de RequiredBagSize.");
            CollectionAssert.DoesNotContain(bag, DiceType.D8);
        }

        [Test]
        public void AddDice_RejectsExceedingMaxInBagPerType()
        {
            Push();
            // D8 tiene MaxInBag = 2. Intentar agregar 3 deberia rechazar el tercero.
            InvokeAdd(DiceType.D8);
            InvokeAdd(DiceType.D8);
            InvokeAdd(DiceType.D8);

            int d8Count = 0;
            foreach (var d in GetCurrentBag())
                if (d == DiceType.D8) d8Count++;
            Assert.AreEqual(2, d8Count, "MaxInBag por tipo debe respetarse.");
        }

        [Test]
        public void RemoveDice_DropsLastOccurrenceAndDisablesConfirm()
        {
            Push();
            for (int i = 0; i < 5; i++)
                InvokeAdd(DiceType.D6);
            Assert.IsTrue(_confirmButton.interactable);

            InvokeRemove(DiceType.D6);

            Assert.AreEqual(4, GetCurrentBag().Count);
            Assert.IsFalse(_confirmButton.interactable, "Confirm debe apagarse al bajar de RequiredBagSize.");
        }

        [Test]
        public void TryBuildAndStoreRequest_WithFullBag_StoresBuiltDiceBag()
        {
            Push();
            for (int i = 0; i < 5; i++)
                InvokeAdd(DiceType.D6);

            // Llamamos al metodo privado aislado de SceneManager.LoadScene para
            // poder ejercitar el "build + store" sin saltar de scene en EditMode.
            bool result = (bool)InvokePrivate(_screen, "TryBuildAndStoreRequest");

            Assert.IsTrue(result);
            Assert.IsTrue(PendingRunRequest.HasRequest);
            Assert.IsNotNull(PendingRunRequest.BuiltDiceBag, "Debe haber un BuiltDiceBag.");
            Assert.AreEqual(5, PendingRunRequest.BuiltDiceBag.Dice.Count);
            foreach (var d in PendingRunRequest.BuiltDiceBag.Dice)
                Assert.AreEqual(DiceType.D6, d);

            UnityEngine.Object.DestroyImmediate(PendingRunRequest.BuiltDiceBag);
        }

        [Test]
        public void TryBuildAndStoreRequest_WithIncompleteBag_ReturnsFalse()
        {
            Push();
            for (int i = 0; i < 3; i++)
                InvokeAdd(DiceType.D6);

            bool result = (bool)InvokePrivate(_screen, "TryBuildAndStoreRequest");

            Assert.IsFalse(result);
            Assert.IsFalse(PendingRunRequest.HasRequest, "No debe setear request con bolsa incompleta.");
        }

        [Test]
        public void HeroWithoutPool_FallsBackToLegacyMode()
        {
            // Hero sin pool — usa el flujo viejo (label fallback visible).
            _hero.DiceBagPool = null;
            Push();

            var fallback = GetPrivate<TextMeshProUGUI>(_screen, "_diceBagFallbackLabel");
            Assert.IsTrue(fallback.gameObject.activeSelf,
                "Sin pool ni StartingDiceBagRef, fallback debe estar activo.");
            Assert.AreEqual(0, GetPoolRows().Count, "No debe instanciar pool rows en legacy mode.");
            Assert.IsTrue(_confirmButton.interactable, "Legacy mode mantiene Confirm habilitado.");
        }

        // ---------------- helpers ----------------

        private void Push()
        {
            var payload = new BuildSelectionPayload
            {
                SelectedHero = _hero,
                RunId = Guid.NewGuid(),
                RulesetId = "default",
            };
            ((IBaseScreen)_screen)._Internal_OnPushed(payload);
        }

        private void InvokeAdd(DiceType type)
        {
            var method = typeof(BuildSelectionScreen).GetMethod(
                "OnAddDice", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(_screen, new object[] { type });
        }

        private void InvokeRemove(DiceType type)
        {
            var method = typeof(BuildSelectionScreen).GetMethod(
                "OnRemoveDice", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(_screen, new object[] { type });
        }

        private List<DiceType> GetCurrentBag()
        {
            var field = typeof(BuildSelectionScreen).GetField(
                "_currentBag", BindingFlags.Instance | BindingFlags.NonPublic);
            return (List<DiceType>)field.GetValue(_screen);
        }

        private List<PoolOfferingRow> GetPoolRows()
        {
            var field = typeof(BuildSelectionScreen).GetField(
                "_poolRows", BindingFlags.Instance | BindingFlags.NonPublic);
            return (List<PoolOfferingRow>)field.GetValue(_screen);
        }

        private static object InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            return method.Invoke(target, null);
        }

        private Button AttachButton(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_screenGO.transform, false);
            return go.AddComponent<Button>();
        }

        private TextMeshProUGUI AttachTMP(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_screenGO.transform, false);
            return go.AddComponent<TextMeshProUGUI>();
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            FieldInfo field = null;
            var type = target.GetType();
            while (type != null && field == null)
            {
                field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(field, $"Field '{fieldName}' not found in {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static T GetPrivate<T>(object target, string fieldName) where T : class
        {
            FieldInfo field = null;
            var type = target.GetType();
            while (type != null && field == null)
            {
                field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(field, $"Field '{fieldName}' not found in {target.GetType().Name}.");
            return field.GetValue(target) as T;
        }
    }
}
