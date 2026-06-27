using System.Linq;
using NUnit.Framework;
using Rollgeon.DevConsole.Autocomplete;
using Rollgeon.DevConsole.Cheats;
using Rollgeon.DevConsole.Commands;

namespace Rollgeon.DevConsole.Tests
{
    public class AutocompleteEngineTests
    {
        private static DevCommandRegistry BuildRealRegistry(FakeConsoleContext ctx)
            => DefaultCommands.CreateDefault(ctx,
                new GodModeController(ctx), new InfiniteEnergyController(ctx), new FreeMoveController());

        [Test]
        public void should_suggest_command_names_by_prefix()
        {
            var ctx = new FakeConsoleContext();
            var engine = new AutocompleteEngine(BuildRealRegistry(ctx));

            var r = engine.Compute("he", 2, ctx);

            Assert.AreEqual(SuggestionTarget.CommandName, r.Target);
            var texts = r.Suggestions.Select(s => s.Text).ToList();
            CollectionAssert.Contains(texts, "heal");
            CollectionAssert.Contains(texts, "help");
        }

        [Test]
        public void should_suggest_arg_options_of_active_param()
        {
            var ctx = new FakeConsoleContext();
            var engine = new AutocompleteEngine(BuildRealRegistry(ctx));

            var r = engine.Compute("setstat ", 8, ctx);

            Assert.AreEqual(SuggestionTarget.ArgValue, r.Target);
            // StatAccessor.SettableNames ordenados alfabéticamente: Attack primero.
            Assert.AreEqual(5, r.Suggestions.Count);
            Assert.AreEqual("Attack", r.Suggestions[0].Text);
        }

        [Test]
        public void should_report_none_when_arg_has_no_provider()
        {
            var ctx = new FakeConsoleContext();
            var engine = new AutocompleteEngine(BuildRealRegistry(ctx));

            var r = engine.Compute("sethp ", 6, ctx);

            Assert.AreEqual(SuggestionTarget.None, r.Target);
            Assert.AreEqual(0, r.Suggestions.Count);
        }

        [Test]
        public void should_cycle_selection_with_move()
        {
            var ctx = new FakeConsoleContext();
            var engine = new AutocompleteEngine(BuildRealRegistry(ctx));
            var r = engine.Compute("he", 2, ctx); // heal, help

            Assert.AreEqual(0, engine.SelectedIndex);
            engine.MoveDown();
            Assert.AreEqual(1, engine.SelectedIndex);
            engine.MoveDown();
            Assert.AreEqual(0, engine.SelectedIndex); // wrap
            engine.MoveUp();
            Assert.AreEqual(r.Suggestions.Count - 1, engine.SelectedIndex);
        }

        [Test]
        public void should_reset_selection_when_input_changes()
        {
            var ctx = new FakeConsoleContext();
            var engine = new AutocompleteEngine(BuildRealRegistry(ctx));
            engine.Compute("he", 2, ctx);
            engine.MoveDown();

            engine.Compute("hea", 3, ctx);

            Assert.AreEqual(0, engine.SelectedIndex);
        }

        [Test]
        public void should_replace_command_token_on_accept()
        {
            var ctx = new FakeConsoleContext();
            var engine = new AutocompleteEngine(BuildRealRegistry(ctx));
            engine.Compute("hea", 3, ctx); // selecciona "heal"

            Assert.IsTrue(engine.TryAccept(out var newInput, out var newCaret));
            Assert.AreEqual("heal ", newInput);
            Assert.AreEqual(5, newCaret);
        }

        [Test]
        public void should_quote_value_with_space_on_accept()
        {
            var ctx = new FakeConsoleContext();
            var reg = new DevCommandRegistry();
            reg.Register(new FakeCommand("spawn", args: new[]
            {
                new ArgSpec("name", ArgKind.String, options: new StaticArgProvider("with space"))
            }));
            var engine = new AutocompleteEngine(reg);
            engine.Compute("spawn ", 6, ctx);

            Assert.IsTrue(engine.TryAccept(out var newInput, out _));
            Assert.AreEqual("spawn \"with space\" ", newInput);
        }
    }
}
