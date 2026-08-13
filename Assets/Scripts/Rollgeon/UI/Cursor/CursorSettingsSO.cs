using UnityEngine;

namespace Rollgeon.UI.Cursor
{
    /// <summary>
    /// Config del cursor custom: las 4 texturas de cursor (indexadas por
    /// <see cref="CursorState"/>), hotspot y raycast de mundo. El bootstrap lo
    /// carga desde <c>Resources/Cursor/CursorSettings</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/UI/Cursor Settings", fileName = "CursorSettings")]
    public class CursorSettingsSO : ScriptableObject
    {
        [Tooltip("Texturas standalone (import type Cursor) en orden Default(0), " +
                 "ClickEmpty(1), Hover(2), ClickHover(3). Las bakea 'Rollgeon → Cursor → Setup' " +
                 "desde el sheet, ya escaladas.")]
        public Texture2D[] StateCursors = new Texture2D[4];

        [Tooltip("Escala de bake (pixel-art: usar enteros). 2 = doble tamaño nativo. " +
                 "Se aplica al generar las texturas — cambiarla requiere re-correr el Setup.")]
        public float Scale = 2f;

        [Tooltip("Punto de click dentro del sprite, normalizado (0,0=abajo-izq, 1,1=arriba-der). " +
                 "Para una flecha, la punta suele estar arriba-izquierda.")]
        public Vector2 HotspotPivot = new Vector2(0.1f, 0.9f);

        [Tooltip("Alcance del raycast al mundo para detectar enemigos/interactuables.")]
        public float WorldRaycastDistance = 100f;

        public Texture2D CursorFor(CursorState state)
        {
            int i = (int)state;
            return StateCursors != null && i >= 0 && i < StateCursors.Length ? StateCursors[i] : null;
        }
    }
}
