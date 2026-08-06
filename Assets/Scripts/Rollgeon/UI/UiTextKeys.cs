using System.Collections.Generic;

namespace Rollgeon.UI
{
    /// <summary>
    /// Keys de la String Table <c>UI</c> para el chrome del HUD que se setea por código.
    /// Mismo criterio que <c>TutorialTextKeys</c>: los textos viven en la tabla y las
    /// vistas los resuelven con <c>LocalizedContent.Ui(key, fallbackSerializado)</c>.
    /// <para>
    /// Los textos de costo llevan el placeholder <c>{ENERGY}</c>, que
    /// <c>IconSpriteTags</c> expande al glifo del atlas <b>después</b> de traducir —
    /// así que el token tiene que sobrevivir la traducción intacto.
    /// </para>
    /// </summary>
    public static class UiTextKeys
    {
        // Costo en energía de un roll extra (fase de defensa y cualquier otra chain).
        public const string RerollPaid = "roll.reroll_paid";
        public const string ChainRollPaid = "roll.chain_paid";

        /// <summary>Todas las keys de esta clase, para validación en tests.</summary>
        public static IReadOnlyList<string> All { get; } = new[]
        {
            RerollPaid, ChainRollPaid,
        };
    }
}
