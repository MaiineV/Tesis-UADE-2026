using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Configuración del minimapa estilo Isaac: los 9 sprites del sheet
    /// <c>Assets/Art/UI/Minimap/Minimap.png</c> indexados por
    /// <see cref="MinimapSpriteMap"/> (0..8) + las perillas de layout/rotación.
    /// Los sprites los asigna el installer (Rollgeon → Minimap → 1 - Create Settings).
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/UI/Minimap Settings", fileName = "MinimapSettings")]
    public class MinimapSettingsSO : ScriptableObject
    {
        [Title("Sprites (slices Minimap_0..8 — los asigna el installer)")]
        [Tooltip("Indexados por MinimapSpriteMap.Resolve: 0 adyacente sin visitar, 1 actual, " +
                 "2 visitada, 3/6 tienda actual/no, 4/5 encantamientos actual/no, 8/7 boss actual/no.")]
        [SerializeField]
        private Sprite[] _cellSprites = new Sprite[9];

        [Title("Layout")]
        [Tooltip("Lado de cada celda en px.")]
        [MinValue(4f)]
        public float CellSize = 32f;

        [Tooltip("Separación entre celdas en px.")]
        [MinValue(0f)]
        public float CellGap = 3f;

        [Title("Rotación (calibración contra la cámara)")]
        [Tooltip("Fase extra en grados sumada al yaw de la cámara (ajuste fino de orientación).")]
        public float ExtraYawDegrees = 0f;

        [Tooltip("Sentido de la rotación del mapa respecto del yaw. Si al rotar la cámara " +
                 "el mapa gira al revés, apagar esto.")]
        public bool Clockwise = true;

        /// <summary>Distancia centro-a-centro entre celdas.</summary>
        public float Pitch => CellSize + CellGap;

        /// <summary>Sprite para el índice 0..8 de <see cref="MinimapSpriteMap"/>. Null-safe.</summary>
        public Sprite CellSprite(int index)
            => _cellSprites != null && index >= 0 && index < _cellSprites.Length
                ? _cellSprites[index]
                : null;

        /// <summary>Asignación por índice para el installer/tests.</summary>
        public void SetCellSprite(int index, Sprite sprite)
        {
            if (_cellSprites == null || _cellSprites.Length < 9) _cellSprites = new Sprite[9];
            if (index >= 0 && index < _cellSprites.Length) _cellSprites[index] = sprite;
        }
    }
}
