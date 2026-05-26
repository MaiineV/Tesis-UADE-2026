using UnityEngine;

namespace Rollgeon.Audio
{
    /// <summary>
    /// Contrato del servicio de audio global. TECHNICAL.md §17.A.1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Único canal por el que el código de gameplay toca audio. Los entries SFX
    /// del pipeline de feedback (§10) enrutan acá via <see cref="PlaySfx"/> —
    /// nadie debe llamar <c>AudioSource.PlayClipAtPoint</c> fuera de esta capa.
    /// </para>
    /// <para>
    /// Los valores de volumen son lineales en <c>[0, 1]</c>. La implementación
    /// concreta los convierte a dB cuando los aplica al <c>AudioMixer</c>.
    /// </para>
    /// </remarks>
    public interface IAudioService
    {
        // ====================================================================
        // SFX
        // ====================================================================

        /// <summary>
        /// Reproduce un SFX 3D posicionado en <paramref name="worldPos"/>. El
        /// source sale del pool interno (§A.2). Si el pool está saturado y la
        /// llamada no es importante (ver <paramref name="isImportant"/>), se
        /// descarta el source más viejo (FIFO). <paramref name="pitch"/> en
        /// <c>[0.5, 2.0]</c> siguiendo el rango estándar de Unity.
        /// </summary>
        void PlaySfx(AudioClip clip, Vector3 worldPos, float volume = 1f, float pitch = 1f, bool isImportant = false);

        /// <summary>
        /// Reproduce un SFX 2D (UI, feedback no posicional). No se spatializa.
        /// </summary>
        void PlaySfx2D(AudioClip clip, float volume = 1f, float pitch = 1f, bool isImportant = false);

        // ====================================================================
        // Music
        // ====================================================================

        /// <summary>
        /// Levanta una pista de música con crossfade lineal contra la que esté
        /// sonando. Si <paramref name="clip"/> es el mismo que la actual, no-op.
        /// </summary>
        void PlayMusic(AudioClip clip, float fadeSeconds = 1f);

        /// <summary>
        /// Busca un clip en <see cref="AudioSettingsSO.BiomeMusic"/> por
        /// <paramref name="biomeId"/> y llama <see cref="PlayMusic"/>. Si no
        /// matchea ningún entry, loguea warning y no cambia la música.
        /// </summary>
        void PlayMusicForBiome(string biomeId, float fadeSeconds = 1f);

        void StopMusic(float fadeSeconds = 1f);
        void PauseMusic();
        void ResumeMusic();

        // ====================================================================
        // Volume
        // ====================================================================

        /// <summary>Setea el volumen lineal <c>[0, 1]</c> de un canal. Convierte a dB internamente.</summary>
        void SetVolume(AudioChannel channel, float value);

        /// <summary>Devuelve el último valor lineal seteado (no lee el mixer).</summary>
        float GetVolume(AudioChannel channel);
    }
}
