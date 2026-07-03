using NUnit.Framework;
using Rollgeon.DevConsole.Parsing;

namespace Rollgeon.DevConsole.Tests
{
    public class DevCommandParserTests
    {
        [Test]
        public void should_split_command_and_args_when_simple_line()
        {
            var p = DevCommandParser.Parse("heal 5", 6);

            Assert.AreEqual("heal", p.CommandToken);
            Assert.AreEqual(1, p.Args.Count);
            Assert.AreEqual("5", p.Args[0]);
        }

        [Test]
        public void should_keep_quoted_value_as_single_arg_when_quotes_present()
        {
            const string input = "giveitem \"item con espacios\"";

            var p = DevCommandParser.Parse(input, input.Length);

            Assert.AreEqual("giveitem", p.CommandToken);
            Assert.AreEqual(1, p.Args.Count);
            Assert.AreEqual("item con espacios", p.Args[0]);
        }

        [Test]
        public void should_parse_unclosed_quote_as_open_token()
        {
            const string input = "giveitem \"abc";

            var p = DevCommandParser.Parse(input, input.Length);

            Assert.AreEqual(1, p.Args.Count);
            Assert.AreEqual("abc", p.Args[0]);
        }

        [Test]
        public void should_drop_leading_slash_when_present()
        {
            var p = DevCommandParser.Parse("/heal", 5);

            Assert.AreEqual("heal", p.CommandToken);
        }

        [Test]
        public void should_flag_new_token_when_caret_after_trailing_space()
        {
            var p = DevCommandParser.Parse("heal ", 5);

            Assert.AreEqual(1, p.ActiveTokenIndex);
            Assert.IsTrue(p.CaretInNewToken);
            Assert.AreEqual(string.Empty, p.ActiveTokenPrefix);
        }

        [Test]
        public void should_report_active_prefix_when_caret_mid_arg()
        {
            var p = DevCommandParser.Parse("setstat he", 10);

            Assert.AreEqual(1, p.ActiveTokenIndex);
            Assert.AreEqual("he", p.ActiveTokenPrefix);
            Assert.IsFalse(p.CaretInNewToken);
        }

        [Test]
        public void should_target_command_token_when_caret_in_command()
        {
            var p = DevCommandParser.Parse("hea", 3);

            Assert.AreEqual(0, p.ActiveTokenIndex);
            Assert.AreEqual("hea", p.ActiveTokenPrefix);
        }
    }
}
