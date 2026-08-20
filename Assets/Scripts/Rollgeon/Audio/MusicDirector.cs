using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Dungeon;
using Rollgeon.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rollgeon.Audio
{
    /// <summary>
    /// Traduce los eventos de juego a música: main theme en el menú, exploración
    /// al recorrer salas (variante del piso actual), combate/boss al disparar
    /// combate y vuelta a exploración al ganar. Único caller de
    /// <see cref="IAudioService.PlayMusic"/> en gameplay.
    /// </summary>
    /// <remarks>
    /// Global (vive toda la sesión, lo crea <see cref="MusicDirectorBootstrap"/>).
    /// No re-arranca la pista cuando el contexto (contexto, piso) no cambió — la
    /// música de exploración sobrevive el cruce de salas. Tolera la cascada
    /// CombatEnd→CombatTriggered en el mismo frame (<c>CombatTurnFSM</c> encadena
    /// combates síncronos): el último evento gana.
    /// </remarks>
    public sealed class MusicDirector : IDisposable
    {
        private const string MainMenuSceneName = "01_MainMenu";

        private readonly IAudioService _audio;
        private readonly MusicLibrarySO _library;
        private readonly System.Random _rng;

        private EventManager.EventReceiver _onRoomEntered;
        private EventManager.EventReceiver _onCombatTriggered;
        private EventManager.EventReceiver _onCombatEnd;
        private EventManager.EventReceiver _onFloorChanged;

        private MusicContext? _context;
        private int _floorIndex;
        private AudioClip _currentClip;
        private bool _disposed;

        public MusicDirector(IAudioService audio, MusicLibrarySO library, System.Random rng = null)
        {
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _library = library ?? throw new ArgumentNullException(nameof(library));
            _rng = rng ?? new System.Random();

            _onRoomEntered = OnRoomEntered;
            EventManager.Subscribe(EventName.OnRoomEntered, _onRoomEntered);

            _onCombatTriggered = OnCombatTriggered;
            EventManager.Subscribe(EventName.OnCombatTriggered, _onCombatTriggered);

            _onCombatEnd = OnCombatEnd;
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEnd);

            _onFloorChanged = OnFloorChanged;
            EventManager.Subscribe(EventName.OnFloorChanged, _onFloorChanged);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_onRoomEntered != null) EventManager.UnSubscribe(EventName.OnRoomEntered, _onRoomEntered);
            if (_onCombatTriggered != null) EventManager.UnSubscribe(EventName.OnCombatTriggered, _onCombatTriggered);
            if (_onCombatEnd != null) EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEnd);
            if (_onFloorChanged != null) EventManager.UnSubscribe(EventName.OnFloorChanged, _onFloorChanged);
            _onRoomEntered = _onCombatTriggered = _onCombatEnd = _onFloorChanged = null;

            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // ====================================================================
        // Event handlers
        // ====================================================================

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HandleSceneLoaded(scene.name);

        /// <summary>Separado del callback de Unity para poder dispararlo desde EditMode tests.</summary>
        public void HandleSceneLoaded(string sceneName)
        {
            if (sceneName == MainMenuSceneName)
                RequestContext(MusicContext.MainMenu, 0);
        }

        private void OnRoomEntered(params object[] args)
        {
            // En combate no hay cruce de salas legítimo — un OnRoomEntered espurio
            // (restore de save con combate en curso) no debe pisar la música.
            if (_context == MusicContext.Combat || _context == MusicContext.Boss) return;

            RequestContext(MusicContext.Exploration, CurrentFloor());
        }

        private void OnCombatTriggered(params object[] args)
        {
            // args: [Guid roomInstanceId, string roomId, RoomType roomType]
            if (args == null || args.Length < 3 || args[2] is not RoomType roomType) return;

            var context = roomType == RoomType.Boss ? MusicContext.Boss : MusicContext.Combat;
            RequestContext(context, CurrentFloor());
        }

        private void OnCombatEnd(params object[] args)
        {
            // args: [Guid roomInstanceId, CombatOutcome outcome]
            if (args == null || args.Length < 2 || args[1] is not CombatOutcome outcome) return;

            // Defeat no toca nada: el flujo de derrota cambia de escena y el
            // sceneLoaded del menú resuelve la música.
            if (outcome == CombatOutcome.Victory || outcome == CombatOutcome.Aborted)
                RequestContext(MusicContext.Exploration, CurrentFloor());
        }

        private void OnFloorChanged(params object[] args)
        {
            // args: [Guid runId, int newFloorIndex]
            if (args == null || args.Length < 2 || args[1] is not int newFloor) return;

            // El estado (_floorIndex) lo actualiza RequestContext — asignarlo acá
            // antes hacía que el guard de no-op viera el piso "sin cambios" y la
            // pista nunca rotara al subir de piso.
            if (_context == MusicContext.Combat || _context == MusicContext.Boss)
            {
                _floorIndex = newFloor;
                return;
            }

            RequestContext(MusicContext.Exploration, newFloor);
        }

        // ====================================================================
        // Core
        // ====================================================================

        private int CurrentFloor()
        {
            if (ServiceLocator.TryGetService<IRunContextService>(out var run) && run != null)
                _floorIndex = run.FloorIndex;
            return _floorIndex;
        }

        private void RequestContext(MusicContext context, int floorIndex)
        {
            if (_context == context && _floorIndex == floorIndex && _currentClip != null) return;

            var variants = _library.GetVariants(context, floorIndex);
            if (variants.Count == 0)
            {
                Debug.LogWarning($"[MusicDirector] Sin clips para ({context}, piso {floorIndex + 1}) " +
                                 "en MusicLibrarySO — la música no cambia.");
                return;
            }

            var clip = Pick(variants);
            _context = context;
            _floorIndex = floorIndex;
            _currentClip = clip;
            _audio.PlayMusic(clip, _library.GetFadeFor(context));
        }

        /// <summary>Variante al azar, evitando repetir la que está sonando cuando hay más de una.</summary>
        private AudioClip Pick(IReadOnlyList<AudioClip> variants)
        {
            int index = _rng.Next(variants.Count);
            if (variants.Count > 1 && variants[index] == _currentClip)
                index = (index + 1) % variants.Count;
            return variants[index];
        }
    }
}
