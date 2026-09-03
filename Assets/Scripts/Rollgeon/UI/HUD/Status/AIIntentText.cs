using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Localization;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// La regla de una tarjeta de intención: qué va a hacer el enemigo, con sus números adentro
    /// de la frase.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sin un <c>case</c> por intención.</b> Todas las reglas se formatean con la misma terna
    /// —daño, cantidad, turnos— y cada frase usa los <c>{n}</c> que le sirven; <c>string.Format</c>
    /// ignora el resto. Un nodo nuevo es una key y una entry, no una rama acá.
    /// </para>
    /// <para>
    /// Lo que la intención deja en el piso va en una frase aparte y compartida, con los números de
    /// la definición real: cuatro fuegos comparten <c>SpecialTileType</c> y cobran 8/12, 6/10 y
    /// 15/15. Mismo criterio que <see cref="TileStandStatusProvider.BurnState"/>.
    /// </para>
    /// </remarks>
    public static class AIIntentText
    {
        public static string Describe(in AIIntent intent)
        {
            // El slot {0} de toda regla es daño: viaja ya formateado con el indicador
            // pegado a la derecha, así cada frase lo hereda sin tocar las traducciones.
            string rule = Format(intent.LabelKey,
                Rollgeon.UI.Utility.IconSpriteTags.DamageAmount(intent.Damage),
                intent.Amount, intent.TurnsAway);

            // Una intención sin frase propia se explica con su título y el badge de la mecha, y
            // tampoco arrastra lo que deja: vaciar su entry en la tabla ES pedir esa tarjeta.
            if (string.IsNullOrEmpty(rule)) return string.Empty;
            if (intent.Leaves == null) return rule;

            string leaves = Format(AIIntentTextKeys.Leaves,
                Rollgeon.UI.Utility.IconSpriteTags.DamageAmount(intent.Leaves.EnterDamage),
                Rollgeon.UI.Utility.IconSpriteTags.DamageAmount(intent.Leaves.TurnStartDamage),
                intent.LeavesRounds);

            return string.IsNullOrEmpty(leaves) ? rule : rule + " " + leaves;
        }

        private static string Format(string key, params object[] args)
            => string.IsNullOrEmpty(key)
                ? string.Empty
                : LocalizedContent.DescriptionFormat(key, AIIntentTextKeys.RuleFallback(key), args);
    }
}
