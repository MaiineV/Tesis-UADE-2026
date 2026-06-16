using System.Collections.Generic;
using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Grid
{
    [CreateAssetMenu(menuName = "Rollgeon/Grid/Tile Highlight Service Bootstrap",
        fileName = "TileHighlightServiceBootstrap")]
    public sealed class TileHighlightServiceBootstrap : ScriptableObject, IPreloadableService
    {
        [Header("Casilla \"frente a puerta\" (Exploración — pass-door)")]
        [Tooltip("Color de la casilla que, al seleccionarla durante el movimiento, " +
                 "cruza a la sala vecina. Por defecto rojo.")]
        [SerializeField] private Color _doorTileColor = Color.red;

        [Tooltip("Textura/sprite opcional para la casilla de puerta. Si se asigna, se " +
                 "aplica sobre el tile (shader _BaseMap) además del color. Null = solo color.")]
        [SerializeField] private Texture2D _doorTileSprite;

        public int Priority => 76;
        public ServiceScope Scope => ServiceScope.Run;

        public void Register()
        {
            var colorOverrides = new Dictionary<string, Color> { { "door", _doorTileColor } };
            var textureOverrides = new Dictionary<string, Texture>();
            if (_doorTileSprite != null) textureOverrides["door"] = _doorTileSprite;

            var instance = new TileHighlightService(colorOverrides, textureOverrides);
            ServiceLocator.AddService<ITileHighlightService>(instance, ServiceScope.Run);
        }
    }
}
