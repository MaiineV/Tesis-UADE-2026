using NUnit.Framework;

namespace Rollgeon.EditorTools.Playtest.Tests
{
    /// <summary>
    /// Tests de <see cref="BossBotArgs"/>: la traducción de la línea de comandos del
    /// <c>.ps1</c> a la config de una corrida.
    /// </summary>
    /// <remarks>
    /// Lo que protegen: que los alias de jefe resuelvan al <c>EntityId</c> real (nadie se
    /// acuerda de que La Bandida es <c>boss.one_armed</c>), y que un valor basura caiga al
    /// default en vez de tirar la corrida — el bot es una herramienta de validación, no un
    /// parser estricto.
    /// </remarks>
    [TestFixture]
    public class BossBotArgsTests
    {
        private const string P = BossBotArgs.Prefix;

        [Test]
        public void NoArgs_GivesTheDefaults()
        {
            var args = BossBotArgs.Parse(new string[0]);

            Assert.AreEqual(BossBotArgs.DefaultBossId, args.BossId);
            Assert.AreEqual(BossBotArgs.DefaultTurns, args.Turns);

            // Los cheats vienen PRENDIDOS: el bot existe para que el jefe actúe en cámara, y con
            // la economía real el Warrior muere cerca del turno 4 — una mesa de ~8 turnos nunca
            // se llegaría a ver.
            Assert.IsTrue(args.InfiniteEnergy);
            Assert.IsTrue(args.GodMode);
            Assert.IsFalse(args.Honest);
        }

        [Test]
        public void Honest_TurnsBothCheatsOff()
        {
            var args = BossBotArgs.Parse(new[] { P + "honest" });

            Assert.IsTrue(args.Honest);
            Assert.IsFalse(args.InfiniteEnergy);
            Assert.IsFalse(args.GodMode);
        }

        [Test]
        public void NullArgv_DoesNotThrow()
        {
            // Environment.GetCommandLineArgs() nunca es null, pero el MenuItem y los tests
            // llaman a esto a mano.
            var args = BossBotArgs.Parse(null);

            Assert.AreEqual(BossBotArgs.DefaultBossId, args.BossId);
        }

        // ---- Alias de jefe ---------------------------------------------------

        [TestCase("cajero", "boss.cashier")]
        [TestCase("generala", "boss.la_generala")]
        [TestCase("croupier", "boss.croupier")]
        [TestCase("sunkengrand", "boss.sunken_grand")]
        [TestCase("anotador", "boss.scorekeeper")]
        [TestCase("bandida", "boss.one_armed")]
        public void AFriendlyAlias_ResolvesToTheRealEntityId(string alias, string expected)
        {
            var args = BossBotArgs.Parse(new[] { P + "boss", alias });

            Assert.AreEqual(expected, args.BossId);
        }

        [Test]
        public void TheAliasIsCaseInsensitive()
        {
            Assert.AreEqual("boss.la_generala", BossBotArgs.Parse(new[] { P + "boss", "GENERALA" }).BossId);
        }

        [Test]
        public void ARawEntityId_PassesThroughUntouched()
        {
            // Así un jefe nuevo funciona sin tocar la tabla de alias.
            var args = BossBotArgs.Parse(new[] { P + "boss", "boss.general_director" });

            Assert.AreEqual("boss.general_director", args.BossId);
        }

        [Test]
        public void AnUnknownName_PassesThrough_SoTheGameReportsIt()
        {
            // No validamos contra el pool acá: el comando 'boss' ya lista los ids alcanzables
            // en su mensaje de error, y ese mensaje es más útil que uno nuestro.
            var args = BossBotArgs.Parse(new[] { P + "boss", "el_que_no_existe" });

            Assert.AreEqual("el_que_no_existe", args.BossId);
        }

        // ---- Números ---------------------------------------------------------

        [Test]
        public void TurnsAndSeed_AreRead()
        {
            var args = BossBotArgs.Parse(new[] { P + "turns", "20", P + "seed", "77" });

            Assert.AreEqual(20, args.Turns);
            Assert.AreEqual(77, args.Seed);
        }

        [Test]
        public void ANegativeSeed_IsAccepted()
        {
            // BossBotRoll usa módulo positivo justamente para tolerarlo.
            var args = BossBotArgs.Parse(new[] { P + "seed", "-5" });

            Assert.AreEqual(-5, args.Seed);
        }

        [TestCase("cero-turnos")]
        [TestCase("0")]
        [TestCase("-3")]
        public void GarbageTurns_FallBackToTheDefault(string value)
        {
            var args = BossBotArgs.Parse(new[] { P + "turns", value });

            Assert.AreEqual(BossBotArgs.DefaultTurns, args.Turns,
                "Perder la corrida por un typo en un número es peor que correr los 12 de siempre.");
        }

        [Test]
        public void Turns_AreCappedSoARunAlwaysEnds()
        {
            var args = BossBotArgs.Parse(new[] { P + "turns", "99999" });

            Assert.AreEqual(BossBotArgs.MaxTurns, args.Turns);
        }

        [Test]
        public void TimeScale_IsCapped_SoCapturesStayReadable()
        {
            var args = BossBotArgs.Parse(new[] { P + "timeScale", "500" });

            Assert.AreEqual(10f, args.TimeScale, 0.001f);
        }

        [Test]
        public void TimeScale_ParsesWithADot_RegardlessOfMachineCulture()
        {
            var args = BossBotArgs.Parse(new[] { P + "timeScale", "2.5" });

            Assert.AreEqual(2.5f, args.TimeScale, 0.001f);
        }

        [Test]
        public void AZeroTimeScale_FallsBack_OrTheRunWouldFreeze()
        {
            var args = BossBotArgs.Parse(new[] { P + "timeScale", "0" });

            Assert.AreEqual(BossBotArgs.DefaultTimeScale, args.TimeScale, 0.001f);
        }

        // ---- Flags -----------------------------------------------------------

        [Test]
        public void AFlagDoesNotEatTheNextArgument()
        {
            // Si el flag consumiera un valor, 'boss' se perdería y la corrida iría al Cajero.
            var args = BossBotArgs.Parse(new[] { P + "honest", P + "boss", "generala" });

            Assert.IsTrue(args.Honest);
            Assert.AreEqual("boss.la_generala", args.BossId);
        }

        [Test]
        public void AnExplicitCheatFlagAfterHonest_WinsForThatCheat()
        {
            // Es como el runner reconstruye la config tras el domain reload: 'honest' apaga los
            // dos y después se vuelven a prender los que estaban en true.
            var args = BossBotArgs.Parse(new[] { P + "honest", P + "godMode" });

            Assert.IsTrue(args.GodMode);
            Assert.IsFalse(args.InfiniteEnergy);
        }

        // ---- Convivencia con los args de Unity ------------------------------

        [Test]
        public void UnityOwnArguments_AreIgnored()
        {
            // Esto es lo que realmente llega: Unity mete sus flags en el mismo array.
            var argv = new[]
            {
                "C:/Program Files/Unity/Editor/Unity.exe",
                "-projectPath", "C:/repo",
                "-executeMethod", "Rollgeon.EditorTools.Playtest.BossBotRunner.Run",
                "-logFile", "C:/out/unity.log",
                P + "boss", "generala",
                P + "turns", "5",
            };

            var args = BossBotArgs.Parse(argv);

            Assert.AreEqual("boss.la_generala", args.BossId);
            Assert.AreEqual(5, args.Turns);
        }

        [Test]
        public void AKeyWithNoValue_AtTheEnd_DoesNotThrow()
        {
            var args = BossBotArgs.Parse(new[] { P + "boss" });

            Assert.AreEqual(BossBotArgs.DefaultBossId, args.BossId);
        }

        [Test]
        public void AKeyFollowedByAnotherFlag_KeepsItsDefault()
        {
            // '-bossBot.boss -projectPath' no debe dejar "-projectPath" como id de jefe.
            var args = BossBotArgs.Parse(new[] { P + "boss", "-projectPath", "C:/repo" });

            Assert.AreEqual(BossBotArgs.DefaultBossId, args.BossId);
        }

        [Test]
        public void OutputDir_IsRead()
        {
            var args = BossBotArgs.Parse(new[] { P + "out", @"C:\runs\mine" });

            Assert.AreEqual(@"C:\runs\mine", args.OutputDir);
        }
    }
}
