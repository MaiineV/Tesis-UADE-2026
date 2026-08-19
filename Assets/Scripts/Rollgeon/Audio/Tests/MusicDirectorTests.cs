using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Dungeon;
using Rollgeon.Heroes;
using Rollgeon.Run;
using UnityEngine;

namespace Rollgeon.Audio.Tests
{
    [TestFixture]
    public class MusicDirectorTests
    {
        private readonly List<UnityEngine.Object> _created = new();
        private SpyAudioService _audio;
        private StubRunContext _run;
        private MusicLibrarySO _library;
        private MusicDirector _director;
        private AudioClip _mainTheme;
        private AudioClip _explo1;
        private AudioClip _combat1;
        private AudioClip _boss1;
        private AudioClip _explo2;

        // -------------------------------------------------------------------
        // Stubs / Spies
        // -------------------------------------------------------------------

        private class SpyAudioService : IAudioService
        {
            public readonly List<(AudioClip clip, float fade)> MusicCalls = new();

            public void PlaySfx(AudioClip clip, Vector3 worldPos, float volume = 1f, float pitch = 1f, bool isImportant = false) { }
            public void PlaySfx2D(AudioClip clip, float volume = 1f, float pitch = 1f, bool isImportant = false) { }
            public void PlayMusic(AudioClip clip, float fadeSeconds = 1f) => MusicCalls.Add((clip, fadeSeconds));
            public void PlayMusicForBiome(string biomeId, float fadeSeconds = 1f) { }
            public void StopMusic(float fadeSeconds = 1f) { }
            public void PauseMusic() { }
            public void ResumeMusic() { }
            public void SetVolume(AudioChannel channel, float value) { }
            public float GetVolume(AudioChannel channel) => 1f;
        }

        private class StubRunContext : IRunContextService
        {
            public Guid RunId { get; set; } = Guid.NewGuid();
            public int FloorIndex { get; set; }
            public ClassHeroSO SelectedHero => null;
            public bool IsRunActive => true;
            public void AdvanceFloor() { }
        }

        // -------------------------------------------------------------------
        // Setup
        // -------------------------------------------------------------------

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _mainTheme = NewClip("main");
            _explo1 = NewClip("explo1");
            _combat1 = NewClip("combat1");
            _boss1 = NewClip("boss1");
            _explo2 = NewClip("explo2");

            _library = ScriptableObject.CreateInstance<MusicLibrarySO>();
            _created.Add(_library);
            _library.MainTheme = _mainTheme;
            _library.DefaultFadeSeconds = 1.5f;
            _library.CombatFadeSeconds = 0.8f;
            _library.Floors = new List<FloorMusicSet>
            {
                new FloorMusicSet
                {
                    Exploration = new List<AudioClip> { _explo1 },
                    Combat = new List<AudioClip> { _combat1 },
                    Boss = new List<AudioClip> { _boss1 },
                },
                new FloorMusicSet
                {
                    Exploration = new List<AudioClip> { _explo2 },
                    Combat = new List<AudioClip> { _combat1 },
                    Boss = new List<AudioClip> { _boss1 },
                },
            };

            _audio = new SpyAudioService();
            _run = new StubRunContext();
            ServiceLocator.AddService<IRunContextService>(_run, ServiceScope.Global);

            _director = new MusicDirector(_audio, _library, new System.Random(1234));
        }

        [TearDown]
        public void TearDown()
        {
            _director?.Dispose();

            foreach (var obj in _created)
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            _created.Clear();

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private AudioClip NewClip(string name)
        {
            var clip = AudioClip.Create(name, 44100, 1, 44100, false);
            _created.Add(clip);
            return clip;
        }

        private void EnterRoom() =>
            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "room_x");

        private void TriggerCombat(RoomType type) =>
            EventManager.Trigger(EventName.OnCombatTriggered, Guid.NewGuid(), "room_x", type);

        private void EndCombat(CombatOutcome outcome) =>
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid(), outcome);

        // -------------------------------------------------------------------
        // Tests
        // -------------------------------------------------------------------

        [Test]
        public void HandleSceneLoaded_MainMenu_PlaysMainTheme()
        {
            _director.HandleSceneLoaded("01_MainMenu");

            Assert.That(_audio.MusicCalls, Has.Count.EqualTo(1));
            Assert.That(_audio.MusicCalls[0].clip, Is.EqualTo(_mainTheme));
            Assert.That(_audio.MusicCalls[0].fade, Is.EqualTo(1.5f));
        }

        [Test]
        public void HandleSceneLoaded_OtherScene_PlaysNothing()
        {
            _director.HandleSceneLoaded("02_Gameplay");

            Assert.That(_audio.MusicCalls, Is.Empty);
        }

        [Test]
        public void OnRoomEntered_PlaysExplorationOfCurrentFloor()
        {
            _run.FloorIndex = 1;

            EnterRoom();

            Assert.That(_audio.MusicCalls, Has.Count.EqualTo(1));
            Assert.That(_audio.MusicCalls[0].clip, Is.EqualTo(_explo2));
        }

        [Test]
        public void OnRoomEntered_SameFloorTwice_DoesNotRestartMusic()
        {
            EnterRoom();
            EnterRoom();

            Assert.That(_audio.MusicCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public void OnCombatTriggered_NormalRoom_PlaysCombatWithCombatFade()
        {
            EnterRoom();

            TriggerCombat(RoomType.Combat);

            Assert.That(_audio.MusicCalls, Has.Count.EqualTo(2));
            Assert.That(_audio.MusicCalls[1].clip, Is.EqualTo(_combat1));
            Assert.That(_audio.MusicCalls[1].fade, Is.EqualTo(0.8f));
        }

        [Test]
        public void OnCombatTriggered_BossRoom_PlaysBossTrack()
        {
            EnterRoom();

            TriggerCombat(RoomType.Boss);

            Assert.That(_audio.MusicCalls[^1].clip, Is.EqualTo(_boss1));
        }

        [Test]
        public void OnRoomEntered_DuringCombat_IsIgnored()
        {
            EnterRoom();
            TriggerCombat(RoomType.Combat);

            EnterRoom();

            Assert.That(_audio.MusicCalls, Has.Count.EqualTo(2));
        }

        [Test]
        public void OnCombatEnd_Victory_ReturnsToExploration()
        {
            EnterRoom();
            TriggerCombat(RoomType.Combat);

            EndCombat(CombatOutcome.Victory);

            Assert.That(_audio.MusicCalls[^1].clip, Is.EqualTo(_explo1));
        }

        [Test]
        public void OnCombatEnd_Defeat_LeavesMusicUntouched()
        {
            EnterRoom();
            TriggerCombat(RoomType.Combat);
            int callsBefore = _audio.MusicCalls.Count;

            EndCombat(CombatOutcome.Defeat);

            Assert.That(_audio.MusicCalls, Has.Count.EqualTo(callsBefore));
        }

        [Test]
        public void OnCombatEnd_ThenCombatTriggered_SameFrameChainsToCombat()
        {
            // CombatTurnFSM puede encadenar un combate nuevo sincrónicamente
            // después del OnCombatEnd — el último evento gana.
            EnterRoom();
            TriggerCombat(RoomType.Combat);

            EndCombat(CombatOutcome.Victory);
            TriggerCombat(RoomType.Combat);

            Assert.That(_audio.MusicCalls[^1].clip, Is.EqualTo(_combat1));
        }

        [Test]
        public void OnFloorChanged_DuringExploration_SwitchesToNewFloorVariant()
        {
            EnterRoom();

            _run.FloorIndex = 1;
            EventManager.Trigger(EventName.OnFloorChanged, Guid.NewGuid(), 1);

            Assert.That(_audio.MusicCalls[^1].clip, Is.EqualTo(_explo2));
        }

        [Test]
        public void OnRoomEntered_WithoutRunService_FallsBackToLastKnownFloor()
        {
            ServiceLocator.Clear();

            EnterRoom();

            Assert.That(_audio.MusicCalls, Has.Count.EqualTo(1));
            Assert.That(_audio.MusicCalls[0].clip, Is.EqualTo(_explo1));
        }

        [Test]
        public void Dispose_UnsubscribesFromEvents()
        {
            _director.Dispose();

            EnterRoom();
            TriggerCombat(RoomType.Combat);

            Assert.That(_audio.MusicCalls, Is.Empty);
        }
    }
}
