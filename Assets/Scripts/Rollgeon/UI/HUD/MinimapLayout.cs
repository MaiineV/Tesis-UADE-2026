using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Math pura del layout del minimapa. Las celdas viven en posiciones FIJAS dentro
    /// del contenedor (<see cref="CellPosition"/>) y la rotación con la cámara se
    /// aplica girando el contenedor entero (<see cref="ContainerAngle"/>) — el mapa
    /// gira rígido, como una brújula, en vez de que cada celda orbite por su cuenta.
    /// </summary>
    public static class MinimapLayout
    {
        /// <summary>
        /// Posición local de la celda dentro del contenedor sin rotar:
        /// North (0,+1) arriba, East (+1,0) a la derecha.
        /// </summary>
        /// <param name="offset">Offset en celdas respecto de la sala actual.</param>
        /// <param name="pitch">Distancia centro-a-centro entre celdas, en px (celda + gap).</param>
        public static Vector2 CellPosition(Vector2Int offset, float pitch)
            => new Vector2(offset.x * pitch, offset.y * pitch);

        /// <summary>
        /// Ángulo Z (grados) del contenedor para el yaw actual de la cámara: lo que
        /// está frente a la cámara queda arriba. <paramref name="clockwise"/> y
        /// <paramref name="extraDegrees"/> son las perillas de calibración del signo
        /// y la fase (se ajustan en playtest sin tocar código).
        /// </summary>
        /// <param name="yawDegrees">Yaw actual de la cámara (eulerAngles.y).</param>
        public static float ContainerAngle(float yawDegrees, float extraDegrees, bool clockwise)
            => (clockwise ? 1f : -1f) * (yawDegrees + extraDegrees);
    }
}
