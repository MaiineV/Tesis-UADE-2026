namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Las keys de texto que publican los <see cref="IAIIntentNode"/>, enumeradas.
    /// </summary>
    /// <remarks>
    /// Enumeradas y no sueltas en cada nodo porque el guard de localización necesita una lista
    /// que recorrer: las cuatro vivieron una rama entera sin entry en las tablas y salían en
    /// español con el juego en inglés, y nada lo detectó.
    /// </remarks>
    public static class AIIntentTextKeys
    {
        public const string Ignite = "intent.ignite";
        public const string RangedShot = "intent.ranged_shot";
        public const string BombField = "intent.bomb_field";
        public const string BombBlast = "intent.bomb_blast";

        /// <summary>Frase compartida de lo que una intención deja en el piso.</summary>
        public const string Leaves = "intent.leaves";

        public static readonly string[] All =
        {
            Ignite, RangedShot, BombField, BombBlast, Leaves,
        };

        /// <summary>
        /// Texto de autor de cada regla, con sus <c>{n}</c>. Vive acá y no en la vista: es la
        /// misma decisión que declarar la key, y un build con bundles viejos cae a esto.
        /// </summary>
        /// <remarks>
        /// Los argumentos son siempre los mismos tres —daño, cantidad, turnos— y cada frase usa
        /// los que le sirven. <see cref="BombBlast"/> no nombra el daño a propósito: el estallido
        /// del Croupier cobra cero y todo lo que hace es el fuego que deja.
        /// </remarks>
        public static string RuleFallback(string key) => key switch
        {
            Ignite => "Prende la banda que marcó, por <b>{0}</b>.",
            RangedShot => "Te dispara desde lejos por <b>{0}</b>.",
            BombField => "Siembra <b>{1}</b> bombas. Dónde caen se sortea al sembrarlas.",
            BombBlast => "Estalla en cruz.",
            Leaves => "Deja fuego: <b>{0}</b> al entrar, <b>{1}</b> por turno, {2} rondas.",
            _ => string.Empty,
        };
    }
}
