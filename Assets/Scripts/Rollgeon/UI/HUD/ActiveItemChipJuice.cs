using Patterns;
using Rollgeon.Audio;
using Rollgeon.Items.Active;
using Rollgeon.Timing;
using Rollgeon.UI.HUD.Breakdown;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// El juice de la tirada de la ficha de item activo (GDD "Ítems Activos" §19):
    /// SFX propio via <c>IAudioService</c> — distinto al de los dados de combate a
    /// proposito, es un sistema separado y tiene que sonar como tal —, burst y flash
    /// escalados por banda con <see cref="ActiveItemRollFeelMath.Intensity01"/>,
    /// hit-stop reservado a la banda positiva y texto flotante con la banda obtenida.
    /// </summary>
    /// <remarks>
    /// <b>Fire-and-forget</b> (patron <c>ChestRevealJuice</c>): cuelga del mismo
    /// GameObject que <see cref="ActiveItemChipView"/> y escucha sus hooks; sin clips o
    /// refs cada beat es no-op, la view nunca depende de este componente. La intensidad
    /// sale de la banda y no del numero: en Riesgo la negativa es un buen resultado y en
    /// Precision el maximo del dado puede ser el peor.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Active Item Chip Juice")]
    [RequireComponent(typeof(ActiveItemChipView))]
    public sealed class ActiveItemChipJuice : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Optional, Tooltip("Burst de particulas UI compartido del HUD (el " +
                 "mismo que usan la boss bar y el chest reveal).")]
        private DiceThrowImpactBurst _burst;

        [SerializeField, Optional, Tooltip("Flash de pantalla, tinteado con el color de " +
                 "banda al resolver.")]
        private ScreenFlashView _flash;

        [SerializeField, Optional, Tooltip("Spawner de texto flotante del HUD. El texto " +
                 "sale desde la ficha con el color de la banda. Null = se resuelve en " +
                 "runtime por escena (vive en Canvas_CombatHUD, otro prefab — no se " +
                 "puede serializar la ref desde aca).")]
        private FloatingDamageSpawner _floatingText;
        private bool _floatingTextResolved;

        [Title("Clips — propios de la ficha, NUNCA los de DiceZoneJuice")]
        [SerializeField, Optional, Tooltip("Traqueteo al arrancar el giro dentro de la ficha.")]
        private AudioClip _rattleClip;

        [SerializeField, Optional, Tooltip("Tick por cara preview del giro.")]
        private AudioClip _tickClip;

        [SerializeField, Optional, Tooltip("Golpe seco al asentarse la cara cruda.")]
        private AudioClip _settleClip;

        [SerializeField, Optional, Tooltip("Stinger al resolver: volumen y pitch escalan " +
                 "con la banda.")]
        private AudioClip _resolveClip;

        [SerializeField, Optional, Tooltip("Campanita del encantamiento corriendo el " +
                 "resultado (solo si intervino).")]
        private AudioClip _enchantClip;

        [SerializeField, Range(0f, 1f)]
        private float _sfxVolume = 0.8f;

        [SerializeField, MinValue(0f), Tooltip("Intervalo minimo entre ticks de giro — se " +
                 "divide por la velocidad de juego para no saturar el mixer a x4.")]
        private float _tickMinInterval = 0.04f;

        [Title("Payoff por banda")]
        [SerializeField, MinValue(0f), Tooltip("Hit-stop al resolver en banda positiva. " +
                 "0 = apagado.")]
        private float _hitstopSeconds = 0.06f;

        [SerializeField, Range(0f, 1f), Tooltip("Alpha maximo del flash (banda positiva). " +
                 "Las otras bandas escalan hacia abajo.")]
        private float _flashPeakAlphaMax = 0.22f;

        [SerializeField, MinValue(0f)]
        private float _flashSeconds = 0.18f;

        [SerializeField, MinValue(0f), Tooltip("Intensidad del burst en banda positiva.")]
        private float _burstIntensityMax = 1.1f;

        [SerializeField, Tooltip("Pitch del stinger en banda negativa → positiva.")]
        private Vector2 _resolvePitchRange = new Vector2(0.85f, 1.15f);

        [Title("Texto flotante — la banda, no el numero (el numero ya esta en la ficha)")]
        [SerializeField] private string _negativeText = "Riesgo";
        [SerializeField] private string _mixedText = "Mixto";
        [SerializeField] private string _positiveText = "¡Fuerte!";

        private ActiveItemChipView _view;
        private float _nextTickAt;

        // ------------------------------------------------------------------
        // Gates — mismo idiom que ChestRevealJuice/BossBarJuice. El audio NO se
        // gatea por ReducedMotion: reduced motion corta movimiento, no sonido.
        // ------------------------------------------------------------------
        private static bool Active => Application.isPlaying;
        private static bool Motion => !DiceUiMotionPrefs.ReducedMotion;
        private static bool Particles => Active && Motion;
        private static bool ShakeOk => Active && Motion;

        private void OnEnable()
        {
            if (_view == null) _view = GetComponent<ActiveItemChipView>();
            if (_view == null) return;

            _view.RollSpinStarted += HandleSpinStarted;
            _view.SpinTicked += HandleSpinTicked;
            _view.RawFaceSettled += HandleRawFaceSettled;
            _view.ResultLanded += HandleResultLanded;
        }

        private void OnDisable()
        {
            if (_view == null) return;
            _view.RollSpinStarted -= HandleSpinStarted;
            _view.SpinTicked -= HandleSpinTicked;
            _view.RawFaceSettled -= HandleRawFaceSettled;
            _view.ResultLanded -= HandleResultLanded;
        }

        // ==================================================================
        // Beats
        // ==================================================================

        private void HandleSpinStarted(ActiveItemRoll pending)
        {
            _nextTickAt = 0f;
            PlaySfx(_rattleClip, _sfxVolume * 0.7f, Random.Range(0.95f, 1.05f));
        }

        private void HandleSpinTicked()
        {
            if (Time.unscaledTime < _nextTickAt) return;
            float speed = Mathf.Max(0.01f, GameSpeedPrefs.Multiplier);
            _nextTickAt = Time.unscaledTime + _tickMinInterval / speed;
            PlaySfx(_tickClip, _sfxVolume * 0.25f);
        }

        private void HandleRawFaceSettled(ActiveItemRoll pending)
        {
            // Neutro a proposito: la banda todavia no se resolvio (el encantamiento corre
            // al resolver) — el payoff va en la resolucion, que llega enseguida.
            PlaySfx(_settleClip, _sfxVolume * 0.6f);
        }

        private void HandleResultLanded(ActiveItemActivationResult result)
        {
            float k = ActiveItemRollFeelMath.Intensity01(result.Band);
            var tint = _view != null ? _view.BandColor(result.Band) : Color.white;

            if (Motion && _flash != null)
            {
                float peak = _flashPeakAlphaMax * k;
                // Por debajo del umbral perceptible mejor nada que un parpadeo sucio
                // (mismo criterio que el chest reveal).
                if (peak >= 0.05f) _flash.Flash(tint, peak, _flashSeconds);
            }

            if (Particles && _burst != null)
                _burst.Burst(ToBurstSpace(transform as RectTransform), Vector2.up,
                    _burstIntensityMax * k);

            if (ShakeOk
                && ActiveItemRollFeelMath.HitstopAllowed(result.Band)
                && _hitstopSeconds > 0f)
            {
                DiceHitstop.Play(_hitstopSeconds);
            }

            var floating = ResolveFloatingText();
            if (floating != null)
                floating.SpawnAt(BandText(result.Band), tint, transform.position);

            if (result.WasEnchanted) PlaySfx(_enchantClip, _sfxVolume * 0.8f, 1.1f);
            PlaySfx(_resolveClip, _sfxVolume * Mathf.Lerp(0.6f, 1f, k),
                Mathf.Lerp(_resolvePitchRange.x, _resolvePitchRange.y, k),
                isImportant: result.Band == ActiveItemBand.Positive);
        }

        // ==================================================================
        // Internos
        // ==================================================================

        private string BandText(ActiveItemBand band)
        {
            switch (band)
            {
                case ActiveItemBand.Negative: return _negativeText;
                case ActiveItemBand.Mixed: return _mixedText;
                default: return _positiveText;
            }
        }

        // El spawner vive en Canvas_CombatHUD y este componente en Canvas_PlayerStatus:
        // la ref no se puede serializar entre prefabs, asi que se resuelve una vez por
        // escena. El resultado (incluso null) se cachea — buscar por tipo cada payoff
        // seria pagar el scan de escena en el frame del impacto.
        private FloatingDamageSpawner ResolveFloatingText()
        {
            if (_floatingText == null && !_floatingTextResolved)
            {
                _floatingText = FindFirstObjectByType<FloatingDamageSpawner>();
                _floatingTextResolved = true;
            }
            return _floatingText;
        }

        // Proyeccion al espacio local del contenedor del burst (patron ChestRevealJuice).
        private Vector2 ToBurstSpace(RectTransform anchor)
        {
            if (_burst == null || anchor == null) return Vector2.zero;
            var container = (RectTransform)_burst.transform;
            return (Vector2)container.InverseTransformPoint(anchor.position);
        }

        private static void PlaySfx(AudioClip clip, float volume, float pitch = 1f,
            bool isImportant = false)
        {
            if (!Active || clip == null) return;
            if (ServiceLocator.TryGetService<IAudioService>(out var audio) && audio != null)
                audio.PlaySfx2D(clip, volume, pitch, isImportant);
        }
    }
}
