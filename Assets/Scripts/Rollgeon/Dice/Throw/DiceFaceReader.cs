using UnityEngine;

namespace Rollgeon.Dice.Throw
{
    /// <summary>
    /// Lectura de la cara superior de un d6 físico a partir de su rotación. Pura
    /// (sin estado de escena) para testearla en EditMode con orientaciones canónicas.
    /// </summary>
    /// <remarks>
    /// Mapeo estándar del prefab <c>DiceThrow3D_Die</c> (caras opuestas suman 7):
    /// +Y=1, −Y=6, +X=3, −X=4, +Z=2, −Z=5. La cara "arriba" es la normal local con
    /// mayor dot contra <see cref="Vector3.up"/> tras aplicar la rotación del cubo.
    /// </remarks>
    public static class DiceFaceReader
    {
        /// <summary>Mapeo normal-local → valor de cara del d6 placeholder.</summary>
        public static readonly (Vector3 normal, int face)[] StandardD6 =
        {
            (Vector3.up, 1),
            (Vector3.down, 6),
            (Vector3.right, 3),
            (Vector3.left, 4),
            (Vector3.forward, 2),
            (Vector3.back, 5),
        };

        /// <summary>
        /// Devuelve la cara mirando hacia arriba para <paramref name="rotation"/>.
        /// <paramref name="bestDot"/> = qué tan "plana" quedó (1 = perfectamente
        /// horizontal; menos que el umbral de cocked = dado de canto).
        /// </summary>
        public static int ReadTopFace(Quaternion rotation, out float bestDot)
        {
            int best = 1;
            bestDot = float.MinValue;
            foreach (var (normal, face) in StandardD6)
            {
                float dot = Vector3.Dot(rotation * normal, Vector3.up);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = face;
                }
            }
            return best;
        }

        /// <summary>true si el dado quedó de canto (ninguna cara suficientemente plana).</summary>
        public static bool IsCocked(float bestDot, float threshold) => bestDot < threshold;

        /// <summary>
        /// Rotación más cercana a <paramref name="current"/> que deja la cara ya
        /// dominante perfectamente plana — para el snap-rotate del timeout (el dado
        /// nunca cuelga la resolución) y para imponer caras riggeadas.
        /// </summary>
        public static Quaternion SnapToNearestFace(Quaternion current)
        {
            ReadTopFace(current, out _);
            // Alinear la normal dominante con up manteniendo el yaw actual: rotación
            // delta mínima que lleva la normal-mundo de la cara dominante a Vector3.up.
            var (normal, _) = DominantFace(current);
            var worldNormal = current * normal;
            var delta = Quaternion.FromToRotation(worldNormal, Vector3.up);
            return delta * current;
        }

        private static (Vector3 normal, int face) DominantFace(Quaternion rotation)
        {
            var best = StandardD6[0];
            float bestDot = float.MinValue;
            foreach (var entry in StandardD6)
            {
                float dot = Vector3.Dot(rotation * entry.normal, Vector3.up);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = entry;
                }
            }
            return best;
        }
    }
}
