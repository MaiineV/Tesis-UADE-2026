using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.UI.HUD;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// EditMode tests para <see cref="RoomEnterToastView"/> (Feature#0086). Sin Play
    /// Mode el toast toma el camino sin tweens: aparece en el acto con los textos de
    /// la sala actual. El slide real se valida en smoke.
    /// </summary>
    [TestFixture]
    public class RoomEnterToastViewTests
    {
        private sealed class StubDungeonService : IDungeonService
        {
            public RoomSO CurrentRoom { get; set; }
            public RoomInstance CurrentRoomInstance => null;
            public DoorDirection? LastEntryDirection => null;

            public void GenerateFloor(FloorLayoutSO layout, int seed) { }
            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() => new Dictionary<Guid, RoomInstance>();
            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() => new Dictionary<Guid, FloorShell>();
            public bool CanEnterRoomByDoor(DoorDirection dir, out Guid id) { id = Guid.Empty; return false; }
            public bool EnterRoomByDoor(DoorDirection dir) => false;
            public bool EnterRoomByInstanceId(Guid id) => false;
            public bool SetRoomState(Guid id, RoomState state) => false;
            public void ResyncDoorVisuals(Guid id) { }
            public Bounds GetFloorBounds() => default;
            public IReadOnlyList<Rollgeon.GameCamera.WallOccluder> GetCurrentRoomOccluders() =>
                Array.Empty<Rollgeon.GameCamera.WallOccluder>();
        }

        private readonly List<UnityEngine.Object> _created = new();
        private StubDungeonService _dungeon;
        private RoomEnterToastView _view;
        private RectTransform _panel;
        private CanvasGroup _group;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _body;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _dungeon = new StubDungeonService();
            ServiceLocator.AddService<IDungeonService>(_dungeon, ServiceScope.Run);

            var root = new GameObject("ToastCanvas", typeof(RectTransform));
            _created.Add(root);
            root.SetActive(false);
            _view = root.AddComponent<RoomEnterToastView>();

            var panelGO = new GameObject("RoomToast", typeof(RectTransform), typeof(CanvasGroup));
            panelGO.transform.SetParent(root.transform, false);
            _panel = panelGO.GetComponent<RectTransform>();
            _panel.sizeDelta = new Vector2(380f, 90f);
            _group = panelGO.GetComponent<CanvasGroup>();
            _title = CreateLabel(panelGO.transform, "Title");
            _body = CreateLabel(panelGO.transform, "Body");

            Assign("_panelRoot", _panel);
            Assign("_canvasGroup", _group);
            Assign("_titleLabel", _title);
            Assign("_bodyLabel", _body);

            root.SetActive(true);
            // EditMode no dispara los callbacks de lifecycle de Unity: la suscripción al
            // evento y el estado inicial oculto viven en OnEnable.
            InvokePrivate("OnEnable");
        }

        private void InvokePrivate(string method)
        {
            var m = typeof(RoomEnterToastView).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(m, $"Método '{method}' no encontrado.");
            m.Invoke(_view, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_view != null) InvokePrivate("OnDisable");
            foreach (var obj in _created)
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            _created.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void OnEnable_PanelStartsHiddenBelowTheEdge()
        {
            Assert.IsFalse(_panel.gameObject.activeSelf);
            Assert.Less(_panel.anchoredPosition.y, 0f, "oculto = fuera de pantalla por abajo");
            Assert.AreEqual(0f, _group.alpha);
        }

        [Test]
        public void OnRoomCrossed_ShowsTypeAndName()
        {
            _dungeon.CurrentRoom = CreateRoom("combat_3", "Sala de las Ratas", RoomType.Combat);

            EventManager.Trigger(EventName.OnRoomCrossed, Guid.NewGuid(), Guid.NewGuid());

            Assert.IsTrue(_panel.gameObject.activeSelf);
            Assert.AreEqual(RoomTypeText.Localized(RoomType.Combat), _title.text);
            Assert.AreEqual("Sala de las Ratas", _body.text);
            Assert.GreaterOrEqual(_panel.anchoredPosition.y, 0f, "visible = sobre el borde inferior");
            Assert.AreEqual(1f, _group.alpha);
        }

        [Test]
        public void OnRoomCrossed_Twice_UpdatesText()
        {
            _dungeon.CurrentRoom = CreateRoom("shop_1", "Tienda del Duende", RoomType.Shop);
            EventManager.Trigger(EventName.OnRoomCrossed, Guid.NewGuid(), Guid.NewGuid());

            _dungeon.CurrentRoom = CreateRoom("boss_1", "Boss", RoomType.Boss);
            EventManager.Trigger(EventName.OnRoomCrossed, Guid.NewGuid(), Guid.NewGuid());

            Assert.IsTrue(_panel.gameObject.activeSelf);
            Assert.AreEqual(RoomTypeText.Localized(RoomType.Boss), _title.text);
            Assert.AreEqual("Boss", _body.text);
        }

        [Test]
        public void OnRoomCrossed_WithoutCurrentRoom_StaysHidden()
        {
            _dungeon.CurrentRoom = null;

            EventManager.Trigger(EventName.OnRoomCrossed, Guid.NewGuid(), Guid.NewGuid());

            Assert.IsFalse(_panel.gameObject.activeSelf);
        }

        [Test]
        public void OnRoomEntered_DoesNotShow()
        {
            // La primera sala del piso y el resume de save entran por OnRoomEntered sin
            // cruce: no hay nada que anunciar.
            _dungeon.CurrentRoom = CreateRoom("start", "Inicio", RoomType.Start);

            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "start");

            Assert.IsFalse(_panel.gameObject.activeSelf);
        }

        private RoomSO CreateRoom(string id, string name, RoomType type)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = id;
            room.DisplayName = name;
            room.Type = type;
            _created.Add(room);
            return room;
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<TextMeshProUGUI>();
        }

        private void Assign(string field, object value)
        {
            var f = typeof(RoomEnterToastView).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"Field '{field}' no encontrado.");
            f.SetValue(_view, value);
        }
    }
}
