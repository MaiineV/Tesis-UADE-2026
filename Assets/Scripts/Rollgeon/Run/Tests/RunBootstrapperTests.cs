using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Run.Tests
{
    [TestFixture]
    public class RunBootstrapperTests
    {
        private ClassHeroSO _hero;
        private PlayerService _playerService;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

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
            if (_hero != null)
                UnityEngine.Object.DestroyImmediate(_hero);
        }

        [Test]
        public void StartRun_RegistersRunContext()
        {
            var runId = Guid.NewGuid();

            RunBootstrapper.StartRun(_hero, null, runId);

            Assert.IsTrue(ServiceLocator.HasService<IRunContextService>());
            var ctx = ServiceLocator.GetService<IRunContextService>();
            Assert.AreEqual(runId, ctx.RunId);
            Assert.AreSame(_hero, ctx.SelectedHero);
            Assert.IsTrue(ctx.IsRunActive);
        }

        [Test]
        public void StartRun_CallsPlayerServiceSetPlayer()
        {
            var runId = Guid.NewGuid();

            RunBootstrapper.StartRun(_hero, null, runId);

            Assert.AreSame(_hero, _playerService.CurrentHero);
            Assert.AreEqual(runId, _playerService.RunId);
        }

        [Test]
        public void StartRun_FiresOnRunStartEvent()
        {
            var runId = Guid.NewGuid();
            bool fired = false;
            EventManager.Subscribe(EventName.OnRunStart, args => fired = true);

            RunBootstrapper.StartRun(_hero, null, runId);

            Assert.IsTrue(fired);
        }

        [Test]
        public void EndRun_ClearsRunScope()
        {
            var runId = Guid.NewGuid();
            RunBootstrapper.StartRun(_hero, null, runId);

            RunBootstrapper.EndRun(runId);

            Assert.IsFalse(ServiceLocator.HasService<IRunContextService>());
        }

        [Test]
        public void EndRun_CallsPlayerServiceClearPlayer()
        {
            var runId = Guid.NewGuid();
            RunBootstrapper.StartRun(_hero, null, runId);

            RunBootstrapper.EndRun(runId);

            Assert.IsNull(_playerService.CurrentHero);
            Assert.AreEqual(Guid.Empty, _playerService.RunId);
        }

        [Test]
        public void EndRun_SetsRunContextInactive()
        {
            var runId = Guid.NewGuid();
            RunBootstrapper.StartRun(_hero, null, runId);
            var ctx = ServiceLocator.GetService<IRunContextService>();

            RunBootstrapper.EndRun(runId);

            Assert.IsFalse(ctx.IsRunActive);
        }

        [Test]
        public void EndRun_FiresOnRunEndEvent()
        {
            var runId = Guid.NewGuid();
            RunBootstrapper.StartRun(_hero, null, runId);
            bool fired = false;
            EventManager.Subscribe(EventName.OnRunEnd, args => fired = true);

            RunBootstrapper.EndRun(runId);

            Assert.IsTrue(fired);
        }

        [Test]
        public void ClearScope_DisposesRunContext()
        {
            var runId = Guid.NewGuid();
            RunBootstrapper.StartRun(_hero, null, runId);
            var ctx = ServiceLocator.GetService<IRunContextService>();

            ServiceLocator.ClearScope(ServiceScope.Run);

            // Dispose sets IsRunActive = false
            Assert.IsFalse(ctx.IsRunActive);
        }
    }
}
