using System.Collections.Generic;
using UnityEngine;
using Rollgeon.Grid;

namespace Rollgeon.Editor.Tools.RoomEditor
{
    [CreateAssetMenu(
        fileName = "TilePainterPalette",
        menuName = "Rollgeon/Tools/Tile Painter Palette",
        order = 100)]
    public sealed class TilePainterPaletteSO : ScriptableObject
    {
        public string DisplayName = "Untitled Palette";

        [Tooltip("Default tile footprint in world units (X, Y, Z). The tool can override this per session.")]
        public Vector3 DefaultTileSize = Vector3.one;

        public List<TilePainterPaletteEntry> Entries = new();
    }

    [System.Serializable]
    public sealed class TilePainterPaletteEntry
    {
        public string Label;
        public GameObject Prefab;
        public Texture2D Icon;
        public TileType Type = TileType.Floor;

        [Tooltip("Blocker entries obstruct NavGraph edges and prevent placing another blocker on the same cell. Leave off for floors, decorations and other stackable tiles.")]
        public bool IsBlocker;
    }
}
