using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Audio.Tests
{
    [TestFixture]
    public class MusicLibrarySOTests
    {
        private readonly List<Object> _created = new();
        private MusicLibrarySO _library;
        private AudioClip _mainTheme;
        private AudioClip _explo1;
        private AudioClip _combat1;
        private AudioClip _boss1;
        private AudioClip _explo2;

        [SetUp]
        public void SetUp()
        {
            _mainTheme = NewClip("main");
            _explo1 = NewClip("explo1");
            _combat1 = NewClip("combat1");
            _boss1 = NewClip("boss1");
            _explo2 = NewClip("explo2");

            _library = ScriptableObject.CreateInstance<MusicLibrarySO>();
            _created.Add(_library);
            _library.MainTheme = _mainTheme;
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
                    Combat = new List<AudioClip>(),
                    Boss = new List<AudioClip>(),
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null)
                    Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private AudioClip NewClip(string name)
        {
            var clip = AudioClip.Create(name, 44100, 1, 44100, false);
            _created.Add(clip);
            return clip;
        }

        [Test]
        public void GetVariants_MainMenu_ReturnsMainTheme()
        {
            var variants = _library.GetVariants(MusicContext.MainMenu, floorIndex: 0);

            Assert.That(variants, Is.EqualTo(new[] { _mainTheme }));
        }

        [Test]
        public void GetVariants_MainMenuWithoutTheme_ReturnsEmpty()
        {
            _library.MainTheme = null;

            var variants = _library.GetVariants(MusicContext.MainMenu, floorIndex: 0);

            Assert.That(variants, Is.Empty);
        }

        [Test]
        public void GetVariants_EachContext_ReadsItsBucket()
        {
            Assert.That(_library.GetVariants(MusicContext.Exploration, 0), Is.EqualTo(new[] { _explo1 }));
            Assert.That(_library.GetVariants(MusicContext.Combat, 0), Is.EqualTo(new[] { _combat1 }));
            Assert.That(_library.GetVariants(MusicContext.Boss, 0), Is.EqualTo(new[] { _boss1 }));
        }

        [Test]
        public void GetVariants_FloorAboveRange_ClampsToLastAuthored()
        {
            var variants = _library.GetVariants(MusicContext.Exploration, floorIndex: 7);

            Assert.That(variants, Is.EqualTo(new[] { _explo2 }));
        }

        [Test]
        public void GetVariants_NegativeFloor_ClampsToFirst()
        {
            var variants = _library.GetVariants(MusicContext.Exploration, floorIndex: -1);

            Assert.That(variants, Is.EqualTo(new[] { _explo1 }));
        }

        [Test]
        public void GetVariants_EmptyBucket_ReturnsEmpty()
        {
            Assert.That(_library.GetVariants(MusicContext.Combat, 1), Is.Empty);
        }

        [Test]
        public void GetVariants_NoFloorsAuthored_ReturnsEmpty()
        {
            _library.Floors.Clear();

            Assert.That(_library.GetVariants(MusicContext.Exploration, 0), Is.Empty);
        }

        [Test]
        public void GetFadeFor_CombatContexts_UseCombatFade()
        {
            _library.DefaultFadeSeconds = 2f;
            _library.CombatFadeSeconds = 0.5f;

            Assert.That(_library.GetFadeFor(MusicContext.Combat), Is.EqualTo(0.5f));
            Assert.That(_library.GetFadeFor(MusicContext.Boss), Is.EqualTo(0.5f));
            Assert.That(_library.GetFadeFor(MusicContext.Exploration), Is.EqualTo(2f));
            Assert.That(_library.GetFadeFor(MusicContext.MainMenu), Is.EqualTo(2f));
        }
    }
}
