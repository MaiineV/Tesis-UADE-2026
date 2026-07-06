using UnityEngine;

namespace Rollgeon.Dice.Throw
{
    /// <summary>
    /// Matemática pura del presenter 2D (spring del grab, rebote, settle, smoothing).
    /// Separada del MonoBehaviour para testearla en EditMode sin escena.
    /// </summary>
    public static class DiceThrow2DMath
    {
        /// <summary>
        /// Paso de spring-damper hacia <paramref name="target"/> (el "agujero negro"
        /// del grab). Integración semi-implícita: primero velocidad, después posición.
        /// </summary>
        public static Vector2 SpringStep(
            Vector2 pos, ref Vector2 vel, Vector2 target,
            float stiffness, float damping, float dt)
        {
            vel += ((target - pos) * stiffness - vel * damping) * dt;
            return pos + vel * dt;
        }

        /// <summary>
        /// Paso de vuelo libre con fricción proporcional. <paramref name="drag"/> es
        /// la fracción de velocidad perdida por segundo.
        /// </summary>
        public static Vector2 FlightStep(Vector2 pos, ref Vector2 vel, float drag, float dt)
        {
            vel *= Mathf.Max(0f, 1f - drag * dt);
            return pos + vel * dt;
        }

        /// <summary>
        /// Rebote contra los bordes de <paramref name="rect"/> (espacio local del
        /// layer): clampea la posición y refleja la componente de velocidad con
        /// <paramref name="restitution"/>. Devuelve true si hubo rebote.
        /// </summary>
        public static bool BounceInRect(ref Vector2 pos, ref Vector2 vel, Rect rect,
            float halfSize, float restitution)
        {
            bool bounced = false;

            float minX = rect.xMin + halfSize, maxX = rect.xMax - halfSize;
            float minY = rect.yMin + halfSize, maxY = rect.yMax - halfSize;

            if (pos.x < minX) { pos.x = minX; vel.x = -vel.x * restitution; bounced = true; }
            else if (pos.x > maxX) { pos.x = maxX; vel.x = -vel.x * restitution; bounced = true; }

            if (pos.y < minY) { pos.y = minY; vel.y = -vel.y * restitution; bounced = true; }
            else if (pos.y > maxY) { pos.y = maxY; vel.y = -vel.y * restitution; bounced = true; }

            return bounced;
        }

        /// <summary>
        /// Suavizado exponencial de la velocidad del mouse (para que el flick lea la
        /// intención y no el jitter del último frame). <paramref name="tau"/> = constante
        /// de tiempo: ~63% de convergencia por cada tau transcurrido.
        /// </summary>
        public static Vector2 SmoothVelocity(Vector2 current, Vector2 instantaneous, float tau, float dt)
        {
            if (tau <= 0f) return instantaneous;
            float alpha = 1f - Mathf.Exp(-dt / tau);
            return Vector2.Lerp(current, instantaneous, alpha);
        }

        /// <summary>
        /// Detector de settle con histéresis: true cuando la velocidad se mantuvo bajo
        /// <paramref name="speedEps"/> durante <paramref name="holdSeconds"/> seguidos.
        /// Un pico de velocidad resetea el contador (<paramref name="heldTime"/>).
        /// </summary>
        public static bool SettleTick(float speed, float speedEps, float holdSeconds,
            float dt, ref float heldTime)
        {
            if (speed >= speedEps)
            {
                heldTime = 0f;
                return false;
            }
            heldTime += dt;
            return heldTime >= holdSeconds;
        }
    }
}
