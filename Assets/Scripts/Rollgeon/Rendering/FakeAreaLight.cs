using UnityEngine;

namespace Rollgeon.Rendering
{
    /// <summary>
    /// Prop que "ilumina" sus alrededores SIN ser una Light real de Unity — pensado
    /// para props emisivos (tira LED, vidriera de un hechizo, etc.) que necesitan que
    /// el banding de <c>PaletteCel*</c>/<c>Cel*</c> reaccione a su forma, sin gastar
    /// presupuesto de <see cref="UnityEngine.Light"/> (URP limita a 4 luces
    /// adicionales reales por objeto, <c>m_AdditionalLightsPerObjectLimit</c>).
    ///
    /// La forma es una POLILÍNEA de hasta <see cref="MaxPoints"/> puntos — se toman
    /// los hijos DIRECTOS de este transform, en el orden de la Hierarchy, como puntos
    /// de control. Para una tira recta alcanza con 2 hijos en las puntas; para que
    /// siga una mesh curva, agregá hijos intermedios acomodados a lo largo de la
    /// curva (3-4 alcanza para una curva suave — no hace falta 1 hijo por vértice).
    /// Sin hijos, se comporta como un point light falso en la posición del objeto.
    ///
    /// Se registra en <see cref="FakeAreaLightManager"/> mientras está habilitado; el
    /// manager sube hasta <see cref="FakeAreaLightManager.MaxLights"/> instancias como
    /// globals cada frame. Los shaders de la familia PaletteCel* buscan el punto más
    /// cercano de TODA la polilínea a cada píxel y lo tratan como si fuera la posición
    /// de una luz real para el banding NdotL — ver <c>FakeAreaLightContribution()</c>
    /// en PaletteCelLit.shader.
    /// </summary>
    [AddComponentMenu("Rollgeon/Lighting/Fake Area Light")]
    public class FakeAreaLight : MonoBehaviour
    {
        /// <summary>Debe matchear el packing de FakeAreaLightManager y el tamaño de
        /// _FakeAreaLightData en los shaders (5 float4 por luz, 4 slots de punto).</summary>
        public const int MaxPoints = 4;

        [Header("Luz falsa")]
        public Color Color = Color.white;
        [Tooltip("Multiplica cuánto empuja el banding hacia Mid/Light. 1 ~ una luz adicional normal.")]
        [Range(0f, 8f)] public float Intensity = 1.5f;
        [Tooltip("Distancia (unidades de mundo) a la que la contribución cae a 0.")]
        [Min(0.01f)] public float Range = 4f;

        // Buffer reusado entre llamadas — evita un alloc de heap por luz por frame en
        // FakeAreaLightManager.LateUpdate(). Solo se re-crea si cambia la cantidad de
        // puntos (childCount), que en juego normalmente es estática.
        private Vector3[] _pointsBuffer;

        /// <summary>
        /// Puntos de control en world space, en orden — los hijos directos del
        /// transform (hasta <see cref="MaxPoints"/>). Sin hijos, un único punto en
        /// la posición del objeto (se comporta como point light falso).
        /// </summary>
        public Vector3[] GetWorldPoints()
        {
            int childCount = transform.childCount;
            int n = childCount == 0 ? 1 : Mathf.Min(childCount, MaxPoints);

            if (_pointsBuffer == null || _pointsBuffer.Length != n)
                _pointsBuffer = new Vector3[n];

            if (childCount == 0)
            {
                _pointsBuffer[0] = transform.position;
            }
            else
            {
                for (int i = 0; i < n; i++) _pointsBuffer[i] = transform.GetChild(i).position;
            }

            return _pointsBuffer;
        }

        private void OnEnable() => FakeAreaLightManager.Register(this);
        private void OnDisable() => FakeAreaLightManager.Unregister(this);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var pts = GetWorldPoints();
            Gizmos.color = Color;
            for (int i = 0; i < pts.Length; i++)
            {
                Gizmos.DrawWireSphere(pts[i], 0.05f);
                if (i > 0) Gizmos.DrawLine(pts[i - 1], pts[i]);
            }

            Vector3 mid = pts[pts.Length / 2];
            UnityEditor.Handles.color = new Color(Color.r, Color.g, Color.b, 0.15f);
            UnityEditor.Handles.DrawWireDisc(mid, Vector3.up, Range);
        }
#endif
    }
}
