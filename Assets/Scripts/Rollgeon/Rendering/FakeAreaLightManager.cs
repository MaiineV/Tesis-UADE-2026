using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Rendering
{
    /// <summary>
    /// Sube hasta <see cref="MaxLights"/> <see cref="FakeAreaLight"/> activas como
    /// globals de shader cada frame: <c>_FakeAreaLightCount</c> (float) y
    /// <c>_FakeAreaLightData[MaxLights * 5]</c> (float4[], 5 slots por luz:
    /// punto0+intensity, punto1+range, punto2+pointCount, punto3+0, color+0). Los
    /// shaders de la familia PaletteCel* consumen esto para simular bandas de luz
    /// siguiendo una polilínea, sin gastar el presupuesto real de
    /// <see cref="UnityEngine.Light"/> — ver comentario de <see cref="FakeAreaLight"/>.
    ///
    /// No requiere wiring manual: se autocrea la primera vez que algún
    /// <see cref="FakeAreaLight"/> se registra (mismo patrón que
    /// <c>ServiceLocator</c> con servicios lazy). Vive mientras dure la escena.
    /// </summary>
    public class FakeAreaLightManager : MonoBehaviour
    {
        public const int MaxLights = 8;
        private const int FloatsPerLight = 5;

        private static FakeAreaLightManager s_instance;
        private static readonly List<FakeAreaLight> s_lights = new List<FakeAreaLight>(MaxLights);

        private static readonly int s_CountID = Shader.PropertyToID("_FakeAreaLightCount");
        private static readonly int s_DataID  = Shader.PropertyToID("_FakeAreaLightData");

        private static readonly Vector4[] s_buffer = new Vector4[MaxLights * FloatsPerLight];

        public static void Register(FakeAreaLight light)
        {
            EnsureInstance();
            if (!s_lights.Contains(light)) s_lights.Add(light);
        }

        public static void Unregister(FakeAreaLight light)
        {
            s_lights.Remove(light);
        }

        private static void EnsureInstance()
        {
            if (s_instance != null) return;
            var go = new GameObject("FakeAreaLightManager (auto)");
            s_instance = go.AddComponent<FakeAreaLightManager>();
            // Sobrevive cambios de sub-escena aditiva pero no hace falta que cruce
            // escenas enteras: cada escena que use luces falsas crea la suya.
        }

        private void LateUpdate()
        {
            int count = Mathf.Min(s_lights.Count, MaxLights);
            for (int i = 0; i < count; i++)
            {
                var l = s_lights[i];
                var pts = l.GetWorldPoints();
                int b = i * FloatsPerLight;

                Vector3 p0 = pts.Length > 0 ? pts[0] : transform.position;
                Vector3 p1 = pts.Length > 1 ? pts[1] : p0;
                Vector3 p2 = pts.Length > 2 ? pts[2] : p1;
                Vector3 p3 = pts.Length > 3 ? pts[3] : p2;

                s_buffer[b]     = new Vector4(p0.x, p0.y, p0.z, l.Intensity);
                s_buffer[b + 1] = new Vector4(p1.x, p1.y, p1.z, l.Range);
                s_buffer[b + 2] = new Vector4(p2.x, p2.y, p2.z, pts.Length);
                s_buffer[b + 3] = new Vector4(p3.x, p3.y, p3.z, 0f);
                s_buffer[b + 4] = new Vector4(l.Color.r, l.Color.g, l.Color.b, 0f);
            }
            // Slots sin usar (i >= count) nunca se leen — el shader corta el loop en
            // _FakeAreaLightCount, no hace falta limpiarlos a mano.

            Shader.SetGlobalFloat(s_CountID, count);
            Shader.SetGlobalVectorArray(s_DataID, s_buffer);
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }
    }
}
