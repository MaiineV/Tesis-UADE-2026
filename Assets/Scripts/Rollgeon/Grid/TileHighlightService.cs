using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Grid
{
    public sealed class TileHighlightService : ITileHighlightService
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly Dictionary<GridCoord, Renderer> _tileRenderers;
        private readonly Dictionary<string, Color> _styleColors;
        private readonly HashSet<GridCoord> _active = new HashSet<GridCoord>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        public TileHighlightService(Dictionary<string, Color> styleColors = null)
        {
            _tileRenderers = new Dictionary<GridCoord, Renderer>();
            _styleColors = styleColors ?? new Dictionary<string, Color>
            {
                { "move", new Color(0.3f, 0.8f, 1f, 0.6f) },
                { "attack", new Color(1f, 0.3f, 0.3f, 0.6f) },
            };
        }

        public void RegisterTile(GridCoord coord, Renderer renderer)
        {
            _tileRenderers[coord] = renderer;
        }

        public void UnregisterAll()
        {
            ClearAll();
            _tileRenderers.Clear();
        }

        public void Highlight(IEnumerable<GridCoord> tiles, string style)
        {
            var color = _styleColors.TryGetValue(style, out var c) ? c : Color.yellow;
            foreach (var coord in tiles)
            {
                if (!_tileRenderers.TryGetValue(coord, out var renderer)) continue;
                _block.SetColor(ColorId, color);
                renderer.SetPropertyBlock(_block);
                _active.Add(coord);
            }
        }

        public void ClearAll()
        {
            foreach (var coord in _active)
            {
                if (_tileRenderers.TryGetValue(coord, out var renderer))
                    renderer.SetPropertyBlock(null);
            }
            _active.Clear();
        }
    }
}
