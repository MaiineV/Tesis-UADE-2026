using System;
using System.Linq;
using NUnit.Framework;
using Rollgeon.DevConsole.Commands;

namespace Rollgeon.DevConsole.Tests
{
    public class DevCommandRegistryTests
    {
        [Test]
        public void should_resolve_by_name_case_insensitive()
        {
            var reg = new DevCommandRegistry();
            reg.Register(new FakeCommand("god"));

            Assert.IsTrue(reg.TryGet("GOD", out var cmd));
            Assert.AreEqual("god", cmd.Name);
        }

        [Test]
        public void should_resolve_by_alias()
        {
            var reg = new DevCommandRegistry();
            reg.Register(new FakeCommand("god", new[] { "g" }));

            Assert.IsTrue(reg.TryGet("g", out var cmd));
            Assert.AreEqual("god", cmd.Name);
        }

        [Test]
        public void should_throw_when_duplicate_name_registered()
        {
            var reg = new DevCommandRegistry();
            reg.Register(new FakeCommand("god"));

            Assert.Throws<ArgumentException>(() => reg.Register(new FakeCommand("god")));
        }

        [Test]
        public void should_order_all_by_name()
        {
            var reg = new DevCommandRegistry();
            reg.Register(new FakeCommand("zebra"));
            reg.Register(new FakeCommand("apple"));

            var names = reg.All.Select(c => c.Name).ToList();

            Assert.AreEqual("apple", names[0]);
            Assert.AreEqual("zebra", names[1]);
        }
    }
}
