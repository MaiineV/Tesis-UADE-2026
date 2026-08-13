using System;
using Patterns;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Feedback visual por-pawn vía <see cref="MaterialPropertyBlock"/> sobre el shader
    /// PaletteCelLit: <b>Hit Flash</b> (tinte blanco al recibir daño) y <b>Crease de
    /// selección</b> (silueta roja mientras el pawn es el objetivo del ataque). CNF-002.
    /// </summary>
    /// <remarks>
    /// Un único MPB por renderer escribe AMBOS efectos en cada <see cref="Apply"/> —
    /// <c>SetPropertyBlock</c> reemplaza el block completo, así que dos writers separados
    /// se pisarían entre sí. Mismo patrón que <see cref="Rollgeon.GameCamera.WallOccluder"/>
    /// (no muta shared materials, no persiste al asset en Editor).
    /// El Hit Flash se auto-dispara: suscribe a <c>TypedEvent&lt;DamageResolvedPayload&gt;</c>
    /// y filtra por el guid del <see cref="PawnRegistryBinding"/> hermano. El Crease lo
    /// dispara el combate vía <see cref="SetSelectedFor"/>.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Rollgeon/Feedback/Pawn Material Feedback")]
    public sealed class PawnMaterialFeedback : MonoBehaviour
    {
        private static readonly int s_HitFlashAmount = Shader.PropertyToID("_HitFlashAmount");
        private static readonly int s_HitFlashColor  = Shader.PropertyToID("_HitFlashColor");
        private static readonly int s_EnableCrease    = Shader.PropertyToID("_EnableCrease");
        private static readonly int s_CreaseColor     = Shader.PropertyToID("_CreaseColor");
        private static readonly int s_CreaseThreshold = Shader.PropertyToID("_CreaseThreshold");
        private static readonly int s_CreaseSmooth    = Shader.PropertyToID("_CreaseSmooth");
        private static readonly int s_CreaseAlpha     = Shader.PropertyToID("_CreaseAlpha");

        [Title("Hit Flash")]
        [SerializeField, Tooltip("Duración del fade 1→0 del tinte al recibir daño.")]
        private float _hitFlashSeconds = 0.12f;

        [SerializeField]
        private Color _hitFlashColor = Color.white;

        [Title("Selección (Crease)")]
        [SerializeField, Tooltip("Preset 'foco': marca la silueta del pawn seleccionado como objetivo.")]
        private Color _selectedCreaseColor = Color.red;

        [SerializeField, Range(0f, 1f)]
        private float _selectedCreaseThreshold = 1f;

        [SerializeField, Range(0f, 0.3f)]
        private float _selectedCreaseSmooth = 0.3f;

        [SerializeField, Range(0f, 1f)]
        private float _selectedCreaseAlpha = 0.52f;

        [Title("Glow (pulso del crease)")]
        [SerializeField, Tooltip("Medio ciclo del pulso (alpha min→max). <= 0 = crease estático " +
                 "en Selected Crease Alpha, sin tween.")]
        private float _glowPulseSeconds = 0.9f;

        [SerializeField, Range(0f, 1f)]
        private float _glowAlphaMin = 0.30f;

        [SerializeField, Range(0f, 1f)]
        private float _glowAlphaMax = 0.70f;

        [SerializeField, Tooltip("Auto-poblado con los Renderers hijos si queda vacío.")]
        private Renderer[] _renderers;

        private MaterialPropertyBlock _mpb;
        private float _flashAmount;
        private bool _selected;
        private float _glowPulse;
        private Tween _flashTween;
        private Tween _glowTween;
        private PawnRegistryBinding _binding;
        private Action<DamageResolvedPayload> _onDamageResolved;

        public bool IsSelected => _selected;

        // ---- Lifecycle -------------------------------------------------------

        private void OnValidate()
        {
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            _onDamageResolved ??= OnDamageResolved;
            TypedEvent<DamageResolvedPayload>.Subscribe(_onDamageResolved);
        }

        private void OnDisable()
        {
            if (_onDamageResolved != null)
                TypedEvent<DamageResolvedPayload>.Unsubscribe(_onDamageResolved);

            // Reset visual al despawnear/desactivar — un pawn pooleado no debe
            // reaparecer blanco ni marcado.
            if (_flashTween.isAlive) _flashTween.Stop();
            if (_glowTween.isAlive) _glowTween.Stop();
            _flashAmount = 0f;
            _selected = false;
            _glowPulse = 0f;
            Apply();
        }

        // Lazy init: Awake no corre en EditMode tras AddComponent (tests) y los
        // llamados pueden llegar antes de Awake en objetos inactivos (ver WallOccluder).
        private void EnsureInitialized()
        {
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);

            _mpb ??= new MaterialPropertyBlock();

            if (_binding == null) _binding = GetComponentInParent<PawnRegistryBinding>();
            if (_binding == null) _binding = GetComponentInChildren<PawnRegistryBinding>(includeInactive: true);
        }

        // ---- Hit Flash -------------------------------------------------------

        private void OnDamageResolved(DamageResolvedPayload payload)
        {
            EnsureInitialized();
            if (_binding == null || _binding.EntityGuid == Guid.Empty) return;
            if (payload.TargetGuid != _binding.EntityGuid) return;
            FlashHit();
        }

        /// <summary>Tiñe el pawn con el color de hit y desvanece en <see cref="_hitFlashSeconds"/>.</summary>
        public void FlashHit()
        {
            EnsureInitialized();
            if (_flashTween.isAlive) _flashTween.Stop();

            if (_hitFlashSeconds <= 0f)
            {
                _flashAmount = 0f;
                Apply();
                return;
            }

            _flashTween = Tween.Custom(
                startValue: 1f,
                endValue: 0f,
                duration: _hitFlashSeconds,
                onValueChange: v =>
                {
                    _flashAmount = v;
                    Apply();
                });
        }

        // ---- Selección -------------------------------------------------------

        /// <summary>Marca/desmarca este pawn como objetivo seleccionado (crease rojo pulsante).</summary>
        public void SetSelected(bool on)
        {
            EnsureInitialized();
            if (_selected == on) return;
            _selected = on;

            if (_glowTween.isAlive) _glowTween.Stop();
            _glowPulse = 0f;

            // Glow: el alpha del crease respira min→max→min mientras dura la selección.
            // Con periodo <= 0 queda estático (tuning / EditMode sin tweens).
            if (on && _glowPulseSeconds > 0f)
            {
                _glowTween = Tween.Custom(
                    startValue: 0f,
                    endValue: 1f,
                    duration: _glowPulseSeconds,
                    onValueChange: v =>
                    {
                        _glowPulse = v;
                        Apply();
                    },
                    ease: Ease.InOutSine,
                    cycles: -1,
                    cycleMode: CycleMode.Yoyo);
            }

            Apply();
        }

        /// <summary>
        /// Helper estático para callers sin referencia al pawn (ej. CombatHandoffService):
        /// resuelve el Transform por guid vía <see cref="IPawnRegistry"/> y delega en
        /// <see cref="SetSelected"/>. No-op silencioso si el pawn no existe (ya murió).
        /// </summary>
        public static void SetSelectedFor(Guid guid, bool on)
        {
            var t = FeedbackPositionResolver.ResolvePawnTransform(guid);
            if (t == null)
            {
                // Pawn ya despawneado (murió) es esperable en el clear; en el set indica
                // que el guid no está en IPawnRegistry — wiring roto.
                if (on) Debug.LogWarning($"[PawnMaterialFeedback] SetSelectedFor: pawn {guid} no está en IPawnRegistry.");
                return;
            }
            var fx = t.GetComponentInChildren<PawnMaterialFeedback>(includeInactive: true);
            if (fx == null)
            {
                Debug.LogWarning($"[PawnMaterialFeedback] SetSelectedFor: '{t.name}' no tiene PawnMaterialFeedback — falta en el prefab.");
                return;
            }
            fx.SetSelected(on);
        }

        // ---- MPB -------------------------------------------------------------

        private void Apply()
        {
            if (_renderers == null || _mpb == null) return;

            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);

                _mpb.SetFloat(s_HitFlashAmount, _flashAmount);
                _mpb.SetColor(s_HitFlashColor, _hitFlashColor);

                _mpb.SetFloat(s_EnableCrease, _selected ? 1f : 0f);
                if (_selected)
                {
                    float alpha = _glowPulseSeconds > 0f
                        ? Mathf.Lerp(_glowAlphaMin, _glowAlphaMax, _glowPulse)
                        : _selectedCreaseAlpha;
                    _mpb.SetColor(s_CreaseColor, _selectedCreaseColor);
                    _mpb.SetFloat(s_CreaseThreshold, _selectedCreaseThreshold);
                    _mpb.SetFloat(s_CreaseSmooth, _selectedCreaseSmooth);
                    _mpb.SetFloat(s_CreaseAlpha, alpha);
                }

                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
