using System.Collections;
using System.Collections.Generic;
using Patterns;
using PrimeTween;
using Rollgeon.Audio;
using Rollgeon.Combat.Damage;
using Rollgeon.GameCamera;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Todo el juice de la secuencia de breakdown: sonidos, flashes, shakes, hitstop,
    /// partículas y fuego del flaming number. <b>Fire-and-forget</b>: jamás participa de
    /// la cadena <c>onDone</c> del player — el director lo llama null-safe y sin wiring
    /// cada método es no-op. Cleanup garantizado: <see cref="OnSequenceEnd"/> corre en
    /// los 3 caminos de cierre (normal/abort/timeout) y <c>OnDisable</c> lo duplica.
    /// </summary>
    public sealed class BreakdownJuice : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Optional] private BreakdownAnimSettingsSO _settings;
        [SerializeField, Optional] private ScreenFlashView _flash;
        [SerializeField, Optional] private DiceThrowImpactBurst _impactBurst;
        [SerializeField, Optional] private DamageBreakdownView _breakdownView;

        [SerializeField, Optional]
        [Tooltip("AudioSource propio con loop=true para el fuego del flaming number " +
                 "(IAudioService no tiene API de loops).")]
        private AudioSource _fireLoopSource;

        [SerializeField, Optional, Tooltip("Partículas de fuego sobre el total del choque (M alto).")]
        private GameObject _clashFireParticles;

        [SerializeField, Optional, Tooltip("Partículas de fuego sobre el CounterM cuando M ≥ 3.")]
        private GameObject _counterFireParticles;

        [SerializeField, Optional, Tooltip("Chispas de despegue/aterrizaje de los vuelos.")]
        private ParticleSystem _departSparkles;

        [SerializeField, Optional, Tooltip("Anillo de glow del popup de proc (Image, alpha 0 en reposo).")]
        private Image _procGlowRing;

        [SerializeField, Optional, Tooltip("Punto del choque — origen del burst del impacto.")]
        private RectTransform _clashAnchor;

        [Title("Clips")]
        [SerializeField, Optional] private AudioClip _whooshClip;
        [SerializeField, Optional] private AudioClip _thunkClip;
        [SerializeField, Optional] private AudioClip _rollupTickClip;
        [SerializeField, Optional] private AudioClip _procEnchantClip;
        [SerializeField, Optional] private AudioClip _procItemClip;
        [SerializeField, Optional] private AudioClip _procPassiveClip;
        [SerializeField, Optional] private AudioClip _cardSlideClip;
        [SerializeField, Optional] private AudioClip _clashImpactClip;
        [SerializeField, Optional] private AudioClip _shieldClankClip;
        [SerializeField, Optional] private AudioClip _weaknessSparkleClip;
        [SerializeField, Optional, Tooltip("Candado que se abre (Forzar Puerta pasa).")]
        private AudioClip _padlockOpenClip;
        [SerializeField, Optional, Tooltip("Candado que aguanta (Forzar Puerta falla).")]
        private AudioClip _padlockShutClip;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 0.8f;

        private readonly List<(Graphic graphic, Color rest)> _dimmedSlots =
            new List<(Graphic, Color)>();

        private int _finalTotal;
        private float _finalM;
        private bool _ducked;
        private float _nextRollupTickAt;
        private Tween _glowScale;
        private Tween _glowFade;

        // ------------------------------------------------------------------
        // Gates — mismo idiom que BossBarJuice/DiceZoneJuice.
        // ------------------------------------------------------------------
        private static bool Active => Application.isPlaying;
        private static bool Motion => !DiceUiMotionPrefs.ReducedMotion;
        private bool Sfx => Active && (_settings == null || _settings.EnableSfx);
        private bool Particles => Active && Motion && (_settings == null || _settings.EnableParticles);
        private bool ShakeOk => Active && Motion && (_settings == null || _settings.EnableShakeAndHitstop);

        // ==================================================================
        // Ciclo — llamados por el director
        // ==================================================================

        public void OnSequenceStart(float finalN, float finalM, int finalTotal)
        {
            _finalTotal = finalTotal;
            _finalM = finalM;
            _nextRollupTickAt = 0f;
            StopResiduals();
        }

        /// <summary>
        /// Celebración en el board al jugarse un combo: SOLO los dados contribuyentes
        /// burstean partículas y se sacuden, escalado por la potencia (total final +
        /// cantidad de dados). El fallback sin combo (ComboBase 0) no celebra — pero
        /// los combos de 1 dado (Número Más Alto) SÍ: son el caso más común.
        /// </summary>
        public void OnComboPlayed(in DamageBreakdown breakdown, DiceZoneView zone)
        {
            if (!Active || zone == null) return;
            var dice = breakdown.Dice;
            if (dice == null || dice.Count == 0 || breakdown.ComboBase <= 0) return;

            int t1 = _settings != null ? _settings.TierThreshold1 : 30;
            int t2 = _settings != null ? _settings.TierThreshold2 : 80;
            float intensity = BreakdownFeelMath.ComboCelebrateIntensity(
                breakdown.Final, t1, t2, dice.Count);

            int countMin = _settings != null ? _settings.ComboBurstCountMin : 3;
            int countMax = _settings != null ? _settings.ComboBurstCountMax : 6;
            int burst = Particles ? Mathf.RoundToInt(Mathf.Lerp(countMin, countMax, intensity)) : 0;
            float punch = ShakeOk
                ? (_settings != null ? _settings.ComboPunchScale : 0.18f) * intensity : 0f;
            float shakeDeg = ShakeOk
                ? (_settings != null ? _settings.ComboShakeRotation : 10f) * intensity : 0f;
            if (burst <= 0 && punch <= 0f && shakeDeg <= 0f) return;

            float stagger = (_settings != null ? _settings.ComboCelebrateStagger : 0.05f)
                / Rollgeon.Timing.GameSpeedPrefs.Multiplier;
            StartCoroutine(ComboCelebrateRoutine(dice, zone, burst, punch, shakeDeg, stagger));
        }

        private IEnumerator ComboCelebrateRoutine(IReadOnlyList<ContributingDie> dice,
            DiceZoneView zone, int burst, float punch, float shakeDeg, float stagger)
        {
            for (int i = 0; i < dice.Count; i++)
            {
                var slot = zone.GetSlotView(dice[i].BagSlot);
                if (slot != null)
                {
                    var juice = slot.GetComponent<DiceSlotJuice>();
                    if (juice != null) juice.PlayComboCelebrate(burst, punch, shakeDeg);
                }
                if (stagger > 0f && i < dice.Count - 1)
                    yield return new WaitForSecondsRealtime(stagger);
            }
        }

        /// <summary>La espada carga antes de soltar su valor (el squash lo hace la view).</summary>
        public void OnSwordWindup(PlayerBaseDamageView view)
        {
            PlaySfx(_whooshClip, _sfxVolume * 0.5f, 0.9f);
        }

        /// <summary>Un valor despegó hacia un contador. dieIndex &lt; 0 = sin ramp de pitch.</summary>
        public void OnFlightDeparted(RectTransform from, bool towardM, int dieIndex)
        {
            float step = _settings != null ? _settings.DiePitchStep : 0.06f;
            PlaySfx(_whooshClip, _sfxVolume * 0.6f, BreakdownFeelMath.PitchForIndex(dieIndex, step));
            if (Particles && _departSparkles != null && from != null)
            {
                _departSparkles.transform.position = from.position;
                _departSparkles.Play(true);
            }
        }

        /// <summary>
        /// El dado soltó su valor: flash blanco (patrón DiceBoardSkinJuice) y queda
        /// "gastado" (dim) hasta que <see cref="OnSequenceEnd"/> lo restaura — se lee
        /// el progreso de la suma mirando el board.
        /// </summary>
        public void OnDieLaunch(DiceSlotView slot, int dieIndex)
        {
            if (!Motion || slot == null) return;
            var graphic = slot.BackgroundGraphic;
            if (graphic == null) return;

            // Capturar el color ACTUAL como rest (puede venir tinteado por hold/blocked).
            var rest = graphic.color;
            RegisterDimmedSlot(graphic, rest);
            float dim = _settings != null ? _settings.SpentDieDim : 0.7f;
            var spent = new Color(rest.r * dim, rest.g * dim, rest.b * dim, rest.a);
            Tween.Color(graphic, Color.Lerp(rest, Color.white, 0.8f), spent, 0.15f, Ease.OutQuad);
        }

        /// <summary>Popup del proc antes de que su valor vuele: glow ring + sonido por familia.</summary>
        public void OnProcPopup(RectTransform at, Sprite icon, Object sourceAsset, bool towardM)
        {
            var family = BreakdownIconResolver.ResolveFamily(sourceAsset);
            var clip = family == BreakdownProcFamily.Item ? _procItemClip
                : family == BreakdownProcFamily.Enchantment ? _procEnchantClip
                : _procPassiveClip;
            PlaySfx(clip, _sfxVolume);

            if (!Particles || _procGlowRing == null || at == null) return;
            if (_glowScale.isAlive) _glowScale.Stop();
            if (_glowFade.isAlive) _glowFade.Stop();

            _procGlowRing.transform.position = at.position;
            _procGlowRing.transform.localScale = Vector3.one * 0.5f;
            var c = _procGlowRing.color;
            c.a = 0.9f;
            _procGlowRing.color = c;
            _procGlowRing.gameObject.SetActive(true);
            _glowScale = Tween.Scale(_procGlowRing.transform, 1.6f, 0.25f, Ease.OutQuad);
            _glowFade = Tween.Custom(this, 0.9f, 0f, 0.25f, (juice, a) =>
            {
                var col = juice._procGlowRing.color;
                col.a = a;
                juice._procGlowRing.color = col;
            }, Ease.OutQuad);
        }

        /// <summary>Un aporte impactó su contador.</summary>
        public void OnCounterImpact(BreakdownCounterView counter, bool isM, int dieIndex, float accumulate01)
        {
            float step = _settings != null ? _settings.DiePitchStep : 0.06f;
            PlaySfx(_thunkClip, _sfxVolume, BreakdownFeelMath.PitchForIndex(dieIndex, step));

            if (Particles && _impactBurst != null && counter != null)
                _impactBurst.Burst(ToBurstSpace(counter.Anchor), Vector2.up,
                    Mathf.Lerp(0.25f, 0.8f, accumulate01));

            if (isM && Motion)
            {
                if (_settings != null && counter != null)
                    counter.Flash(_settings.CounterMHotColor, 0.2f);
                var sign = _breakdownView != null ? _breakdownView.MultSign : null;
                if (sign != null)
                    Tween.PunchScale(sign.transform, Vector3.one * 0.2f, 0.15f, frequency: 2);
            }
        }

        /// <summary>La entrada inferior del cascade avisa que le toca (highlight).</summary>
        public void OnCascadeTelegraph(ModifierEntryView entry)
        {
            if (!Motion || entry == null) return;
            var graphic = entry.GetComponentInChildren<Graphic>();
            if (graphic == null) return;
            var rest = graphic.color;
            Tween.Color(graphic, Color.Lerp(rest, Color.white, 0.7f), rest, 0.18f, Ease.OutQuad);
        }

        public void OnCascadeFall()
        {
            PlaySfx(_cardSlideClip, _sfxVolume * 0.7f);
        }

        /// <summary>El clímax: flash + hitstop + shake + burst + duck + fuego si M alto.</summary>
        public void OnClashImpact(int finalTotal, float finalM)
        {
            int t1 = _settings != null ? _settings.TierThreshold1 : 30;
            int t2 = _settings != null ? _settings.TierThreshold2 : 80;
            int tier = BreakdownFeelMath.TierForTotal(finalTotal, t1, t2);
            float scale = tier == 0
                ? (_settings != null ? _settings.TierScaleSmall : 0.5f)
                : tier == 1
                    ? (_settings != null ? _settings.TierScaleMedium : 1f)
                    : (_settings != null ? _settings.TierScaleBig : 1.7f);

            if (Motion && _flash != null)
                _flash.Flash(Color.white,
                    Mathf.Clamp(( _settings != null ? _settings.FlashPeakAlpha : 0.15f) * scale, 0f, 0.35f),
                    _settings != null ? _settings.FlashSeconds : 0.12f);

            if (ShakeOk)
            {
                float hitstop = (_settings != null ? _settings.HitstopSeconds : 0.06f) * scale;
                if (hitstop > 0f) DiceHitstop.Play(hitstop);
                CameraShake((_settings != null ? _settings.ShakeAmplitude : 0.6f) * scale,
                    _settings != null ? _settings.ShakeSeconds : 0.25f);
            }

            if (Particles && _impactBurst != null)
                _impactBurst.Burst(ToBurstSpace(_clashAnchor), Vector2.up, Mathf.Clamp01(0.4f + 0.3f * tier));

            PlaySfx(_clashImpactClip, _sfxVolume, 1f - 0.06f * tier, isImportant: true);
            DuckMusic(_settings != null ? _settings.ClashDuckFactor : 0.4f);

            float minM = _settings != null ? _settings.FlamingNumberMinM : 2f;
            if (BreakdownFeelMath.IsFlaming(finalM, minM)) StartFire();
        }

        /// <summary>Tick del roll-up del total (rate-limited; pitch sube con el progreso).</summary>
        public void OnClashRollupTick(int shown, int total)
        {
            if (Time.unscaledTime < _nextRollupTickAt) return;
            // El roll-up llega comprimido por el game speed (vía D() del director);
            // el rate-limit acompaña para no perder casi todos los ticks a x4/x8.
            _nextRollupTickAt = Time.unscaledTime
                + 0.03f / Rollgeon.Timing.GameSpeedPrefs.Multiplier;
            float progress = total > 0 ? Mathf.Clamp01(shown / (float)total) : 1f;
            PlaySfx(_rollupTickClip, _sfxVolume * 0.5f, Mathf.Lerp(1f, 1.5f, progress));
        }

        /// <summary>El total apareció de un golpe (skip Jump / ReducedMotion).</summary>
        public void OnClashSlam()
        {
            PlaySfx(_thunkClip, _sfxVolume, 0.8f, isImportant: true);
        }

        /// <summary>Mitigación: el escudo entra con clank y el total se tinta azul-gris.</summary>
        public void OnShieldClank(TMP_Text clashLabel)
        {
            PlaySfx(_shieldClankClip, _sfxVolume, 1f, isImportant: true);
            if (!Motion || clashLabel == null || _settings == null) return;
            Tween.Color(clashLabel, _settings.MitigationColor, Color.white,
                Mathf.Max(0.05f, _settings.MitigationTickDownSeconds), Ease.OutQuad);
        }

        /// <summary>Weakness: el total sube — flash dorado.</summary>
        public void OnWeaknessFlash(TMP_Text clashLabel)
        {
            PlaySfx(_weaknessSparkleClip, _sfxVolume, 1f, isImportant: true);
            if (!Motion || clashLabel == null || _settings == null) return;
            Tween.Color(clashLabel, _settings.WeaknessColor, Color.white, 0.35f, Ease.OutQuad);
        }

        /// <summary>El golpe superó el escudo: fragmentos hacia abajo.</summary>
        public void OnMitigationShatter(RectTransform at)
        {
            if (!Particles || _impactBurst == null) return;
            _impactBurst.Burst(ToBurstSpace(at), Vector2.down, 0.5f);
        }

        /// <summary>Forzar Puerta pasó: el candado se abre — clank + chispas hacia arriba.</summary>
        public void OnPadlockOpened(RectTransform at)
        {
            PlaySfx(_padlockOpenClip != null ? _padlockOpenClip : _weaknessSparkleClip,
                _sfxVolume, 1f, isImportant: true);
            if (!Particles || _impactBurst == null || at == null) return;
            _impactBurst.Burst(ToBurstSpace(at), Vector2.up, 0.6f);
        }

        /// <summary>Forzar Puerta falló: el candado aguanta — thud seco, sin chispas.</summary>
        public void OnPadlockShut(RectTransform at)
        {
            PlaySfx(_padlockShutClip != null ? _padlockShutClip : _shieldClankClip,
                _sfxVolume, 0.7f, isImportant: true);
        }

        /// <summary>
        /// Cierre — SIEMPRE (normal/abort/timeout): apaga fuego, restaura música y dados.
        /// Idempotente a propósito (OnDisable lo repite).
        /// </summary>
        public void OnSequenceEnd() => StopResiduals();

        // ==================================================================
        // Internos
        // ==================================================================

        internal void RegisterDimmedSlot(Graphic graphic, Color rest)
            => _dimmedSlots.Add((graphic, rest));

        private void StopResiduals()
        {
            if (_fireLoopSource != null && _fireLoopSource.isPlaying) _fireLoopSource.Stop();
            SetFire(_clashFireParticles, false);
            SetFire(_counterFireParticles, false);
            if (_ducked)
            {
                DuckMusicRaw(1f);
                _ducked = false;
            }
            for (int i = 0; i < _dimmedSlots.Count; i++)
            {
                if (_dimmedSlots[i].graphic != null)
                    _dimmedSlots[i].graphic.color = _dimmedSlots[i].rest;
            }
            _dimmedSlots.Clear();
            if (_glowScale.isAlive) _glowScale.Stop();
            if (_glowFade.isAlive) _glowFade.Stop();
            if (_procGlowRing != null)
            {
                var c = _procGlowRing.color;
                c.a = 0f;
                _procGlowRing.color = c;
            }
        }

        private void StartFire()
        {
            if (Sfx && _fireLoopSource != null && !_fireLoopSource.isPlaying) _fireLoopSource.Play();
            if (Particles) SetFire(_clashFireParticles, true);
        }

        private static void SetFire(GameObject go, bool on)
        {
            if (go == null) return;
            if (on)
            {
                go.SetActive(true);
                var ps = go.GetComponent<ParticleSystem>();
                if (ps != null) ps.Play(true);
                return;
            }
            var system = go.GetComponent<ParticleSystem>();
            if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            go.SetActive(false);
        }

        // Proyección al espacio local del contenedor del burst (null ⇒ centro).
        private Vector2 ToBurstSpace(RectTransform anchor)
        {
            if (_impactBurst == null) return Vector2.zero;
            var container = (RectTransform)_impactBurst.transform;
            if (anchor == null) return Vector2.zero;
            return (Vector2)container.InverseTransformPoint(anchor.position);
        }

        private void PlaySfx(AudioClip clip, float volume, float pitch = 1f, bool isImportant = false)
        {
            if (!Sfx || clip == null) return;
            if (ServiceLocator.TryGetService<IAudioService>(out var audio) && audio != null)
                audio.PlaySfx2D(clip, volume, pitch, isImportant);
        }

        private void DuckMusic(float factor)
        {
            if (!Sfx || factor >= 1f) return;
            DuckMusicRaw(factor);
            _ducked = true;
        }

        private void DuckMusicRaw(float factor)
        {
            if (ServiceLocator.TryGetService<IAudioService>(out var audio) && audio != null)
                audio.DuckMusic(factor, _settings != null ? _settings.ClashDuckFadeSeconds : 0.15f);
        }

        private void CameraShake(float amplitude, float seconds)
        {
            if (amplitude <= 0f) return;
            if (ServiceLocator.TryGetService<ICameraService>(out var cam) && cam != null)
                cam.Shake(amplitude, seconds);
        }

        private void OnDisable() => StopResiduals();
    }
}
