using System;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Dungeon;
using Rollgeon.Heroes;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Run.Tests
{
    /// <summary>
    /// Cobertura del resume de run (#Continue): semántica de StartRun(resume),
    /// delete-on-run-over vs keep-on-quit en EndRun, fast-forward de floor layout
    /// y round-trip del RunContextSnapshot.
    /// </summary>
    [TestFixture]
    public class ResumeSaveTests
    {
        private ClassHeroSO _hero;
        private PlayerService _playerService;
        private ServiceBootstrapSO _previousActive;
        private InMemoryStore _store;
        private SaveSettingsSO _settings;

        // Copia local del InMemorySaveFileStore de Patterns.Save.Tests — los test
        // asmdefs no se referencian entre sí.
        private sealed class InMemoryStore : ISaveFileStore
        {
            public readonly System.Collections.Generic.Dictionary<string, byte[]> Files = new();
            public bool Exists(string path) => Files.ContainsKey(path);
            public byte[] Read(string path) => Files[path];
            public void Write(string path, byte[] bytes) => Files[path] = bytes;
            public void Delete(string path) => Files.Remove(path);
        }

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            SaveSystem.ResetForTests();

            _previousActive = ServiceBootstrapSO.Active;
            ServiceBootstrapSO.Active = null;

            _store = new InMemoryStore();
            SaveSystem.SetStoreForTests(_store);
            _settings = ScriptableObject.CreateInstance<SaveSettingsSO>();
            ServiceLocator.AddService<SaveSettingsSO>(_settings, ServiceScope.Global);

            _hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _playerService = new PlayerService();
            ServiceLocator.AddService<IPlayerService>(_playerService, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            _playerService.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            SaveSystem.ResetForTests();
            if (_hero != null) Object.DestroyImmediate(_hero);
            if (_settings != null) Object.DestroyImmediate(_settings);
            ServiceBootstrapSO.Active = _previousActive;
        }

        // ====================================================================
        // StartRun resume semantics
        // ====================================================================

        [Test]
        public void StartRun_NewRun_ClearsSaveCache()
        {
            var stale = new RunContextSnapshot { FloorIndex = 3, RunId = Guid.NewGuid().ToString() };
            SeedCache(RunContext.SaveKeyConst, stale);

            RunBootstrapper.StartRun(_hero, null, Guid.NewGuid());

            var ctx = ServiceLocator.GetService<IRunContextService>();
            Assert.AreEqual(0, ctx.FloorIndex, "run nueva no debe heredar el floor del save");
        }

        [Test]
        public void StartRun_Resume_RestoresFloorIndexFromCache()
        {
            var saved = new RunContextSnapshot { FloorIndex = 2, RunId = Guid.NewGuid().ToString() };
            SeedCache(RunContext.SaveKeyConst, saved);

            RunBootstrapper.StartRun(_hero, null, Guid.NewGuid(), resume: true);

            var ctx = ServiceLocator.GetService<IRunContextService>();
            Assert.AreEqual(2, ctx.FloorIndex);
        }

        [Test]
        public void StartRun_Resume_SetsIsResuming_AndEndRunResetsIt()
        {
            var runId = Guid.NewGuid();

            RunBootstrapper.StartRun(_hero, null, runId, resume: true);
            Assert.IsTrue(RunBootstrapper.IsResuming);

            RunBootstrapper.EndRun(runId, runCompleted: false);
            Assert.IsFalse(RunBootstrapper.IsResuming);
        }

        [Test]
        public void StartRun_NewRun_IsResumingFalse()
        {
            RunBootstrapper.StartRun(_hero, null, Guid.NewGuid());

            Assert.IsFalse(RunBootstrapper.IsResuming);
        }

        // ====================================================================
        // EndRun: delete-on-run-over vs keep-on-quit
        // ====================================================================

        [Test]
        public void EndRun_RunCompleted_DeletesSaveFile()
        {
            _store.Files[_settings.GetSavePath()] = new byte[] { 1 };
            var runId = Guid.NewGuid();
            RunBootstrapper.StartRun(_hero, null, runId, resume: true);

            RunBootstrapper.EndRun(runId, runCompleted: true);

            Assert.IsFalse(SaveSystem.HasSave(), "victoria/derrota debe borrar el save");
        }

        [Test]
        public void EndRun_QuitMidRun_KeepsSaveFile()
        {
            _store.Files[_settings.GetSavePath()] = new byte[] { 1 };
            var runId = Guid.NewGuid();
            RunBootstrapper.StartRun(_hero, null, runId, resume: true);

            RunBootstrapper.EndRun(runId, runCompleted: false);

            Assert.IsTrue(SaveSystem.HasSave(), "quit desde pausa debe conservar el save");
        }

        // ====================================================================
        // Floor fast-forward
        // ====================================================================

        [Test]
        public void ResolveLayoutForFloor_IndexZero_ReturnsFirst()
        {
            var chain = MakeChain(3);
            try
            {
                Assert.AreSame(chain[0], FloorProgressionService.ResolveLayoutForFloor(chain[0], 0));
            }
            finally { DestroyChain(chain); }
        }

        [Test]
        public void ResolveLayoutForFloor_WalksNextFloorChain()
        {
            var chain = MakeChain(3);
            try
            {
                Assert.AreSame(chain[2], FloorProgressionService.ResolveLayoutForFloor(chain[0], 2));
            }
            finally { DestroyChain(chain); }
        }

        [Test]
        public void ResolveLayoutForFloor_IndexBeyondChain_ClampsToLast()
        {
            var chain = MakeChain(3);
            try
            {
                Assert.AreSame(chain[2], FloorProgressionService.ResolveLayoutForFloor(chain[0], 99));
            }
            finally { DestroyChain(chain); }
        }

        [Test]
        public void DeriveSeed_SameInputs_Deterministic()
        {
            Assert.AreEqual(
                FloorProgressionService.DeriveSeed(1234, 2),
                FloorProgressionService.DeriveSeed(1234, 2));
            Assert.AreNotEqual(
                FloorProgressionService.DeriveSeed(1234, 1),
                FloorProgressionService.DeriveSeed(1234, 2));
        }

        // ====================================================================
        // RunContextSnapshot round-trip
        // ====================================================================

        [Test]
        public void RunContext_CaptureState_IncludesFloorAndRunId()
        {
            var runId = Guid.NewGuid();
            var ctx = new RunContext(runId, _hero);
            ctx.AdvanceFloor();

            var snapshot = ctx.CaptureState() as RunContextSnapshot;

            Assert.NotNull(snapshot);
            Assert.AreEqual(1, snapshot.FloorIndex);
            Assert.AreEqual(runId.ToString(), snapshot.RunId);
        }

        [Test]
        public void RunContext_RestoreState_AppliesFloorIndex()
        {
            var ctx = new RunContext(Guid.NewGuid(), _hero);

            ctx.RestoreState(new RunContextSnapshot { FloorIndex = 4, RunId = Guid.NewGuid().ToString() });

            Assert.AreEqual(4, ctx.FloorIndex);
        }

        // ====================================================================
        // Shop seed determinista
        // ====================================================================

        [Test]
        public void DeriveShopSeed_SameFloorSeedAndCell_Deterministic()
        {
            var cell = new Vector2Int(3, 5);
            Assert.AreEqual(
                Rollgeon.Shop.ShopManagerService.DeriveShopSeed(777, cell),
                Rollgeon.Shop.ShopManagerService.DeriveShopSeed(777, cell));
            Assert.AreNotEqual(
                Rollgeon.Shop.ShopManagerService.DeriveShopSeed(777, cell),
                Rollgeon.Shop.ShopManagerService.DeriveShopSeed(778, cell));
            Assert.AreNotEqual(
                Rollgeon.Shop.ShopManagerService.DeriveShopSeed(777, cell),
                Rollgeon.Shop.ShopManagerService.DeriveShopSeed(777, new Vector2Int(3, 6)));
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private static void SeedCache(string key, object state)
        {
            // Puebla el cache estático vía el camino público: un saveable efímero
            // que se registra, captura y desregistra.
            var seeder = new CacheSeeder(key, state);
            SaveSystem.Register(seeder);
            SaveSystem.CaptureAll();
            SaveSystem.Unregister(seeder);
        }

        private sealed class CacheSeeder : ISaveable
        {
            private readonly string _key;
            private readonly object _state;
            public CacheSeeder(string key, object state) { _key = key; _state = state; }
            public string SaveKey => _key;
            public object CaptureState() => _state;
            public void RestoreState(object state) { }
        }

        private static FloorLayoutSO[] MakeChain(int count)
        {
            var chain = new FloorLayoutSO[count];
            for (int i = 0; i < count; i++)
                chain[i] = ScriptableObject.CreateInstance<FloorLayoutSO>();
            for (int i = 0; i < count - 1; i++)
                chain[i].NextFloor = chain[i + 1];
            return chain;
        }

        private static void DestroyChain(FloorLayoutSO[] chain)
        {
            foreach (var layout in chain)
                if (layout != null) Object.DestroyImmediate(layout);
        }
    }
}
