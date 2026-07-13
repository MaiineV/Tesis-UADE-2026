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
            return BounceInRect(ref pos, ref vel, rect, halfSize, restitution, out _);
        }

        /// <summary>
        /// Variante que además devuelve la normal del impacto (hacia adentro del rect,
        /// diagonal normalizada si pegó en una esquina) — para orientar partículas.
        /// </summary>
        public static bool BounceInRect(ref Vector2 pos, ref Vector2 vel, Rect rect,
            float halfSize, float restitution, out Vector2 normal)
        {
            normal = Vector2.zero;

            float minX = rect.xMin + halfSize, maxX = rect.xMax - halfSize;
            float minY = rect.yMin + halfSize, maxY = rect.yMax - halfSize;

            if (pos.x < minX) { pos.x = minX; vel.x = -vel.x * restitution; normal.x = 1f; }
            else if (pos.x > maxX) { pos.x = maxX; vel.x = -vel.x * restitution; normal.x = -1f; }

            if (pos.y < minY) { pos.y = minY; vel.y = -vel.y * restitution; normal.y = 1f; }
            else if (pos.y > maxY) { pos.y = maxY; vel.y = -vel.y * restitution; normal.y = -1f; }

            if (normal == Vector2.zero) return false;
            normal.Normalize();
            return true;
        }

        /// <summary>
        /// Intención de drag de un press sostenido: true cuando el cursor se alejó más
        /// del slop o el botón lleva apretado más que la ventana de click. Antes de eso
        /// el press todavía puede ser un click (soltar = seleccionar, no agarrar).
        /// </summary>
        public static bool DragIntent(Vector2 pressPos, Vector2 currentPos, float heldSeconds,
            float slopPixels, float clickSeconds)
        {
            if ((currentPos - pressPos).sqrMagnitude > slopPixels * slopPixels) return true;
            return heldSeconds >= clickSeconds;
        }

        /// <summary>
        /// Colisión círculo-círculo entre dos dados EN VUELO (masas iguales): separa la
        /// superposición mitad y mitad e intercambia las componentes normales de
        /// velocidad escaladas por <paramref name="restitution"/>, solo si se acercan.
        /// Devuelve la velocidad relativa de aproximación (0 = sin contacto/alejándose)
        /// para escalar el juice.
        /// </summary>
        public static float ResolveDiePair(ref Vector2 posA, ref Vector2 velA,
            ref Vector2 posB, ref Vector2 velB, float radius, float restitution)
        {
            if (!Overlap(posA, posB, radius, out var normal, out float overlap)) return 0f;

            posA -= normal * (overlap * 0.5f);
            posB += normal * (overlap * 0.5f);

            float approach = Vector2.Dot(velA - velB, normal);
            if (approach <= 0f) return 0f;

            float impulse = (1f + restitution) * 0.5f * approach;
            velA -= normal * impulse;
            velB += normal * impulse;
            return approach;
        }

        /// <summary>
        /// Dado en vuelo contra dado quieto (asentado o esperando en su spot): el que
        /// vuela cede la superposición y rebota (masa "pesada" del quieto, escalado por
        /// <paramref name="restitution"/>); el quieto recibe un empujón posicional
        /// proporcional a la velocidad de aproximación, acotado por
        /// <paramref name="maxShove"/>. Devuelve la velocidad de aproximación.
        /// </summary>
        public static float ResolveDieStatic(ref Vector2 posFly, ref Vector2 velFly,
            ref Vector2 posStatic, float radius, float restitution,
            float shovePerSpeed, float maxShove)
        {
            if (!Overlap(posFly, posStatic, radius, out var normal, out float overlap)) return 0f;

            posFly -= normal * overlap;

            float approach = Vector2.Dot(velFly, normal);
            if (approach <= 0f) return 0f;

            velFly -= normal * ((1f + restitution) * approach);
            posStatic += normal * Mathf.Min(approach * shovePerSpeed, maxShove);
            return approach;
        }

        /// <summary>
        /// Separación simétrica de dos dados quietos superpuestos (sin velocidades).
        /// Devuelve true si hizo falta separar.
        /// </summary>
        public static bool SeparateOverlap(ref Vector2 posA, ref Vector2 posB, float radius)
        {
            if (!Overlap(posA, posB, radius, out var normal, out float overlap)) return false;
            posA -= normal * (overlap * 0.5f);
            posB += normal * (overlap * 0.5f);
            return true;
        }

        // Normal A→B y profundidad de la superposición de dos círculos de igual radio.
        private static bool Overlap(Vector2 posA, Vector2 posB, float radius,
            out Vector2 normal, out float overlap)
        {
            var delta = posB - posA;
            float minDist = radius * 2f;
            float distSq = delta.sqrMagnitude;
            if (distSq >= minDist * minDist)
            {
                normal = Vector2.zero;
                overlap = 0f;
                return false;
            }

            float dist = Mathf.Sqrt(distSq);
            // Dados exactamente superpuestos (spawn raro): separar por un eje fijo.
            normal = dist > 0.0001f ? delta / dist : Vector2.right;
            overlap = minDist - dist;
            return true;
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
