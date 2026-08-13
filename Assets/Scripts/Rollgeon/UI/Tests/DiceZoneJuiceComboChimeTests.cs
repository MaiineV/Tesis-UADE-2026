using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Audio;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// El chime de combo festeja un combo NUEVO, no cualquier cambio de id.
    /// </summary>
    /// <remarks>
    /// Regresión: sacar un dado de una mano de 4+ suele dejar otro combo (póker → trío).
    /// El id cambiaba, así que el chime sonaba justo después del unlock — más fuerte y
    /// más tarde, así que lo tapaba y se escuchaba como si hubieras SELECCIONADO. Con 3
    /// dados no pasaba: sacar uno deja sin combo y el id vacío ya cortaba antes.
    /// </remarks>
    [TestFixture]
    public class DiceZoneJuiceComboChimeTests
    {
        private GameObject _go;
        private DiceZoneJuice _juice;
        private FakeAudioService _audio;
        private AudioClip _unlockClip;
        private AudioClip _comboChimeClip;

        [SetUp]
        public void Setup()
        {
            ServiceLocator.Clear();
            _audio = new FakeAudioService();
            ServiceLocator.AddService<IAudioService>(_audio);

            _unlockClip = AudioClip.Create("unlock", 1, 1, 1000, false);
            _comboChimeClip = AudioClip.Create("comboChime", 1, 1, 1000, false);

            _go = new GameObject("ZoneJuice");
            _juice = _go.AddComponent<DiceZoneJuice>();
            SetPrivate(_juice, "_unlockClip", _unlockClip);
            SetPrivate(_juice, "_comboChimeClip", _comboChimeClip);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            if (_go != null) Object.DestroyImmediate(_go);
            // OnEnable se suscribió al bus tipado; el destroy lo desengancha, pero el
            // Clear evita arrastrar suscriptores muertos a otros fixtures.
            TypedEvent<ComboMatchedPayload>.Clear();
            if (_unlockClip != null) Object.DestroyImmediate(_unlockClip);
            if (_comboChimeClip != null) Object.DestroyImmediate(_comboChimeClip);
        }

        // EditMode no avanza Time.frameCount entre llamadas, así que todo lo invocado en
        // un test cae en el mismo frame — justo lo que hace DiceZoneView.ApplyHold.
        private void RaiseComboMatched(string comboId)
            => InvokePrivate(_juice, "HandleComboMatched",
                             new ComboMatchedPayload { ComboId = comboId });

        private void RaiseDieUnlocked() => InvokePrivate(_juice, "HandleDieUnlocked");

        [Test]
        public void should_play_chime_when_a_combo_is_reached()
        {
            // Arrange + Act — sin unlock previo: el jugador sumó un dado.
            RaiseComboMatched("poker");

            // Assert
            Assert.AreEqual(1, _audio.CountOf(_comboChimeClip));
        }

        [Test]
        public void should_not_play_chime_when_the_combo_changed_because_a_die_was_removed()
        {
            // Arrange — póker de 4; sacamos uno y queda trío.
            RaiseComboMatched("poker");
            _audio.Clear();

            // Act — mismo frame, igual que ApplyHold: unlock y después la re-detección.
            RaiseDieUnlocked();
            RaiseComboMatched("trio");

            // Assert
            Assert.AreEqual(1, _audio.CountOf(_unlockClip), "el unlock sí tiene que sonar");
            Assert.AreEqual(0, _audio.CountOf(_comboChimeClip), "el chime no: quitaste, no lograste");
        }

        [Test]
        public void should_play_chime_again_when_the_die_is_put_back()
        {
            // Arrange — la supresión no debe dejar el id desincronizado: volver a poner
            // el dado tiene que festejar de nuevo.
            RaiseComboMatched("poker");
            RaiseDieUnlocked();
            RaiseComboMatched("trio");
            _audio.Clear();

            // Act — el re-hold no pasa por HandleDieUnlocked, así que es otro gesto.
            SetPrivate(_juice, "_lastUnlockFrame", -1);
            RaiseComboMatched("poker");

            // Assert
            Assert.AreEqual(1, _audio.CountOf(_comboChimeClip));
        }

        [Test]
        public void should_not_repeat_chime_while_the_combo_stays_the_same()
        {
            // Arrange
            RaiseComboMatched("poker");
            _audio.Clear();

            // Act
            RaiseComboMatched("poker");

            // Assert
            Assert.AreEqual(0, _audio.CountOf(_comboChimeClip));
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private sealed class FakeAudioService : IAudioService
        {
            public readonly List<AudioClip> Played = new();

            public int CountOf(AudioClip clip) => Played.FindAll(c => c == clip).Count;
            public void Clear() => Played.Clear();

            public void PlaySfx(AudioClip clip, Vector3 worldPos, float volume = 1f,
                                float pitch = 1f, bool isImportant = false) => Played.Add(clip);
            public void PlaySfx2D(AudioClip clip, float volume = 1f, float pitch = 1f,
                                  bool isImportant = false) => Played.Add(clip);
            public void PlayMusic(AudioClip clip, float fadeSeconds = 1f) { }
            public void PlayMusicForBiome(string biomeId, float fadeSeconds = 1f) { }
            public void StopMusic(float fadeSeconds = 1f) { }
            public void PauseMusic() { }
            public void ResumeMusic() { }
            public void SetVolume(AudioChannel channel, float value) { }
            public float GetVolume(AudioChannel channel) => 1f;
        }

        private static FieldInfo Field(object target, string field)
        {
            var info = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"campo {field} no encontrado en {target.GetType().Name}");
            return info;
        }

        private static void SetPrivate(object target, string field, object value)
            => Field(target, field).SetValue(target, value);

        private static void InvokePrivate(object target, string method, params object[] args)
        {
            var info = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"método {method} no encontrado en {target.GetType().Name}");
            info.Invoke(target, args);
        }
    }
}
