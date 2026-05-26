using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace Rollgeon.Audio
{
    /// <summary>
    /// Configuración autoral del sistema de audio. TECHNICAL.md §17.A.3.
    /// Registrado como settings catalog en <c>ServiceBootstrapSO.SettingsAssets</c>
    /// — el <see cref="AudioManager"/> lo consume al despertar.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Audio/Audio Settings", fileName = "AudioSettings")]
    public sealed class AudioSettingsSO : ScriptableObject
    {
        // ====================================================================
        // Mixer
        // ====================================================================

        [Title("Mixer")]
        [Tooltip("AudioMixer con 4 grupos (Master / Music / Sfx / Ui) y sus parámetros expuestos.")]
        public AudioMixer Mixer;

        [Tooltip("Nombre del parámetro expuesto (dB) para el canal Master.")]
        public string MasterParam = "MasterVol";

        [Tooltip("Nombre del parámetro expuesto (dB) para el canal Music.")]
        public string MusicParam = "MusicVol";

        [Tooltip("Nombre del parámetro expuesto (dB) para el canal Sfx.")]
        public string SfxParam = "SfxVol";

        [Tooltip("Nombre del parámetro expuesto (dB) para el canal Ui.")]
        public string UiParam = "UiVol";

        [Tooltip("Grupo del mixer al que ruteamos los AudioSource de SFX (pool §A.2).")]
        public AudioMixerGroup SfxGroup;

        [Tooltip("Grupo del mixer al que ruteamos los AudioSource de música (crossfade §A.2).")]
        public AudioMixerGroup MusicGroup;

        [Tooltip("Grupo del mixer al que ruteamos los AudioSource de UI (PlaySfx2D).")]
        public AudioMixerGroup UiGroup;

        // ====================================================================
        // Pool
        // ====================================================================

        [Title("Pool")]
        [MinValue(1), Tooltip("Cantidad de AudioSource reusables para SFX. FIFO cuando se satura.")]
        public int SfxPoolSize = 16;

        // ====================================================================
        // Default volumes (linear [0,1])
        // ====================================================================

        [Title("Default volumes (linear [0,1])")]
        [Range(0f, 1f)] public float DefaultMaster = 1f;
        [Range(0f, 1f)] public float DefaultMusic = 0.8f;
        [Range(0f, 1f)] public float DefaultSfx = 1f;
        [Range(0f, 1f)] public float DefaultUi = 1f;

        // ====================================================================
        // Biome music
        // ====================================================================

        [Title("Biome music")]
        [InfoBox("Tracks por biome/piso. El DungeonManager invoca PlayMusicForBiome al entrar.")]
        public List<BiomeMusicEntry> BiomeMusic = new List<BiomeMusicEntry>();

        /// <summary>
        /// Convierte un volumen lineal <c>[0, 1]</c> a dB para el mixer.
        /// Clamp en <c>-80 dB</c> cuando value = 0 (silencio efectivo).
        /// </summary>
        public static float LinearToDecibels(float linear)
        {
            if (linear <= 0.0001f) return -80f;
            return Mathf.Log10(Mathf.Clamp01(linear)) * 20f;
        }

        public string GetParamFor(AudioChannel channel) => channel switch
        {
            AudioChannel.Master => MasterParam,
            AudioChannel.Music  => MusicParam,
            AudioChannel.Sfx    => SfxParam,
            AudioChannel.Ui     => UiParam,
            _                   => null,
        };

        public float GetDefaultFor(AudioChannel channel) => channel switch
        {
            AudioChannel.Master => DefaultMaster,
            AudioChannel.Music  => DefaultMusic,
            AudioChannel.Sfx    => DefaultSfx,
            AudioChannel.Ui     => DefaultUi,
            _                   => 1f,
        };
    }

    [Serializable]
    public struct BiomeMusicEntry
    {
        public string BiomeId;
        public AudioClip Music;
    }
}
