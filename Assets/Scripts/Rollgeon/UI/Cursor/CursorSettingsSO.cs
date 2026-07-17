using UnityEngine;

namespace Rollgeon.UI.Cursor
{
    /// <summary>
    /// Config del cursor custom: los 4 sprites (indexados por
    /// <see cref="CursorState"/>), escala y hotspot. El bootstrap lo carga desde
    /// <c>Resources/Cursor/CursorSettings</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/UI/Cursor Settings", fileName = "CursorSettings")]
    public class CursorSettingsSO : ScriptableObject
    {
        [Tooltip("Sprites del sheet en orden Default(0), ClickEmpty(1), Hover(2), ClickHover(3).")]
        public Sprite[] StateSprites = new Sprite[4];

        [Tooltip("Escala del cursor (pixel-art: usar enteros). 2 = doble tamaño nativo.")]
        public float Scale = 2f;

        [Tooltip("Punto de click dentro del sprite, normalizado (0,0=abajo-izq, 1,1=arriba-der). " +
                 "Para una flecha, la punta suele estar arriba-izquierda.")]
        public Vector2 HotspotPivot = new Vector2(0.1f, 0.9f);

        [Tooltip("Alcance del raycast al mundo para detectar enemigos/interactuables.")]
        public float WorldRaycastDistance = 100f;

        public Sprite SpriteFor(CursorState state)
        {
            int i = (int)state;
            return StateSprites != null && i >= 0 && i < StateSprites.Length ? StateSprites[i] : null;
        }
    }
}
