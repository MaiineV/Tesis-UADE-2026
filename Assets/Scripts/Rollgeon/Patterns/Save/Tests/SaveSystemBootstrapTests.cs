using System;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Patterns.Save.Tests
{
    [TestFixture]
    public class SaveSystemBootstrapTests
    {
        private InMemorySaveFileStore _store;
        private SaveSettingsSO _settings;
        private SaveSystemBootstrap _bootstrap;

        [SetUp]
        public void SetUp()
        {
            SaveSystem.ResetForTests();
            _store = new InMemorySaveFileStore();
            SaveSystem.SetStoreForTests(_store);

            // Defaults del SO: FlushOn = {RunStart, FloorEnd, Manual, RunEnd, Exit}.
            _settings = ScriptableObject.CreateInstance<SaveSettingsSO>();
            ServiceLocator.AddService<SaveSettingsSO>(_settings, ServiceScope.Global);

            _bootstrap = new SaveSystemBootstrap();
        }

        [TearDown]
        public void TearDown()
        {
            _bootstrap?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            SaveSystem.ResetForTests();
            if (_settings != null) Object.DestroyImmediate(_settings);
        }

        [Test]
        public void Register_ThenOnFloorChanged_CapturesAndFlushes()
        {
            var s = new FakeSaveable("k", 5);
            SaveSystem.Register(s);
            _bootstrap.Register();

            EventManager.Trigger(EventName.OnFloorChanged, Guid.NewGuid(), 1);

            Assert.AreEqual(1, _store.WriteCount);
        }

        [Test]
        public void Register_ThenOnRoomEntered_CapturesButDoesNotFlush_WithDefaultSettings()
        {
            var s = new FakeSaveable("k", 5);
            SaveSystem.Register(s);
            _bootstrap.Register();

            EventManager.Trigger(EventName.OnRoomEntered, Guid.NewGuid(), "room.combate_01");

            // RoomEnd no está en FlushOn por default — sólo captura en memoria.
            Assert.AreEqual(0, _store.WriteCount);
            var probe = new FakeSaveable("k");
            SaveSystem.Register(probe);
            Assert.AreEqual(5, probe.LastRestored);
        }

        [Test]
        public void OnRunEnd_CapturesAndFlushes()
        {
            var s = new FakeSaveable("k", 5);
            SaveSystem.Register(s);
            _bootstrap.Register();

            EventManager.Trigger(EventName.OnRunEnd, Guid.NewGuid(), (object)null);

            Assert.AreEqual(1, _store.WriteCount);
        }

        [Test]
        public void Register_Twice_DoesNotDoubleSubscribe()
        {
            _bootstrap.Register();
            _bootstrap.Register();

            EventManager.Trigger(EventName.OnFloorChanged, Guid.NewGuid(), 1);

            Assert.AreEqual(1, _store.WriteCount);
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            _bootstrap.Register();
            _bootstrap.Dispose();

            EventManager.Trigger(EventName.OnFloorChanged, Guid.NewGuid(), 1);

            Assert.AreEqual(0, _store.WriteCount);
        }
    }
}
