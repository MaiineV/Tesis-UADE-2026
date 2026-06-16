using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dungeon.Components;
using Rollgeon.GameCamera;
using UnityEngine;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Tests del <see cref="FloorShellVisibilityController"/> (#158). Cubre:
    /// (a) la regresión de rematerialización tras transición de piso (GUIDs nuevos),
    /// (b) el fog of war del floor view — solo salas visitadas o vecinas conectadas a
    /// una visitada quedan visibles, y (c) el ícono opcional por sala
    /// (<see cref="FloorShell.Icon"/>) dibujado sobre el shell.
    /// </summary>
    [TestFixture]
    public class FloorShellVisibilityControllerTests
    {
        private FloorShellVisibilityController _ctrl;
        private FakeDungeon _dungeon;
        private CameraConfigSO _config;
        private Material _mat;
        private readonly List<UnityEngine.Object> _toCleanup = new();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            _dungeon = new FakeDungeon();
            _mat = new Material(Shader.Find("Sprites/Default"));
            _config = ScriptableObject.CreateInstance<CameraConfigSO>();
            _config.ShellMaterial = _mat; // evita Shader.Find de URP/Standard en EditMode.
            _ctrl = new FloorShellVisibilityController(_dungeon, _config);
        }

        [TearDown]
        public void TearDown()
        {
            _ctrl?.Dispose(); // en EditMode usa DestroyImmediate (ver DestroyObject helper).
            EventManager.ResetEventDictionary();
            var orphan = GameObject.Find("FloorShells");
            if (orphan != null) UnityEngine.Object.DestroyImmediate(orphan);
            if (_config != null) UnityEngine.Object.DestroyImmediate(_config);
            if (_mat != null) UnityEngine.Object.DestroyImmediate(_mat);
            foreach (var obj in _toCleanup)
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            _toCleanup.Clear();
            _ctrl = null;
        }

        [Test]
        public void FloorView_MaterializesShellsForCurrentFloor()
        {
            // Arrange
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            SetFloor(a, b, c);

            // Act
            EventManager.Trigger(EventName.OnCameraFloorViewToggled, true);

            // Assert
            var root = ShellsRoot();
            Assert.IsNotNull(root, "Debe crear el root 'FloorShells'.");
            Assert.AreEqual(3, root.childCount, "Un shell GameObject por sala del piso.");
        }

        [Test]
        public void FloorRegenerated_RematerializesShellsForNewFloor()
        {
            // Arrange — Piso 1.
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            SetFloor(a, b);
            EventManager.Trigger(EventName.OnCameraFloorViewToggled, true);
            Assert.IsNotNull(FindChild(ShellsRoot(), ShellName(a)), "Piso 1 materializado.");

            // Act — Transición: el dungeon regenera el piso con GUIDs nuevos y dispara
            // OnRoomEntered (lo que hace GenerateFloor).
            var x = Guid.NewGuid();
            var y = Guid.NewGuid();
            var z = Guid.NewGuid();
            SetFloor(x, y, z);
            EventManager.Trigger(EventName.OnRoomEntered, x, "room");

            // Assert
            var root = ShellsRoot();
            Assert.IsNotNull(FindChild(root, ShellName(x)), "Shell de la sala actual del piso nuevo.");
            Assert.IsNotNull(FindChild(root, ShellName(y)), "Shell del piso nuevo.");
            Assert.IsNotNull(FindChild(root, ShellName(z)), "Shell del piso nuevo.");

            // Regresión: los shells del piso viejo ya no deben existir.
            Assert.IsNull(FindChild(root, ShellName(a)), "El shell del piso viejo debe descartarse.");
            Assert.AreEqual(3, root.childCount, "Solo los shells del piso nuevo quedan materializados.");

            // El set vivo es el del piso nuevo: el vecino conectado a la sala actual está
            // visible y el de la sala actual queda oculto.
            Assert.IsTrue(FindChild(root, ShellName(y)).gameObject.activeSelf,
                "Shell vecino a la sala actual visible en floor view.");
            Assert.IsFalse(FindChild(root, ShellName(x)).gameObject.activeSelf,
                "Shell de la sala actual oculto.");
        }

        [Test]
        public void FloorView_ShowsVisitedAndAdjacent_HidesFarRooms()
        {
            // Arrange — cadena a(current/visitada) - b - c.
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            SetFloor(a, b, c);

            // Act
            EventManager.Trigger(EventName.OnCameraFloorViewToggled, true);

            // Assert
            var root = ShellsRoot();
            Assert.IsFalse(FindChild(root, ShellName(a)).gameObject.activeSelf,
                "La sala actual se muestra como prefab, no como shell.");
            Assert.IsTrue(FindChild(root, ShellName(b)).gameObject.activeSelf,
                "Vecina conectada a una visitada ⇒ descubierta ⇒ visible.");
            Assert.IsFalse(FindChild(root, ShellName(c)).gameObject.activeSelf,
                "Sala lejana (no visitada ni vecina de visitada) ⇒ oculta.");
        }

        [Test]
        public void OnRoomEntered_RevealsNewlyAdjacentRooms()
        {
            // Arrange — a(current/visitada) - b - c; con floor view abierto c está oculta.
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            SetFloor(a, b, c);
            EventManager.Trigger(EventName.OnCameraFloorViewToggled, true);
            Assert.IsFalse(FindChild(ShellsRoot(), ShellName(c)).gameObject.activeSelf,
                "Precondición: c oculta antes de avanzar.");

            // Act — el player entra a b (ahora visitada y actual).
            Visit(b);
            EventManager.Trigger(EventName.OnRoomEntered, b, "room");

            // Assert — c pasa a ser vecina de una visitada (b) ⇒ visible.
            var root = ShellsRoot();
            Assert.IsTrue(FindChild(root, ShellName(c)).gameObject.activeSelf,
                "Tras visitar b, su vecina c queda descubierta.");
            Assert.IsFalse(FindChild(root, ShellName(b)).gameObject.activeSelf,
                "b es ahora la sala actual ⇒ oculta como shell.");
        }

        [Test]
        public void MaterializeShells_WithIcon_CreatesSpriteChild()
        {
            // Arrange — la sala b lleva un ícono especial.
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            SetFloor(a, b);
            var sprite = MakeSprite();
            _dungeon.Shells[b] = new FloorShell
            {
                InstanceId = b, WorldPosition = Vector3.zero, Size = Vector3.one, Icon = sprite
            };

            // Act
            EventManager.Trigger(EventName.OnCameraFloorViewToggled, true);

            // Assert
            var icon = FindChild(ShellsRoot(), IconName(b));
            Assert.IsNotNull(icon, "El shell con Icon debe materializar un hijo ShellIcon.");
            var sr = icon.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(sr, "El ícono debe tener un SpriteRenderer.");
            Assert.AreEqual(sprite, sr.sprite, "El SpriteRenderer usa el sprite configurado.");
            Assert.IsTrue(icon.gameObject.activeSelf,
                "El ícono de una sala descubierta no-actual está visible.");
        }

        [Test]
        public void MaterializeShells_WithoutIcon_CreatesNoSpriteChild()
        {
            // Arrange
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            SetFloor(a, b); // Icon == null en ambos shells.

            // Act
            EventManager.Trigger(EventName.OnCameraFloorViewToggled, true);

            // Assert
            Assert.IsNull(FindChild(ShellsRoot(), IconName(a)), "Sin Icon ⇒ sin hijo ShellIcon.");
            Assert.IsNull(FindChild(ShellsRoot(), IconName(b)), "Sin Icon ⇒ sin hijo ShellIcon.");
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static string ShellName(Guid id) => $"Shell_{id:N}";
        private static string IconName(Guid id) => $"ShellIcon_{id:N}";

        private static Transform ShellsRoot() => GameObject.Find("FloorShells")?.transform;

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform child in root)
                if (child.name == name) return child;
            return null;
        }

        private Sprite MakeSprite()
        {
            var tex = new Texture2D(4, 4);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            _toCleanup.Add(tex);
            _toCleanup.Add(sprite);
            return sprite;
        }

        /// <summary>Marca una sala del fake como visitada y la fija como sala actual.</summary>
        private void Visit(Guid id)
        {
            var instance = _dungeon.Instances[id];
            instance.Visited = true;
            _dungeon.Current = instance;
        }

        /// <summary>
        /// Arma un piso como cadena lineal de salas (i ↔ i+1 por puerta). La primera sala
        /// queda como actual y visitada — el resto se descubre por adyacencia.
        /// </summary>
        private void SetFloor(params Guid[] ids)
        {
            var shells = new Dictionary<Guid, FloorShell>();
            var instances = new Dictionary<Guid, RoomInstance>();
            foreach (var id in ids)
            {
                shells[id] = new FloorShell { InstanceId = id, WorldPosition = Vector3.zero, Size = Vector3.one };
                instances[id] = new RoomInstance { InstanceId = id };
            }
            for (int i = 0; i < ids.Length; i++)
            {
                if (i + 1 < ids.Length) instances[ids[i]].Connections[DoorDirection.North] = ids[i + 1];
                if (i - 1 >= 0) instances[ids[i]].Connections[DoorDirection.South] = ids[i - 1];
            }
            if (ids.Length > 0) instances[ids[0]].Visited = true;

            _dungeon.Shells = shells;
            _dungeon.Instances = instances;
            _dungeon.Current = ids.Length > 0 ? instances[ids[0]] : null;
        }

        private sealed class FakeDungeon : IDungeonService
        {
            public Dictionary<Guid, FloorShell> Shells = new();
            public Dictionary<Guid, RoomInstance> Instances = new();
            public RoomInstance Current;

            public RoomSO CurrentRoom => Current?.Template;
            public RoomInstance CurrentRoomInstance => Current;
            public DoorDirection? LastEntryDirection => null;
            public void GenerateFloor(FloorLayoutSO layout, int seed) { }
            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() => Instances;
            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() => Shells;
            public bool CanEnterRoomByDoor(DoorDirection dir, out Guid id) { id = Guid.Empty; return false; }
            public bool EnterRoomByDoor(DoorDirection dir) => false;
            public bool EnterRoomByInstanceId(Guid id) => false;
            public Bounds GetFloorBounds() => default;
            public IReadOnlyList<WallOccluder> GetCurrentRoomOccluders() => Array.Empty<WallOccluder>();
        }
    }
}
