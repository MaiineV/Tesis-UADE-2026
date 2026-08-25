using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.DevConsole.Cheats;
using Rollgeon.Heroes;
using UnityEngine;

namespace Rollgeon.DevConsole.Tests
{
    /// <summary>
    /// BUG-062 (hardening): <see cref="GodModeController"/> vive en <c>DevConsoleSession</c>,
    /// fuera de <c>ServiceScope.Run</c> — sin auto-apagado, God Mode prendido para debuggear
    /// sobrevive a <c>RunBootstrapper.EndRun</c> y la próxima run (o el piso 2+ de la misma)
    /// arranca "inmortal" sin que nadie lo haya pedido. Separado de
    /// <see cref="GodModeControllerTests"/> (que cubre el pin de HP) para no tocar ese archivo.
    /// </summary>
    public class GodModeControllerRunEndTests
    {
        private Guid _pid;
        private AttributesManager _am;
        private FakeConsoleContext _ctx;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();

            _pid = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.SetAttribute<Health>(new Health(50));
            _am = new AttributesManager();
            _am.Register(_pid, attrs);

            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            hero.BaseMaxHp = 100;

            var player = new FakePlayerService { PlayerGuid = _pid, CurrentHero = hero };
            _ctx = new FakeConsoleContext { PlayerGuid = _pid, IsRunActive = true };
            _ctx.Register<AttributesManager>(_am);
            _ctx.Register<Rollgeon.Player.IPlayerService>(player);

            ServiceLocator.AddService<Rollgeon.Player.IPlayerService>(player);
            ServiceLocator.AddService<AttributesManager>(_am);
        }

        [TearDown]
        public void TearDown()
        {
            _am.Dispose();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        [Test]
        public void OnRunEnd_WhileEnabled_AutoDisablesGodMode()
        {
            // Arrange
            var god = new GodModeController(_ctx);
            god.Enable();

            // Act
            EventManager.Trigger(EventName.OnRunEnd, Guid.NewGuid(), (object)null);

            // Assert
            Assert.IsFalse(god.Enabled,
                "God Mode debe auto-apagarse al terminar la run — no debe sobrevivir a la próxima.");
        }

        [Test]
        public void OnRunEnd_WhileEnabled_StopsRestoringHpAfterwards()
        {
            // Arrange
            var god = new GodModeController(_ctx);
            god.Enable();
            EventManager.Trigger(EventName.OnRunEnd, Guid.NewGuid(), (object)null);

            // Act — daño simulado después del fin de la run (ej. ya en la run siguiente).
            _am.SetAttributeValue<Health, int>(_pid, 30);

            // Assert
            Assert.AreEqual(30, _am.GetAttributeValue<Health, int>(_pid),
                "sin el auto-apagado, el pin seguiría restaurando el HP a 100 en la run siguiente.");
        }

        [Test]
        public void OnRunEnd_WhileDisabled_DoesNothing()
        {
            // Arrange — God Mode nunca se activó en esta run.
            var god = new GodModeController(_ctx);

            // Act
            EventManager.Trigger(EventName.OnRunEnd, Guid.NewGuid(), (object)null);

            // Assert
            Assert.IsFalse(god.Enabled);
        }

        [Test]
        public void Dispose_Unsubscribes_NoAutoDisableSideEffectAfterDispose()
        {
            // Arrange
            var god = new GodModeController(_ctx);
            god.Enable();
            god.Dispose();

            // Act — no debe tirar ni volver a suscribirse por accidente.
            TestDelegate act = () => EventManager.Trigger(EventName.OnRunEnd, Guid.NewGuid(), (object)null);

            // Assert
            Assert.DoesNotThrow(act);
        }
    }
}
