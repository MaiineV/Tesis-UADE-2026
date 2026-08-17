using System;
using System.Collections.Generic;
using System.Globalization;

namespace Rollgeon.EditorTools.Playtest
{
    /// <summary>
    /// Config de una corrida del boss bot, parseada de la línea de comandos que arma
    /// <c>tools/playtest/run-boss-bot.ps1</c>.
    /// </summary>
    /// <remarks>
    /// Los alias existen porque el <c>EntityId</c> real de cada jefe no es adivinable
    /// (<c>boss.one_armed</c> es La Bandida, <c>boss.scorekeeper</c> el Anotador). Un
    /// <c>boss.*</c> crudo pasa derecho, así que un jefe nuevo funciona sin tocar esta
    /// tabla — el alias es una comodidad, no un registro.
    /// </remarks>
    public sealed class BossBotArgs
    {
        public const string Prefix = "-bossBot.";

        /// <summary>Fallback cuando nadie pidió jefe: el Cajero es el que más se está tocando.</summary>
        public const string DefaultBossId = "boss.cashier";

        public const int DefaultTurns = 12;
        public const float DefaultTimeScale = 3f;

        /// <summary>
        /// Techo de turnos. No es una regla de diseño: una corrida sin fin llenaría el disco
        /// de PNG y el <c>.ps1</c> nunca volvería.
        /// </summary>
        public const int MaxTurns = 200;

        private static readonly Dictionary<string, string> Aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "cajero", "boss.cashier" },
                { "cashier", "boss.cashier" },
                { "generala", "boss.la_generala" },
                { "croupier", "boss.croupier" },
                { "sunkengrand", "boss.sunken_grand" },
                { "anotador", "boss.scorekeeper" },
                { "scorekeeper", "boss.scorekeeper" },
                { "bandida", "boss.one_armed" },
                { "tahur", "boss.tahur" },
            };

        public string BossId { get; private set; } = DefaultBossId;
        public int Turns { get; private set; } = DefaultTurns;
        public string OutputDir { get; private set; }
        public float TimeScale { get; private set; } = DefaultTimeScale;

        /// <summary>
        /// Seed de las tiradas rigueadas. Misma seed ⇒ mismas caras ⇒ imágenes comparables
        /// entre corridas, que es todo el punto de fijar la tirada.
        /// </summary>
        public int Seed { get; private set; } = 1234;

        /// <summary>Energía infinita. Ver <see cref="Honest"/> para el por qué del default.</summary>
        public bool InfiniteEnergy { get; private set; } = true;

        /// <summary>El bot no muere. Ver <see cref="Honest"/> para el por qué del default.</summary>
        public bool GodMode { get; private set; } = true;

        /// <summary>
        /// Corrida sin cheats: el bot puede morir y quedarse sin energía.
        /// </summary>
        /// <remarks>
        /// God mode y energía infinita vienen prendidos porque el bot existe para que el **jefe**
        /// actúe delante de la cámara. Con la economía real el Warrior de piso 1 muere alrededor
        /// del turno 4, y una mesa que cuesta ~8 turnos romper nunca se llegaría a ver: la corrida
        /// no validaría nada de lo que se quería mirar.
        ///
        /// Esto no es tunear el juego — el kit del player queda intacto. Es sacar de la ecuación
        /// dos variables que no se están validando, igual que un banco de pruebas alimenta el
        /// circuito desde una fuente y no desde una batería que se descarga.
        ///
        /// Con <c>Honest</c> la corrida es una pelea de verdad, útil para responder "¿se puede
        /// ganar?" en vez de "¿qué hace el jefe?".
        /// </remarks>
        public bool Honest { get; private set; }

        /// <summary>
        /// Resuelve un alias a <c>EntityId</c>. Un id que ya viene con forma de id
        /// (<c>boss.*</c>) se respeta tal cual.
        /// </summary>
        public static string ResolveBossId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return DefaultBossId;

            raw = raw.Trim();
            return Aliases.TryGetValue(raw, out var resolved) ? resolved : raw;
        }

        /// <summary>
        /// Parsea los args propios y **ignora todo lo demás**: Unity mete decenas de flags
        /// suyos (<c>-projectPath</c>, <c>-logFile</c>, <c>-executeMethod</c>) en el mismo array.
        /// </summary>
        public static BossBotArgs Parse(IReadOnlyList<string> argv)
        {
            var args = new BossBotArgs();
            if (argv == null) return args;

            for (int i = 0; i < argv.Count; i++)
            {
                string key = argv[i];
                if (string.IsNullOrEmpty(key) || !key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string name = key.Substring(Prefix.Length);

                // Los flags no llevan valor; leer el siguiente token se comería un arg ajeno.
                if (name.Equals("honest", StringComparison.OrdinalIgnoreCase))
                {
                    args.Honest = true;
                    args.InfiniteEnergy = false;
                    args.GodMode = false;
                    continue;
                }
                if (name.Equals("infiniteEnergy", StringComparison.OrdinalIgnoreCase))
                {
                    args.InfiniteEnergy = true;
                    continue;
                }
                if (name.Equals("godMode", StringComparison.OrdinalIgnoreCase))
                {
                    args.GodMode = true;
                    continue;
                }

                string value = i + 1 < argv.Count ? argv[i + 1] : null;
                if (value == null || IsAnotherFlag(value))
                    continue;

                i++;
                Apply(args, name, value);
            }

            return args;
        }

        /// <summary>
        /// Distingue "el próximo token es otro flag" de "es un número negativo". Mirar sólo el
        /// guion inicial hacía que <c>-bossBot.seed -5</c> se descartara sin decir nada, y la
        /// corrida salía con la seed default aunque la línea de comandos pidiera otra.
        /// </summary>
        private static bool IsAnotherFlag(string token)
        {
            if (token.Length < 2 || token[0] != '-') return false;
            return !char.IsDigit(token[1]) && token[1] != '.';
        }

        private static void Apply(BossBotArgs args, string name, string value)
        {
            if (name.Equals("boss", StringComparison.OrdinalIgnoreCase))
            {
                args.BossId = ResolveBossId(value);
                return;
            }
            if (name.Equals("out", StringComparison.OrdinalIgnoreCase))
            {
                args.OutputDir = value;
                return;
            }
            if (name.Equals("turns", StringComparison.OrdinalIgnoreCase))
            {
                // Un valor basura cae al default en vez de abortar: perder la corrida entera por
                // un typo en un número sería peor que correr los 12 de siempre y decirlo en el log.
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int turns)
                    && turns > 0)
                {
                    args.Turns = Math.Min(turns, MaxTurns);
                }
                return;
            }
            if (name.Equals("seed", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
                    args.Seed = seed;
                return;
            }
            if (name.Equals("timeScale", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float scale)
                    && scale > 0f)
                {
                    // Techo bajo a propósito: más allá de ~10 las animaciones y los feedbacks se
                    // saltean frames y la captura agarra estados intermedios ilegibles.
                    args.TimeScale = Math.Min(scale, 10f);
                }
            }
        }

        public override string ToString() =>
            $"boss={BossId} turns={Turns} seed={Seed} timeScale={TimeScale.ToString(CultureInfo.InvariantCulture)} " +
            $"infiniteEnergy={InfiniteEnergy} godMode={GodMode} honest={Honest} out={OutputDir ?? "<default>"}";
    }
}
