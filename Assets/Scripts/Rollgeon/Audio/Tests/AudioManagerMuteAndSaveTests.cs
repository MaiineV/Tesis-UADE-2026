using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using UnityEngine;

namespace Rollgeon.Audio.Tests
{
    /// <summary>
    /// Mute por canal y persistencia de <c>audio.volumes</c> (DTO nuevo +
    /// compatibilidad con el formato legacy pre-mutes). Sin mixer: acá se
    /// verifica el estado del manager; la conversión a dB es pura y se cubre
    /// aparte en <see cref="AudioSettingsSO.LinearToDecibels"/>.
    /// </summary>
    [TestFixture]
    public class AudioManagerMuteAndSaveTests
    {
        private readonly List<Object> _created = new();
        private AudioManager _manager;
        private AudioSettingsSO _settings;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            SaveSystem.ResetForTests();

            _manager = NewManager();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null)
                    Object.DestroyImmediate(obj);
            _created.Clear();

            SaveSystem.ResetForTests();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        private AudioManager NewManager()
        {
            _settings = ScriptableObject.CreateInstance<AudioSettingsSO>();
            _settings.SfxPoolSize = 1;
            _created.Add(_settings);

            var go = new GameObject("[AudioManagerUnderTest]");
            _created.Add(go);
            var manager = go.AddComponent<AudioManager>();
            manager.Configure(_settings);
            return manager;
        }

        // -------------------------------------------------------------------
        // Mute
        // -------------------------------------------------------------------

        [Test]
        public void SetMuted_True_DoesNotTouchUserVolume()
        {
            _manager.SetVolume(AudioChannel.Music, 0.6f);

            _manager.SetMuted(AudioChannel.Music, true);

            Assert.That(_manager.IsMuted(AudioChannel.Music), Is.True);
            Assert.That(_manager.GetVolume(AudioChannel.Music), Is.EqualTo(0.6f));
        }

        [Test]
        public void SetMuted_False_Unmutes()
        {
            _manager.SetMuted(AudioChannel.Sfx, true);

            _manager.SetMuted(AudioChannel.Sfx, false);

            Assert.That(_manager.IsMuted(AudioChannel.Sfx), Is.False);
        }

        [Test]
        public void IsMuted_ByDefault_IsFalseForAllChannels()
        {
            Assert.That(_manager.IsMuted(AudioChannel.Master), Is.False);
            Assert.That(_manager.IsMuted(AudioChannel.Music), Is.False);
            Assert.That(_manager.IsMuted(AudioChannel.Sfx), Is.False);
            Assert.That(_manager.IsMuted(AudioChannel.Ui), Is.False);
        }

        // -------------------------------------------------------------------
        // Save state
        // -------------------------------------------------------------------

        [Test]
        public void CaptureState_ContainsVolumesAndMutes()
        {
            _manager.SetVolume(AudioChannel.Music, 0.25f);
            _manager.SetMuted(AudioChannel.Sfx, true);

            var state = _manager.CaptureState() as AudioManager.AudioSaveState;

            Assert.That(state, Is.Not.Null);
            Assert.That(state.Volumes[AudioChannel.Music], Is.EqualTo(0.25f));
            Assert.That(state.Muted, Is.EquivalentTo(new[] { AudioChannel.Sfx }));
        }

        [Test]
        public void RestoreState_RoundTrip_RecoversVolumesAndMutes()
        {
            _manager.SetVolume(AudioChannel.Master, 0.7f);
            _manager.SetVolume(AudioChannel.Music, 0.3f);
            _manager.SetMuted(AudioChannel.Music, true);
            var state = _manager.CaptureState();

            var restored = NewManager();
            restored.RestoreState(state);

            Assert.That(restored.GetVolume(AudioChannel.Master), Is.EqualTo(0.7f));
            Assert.That(restored.GetVolume(AudioChannel.Music), Is.EqualTo(0.3f));
            Assert.That(restored.IsMuted(AudioChannel.Music), Is.True);
            Assert.That(restored.IsMuted(AudioChannel.Master), Is.False);
        }

        [Test]
        public void RestoreState_LegacyDictionary_AppliesVolumesWithoutMutes()
        {
            var legacy = new Dictionary<AudioChannel, float>
            {
                { AudioChannel.Master, 0.9f },
                { AudioChannel.Music, 0.1f },
            };

            _manager.RestoreState(legacy);

            Assert.That(_manager.GetVolume(AudioChannel.Master), Is.EqualTo(0.9f));
            Assert.That(_manager.GetVolume(AudioChannel.Music), Is.EqualTo(0.1f));
            Assert.That(_manager.IsMuted(AudioChannel.Master), Is.False);
        }

        [Test]
        public void SetVolume_MarksDirty_SoCaptureDirtyPicksItUp()
        {
            // Configure ya marcó dirty con los defaults — se drena primero para
            // aislar el MarkDirty del SetVolume bajo prueba.
            SaveSystem.CaptureDirty();

            _manager.SetVolume(AudioChannel.Music, 0.42f);
            SaveSystem.CaptureDirty();

            Assert.That(SaveSystem.TryGetCached("audio.volumes", out var cached), Is.True);
            var state = cached as AudioManager.AudioSaveState;
            Assert.That(state, Is.Not.Null);
            Assert.That(state.Volumes[AudioChannel.Music], Is.EqualTo(0.42f));
        }

        [Test]
        public void SetMuted_MarksDirty_SoCaptureDirtyPicksItUp()
        {
            SaveSystem.CaptureDirty();

            _manager.SetMuted(AudioChannel.Master, true);
            SaveSystem.CaptureDirty();

            Assert.That(SaveSystem.TryGetCached("audio.volumes", out var cached), Is.True);
            var state = cached as AudioManager.AudioSaveState;
            Assert.That(state.Muted, Is.EquivalentTo(new[] { AudioChannel.Master }));
        }

        // -------------------------------------------------------------------
        // Conversión lineal → dB (pura)
        // -------------------------------------------------------------------

        [Test]
        public void LinearToDecibels_Zero_IsSilenceFloor()
        {
            Assert.That(AudioSettingsSO.LinearToDecibels(0f), Is.EqualTo(-80f));
        }

        [Test]
        public void LinearToDecibels_One_IsUnity()
        {
            Assert.That(AudioSettingsSO.LinearToDecibels(1f), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void LinearToDecibels_Half_IsMinusSixDb()
        {
            Assert.That(AudioSettingsSO.LinearToDecibels(0.5f), Is.EqualTo(-6.0206f).Within(0.001f));
        }
    }
}
