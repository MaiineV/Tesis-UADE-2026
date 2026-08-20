using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.GameCamera
{
    /// <summary>
    /// Espeja la Main Camera para renderizar SOLO el layer WorldUI (barras de vida
    /// world-space de los enemigos) a un RenderTexture a resolución nativa, que
    /// Canvas_Display compone encima del pixel art. Así la UI que vive en el mundo
    /// no pasa por el RT de baja resolución y se lee nítida.
    /// </summary>
    /// <remarks>
    /// La cámara debe ser CHILD de la Main Camera con transform identidad — la pose
    /// se hereda gratis y acá solo se copian <c>orthographicSize</c> (el zoom de
    /// <see cref="CameraService"/> tweenea por frame) y los clip planes. El execution
    /// order 100 garantiza correr después del pixel-snap de CameraService. El RT se
    /// recrea solo cuando cambia la resolución de pantalla.
    /// </remarks>
    [AddComponentMenu("Rollgeon/Camera/World Ui Camera Sync")]
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public sealed class WorldUiCameraSync : MonoBehaviour
    {
        [Title("Wiring")]
        [SerializeField, Tooltip("Cámara fuente a espejar. Si null, cae a Camera.main.")]
        private Camera _source;

        [SerializeField, Tooltip("RawImage 'WorldUI' de Canvas_Display que muestra el RT nativo.")]
        private RawImage _output;

        private Camera _cam;
        private RenderTexture _rt;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        private void OnDestroy()
        {
            ReleaseRenderTexture();
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            var source = _source != null ? _source : Camera.main;
            if (source == null) return;

            EnsureRenderTexture();

            _cam.orthographic = source.orthographic;
            _cam.orthographicSize = source.orthographicSize;
            _cam.nearClipPlane = source.nearClipPlane;
            _cam.farClipPlane = source.farClipPlane;
        }

        private void EnsureRenderTexture()
        {
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);
            if (_rt != null && _rt.width == width && _rt.height == height) return;

            ReleaseRenderTexture();
            // Depth 24 obligatorio: el render graph de URP rechaza un output RT sin
            // depth buffer ("output Render Texture must have a depth buffer") y el
            // frame sale negro opaco. Alpha obligatorio — el composite deja ver el
            // pixel art debajo. OJO: la cámara además necesita el renderer dedicado
            // WorldUI_Renderer (sin FullScreenPass): los renderer features corren en
            // TODAS las cámaras del renderer y el post de pixelado escribe alpha=1.
            _rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "WorldUI_RT",
                filterMode = FilterMode.Bilinear
            };
            _rt.Create();
            _cam.targetTexture = _rt;
            if (_output != null)
            {
                _output.texture = _rt;
                _output.enabled = true;
            }
        }

        private void ReleaseRenderTexture()
        {
            if (_rt == null) return;
            if (_cam != null && _cam.targetTexture == _rt) _cam.targetTexture = null;
            if (_output != null && _output.texture == _rt)
            {
                _output.texture = null;
                _output.enabled = false;
            }
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }
    }
}
