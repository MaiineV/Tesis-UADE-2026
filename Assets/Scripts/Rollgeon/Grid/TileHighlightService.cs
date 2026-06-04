using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Grid
{
    public sealed class TileHighlightService : ITileHighlightService
    {
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private readonly Dictionary<GridCoord, Renderer> _tileRenderers;
        private readonly Dictionary<string, Color> _styleColors;
        private readonly Dictionary<string, Texture> _styleTextures;
        private readonly HashSet<GridCoord> _active = new HashSet<GridCoord>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        /// <summary>
        /// Crea el service con la paleta default. Los <paramref name="colorOverrides"/> y
        /// <paramref name="textureOverrides"/> pisan/agregan estilos por nombre — usados por
        /// el bootstrap para hacer configurable el color (y, opcionalmente, un sprite) de
        /// la casilla "frente a puerta" sin tener que redefinir toda la paleta.
        /// </summary>
        public TileHighlightService(
            IReadOnlyDictionary<string, Color> colorOverrides = null,
            IReadOnlyDictionary<string, Texture> textureOverrides = null)
        {
            _tileRenderers = new Dictionary<GridCoord, Renderer>();
            _styleColors = new Dictionary<string, Color>
            {
                { "move", new Color(0.3f, 0.8f, 1f, 0.6f) },
                { "attack", new Color(1f, 0.3f, 0.3f, 0.6f) },
                { "selected", new Color(1f, 0.9f, 0.2f, 0.7f) },
                // Verde brillante para el camino A* previewado durante hover. Se pinta
                // sobre el rango "move" así que tiene que distinguirse claramente.
                { "path", new Color(0.45f, 1f, 0.55f, 0.85f) },
                // Rojo para la casilla "frente a puerta" en Exploración: seleccionarla
                // cruza a la sala vecina. Configurable via el bootstrap.
                { "door", new Color(1f, 0f, 0f, 0.7f) },
            };
            _styleTextures = new Dictionary<string, Texture>();

            if (colorOverrides != null)
                foreach (var kv in colorOverrides) _styleColors[kv.Key] = kv.Value;
            if (textureOverrides != null)
                foreach (var kv in textureOverrides) _styleTextures[kv.Key] = kv.Value;
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
            _styleTextures.TryGetValue(style, out var tex);
            int total = 0, matched = 0;
            foreach (var coord in tiles)
            {
                total++;
                if (!_tileRenderers.TryGetValue(coord, out var renderer)) continue;
                ApplyStyle(renderer, color, tex);
                _active.Add(coord);
                matched++;
            }
            Debug.Log($"[TileHighlightService] Highlight style='{style}' — {matched}/{total} tiles matched ({_tileRenderers.Count} registered renderers)");
        }

        public void HighlightSingle(GridCoord coord, string style)
        {
            if (!_tileRenderers.TryGetValue(coord, out var renderer)) return;
            var color = _styleColors.TryGetValue(style, out var c) ? c : Color.yellow;
            _styleTextures.TryGetValue(style, out var tex);
            ApplyStyle(renderer, color, tex);
            _active.Add(coord);
        }

        // Clear() antes de cada set: el _block se reusa entre llamadas, así que sin
        // limpiarlo una textura de un estilo previo (ej. "door") quedaría pegada al
        // repintar un tile con un estilo que solo define color.
        private void ApplyStyle(Renderer renderer, Color color, Texture tex)
        {
            _block.Clear();
            _block.SetColor(ColorId, color);
            if (tex != null) _block.SetTexture(BaseMapId, tex);
            renderer.SetPropertyBlock(_block);
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
