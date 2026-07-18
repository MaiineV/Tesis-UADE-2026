using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.GameCamera;
using Rollgeon.Heroes;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Run.Tests
{
    [TestFixture]
    public class RunBootstrapperTests
    {
        private ClassHeroSO _hero;
        private PlayerService _playerService;
        private ServiceBootstrapSO _previousActive;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            _previousActive = ServiceBootstrapSO.Active;
            ServiceBootstrapSO.Active = null;

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

            ServiceBootstrapSO.Active = _previousActive;
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

        // Regresión: StartRun abre con ClearScope(Run) para disponer leftovers de una run
        // anterior. La cámara de 02_Gameplay se registraba en Run scope desde su Awake y,
        // como Unity corre todos los Awake antes de cualquier Start, ese ClearScope la
        // borraba apenas arrancaba la run: se perdían el SetFollowTarget del bootstrapper
        // y el recenter del RoomGridLoader al entrar a una sala. La cámara vive con la
        // scene, no con la run — StartRun no la puede tocar.
        [Test]
        public void StartRun_KeepsSceneRegisteredCameraService()
        {
            var cameraGO = new GameObject("TestMainCamera", typeof(UnityEngine.Camera));
            var config = ScriptableObject.CreateInstance<CameraConfigSO>();
            try
            {
                // Initialize es lo que corre el Awake de la cámara en 02_Gameplay, y es
                // quien la registra. Acá se llama a mano porque en EditMode los callbacks
                // de lifecycle de Unity no se disparan.
                var camera = cameraGO.AddComponent<CameraService>();
                camera.Initialize(config);

                RunBootstrapper.StartRun(_hero, null, Guid.NewGuid());

                Assert.IsTrue(ServiceLocator.TryGetService<ICameraService>(out var registered),
                    "StartRun no debe desregistrar la cámara: sin ICameraService no hay " +
                    "recenter al entrar a una sala.");
                Assert.AreSame(camera, registered);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraGO);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void EndRun_ClearsRunScope()
        {
            var runId = Guid.NewGuid();
            RunBootstrapper.StartRun(_hero, null, runId);

            RunBootstrapper.EndRun(runId, runCompleted: false);

            Assert.IsFalse(ServiceLocator.HasService<IRunContextService>());
        }

        [Test]
        public void EndRun_CallsPlayerServiceClearPlayer()
        {
            var runId = Guid.NewGuid();
            RunBootstrapper.StartRun(_hero, null, runId);

            RunBootstrapper.EndRun(runId, runCompleted: false);

            Assert.IsNull(_playerService.CurrentHero);
            Assert.AreEqual(Guid.Empty, _playerService.RunId);
        }

        [Test]
        public void EndRun_SetsRunContextInactive()
        {
            var runId = Guid.NewGuid();
            RunBootstrapper.StartRun(_hero, null, runId);
            var ctx = ServiceLocator.GetService<IRunContextService>();

            RunBootstrapper.EndRun(runId, runCompleted: false);

            Assert.IsFalse(ctx.IsRunActive);
        }

        [Test]
        public void EndRun_FiresOnRunEndEvent()
        {
            var runId = Guid.NewGuid();
            RunBootstrapper.StartRun(_hero, null, runId);
            bool fired = false;
            EventManager.Subscribe(EventName.OnRunEnd, args => fired = true);

            RunBootstrapper.EndRun(runId, runCompleted: false);

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

        // ----------------------------------------------------------------
        // Regresión: F#0005 Run-scoped IPreloadableService re-registration
        // bug — los servicios Run-scope se borraban en EndRun y no se volvían
        // a registrar en la próxima StartRun.
        // ----------------------------------------------------------------

        [Test]
        public void StartRun_AfterEndRun_ReregistersRunScopedPreloadables()
        {
            // Arrange
            var stub = new RunScopedStub();
            var so = ScriptableObject.CreateInstance<ServiceBootstrapSO>();
            so.ExtraServices = new List<IPreloadableService> { stub };
            ServiceBootstrapSO.Active = so;
            try
            {
                var runId1 = Guid.NewGuid();

                // Act
                RunBootstrapper.StartRun(_hero, null, runId1);
                Assert.AreEqual(1, stub.RegisterCount, "Primera StartRun no invocó Register.");
                Assert.IsTrue(ServiceLocator.HasService<RunScopedStub>(), "Stub no quedó registrado tras la 1ª run.");

                RunBootstrapper.EndRun(runId1, runCompleted: false);
                Assert.IsFalse(ServiceLocator.HasService<RunScopedStub>(), "EndRun no limpió el stub Run-scoped.");

                RunBootstrapper.StartRun(_hero, null, Guid.NewGuid());

                // Assert
                Assert.AreEqual(2, stub.RegisterCount, "StartRun #2 no reinvocó Register.");
                Assert.IsTrue(ServiceLocator.HasService<RunScopedStub>(), "Stub no se re-registró tras la 2ª run.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // ----------------------------------------------------------------
        // Regresión: BUG-012 — la build de dados volvía a ser todos D6.
        //
        // El built bag se aplicaba DESPUÉS de disparar OnRunStart, así que los
        // servicios que siembran estado desde IPlayerService.DiceBag en ese evento
        // (DiceEnchantmentService → RuntimeDiceBag, consumido por EnchantedDiceRoller)
        // quedaban sembrados con el StartingDiceBagRef del hero (5×D6 para el Guerrero)
        // y todos los dados se tiraban como D6 ignorando la build. La build debe estar
        // aplicada ANTES de que OnRunStart se dispare.
        // ----------------------------------------------------------------

        [Test]
        public void StartRun_AppliesBuiltDiceBag_BeforeFiringOnRunStart()
        {
            // Arrange — hero con StartingDiceBagRef = 5×D6 (como CH_Warrior).
            var startingBag = ScriptableObject.CreateInstance<DiceBagSO>();
            startingBag.Dice = new List<DiceType>
                { DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6 };
            _hero.StartingDiceBagRef = startingBag;

            var builtBag = ScriptableObject.CreateInstance<DiceBagSO>();
            builtBag.Dice = new List<DiceType>
                { DiceType.D20, DiceType.D12, DiceType.D10, DiceType.D8, DiceType.D8 };

            // Capturamos el bag que ven los listeners de OnRunStart — el momento en que
            // DiceEnchantmentService siembra su RuntimeDiceBag.
            List<DiceType> bagAtRunStart = null;
            EventManager.Subscribe(EventName.OnRunStart,
                _ => bagAtRunStart = _playerService.DiceBag != null
                    ? new List<DiceType>(_playerService.DiceBag.Dice)
                    : null);

            try
            {
                // Act
                RunBootstrapper.StartRun(_hero, null, Guid.NewGuid(), builtBag);

                // Assert — al disparar OnRunStart el bag activo ya es la build, no 5×D6.
                Assert.IsNotNull(bagAtRunStart, "OnRunStart no vio ningún DiceBag.");
                CollectionAssert.AreEqual(builtBag.Dice, bagAtRunStart,
                    "OnRunStart vio el StartingDiceBagRef (5×D6) en vez de la build elegida (BUG-012).");
                Assert.AreSame(builtBag, _playerService.DiceBag,
                    "El built bag debe quedar activo tras StartRun.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(startingBag);
                UnityEngine.Object.DestroyImmediate(builtBag);
            }
        }

        [Test]
        public void StartRun_WithNullActive_DoesNotThrow()
        {
            // Arrange
            ServiceBootstrapSO.Active = null;

            // Act + Assert
            Assert.DoesNotThrow(() => RunBootstrapper.StartRun(_hero, null, Guid.NewGuid()));
        }

        [Test]
        public void StartRun_DoesNotReinvokeGlobalPreloadables()
        {
            // Arrange
            var globalStub = new GlobalScopedStub();
            var so = ScriptableObject.CreateInstance<ServiceBootstrapSO>();
            so.ExtraServices = new List<IPreloadableService> { globalStub };
            ServiceBootstrapSO.Active = so;
            try
            {
                // Sólo RegisterRunScoped corre desde StartRun. Como el stub es Global,
                // su contador queda en 0 incluso después de varios StartRun.
                RunBootstrapper.StartRun(_hero, null, Guid.NewGuid());
                RunBootstrapper.EndRun(Guid.Empty, runCompleted: false);
                RunBootstrapper.StartRun(_hero, null, Guid.NewGuid());

                Assert.AreEqual(0, globalStub.RegisterCount,
                    "RegisterRunScoped invocó Register() en un stub con Scope=Global.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // ----------------------------------------------------------------
        // Regresión: boss no spawnea en la 2ª run. Si un camino al menú saltea
        // EndRun, los servicios Run-scoped de la run previa sobreviven en el
        // ServiceLocator estático y duplican suscripciones al EventManager (ej.
        // CombatHandoffService resuelve contra su dungeon con el boss room ya
        // Cleared, y bloquea el combate real). StartRun debe disponerlos.
        // ----------------------------------------------------------------

        [Test]
        public void StartRun_DisposesLeftoverRunScopedServices_WhenPreviousEndRunSkipped()
        {
            // Arrange: simula un servicio Run-scoped que sobrevivió porque la run anterior
            // no llamó EndRun (SceneSwitcher de dev, quit sin IRunContextService, etc.).
            var leftover = new DisposableRunStub();
            ServiceLocator.AddService<DisposableRunStub>(leftover, ServiceScope.Run);

            // Act
            RunBootstrapper.StartRun(_hero, null, Guid.NewGuid());

            // Assert
            Assert.IsTrue(leftover.Disposed,
                "StartRun no dispuso el servicio Run-scoped leftover de la run anterior.");
            Assert.IsFalse(ServiceLocator.HasService<DisposableRunStub>(),
                "El servicio leftover debe quedar desregistrado tras StartRun.");
        }

        private sealed class DisposableRunStub : IDisposable
        {
            public bool Disposed { get; private set; }
            public void Dispose() => Disposed = true;
        }

        private sealed class RunScopedStub : IPreloadableService
        {
            public int RegisterCount { get; private set; }
            public int Priority => 0;
            public ServiceScope Scope => ServiceScope.Run;

            public void Register()
            {
                RegisterCount++;
                ServiceLocator.AddService<RunScopedStub>(this, ServiceScope.Run);
            }
        }

        private sealed class GlobalScopedStub : IPreloadableService
        {
            public int RegisterCount { get; private set; }
            public int Priority => 0;
            public ServiceScope Scope => ServiceScope.Global;

            public void Register()
            {
                RegisterCount++;
            }
        }
    }
}
