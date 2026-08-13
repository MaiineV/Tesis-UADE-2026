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

        [Header("Paleta de estilos (RGB = color del tinte, Alpha = fuerza por estilo)")]
        [Tooltip("Rango de movimiento / celdas válidas de una acción. Alpha bajo: en " +
                 "Exploración el rango está SIEMPRE pintado (perma-move).")]
        [SerializeField] private Color _moveColor = new Color(0.3f, 0.8f, 1f, 0.28f);

        [Tooltip("Celdas de enemigos targeteables por un ataque.")]
        [SerializeField] private Color _attackColor = new Color(1f, 0.3f, 0.3f, 0.6f);

        [Tooltip("Rango geométrico completo de un ataque (rojo suave). Se pinta bajo las " +
                 "celdas seleccionables, que llevan el color de ataque por encima.")]
        [SerializeField] private Color _rangeColor = new Color(1f, 0.5f, 0.35f, 0.22f);

        [Tooltip("Celda seleccionada / hovereada por el drag (snap).")]
        [SerializeField] private Color _selectedColor = new Color(1f, 0.9f, 0.2f, 0.7f);

        [Tooltip("Preview del camino A* durante hover de movimiento.")]
        [SerializeField] private Color _pathColor = new Color(0.45f, 1f, 0.55f, 0.85f);

        [Tooltip("Área telegráfica de los Bosses (daño entrante el próximo turno).")]
        [SerializeField] private Color _warningColor = new Color(1f, 0.55f, 0.1f, 0.65f);

        [Tooltip("Área AoE previewada al hovear/elegir un ancla (TargetMode.Aoe).")]
        [SerializeField] private Color _aoeColor = new Color(1f, 0.6f, 0.1f, 0.55f);

        [Header("Tinte (shader _HitFlash* de PaletteCelFloor)")]
        [Tooltip("Fuerza global del tinte de highlight sobre los tiles. Escala el alpha " +
                 "de cada estilo: 1 = como autoría, hacia 0 = más sutil (se ve más piso).")]
        [SerializeField, Range(0f, 1f)] private float _tintStrength = 0.85f;

        public int Priority => 76;
        public ServiceScope Scope => ServiceScope.Run;

        public void Register()
        {
            var colorOverrides = new Dictionary<string, Color>
            {
                { "move", _moveColor },
                { "attack", _attackColor },
                { "range", _rangeColor },
                { "selected", _selectedColor },
                { "path", _pathColor },
                { "warning", _warningColor },
                { "aoe", _aoeColor },
                { "door", _doorTileColor },
            };
            var textureOverrides = new Dictionary<string, Texture>();
            if (_doorTileSprite != null) textureOverrides["door"] = _doorTileSprite;

            var instance = new TileHighlightService(colorOverrides, textureOverrides, _tintStrength);
            ServiceLocator.AddService<ITileHighlightService>(instance, ServiceScope.Run);
        }
    }
}
