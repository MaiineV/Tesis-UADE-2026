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
        // El shader PaletteCelLitPattern hace clip() dithered por este valor.
        // 1 = pared totalmente visible; 0 = pared totalmente oculta.
        private static readonly int s_AlphaCutoff = Shader.PropertyToID("_AlphaCutoff");

        [EnumToggleButtons] public WallDirection Direction;

        [Tooltip("Cuando está activo, el Room Editor no pisa Direction al re-bakear. " +
                 "Útil para pillars internos o paredes con orientación corregida a mano.")]
        public bool ManualOverride;

        [SerializeField] private Renderer[] _renderers;

        private MaterialPropertyBlock _mpb;
        private float _currentCutoff = 1f;
        private Tween _fadeTween;

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
            EnsureInitialized();
            ApplyCutoffToRenderers(_currentCutoff);
        }

        // Lazy init: Awake no corre en EditMode tras AddComponent (tests) y
        // SetHidden puede llegar antes de Awake en objetos inactivos —
        // sin esto, ApplyCutoffToRenderers no-opea en silencio.
        private void EnsureInitialized()
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            }

            _mpb ??= new MaterialPropertyBlock();
        }

        /// <summary>
        /// Fade dither cutoff. <paramref name="fadeSeconds"/> &lt;= 0 aplica inmediato.
        /// Usa <see cref="MaterialPropertyBlock"/> — no muta el shared material,
        /// no rompe SRP batching y no se persiste al asset en Editor.
        /// </summary>
        public void SetHidden(bool hidden, float fadeSeconds)
        {
            IsHidden = hidden;
            EnsureInitialized();

            float target = hidden ? 0f : 1f;
            if (_fadeTween.isAlive) _fadeTween.Stop();

            if (fadeSeconds <= 0f)
            {
                _currentCutoff = target;
                ApplyCutoffToRenderers(_currentCutoff);
                return;
            }

            _fadeTween = Tween.Custom(
                startValue: _currentCutoff,
                endValue: target,
                duration: fadeSeconds,
                onValueChange: v =>
                {
                    _currentCutoff = v;
                    ApplyCutoffToRenderers(v);
                });
        }

        private void ApplyCutoffToRenderers(float cutoff)
        {
            if (_renderers == null || _mpb == null) return;

            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(s_AlphaCutoff, cutoff);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
