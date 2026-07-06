using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Items;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Rollgeon.Patterns.Save.Tests
{
    [TestFixture]
    public class SaveSystemTests
    {
        private InMemorySaveFileStore _store;
        private SaveSettingsSO _settings;

        [SetUp]
        public void SetUp()
        {
            SaveSystem.ResetForTests();
            _store = new InMemorySaveFileStore();
            SaveSystem.SetStoreForTests(_store);

            _settings = ScriptableObject.CreateInstance<SaveSettingsSO>();
            ServiceLocator.AddService<SaveSettingsSO>(_settings, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            SaveSystem.ResetForTests();
            if (_settings != null) Object.DestroyImmediate(_settings);
        }

        // ====================================================================
        // Registration
        // ====================================================================

        [Test]
        public void Register_WithCachedState_AutoRestores()
        {
            var first = new FakeSaveable("k", 42);
            SaveSystem.Register(first);
            SaveSystem.CaptureAll();

            var second = new FakeSaveable("k");
            SaveSystem.Register(second);

            Assert.AreEqual(1, second.RestoreCalls);
            Assert.AreEqual(42, second.LastRestored);
        }

        [Test]
        public void Register_SameInstanceTwice_RegistersOnce()
        {
            var s = new FakeSaveable("k", 1);
            SaveSystem.Register(s);
            SaveSystem.Register(s);

            Assert.AreEqual(1, SaveSystem.RegisteredCountForTests);
        }

        [Test]
        public void Unregister_RemovesAndCapturesFinalStateIntoCache()
        {
            var s = new FakeSaveable("k", 1);
            SaveSystem.Register(s);
            s.State = 99;
            SaveSystem.Unregister(s);

            Assert.AreEqual(0, SaveSystem.RegisteredCountForTests);

            // El cache retuvo el estado final: un registro nuevo lo re-hidrata.
            var reborn = new FakeSaveable("k");
            SaveSystem.Register(reborn);
            Assert.AreEqual(99, reborn.LastRestored);
        }

        // ====================================================================
        // Capture / Restore
        // ====================================================================

        [Test]
        public void CaptureAll_UpdatesCacheAndFiresOnCaptureRequested()
        {
            var fired = 0;
            EventManager.EventReceiver handler = _ => fired++;
            EventManager.Subscribe(EventName.OnCaptureRequested, handler);
            try
            {
                var s = new FakeSaveable("k", 7);
                SaveSystem.Register(s);
                SaveSystem.CaptureAll();

                Assert.AreEqual(1, fired);
                var probe = new FakeSaveable("k");
                SaveSystem.Register(probe);
                Assert.AreEqual(7, probe.LastRestored);
            }
            finally
            {
                EventManager.UnSubscribe(EventName.OnCaptureRequested, handler);
            }
        }

        [Test]
        public void RestoreAll_FiresOnRestoreCompleted()
        {
            var fired = 0;
            EventManager.EventReceiver handler = _ => fired++;
            EventManager.Subscribe(EventName.OnRestoreCompleted, handler);
            try
            {
                SaveSystem.RestoreAll();
                Assert.AreEqual(1, fired);
            }
            finally
            {
                EventManager.UnSubscribe(EventName.OnRestoreCompleted, handler);
            }
        }

        // ====================================================================
        // Flush / Load round-trip
        // ====================================================================

        [Test]
        public void FlushAndLoadFromDisk_PolymorphicPayload_RoundTrips()
        {
            var boxedInt = new FakeSaveable("run.floor_index", 3);
            var dict = new FakeSaveable("run.combo_counter_state",
                new Dictionary<string, int> { { "combo.par", 2 }, { "combo.trio", 1 } });
            var dto = new FakeSaveable("run.inventory", new InventorySnapshot
            {
                PassiveItemIds = new List<string> { "item.ring" },
                ActiveSlots = new List<InventorySlotSnapshot>
                {
                    new InventorySlotSnapshot { ItemId = "item.potion", CurrentCooldown = 2 },
                },
            });

            SaveSystem.Register(boxedInt);
            SaveSystem.Register(dict);
            SaveSystem.Register(dto);
            SaveSystem.CaptureAll();
            SaveSystem.Flush(SaveTrigger.Manual);

            Assert.AreEqual(1, _store.WriteCount);

            // Simula reinicio de proceso: estado estático limpio, mismo disco.
            SaveSystem.ResetForTests();
            SaveSystem.SetStoreForTests(_store);

            var rInt = new FakeSaveable("run.floor_index");
            var rDict = new FakeSaveable("run.combo_counter_state");
            var rDto = new FakeSaveable("run.inventory");
            SaveSystem.Register(rInt);
            SaveSystem.Register(rDict);
            SaveSystem.Register(rDto);

            SaveSystem.LoadFromDisk();

            Assert.AreEqual(3, rInt.LastRestored);

            var restoredDict = rDict.LastRestored as Dictionary<string, int>;
            Assert.NotNull(restoredDict, "el Dictionary<string,int> debe round-trippear con su tipo");
            Assert.AreEqual(2, restoredDict["combo.par"]);
            Assert.AreEqual(1, restoredDict["combo.trio"]);

            var restoredDto = rDto.LastRestored as InventorySnapshot;
            Assert.NotNull(restoredDto, "el DTO debe round-trippear con su tipo");
            Assert.AreEqual("item.ring", restoredDto.PassiveItemIds[0]);
            Assert.AreEqual("item.potion", restoredDto.ActiveSlots[0].ItemId);
            Assert.AreEqual(2, restoredDto.ActiveSlots[0].CurrentCooldown);
        }

        [Test]
        public void LoadFromDisk_CorruptBytes_WarnsAndKeepsFreshState()
        {
            _store.Files[_settings.GetSavePath()] = Encoding.UTF8.GetBytes("{ not valid json ]");

            // Odin puede loguear sus propios errores al deserializar basura — el
            // contrato observable es "degradar a sin-save sin crashear ni restaurar".
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var s = new FakeSaveable("k");
                SaveSystem.Register(s);

                SaveSystem.LoadFromDisk();

                Assert.AreEqual(0, s.RestoreCalls);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void LoadFromDisk_SchemaMismatch_DiscardsSave()
        {
            var stale = new SavePayload
            {
                SchemaVersion = 999,
                Data = new Dictionary<string, object> { { "k", 1 } },
            };
            _store.Files[_settings.GetSavePath()] =
                SerializationUtility.SerializeValue(stale, DataFormat.JSON);

            LogAssert.Expect(LogType.Warning, new Regex("schema v999 incompatible"));

            var s = new FakeSaveable("k");
            SaveSystem.Register(s);
            SaveSystem.LoadFromDisk();

            Assert.AreEqual(0, s.RestoreCalls);
        }

        [Test]
        public void LoadFromDisk_MissingFile_NoOps()
        {
            var s = new FakeSaveable("k");
            SaveSystem.Register(s);

            SaveSystem.LoadFromDisk();

            Assert.AreEqual(0, s.RestoreCalls);
            LogAssert.NoUnexpectedReceived();
        }

        // ====================================================================
        // WeakReference purge
        // ====================================================================

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RegisterTransient()
        {
            SaveSystem.Register(new FakeSaveable("gc.transient", 1));
        }

        [Test]
        public void CaptureAll_CollectedTarget_PurgesWeakReference()
        {
            RegisterTransient();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            SaveSystem.CaptureAll();

            Assert.AreEqual(0, SaveSystem.RegisteredCountForTests);
        }

        // ====================================================================
        // Settings gating
        // ====================================================================

        [Test]
        public void Flush_TriggerDisabledInSettings_DoesNotWrite()
        {
            _settings.FlushOn = Array.Empty<SaveTrigger>();

            SaveSystem.Register(new FakeSaveable("k", 1));
            SaveSystem.CaptureAll();
            SaveSystem.Flush(SaveTrigger.Manual);

            Assert.AreEqual(0, _store.WriteCount);
        }

        [Test]
        public void Flush_NoSettingsRegistered_WarnsAndNoOps()
        {
            ServiceLocator.RemoveService<SaveSettingsSO>();
            LogAssert.Expect(LogType.Warning, new Regex(@"Flush\(Manual\) sin SaveSettingsSO"));

            SaveSystem.Flush(SaveTrigger.Manual);

            Assert.AreEqual(0, _store.WriteCount);
        }

        [Test]
        public void Clear_EmptiesCacheAndDirty()
        {
            var s = new FakeSaveable("k", 5);
            SaveSystem.Register(s);
            SaveSystem.CaptureAll();

            SaveSystem.Clear();

            // Sin cache, un registro nuevo con la misma key no re-hidrata nada.
            var probe = new FakeSaveable("k");
            SaveSystem.Register(probe);
            Assert.AreEqual(0, probe.RestoreCalls);
        }
    }
}
