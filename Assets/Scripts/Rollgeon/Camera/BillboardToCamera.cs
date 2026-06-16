using UnityEngine;

namespace Rollgeon.GameCamera
{
    /// <summary>
    /// Mantiene el transform orientado hacia la cámara principal cada frame (billboard).
    /// Alinea la rotación del objeto con la de la cámara —misma convención que usaban los
    /// íconos del floor view al materializarse— para que la cara visible mire siempre al
    /// lente, incluso cuando la cámara rota (<c>RotateBy45</c>).
    /// <para>
    /// Cachea <see cref="Camera.main"/> y solo lo re-resuelve si se pierde (no consulta el
    /// tag cada frame por cada billboard). Sin cámara no hace nada — seguro en EditMode/tests.
    /// </para>
    /// </summary>
    public sealed class BillboardToCamera : MonoBehaviour
    {
        private Camera _camera;

        private void OnEnable() => Apply();

        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            transform.rotation = _camera.transform.rotation;
        }
    }
}
