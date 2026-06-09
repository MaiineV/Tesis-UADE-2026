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
    /// Tests del <see cref="FloorShellVisibilityController"/> (#158). El foco es la
    /// regresión: tras una transición de piso (<c>GenerateFloor</c> regenera el grafo
    /// con GUIDs nuevos), los shells del floor view deben REMATERIALIZARSE al set del
    /// piso nuevo — antes el guard <c>_shellGOs.Count &gt; 0</c> los dejaba congelados
    /// en el piso anterior.
    /// </summary>
    [TestFixture]
    public class FloorShellVisibilityControllerTests
    {
        private FloorShellVisibilityController _ctrl;
        private FakeDungeon _dungeon;
        private CameraConfigSO _config;
        private Material _mat;

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
            _ctrl = null;
        }

        [Test]
        public void FloorView_MaterializesShellsForCurrentFloor()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            SetFloor(a, b, c);

            EventManager.Trigger(EventName.OnCameraFloorViewToggled, true);

            var root = ShellsRoot();
            Assert.IsNotNull(root, "Debe crear el root 'FloorShells'.");
            Assert.AreEqual(3, root.childCount, "Un shell GameObject por sala del piso.");
        }

        [Test]
        public void FloorRegenerated_RematerializesShellsForNewFloor()
        {
            // Piso 1.
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            SetFloor(a, b);
            EventManager.Trigger(EventName.OnCameraFloorViewToggled, true);
            Assert.IsNotNull(FindChild(ShellsRoot(), ShellName(a)), "Piso 1 materializado.");

            // Transición: el dungeon regenera el piso con GUIDs nuevos y dispara
            // OnRoomEntered (lo que hace GenerateFloor).
            var x = Guid.NewGuid();
            var y = Guid.NewGuid();
            var z = Guid.NewGuid();
            SetFloor(x, y, z);
            EventManager.Trigger(EventName.OnRoomEntered, x, "room");

            var root = ShellsRoot();
            Assert.IsNotNull(FindChild(root, ShellName(x)), "Shell de la sala actual del piso nuevo.");
            Assert.IsNotNull(FindChild(root, ShellName(y)), "Shell del piso nuevo.");
            Assert.IsNotNull(FindChild(root, ShellName(z)), "Shell del piso nuevo.");

            // Regresión: los shells del piso viejo ya no deben existir.
            Assert.IsNull(FindChild(root, ShellName(a)), "El shell del piso viejo debe descartarse.");
            Assert.AreEqual(3, root.childCount, "Solo los shells del piso nuevo quedan materializados.");

            // El set vivo es el del piso nuevo: un shell no-current está visible y el
            // de la sala actual queda oculto.
            Assert.IsTrue(FindChild(root, ShellName(y)).gameObject.activeSelf,
                "Shell nuevo no-current visible en floor view.");
            Assert.IsFalse(FindChild(root, ShellName(x)).gameObject.activeSelf,
                "Shell de la sala actual oculto.");
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static string ShellName(Guid id) => $"Shell_{id:N}";

        private static Transform ShellsRoot() => GameObject.Find("FloorShells")?.transform;

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform child in root)
                if (child.name == name) return child;
            return null;
        }

        private void SetFloor(params Guid[] ids)
        {
            var shells = new Dictionary<Guid, FloorShell>();
            foreach (var id in ids)
                shells[id] = new FloorShell { InstanceId = id, WorldPosition = Vector3.zero, Size = Vector3.one };
            _dungeon.Shells = shells;
            _dungeon.Current = ids.Length > 0 ? new RoomInstance { InstanceId = ids[0] } : null;
        }

        private sealed class FakeDungeon : IDungeonService
        {
            public IReadOnlyDictionary<Guid, FloorShell> Shells = new Dictionary<Guid, FloorShell>();
            public RoomInstance Current;

            public RoomSO CurrentRoom => Current?.Template;
            public RoomInstance CurrentRoomInstance => Current;
            public DoorDirection? LastEntryDirection => null;
            public void GenerateFloor(FloorLayoutSO layout, int seed) { }
            public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() => new Dictionary<Guid, RoomInstance>();
            public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() => Shells;
            public bool CanEnterRoomByDoor(DoorDirection dir, out Guid id) { id = Guid.Empty; return false; }
            public bool EnterRoomByDoor(DoorDirection dir) => false;
            public bool EnterRoomByInstanceId(Guid id) => false;
            public Bounds GetFloorBounds() => default;
            public IReadOnlyList<WallOccluder> GetCurrentRoomOccluders() => Array.Empty<WallOccluder>();
        }
    }
}
