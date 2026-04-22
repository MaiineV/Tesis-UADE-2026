using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.GameCamera
{
    /// <summary>
    /// Marker component para cada pared de un <c>RoomPrefab</c>. El
    /// <see cref="CameraService"/> cruza la <see cref="Direction"/> contra
    /// <see cref="CameraConfigSO.OcclusionMap"/> y llama
    /// <see cref="SetHidden"/> cuando corresponde (§17.E.8).
    /// </summary>
    [AddComponentMenu("Rollgeon/Camera/Wall Occluder")]
    public sealed class WallOccluder : MonoBehaviour
    {
        [EnumToggleButtons] public WallDirection Direction;

        [SerializeField] private Renderer[] _renderers;

        public bool IsHidden { get; private set; }

        private void OnValidate()
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            }
        }

        private void Awake()
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            }
        }

        /// <summary>
        /// Fade alpha de los materiales. <paramref name="fadeSeconds"/> &lt;= 0 aplica inmediato.
        /// </summary>
        public void SetHidden(bool hidden, float fadeSeconds)
        {
            IsHidden = hidden;

            if (_renderers == null) return;
            float target = hidden ? 0f : 1f;

            foreach (var r in _renderers)
            {
                if (r == null) continue;

                if (fadeSeconds <= 0f)
                {
                    var mat = r.sharedMaterial;
                    var color = mat.color;
                    color.a = target;
                    mat.color = color;
                }
                else
                {
                    Tween.MaterialAlpha(r.sharedMaterial, target, fadeSeconds);
                }
            }
        }
    }
}
