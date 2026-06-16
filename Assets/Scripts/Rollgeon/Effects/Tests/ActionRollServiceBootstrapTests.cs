using System;
using Patterns;
using NUnit.Framework;
using Rollgeon.ActionRolls;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Dice;
using UnityEngine;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Regresión BUG-016: el heal (y Force Door) en exploración dejaba de funcionar
    /// al reiniciar una run dentro de la misma sesión.
    /// <para>
    /// <see cref="ActionRollServiceBootstrap"/> es el único <c>IPreloadableService</c>
    /// con <c>Scope == Run</c>. <c>RunBootstrapper.EndRun</c> hace
    /// <c>ServiceLocator.ClearScope(Run)</c> (que dispone y borra el
    /// <see cref="IActionRollService"/>), y <c>RegisterRunScoped()</c> reinvoca
    /// <see cref="ActionRollServiceBootstrap.Register"/> en cada <c>StartRun</c>. El
    /// guard viejo <c>if (_instance != null) return</c> cacheaba la instancia en el SO
    /// (que vive toda la sesión) y nunca re-registraba el servicio tras la primera run,
    /// dejando a <c>ExplorationBehaviorService.StartActionRoll</c> sin
    /// <see cref="IActionRollService"/> → "IActionRollService no registrado".
    /// </para>
    /// </summary>
    [TestFixture]
    public class ActionRollServiceBootstrapTests
    {
        private ActionRollServiceBootstrap _bootstrap;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            // Prerrequisitos del bootstrap (Priority < 74): roller + energy en Global,
            // así sobreviven al ClearScope(Run) igual que en runtime real.
            ServiceLocator.AddService<IDiceRoller>(new StubRoller(), ServiceScope.Global);
            ServiceLocator.AddService<IEnergyService>(new StubEnergy(), ServiceScope.Global);

            _bootstrap = ScriptableObject.CreateInstance<ActionRollServiceBootstrap>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_bootstrap != null) UnityEngine.Object.DestroyImmediate(_bootstrap);
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void Register_FirstRun_RegistersActionRollService()
        {
            _bootstrap.Register();

            Assert.IsTrue(ServiceLocator.HasService<IActionRollService>(),
                "La primera run debe registrar IActionRollService.");
        }

        [Test]
        public void Register_AfterClearScopeRun_ReregistersActionRollService()
        {
            // Run 1: queda registrado.
            _bootstrap.Register();
            Assert.IsTrue(ServiceLocator.HasService<IActionRollService>(),
                "Precondición: la 1ª run registra el servicio.");

            // EndRun de la run 1: dispone + borra los servicios Run-scope.
            ServiceLocator.ClearScope(ServiceScope.Run);
            Assert.IsFalse(ServiceLocator.HasService<IActionRollService>(),
                "ClearScope(Run) debe borrar el IActionRollService de la run previa.");

            // Run 2 (reinicio): RegisterRunScoped() reinvoca Register() sobre el MISMO
            // SO (que vive toda la sesión). Con el guard viejo esto era no-op y el
            // servicio quedaba sin registrar (BUG-016).
            _bootstrap.Register();

            Assert.IsTrue(ServiceLocator.HasService<IActionRollService>(),
                "La 2ª run debe re-registrar IActionRollService (BUG-016).");
        }

        [Test]
        public void Register_TwiceWithinSameRun_IsIdempotent()
        {
            _bootstrap.Register();
            var first = ServiceLocator.GetService<IActionRollService>();

            _bootstrap.Register();
            var second = ServiceLocator.GetService<IActionRollService>();

            Assert.AreSame(first, second,
                "Dos Register() dentro de la misma run no deben reemplazar la instancia activa.");
        }

        // ---- Stubs mínimos (los fakes de ActionRollServiceTests son private) --------

        private sealed class StubRoller : IDiceRoller
        {
            public int[] RollAll(DiceBagSO bag) => new[] { 1, 1, 1, 1, 1 };
            public int[] Reroll(DiceBagSO bag, int[] previousResult, bool[] keep) => RollAll(bag);
        }

        private sealed class StubEnergy : IEnergyService
        {
            public bool SpendEnergy(Guid id, int cost) => true;
            public int GetCurrent(Guid id) => 99;
            public int GetMax(Guid id) => 99;
            public void InitializeForEntity(Guid id) { }
            public void RegenerateAtTurnEnd(Guid id) { }
        }
    }
}
