using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Audio
{
    /// <summary>
    /// Catálogo autoral de música por contexto y piso. Lo consume
    /// <see cref="MusicDirector"/> para resolver qué clip suena en cada momento.
    /// Las variantes por bucket se eligen al azar (sin repetir la inmediata
    /// anterior — eso lo maneja el director).
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Audio/Music Library", fileName = "MusicLibrary")]
    public sealed class MusicLibrarySO : ScriptableObject
    {
        [Title("Main menu")]
        public AudioClip MainTheme;

        [Title("Por piso")]
        [InfoBox("Índice 0 = piso 1. Un contexto sin clips en su piso no cambia la música (warning en consola).")]
        public List<FloorMusicSet> Floors = new List<FloorMusicSet>();

        [Title("Fades (segundos)")]
        [MinValue(0f)] public float DefaultFadeSeconds = 1.5f;
        [MinValue(0f)] public float CombatFadeSeconds = 0.8f;

        private static readonly IReadOnlyList<AudioClip> Empty = Array.Empty<AudioClip>();

        /// <summary>
        /// Variantes candidatas para (contexto, piso). El piso se clampea al rango
        /// autorado — un piso 4 sin entry reusa el último autorado en vez de
        /// silenciar el juego.
        /// </summary>
        public IReadOnlyList<AudioClip> GetVariants(MusicContext context, int floorIndex)
        {
            if (context == MusicContext.MainMenu)
                return MainTheme != null ? new[] { MainTheme } : Empty;

            if (Floors == null || Floors.Count == 0) return Empty;

            var floor = Floors[Mathf.Clamp(floorIndex, 0, Floors.Count - 1)];
            if (floor == null) return Empty;

            var list = context switch
            {
                MusicContext.Exploration => floor.Exploration,
                MusicContext.Combat      => floor.Combat,
                MusicContext.Boss        => floor.Boss,
                _                        => null,
            };
            return list ?? Empty;
        }

        public float GetFadeFor(MusicContext context) =>
            context == MusicContext.Combat || context == MusicContext.Boss
                ? CombatFadeSeconds
                : DefaultFadeSeconds;
    }

    [Serializable]
    public sealed class FloorMusicSet
    {
        public List<AudioClip> Exploration = new List<AudioClip>();
        public List<AudioClip> Combat = new List<AudioClip>();

        [Tooltip("Un solo track por combate de boss (las fases quedan para después) — con varios, se elige uno al azar.")]
        public List<AudioClip> Boss = new List<AudioClip>();
    }
}
