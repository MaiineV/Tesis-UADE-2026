using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dungeon.Components;
using UnityEngine;

namespace Rollgeon.Grid.Tests
{
    [TestFixture]
    public sealed class TileRendererRegistrarTests
    {
        private GameObject _root;
        private RoomLayout _layout;
        private TileHighlightService _highlight;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Room");
            _layout = _root.AddComponent<RoomLayout>();
            _highlight = new TileHighlightService();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        private GameObject AddMarkerTile(int x, int z, TileType type, bool rendererOnChild)
        {
            GameObject tile;
            GameObject meshGo;
            if (rendererOnChild)
            {
                tile = new GameObject($"Tile_{x}_{z}");
                tile.transform.SetParent(_root.transform, false);
                meshGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                meshGo.transform.SetParent(tile.transform, false);
            }
            else
            {
                tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.transform.SetParent(_root.transform, false);
                meshGo = tile;
            }

            tile.transform.localPosition = new Vector3(x + 0.5f, 0f, z + 0.5f);
            var marker = tile.AddComponent<TileMarker>();
            marker.Coord = new GridCoord(x, z);
            marker.Type = type;
            return meshGo;
        }

        private static bool IsPainted(GameObject meshGo)
            => meshGo.GetComponent<Renderer>().HasPropertyBlock();

        [Test]
        public void RegisterRoomTiles_RendererOnChild_TileIsPaintable()
        {
            // Arrange — el patrón de Pedestal/TileWall/CornerUp: marker en el
            // root del tile, mesh en un hijo.
            var mesh = AddMarkerTile(2, 3, TileType.Floor, rendererOnChild: true);

            // Act
            TileRendererRegistrar.RegisterRoomTiles(_root, _layout, _highlight);
            _highlight.Highlight(new List<GridCoord> { new GridCoord(2, 3) }, "move");

            // Assert
            Assert.IsTrue(IsPainted(mesh),
                "El tile con renderer en un hijo debe quedar registrado y pintable.");
        }

        [Test]
        public void RegisterRoomTiles_DecorationStackedOnFloor_FloorRendererWins()
        {
            // Arrange — el piso primero en la jerarquía, la decoración después
            // (el caso donde el last-wins viejo le robaba el slot al piso).
            var floorMesh = AddMarkerTile(1, 1, TileType.Floor, rendererOnChild: false);
            var decoMesh = AddMarkerTile(1, 1, TileType.Decoration, rendererOnChild: false);

            // Act
            TileRendererRegistrar.RegisterRoomTiles(_root, _layout, _highlight);
            _highlight.Highlight(new List<GridCoord> { new GridCoord(1, 1) }, "move");

            // Assert
            Assert.IsTrue(IsPainted(floorMesh), "Debe pintarse el piso, no la decoración.");
            Assert.IsFalse(IsPainted(decoMesh), "La decoración no debe robarse el slot del coord.");
        }

        [Test]
        public void RegisterRoomTiles_LegacyMeshWithoutMarker_RegistersInferredCoord()
        {
            // Arrange — paridad con NavGraphBaker: un mesh sin TileMarker genera
            // nodo caminable en el bake con coord inferida por posición; también
            // tiene que ser pintable.
            var legacy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            legacy.transform.SetParent(_root.transform, false);
            legacy.transform.localPosition = new Vector3(2.5f, 0f, 0.5f); // celda (2,0)

            // Act
            TileRendererRegistrar.RegisterRoomTiles(_root, _layout, _highlight);
            _highlight.Highlight(new List<GridCoord> { new GridCoord(2, 0) }, "move");

            // Assert
            Assert.IsTrue(IsPainted(legacy),
                "El mesh legacy debe registrarse bajo la coord que infiere el baker.");
        }
    }
}
