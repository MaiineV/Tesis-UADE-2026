using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.GameCamera;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Tests EditMode del <see cref="MinimapView"/>: pool de celdas, estado→sprite y
    /// rebuild por OnRoomEntered. Rig inactivo (sin Update) — asserts deterministas,
    /// espejo de <c>TurnQueueViewTests</c>.
    /// </summary>
    [TestFixture]
    public class MinimapViewTests
    {
        private GameObject _go;
        private MinimapView _view;
        private RectTransform _cellRoot;
        private MinimapSettingsSO _settings;
        private FakeDungeonService _dungeon;
        private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
        private readonly Sprite[] _sprites = new Sprite[9];

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _dungeon = new FakeDungeonService();
            ServiceLocator.AddService<IDungeonService>(_dungeon, ServiceScope.Run);

            _settings = ScriptableObject.CreateInstance<MinimapSettingsSO>();
            _createdObjects.Add(_settings);
            for (int i = 0; i < 9; i++)
            {
                var tex = new Texture2D(2, 2);
                var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
                sprite.name = $"Minimap_{i}";
                _createdObjects.Add(tex);
                _createdObjects.Add(sprite);
                _sprites[i] = sprite;
                _settings.SetCellSprite(i, sprite);
            }

            // Rig inactivo: sin Update ⇒ el layout aplicado en Rebuild queda quieto.
            _go = new GameObject("Minimap", typeof(RectTransform));
            _go.SetActive(false);
            _view = _go.AddComponent<MinimapView>();
            _cellRoot = new GameObject("Cells", typeof(RectTransform)).GetComponent<RectTransform>();
            _cellRoot.SetParent(_go.transform, false);

            AssignPrivate(_view, "_settings", _settings);
            AssignPrivate(_view, "_cellRoot", _cellRoot);
        }

        [TearDown]
        public void TearDown()
        {
            _view.Unbind();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            foreach (var obj in _createdObjects)
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            _createdObjects.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void Bind_BuildsDiscoveredCells_WithSpecSprites()
        {
            // Arrange — actual (Start, visitada) + boss adyacente sin visitar al Norte.
            var current = _dungeon.AddRoom(new Vector2Int(0, 0), RoomType.Start, visited: true, _createdObjects);
            var boss = _dungeon.AddRoom(new Vector2Int(0, 1), RoomType.Boss, visited: false, _createdObjects);
            _dungeon.Connect(boss, DoorDirection.South, current);
            _dungeon.CurrentId = current;

            // Act
            _view.Bind(Guid.NewGuid());

            // Assert — 2 celdas: actual = sprite 1, boss no-actual = sprite 7.
            var images = ActiveCellImages();
            Assert.AreEqual(2, images.Count);
            CollectionAssert.AreEquivalent(
                new[] { _sprites[1], _sprites[7] },
                images.Select(i => i.sprite).ToList());
        }

        [Test]
        public void Bind_CurrentCell_SitsAtCenter()
        {
            // Arrange
            var current = _dungeon.AddRoom(new Vector2Int(3, 2), RoomType.Combat, visited: true, _createdObjects);
            _dungeon.CurrentId = current;

            // Act
            _view.Bind(Guid.NewGuid());

            // Assert — offset (0,0) ⇒ centro del panel a cualquier yaw.
            var cell = ActiveCellImages().Single();
            Assert.AreEqual(Vector2.zero, cell.rectTransform.anchoredPosition);
        }

        [Test]
        public void OnRoomEntered_RebuildsFromDungeonState()
        {
            // Arrange — bind con una sala; después el player "entra" a la vecina.
            var a = _dungeon.AddRoom(new Vector2Int(0, 0), RoomType.Start, visited: true, _createdObjects);
            var b = _dungeon.AddRoom(new Vector2Int(1, 0), RoomType.Shop, visited: false, _createdObjects);
            _dungeon.Connect(b, DoorDirection.West, a);
            _dungeon.CurrentId = a;
            _view.Bind(Guid.NewGuid());
            Assert.AreEqual(2, ActiveCellImages().Count);

            // Act — visita la tienda y entra: el evento debe re-leer el dungeon.
            _dungeon.Rooms[b].Visited = true;
            _dungeon.CurrentId = b;
            EventManager.Trigger(EventName.OnRoomEntered, b, "shop");

            // Assert — la tienda ahora es la actual (sprite 3) y la vieja quedó visitada (2).
            var sprites = ActiveCellImages().Select(i => i.sprite).ToList();
            CollectionAssert.AreEquivalent(new[] { _sprites[2], _sprites[3] }, sprites);
        }

        [Test]
        public void Unbind_StopsReactingToRoomEntered()
        {
            // Arrange
            var a = _dungeon.AddRoom(new Vector2Int(0, 0), RoomType.Start, visited: true, _createdObjects);
            _dungeon.CurrentId = a;
            _view.Bind(Guid.NewGuid());

            // Act — tras Unbind, un evento nuevo no debe reconstruir.
            _view.Unbind();
            var b = _dungeon.AddRoom(new Vector2Int(1, 0), RoomType.Combat, visited: true, _createdObjects);
            _dungeon.CurrentId = b;
            EventManager.Trigger(EventName.OnRoomEntered, b, "combat");

            // Assert — sigue mostrando el estado del último rebuild (1 celda).
            Assert.AreEqual(1, ActiveCellImages().Count);
        }

        [Test]
        public void Rebuild_WithoutDungeonService_ShowsNothing()
        {
            // Arrange — sin IDungeonService (pre-run): cero celdas, cero excepciones.
            ServiceLocator.Clear();

            // Act
            _view.Bind(Guid.NewGuid());

            // Assert
            Assert.AreEqual(0, ActiveCellImages().Count);
        }

        // ----- Helpers ---------------------------------------------------------

        private List<Image> ActiveCellImages()
            => _cellRoot.GetComponentsInChildren<Image>(includeInactive: true)
                .Where(i => i.gameObject.activeSelf)
                .ToList();

        private static void AssignPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"Reflection layout cambió: '{field}' no encontrado.");
            f.SetValue(target, value);
        }

        // Fake mínimo: solo el grafo y la sala actual importan para el minimapa.
        private sealed class FakeDungeonService : IDungeonService
        {
            public readonly Dictionary<Guid, RoomInstance> Rooms = new Dictionary<Guid, RoomInstance>();
            public Guid CurrentId;

            public Guid AddRoom(Vector2Int cell, RoomType type, bool visited,
                List<UnityEngine.Object> createdObjects)
            {
                var template = ScriptableObject.CreateInstance<RoomSO>();
                template.Type = type;
                createdObjects.Add(template);

                var id = Guid.NewGuid();
                Rooms[id] = new RoomInstance
                {
                    InstanceId = id,
                    GridCell = cell,
                    Visited = visited,
                    Template = template,
                };
                return id;
            }

            public void Connect(Guid from, DoorDirection dir, Guid to)
                => Rooms[from].Connections[dir] = to;

            public RoomSO CurrentRoom => CurrentRoomInstance?.Template;
            public RoomInstance CurrentRoomInstance
                => Rooms.TryGetValue(CurrentId, out var room) ? room : null;
            public void GenerateFloor(FloorLayoutSO layout, int seed) { }
            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() => Rooms;
            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells()
                => new Dictionary<Guid, FloorShell>();
            public bool CanEnterRoomByDoor(DoorDirection direction, out Guid neighborInstanceId)
            {
                neighborInstanceId = Guid.Empty;
                return false;
            }
            public bool EnterRoomByDoor(DoorDirection direction) => false;
            public DoorDirection? LastEntryDirection => null;
            public bool EnterRoomByInstanceId(Guid instanceId) => false;
            public bool SetRoomState(Guid instanceId, RoomState state) => false;
            public void ResyncDoorVisuals(Guid instanceId) { }
            public Bounds GetFloorBounds() => default;
            public IReadOnlyList<WallOccluder> GetCurrentRoomOccluders()
                => Array.Empty<WallOccluder>();
        }
    }
}
