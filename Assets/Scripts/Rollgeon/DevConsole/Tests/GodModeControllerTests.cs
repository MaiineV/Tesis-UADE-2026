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
    public class GodModeControllerTests
    {
        private Guid _pid;
        private AttributesManager _am;
        private FakeConsoleContext _ctx;

        [SetUp]
        public void SetUp()
        {
            _pid = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.SetAttribute<Health>(new Health(50));
            _am = new AttributesManager();
            _am.Register(_pid, attrs);

            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            hero.BaseMaxHp = 100;

            _ctx = new FakeConsoleContext { PlayerGuid = _pid, IsRunActive = true };
            _ctx.Register<AttributesManager>(_am);
            _ctx.Register<Rollgeon.Player.IPlayerService>(new FakePlayerService { PlayerGuid = _pid, CurrentHero = hero });
        }

        [TearDown]
        public void TearDown()
        {
            _am.Dispose();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void should_pin_hp_to_max_on_enable()
        {
            var god = new GodModeController(_ctx);
            god.Enable();

            Assert.AreEqual(100, _am.GetAttributeValue<Health, int>(_pid));
        }

        [Test]
        public void should_restore_hp_when_it_drops_while_enabled()
        {
            var god = new GodModeController(_ctx);
            god.Enable();

            _am.SetAttributeValue<Health, int>(_pid, 30); // simula daño

            Assert.AreEqual(100, _am.GetAttributeValue<Health, int>(_pid));
        }

        [Test]
        public void should_not_restore_after_disable()
        {
            var god = new GodModeController(_ctx);
            god.Enable();
            god.Disable();

            _am.SetAttributeValue<Health, int>(_pid, 20);

            Assert.AreEqual(20, _am.GetAttributeValue<Health, int>(_pid));
        }

        [Test]
        public void should_ignore_changes_for_other_entities()
        {
            var other = Guid.NewGuid();
            var otherAttrs = new ModifiableAttributes();
            otherAttrs.SetAttribute<Health>(new Health(10));
            _am.Register(other, otherAttrs);

            var god = new GodModeController(_ctx);
            god.Enable();

            _am.SetAttributeValue<Health, int>(other, 5);

            Assert.AreEqual(5, _am.GetAttributeValue<Health, int>(other));
        }
    }
}
