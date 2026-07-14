using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Grid
{
    public sealed class TileHighlightService : ITileHighlightService
    {
        // Tinte vía shader (PaletteCelFloor comparte el contrato _HitFlash* de
        // PaletteCelLit): en vez de pisar _BaseColor con un color plano, se lerpea
        // el resultado final del shading hacia el color del estilo — el patrón del
        // piso sigue leyéndose debajo del highlight.
        private static readonly int HitFlashColorId = Shader.PropertyToID("_HitFlashColor");
        private static readonly int HitFlashAmountId = Shader.PropertyToID("_HitFlashAmount");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private readonly Dictionary<GridCoord, Renderer> _tileRenderers;
        private readonly Dictionary<string, Color> _styleColors;
        private readonly Dictionary<string, Texture> _styleTextures;
        private readonly HashSet<GridCoord> _active = new HashSet<GridCoord>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly float _tintStrength;

        /// <summary>
        /// Crea el service con la paleta default. Los <paramref name="colorOverrides"/> y
        /// <paramref name="textureOverrides"/> pisan/agregan estilos por nombre — usados por
        /// el bootstrap para hacer configurable el color (y, opcionalmente, un sprite) de
        /// la casilla "frente a puerta" sin tener que redefinir toda la paleta.
        /// <paramref name="tintStrength"/> escala globalmente la fuerza del tinte
        /// (el alpha de cada estilo aporta la fuerza relativa por estilo).
        /// </summary>
        public TileHighlightService(
            IReadOnlyDictionary<string, Color> colorOverrides = null,
            IReadOnlyDictionary<string, Texture> textureOverrides = null,
            float tintStrength = 1f)
        {
            _tintStrength = Mathf.Clamp01(tintStrength);
            _tileRenderers = new Dictionary<GridCoord, Renderer>();
            _styleColors = new Dictionary<string, Color>
            {
                // El rango de movimiento ahora está SIEMPRE pintado en Exploración
                // (click-to-move permanente), así que se baja el alpha para que el
                // tinte permanente no sea molesto. El path A* (verde) sigue marcando
                // claramente el destino al hacer hover.
                { "move", new Color(0.3f, 0.8f, 1f, 0.28f) },
                { "attack", new Color(1f, 0.3f, 0.3f, 0.6f) },
                // Rango geométrico completo de un ataque: rojo suave, alpha bajo. Se pinta
                // sobre TODO el alcance y los enemigos targeteables quedan con "attack"
                // (rojo vívido) por encima, así el jugador ve el rango sin confundirlo con
                // los slots seleccionables.
                { "range", new Color(1f, 0.5f, 0.35f, 0.22f) },
                { "selected", new Color(1f, 0.9f, 0.2f, 0.7f) },
                // Verde brillante para el camino A* previewado durante hover. Se pinta
                // sobre el rango "move" así que tiene que distinguirse claramente.
                { "path", new Color(0.45f, 1f, 0.55f, 0.85f) },
                // Rojo para la casilla "frente a puerta" en Exploración: seleccionarla
                // cruza a la sala vecina. Configurable via el bootstrap.
                { "door", new Color(1f, 0f, 0f, 0.7f) },
                // Naranja de advertencia para el área telegráfica de los Bosses: estas
                // casillas van a recibir daño el próximo turno del Boss (Bosses §1).
                { "warning", new Color(1f, 0.55f, 0.1f, 0.65f) },
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
            // RGB del estilo = color del tinte; alpha del estilo × strength global =
            // fuerza del lerp (0 = piso intacto, 1 = color plano como antes).
            _block.SetColor(HitFlashColorId, color);
            _block.SetFloat(HitFlashAmountId, color.a * _tintStrength);
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
