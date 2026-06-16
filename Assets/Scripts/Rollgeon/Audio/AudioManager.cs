using System;
using System.Collections.Generic;
using Patterns.Save;
using PrimeTween;
using UnityEngine;

namespace Rollgeon.Audio
{
    /// <summary>
    /// Implementación de <see cref="IAudioService"/>. Pool de <c>AudioSource</c>
    /// para SFX, dos sources persistentes para música con crossfade PrimeTween,
    /// volúmenes lineales convertidos a dB sobre el <c>AudioMixer</c> autoral.
    /// TECHNICAL.md §17.A.2.
    /// </summary>
    /// <remarks>
    /// Vive en <c>DontDestroyOnLoad</c>, se instancia desde
    /// <see cref="AudioManagerBootstrap"/>. Implementa <see cref="ISaveable"/>
    /// para que los volúmenes persistan entre sesiones (§A.5) — la key es
    /// estable y round-trippable.
    /// </remarks>
    public sealed class AudioManager : MonoBehaviour, IAudioService, ISaveable
    {
        private AudioSettingsSO _settings;

        private readonly List<PooledSfxSource> _sfxPool = new List<PooledSfxSource>();
        private int _sfxCursor;

        private AudioSource _musicA;
        private AudioSource _musicB;
        private AudioSource _activeMusic;
        private AudioSource _idleMusic;

        private readonly Dictionary<AudioChannel, float> _volumes = new Dictionary<AudioChannel, float>();

        private Tween _crossfadeInTween;
        private Tween _crossfadeOutTween;

        // ====================================================================
        // Bootstrap wiring
        // ====================================================================

        public void Configure(AudioSettingsSO settings)
        {
            _settings = settings;
            if (_settings == null)
            {
                Debug.LogError("[AudioManager] Configure invocado sin AudioSettingsSO.");
                return;
            }

            BuildSfxPool();
            BuildMusicSources();
            ApplyDefaultVolumes();
        }

        private void BuildSfxPool()
        {
            int size = Mathf.Max(1, _settings.SfxPoolSize);
            for (int i = 0; i < size; i++)
            {
                var go = new GameObject($"[SfxSource_{i}]");
                go.transform.SetParent(transform, worldPositionStays: false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.outputAudioMixerGroup = _settings.SfxGroup;
                _sfxPool.Add(new PooledSfxSource
                {
                    Source = src,
                    AcquiredAtFrame = -1,
                    IsImportant = false,
                });
            }
        }

        private void BuildMusicSources()
        {
            _musicA = CreateMusicSource("[MusicA]");
            _musicB = CreateMusicSource("[MusicB]");
            _activeMusic = _musicA;
            _idleMusic = _musicB;
        }

        private AudioSource CreateMusicSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.volume = 0f;
            src.outputAudioMixerGroup = _settings.MusicGroup;
            return src;
        }

        private void ApplyDefaultVolumes()
        {
            SetVolume(AudioChannel.Master, _settings.DefaultMaster);
            SetVolume(AudioChannel.Music, _settings.DefaultMusic);
            SetVolume(AudioChannel.Sfx, _settings.DefaultSfx);
            SetVolume(AudioChannel.Ui, _settings.DefaultUi);
        }

        // ====================================================================
        // IAudioService — SFX
        // ====================================================================

        public void PlaySfx(AudioClip clip, Vector3 worldPos, float volume = 1f, float pitch = 1f, bool isImportant = false)
        {
            if (clip == null || _settings == null) return;

            var pooled = AcquireSfxSource(isImportant);
            if (pooled == null) return;

            var src = pooled.Source;
            src.transform.position = worldPos;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            src.spatialBlend = 1f; // 3D
            src.outputAudioMixerGroup = _settings.SfxGroup;
            src.Play();

            pooled.AcquiredAtFrame = Time.frameCount;
            pooled.IsImportant = isImportant;
        }

        public void PlaySfx2D(AudioClip clip, float volume = 1f, float pitch = 1f, bool isImportant = false)
        {
            if (clip == null || _settings == null) return;

            var pooled = AcquireSfxSource(isImportant);
            if (pooled == null) return;

            var src = pooled.Source;
            src.transform.localPosition = Vector3.zero;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            src.spatialBlend = 0f; // 2D
            src.outputAudioMixerGroup = _settings.UiGroup != null ? _settings.UiGroup : _settings.SfxGroup;
            src.Play();

            pooled.AcquiredAtFrame = Time.frameCount;
            pooled.IsImportant = isImportant;
        }

        /// <summary>
        /// Devuelve un source libre. Si todos están activos, toma el más viejo
        /// que no esté marcado <c>IsImportant</c>; si todos son importantes,
        /// interrumpe el más viejo de todas formas (último recurso — evita
        /// quedarnos sin audio).
        /// </summary>
        private PooledSfxSource AcquireSfxSource(bool isImportant)
        {
            if (_sfxPool.Count == 0) return null;

            // Primero buscamos uno que no esté sonando.
            for (int i = 0; i < _sfxPool.Count; i++)
            {
                int idx = (_sfxCursor + i) % _sfxPool.Count;
                var p = _sfxPool[idx];
                if (!p.Source.isPlaying)
                {
                    _sfxCursor = (idx + 1) % _sfxPool.Count;
                    return p;
                }
            }

            // Todos activos — buscamos el más viejo no-importante.
            PooledSfxSource candidate = null;
            int oldestFrame = int.MaxValue;
            foreach (var p in _sfxPool)
            {
                if (p.IsImportant) continue;
                if (p.AcquiredAtFrame < oldestFrame)
                {
                    oldestFrame = p.AcquiredAtFrame;
                    candidate = p;
                }
            }

            if (candidate == null)
            {
                // Todos importantes — cortamos el más viejo igual.
                oldestFrame = int.MaxValue;
                foreach (var p in _sfxPool)
                {
                    if (p.AcquiredAtFrame < oldestFrame)
                    {
                        oldestFrame = p.AcquiredAtFrame;
                        candidate = p;
                    }
                }
            }

            if (candidate != null) candidate.Source.Stop();
            return candidate;
        }

        // ====================================================================
        // IAudioService — Music
        // ====================================================================

        public void PlayMusic(AudioClip clip, float fadeSeconds = 1f)
        {
            if (clip == null) return;
            if (_activeMusic != null && _activeMusic.clip == clip && _activeMusic.isPlaying) return;

            // Cancel tweens anteriores para evitar que se pisen.
            if (_crossfadeInTween.isAlive) _crossfadeInTween.Stop();
            if (_crossfadeOutTween.isAlive) _crossfadeOutTween.Stop();

            // Swap: idle pasa a ser el nuevo active.
            var next = _idleMusic;
            var prev = _activeMusic;
            _idleMusic = prev;
            _activeMusic = next;

            next.clip = clip;
            next.volume = 0f;
            next.Play();

            float targetMusicVolume = GetVolume(AudioChannel.Music);
            float safeFade = Mathf.Max(0f, fadeSeconds);

            if (safeFade <= 0f)
            {
                next.volume = targetMusicVolume;
                if (prev != null) { prev.Stop(); prev.volume = 0f; }
                return;
            }

            var nextSrc = next;
            _crossfadeInTween = Tween.Custom(
                startValue: 0f,
                endValue: targetMusicVolume,
                duration: safeFade,
                onValueChange: v => { if (nextSrc != null) nextSrc.volume = v; });

            if (prev != null && prev.isPlaying)
            {
                var prevSrc = prev;
                float from = prev.volume;
                _crossfadeOutTween = Tween.Custom(
                    startValue: from,
                    endValue: 0f,
                    duration: safeFade,
                    onValueChange: v => { if (prevSrc != null) prevSrc.volume = v; })
                    .OnComplete(() => { if (prevSrc != null) prevSrc.Stop(); });
            }
        }

        public void PlayMusicForBiome(string biomeId, float fadeSeconds = 1f)
        {
            if (_settings == null || string.IsNullOrEmpty(biomeId)) return;
            foreach (var entry in _settings.BiomeMusic)
            {
                if (entry.BiomeId == biomeId)
                {
                    PlayMusic(entry.Music, fadeSeconds);
                    return;
                }
            }
            Debug.LogWarning($"[AudioManager] Biome '{biomeId}' no tiene música asignada en AudioSettingsSO.BiomeMusic.");
        }

        public void StopMusic(float fadeSeconds = 1f)
        {
            if (_activeMusic == null || !_activeMusic.isPlaying) return;

            if (_crossfadeInTween.isAlive) _crossfadeInTween.Stop();
            if (_crossfadeOutTween.isAlive) _crossfadeOutTween.Stop();

            float safeFade = Mathf.Max(0f, fadeSeconds);
            var src = _activeMusic;
            if (safeFade <= 0f)
            {
                src.Stop();
                src.volume = 0f;
                return;
            }

            _crossfadeOutTween = Tween.Custom(
                startValue: src.volume,
                endValue: 0f,
                duration: safeFade,
                onValueChange: v => { if (src != null) src.volume = v; })
                .OnComplete(() => { if (src != null) src.Stop(); });
        }

        public void PauseMusic()
        {
            if (_activeMusic != null && _activeMusic.isPlaying) _activeMusic.Pause();
        }

        public void ResumeMusic()
        {
            if (_activeMusic != null && !_activeMusic.isPlaying && _activeMusic.clip != null)
                _activeMusic.UnPause();
        }

        // ====================================================================
        // IAudioService — Volume
        // ====================================================================

        public void SetVolume(AudioChannel channel, float value)
        {
            float linear = Mathf.Clamp01(value);
            _volumes[channel] = linear;

            if (_settings == null || _settings.Mixer == null) return;
            string param = _settings.GetParamFor(channel);
            if (string.IsNullOrEmpty(param)) return;

            float db = AudioSettingsSO.LinearToDecibels(linear);
            if (!_settings.Mixer.SetFloat(param, db))
            {
                Debug.LogWarning($"[AudioManager] El parámetro '{param}' no está expuesto en el mixer — " +
                                 "verificar que el AudioMixerGroup lo tenga expuesto como 'Exposed Parameter'.");
            }
        }

        public float GetVolume(AudioChannel channel)
        {
            if (_volumes.TryGetValue(channel, out var v)) return v;
            return _settings != null ? _settings.GetDefaultFor(channel) : 1f;
        }

        // ====================================================================
        // ISaveable
        // ====================================================================

        public string SaveKey => "audio.volumes";

        public object CaptureState()
        {
            return new Dictionary<AudioChannel, float>(_volumes);
        }

        public void RestoreState(object state)
        {
            if (state is not Dictionary<AudioChannel, float> dict) return;
            foreach (var kv in dict)
                SetVolume(kv.Key, kv.Value);
        }

        // ====================================================================
        // Cleanup
        // ====================================================================

        private void OnDestroy()
        {
            if (_crossfadeInTween.isAlive) _crossfadeInTween.Stop();
            if (_crossfadeOutTween.isAlive) _crossfadeOutTween.Stop();
        }

        // ====================================================================
        // Internal types
        // ====================================================================

        private sealed class PooledSfxSource
        {
            public AudioSource Source;
            public int AcquiredAtFrame;
            public bool IsImportant;
        }
    }
}
