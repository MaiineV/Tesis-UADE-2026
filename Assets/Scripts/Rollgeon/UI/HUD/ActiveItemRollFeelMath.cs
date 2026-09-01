using Rollgeon.Items.Active;
using Rollgeon.UI.HUD.DiceAnim;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Fase de la animacion de tirada de la ficha de item activo.
    /// </summary>
    public enum ActiveItemRollPhase
    {
        /// <summary>Reposo: la ficha muestra su tabla de bandas.</summary>
        Idle = 0,

        /// <summary>El dado gira dentro de la ficha, desacelerando.</summary>
        Spinning = 1,

        /// <summary>Se asento la cara cruda del dado.</summary>
        Settled = 2,

        /// <summary>El encantamiento corrio el resultado a la cara final.</summary>
        Enchanted = 3,

        /// <summary>Se sostiene el resultado final antes de volver al reposo.</summary>
        Holding = 4,
    }

    /// <summary>
    /// Logica pura de la animacion de tirada de la ficha (patron
    /// <c>ChestRevealFeelMath</c>): en que fase estamos, que cara mostrar y con cuanta
    /// intensidad. Estatica y sin estado para poder testear el feel en EditMode sin
    /// escena.
    /// </summary>
    /// <remarks>
    /// El GDD pide dos cosas que esta clase hace explicitas:
    /// <list type="bullet">
    ///   <item>El dado gira <b>dentro de la ficha</b>, nunca en la mesa de los 5 dados de
    ///         combate.</item>
    ///   <item>El feedback diferencia las tres bandas, y <b>alto no siempre es mejor</b>:
    ///         la intensidad sale de la banda, no del numero.</item>
    /// </list>
    /// Cuando hay encantamiento la animacion pasa por la cara cruda antes de la final, asi
    /// se lee que <i>intervino el encantamiento</i> y no que el dado salio distinto.
    /// </remarks>
    public static class ActiveItemRollFeelMath
    {
        /// <summary>
        /// Duracion del giro. Sale de <see cref="DiceAnimTimings.Defaults"/> para que el
        /// dado del item gire al mismo ritmo que los de combate — lo que cambia es DONDE
        /// gira (dentro de la ficha), no como.
        /// </summary>
        public static float SpinSeconds => DiceAnimTimings.Defaults.SpinSeconds;

        /// <summary>Cuanto se sostiene la cara cruda antes de aplicar el encantamiento.</summary>
        public const float RawHoldSeconds = 0.25f;

        /// <summary>Cuanto se sostiene el resultado final antes de volver al reposo.</summary>
        public const float ResultHoldSeconds = 0.9f;

        /// <summary>
        /// Fase segun el tiempo transcurrido desde que se resolvio la activacion.
        /// </summary>
        /// <param name="wasEnchanted">
        /// Si el encantamiento cambio el resultado. Sin encantamiento no existe la pausa
        /// en la cara cruda: seria una pausa sin nada que comunicar.
        /// </param>
        public static ActiveItemRollPhase PhaseAt(float elapsed, bool wasEnchanted)
        {
            if (elapsed < 0f) return ActiveItemRollPhase.Idle;
            if (elapsed < SpinSeconds) return ActiveItemRollPhase.Spinning;

            if (!wasEnchanted)
                return elapsed < SpinSeconds + ResultHoldSeconds
                    ? ActiveItemRollPhase.Settled
                    : ActiveItemRollPhase.Idle;

            if (elapsed < SpinSeconds + RawHoldSeconds) return ActiveItemRollPhase.Settled;
            if (elapsed < SpinSeconds + RawHoldSeconds + EnchantFlashSeconds)
                return ActiveItemRollPhase.Enchanted;
            if (elapsed < SpinSeconds + RawHoldSeconds + EnchantFlashSeconds + ResultHoldSeconds)
                return ActiveItemRollPhase.Holding;

            return ActiveItemRollPhase.Idle;
        }

        /// <summary>Destello corto al momento en que el encantamiento corre el resultado.</summary>
        public const float EnchantFlashSeconds = 0.18f;

        /// <summary>Duracion total de la animacion, para saber cuando volver al reposo.</summary>
        public static float TotalSeconds(bool wasEnchanted)
            => wasEnchanted
                ? SpinSeconds + RawHoldSeconds + EnchantFlashSeconds + ResultHoldSeconds
                : SpinSeconds + ResultHoldSeconds;

        /// <summary>
        /// Cara <b>asentada</b> en un instante dado: la cruda hasta que el encantamiento
        /// interviene, la final despues.
        /// </summary>
        /// <remarks>
        /// No cubre el ciclado del giro: esas caras las decide
        /// <see cref="DiceAnimChoreographer.NextPreviewFace"/>, la misma coreografia que
        /// usan los dados de combate.
        /// </remarks>
        public static int SettledFaceAt(float elapsed, bool wasEnchanted, int rawRoll, int finalRoll)
        {
            var phase = PhaseAt(elapsed, wasEnchanted);
            return phase == ActiveItemRollPhase.Spinning || phase == ActiveItemRollPhase.Settled
                ? rawRoll
                : finalRoll;
        }

        /// <summary>
        /// Indice del tick de giro (1-based) que corresponde a <paramref name="elapsed"/>,
        /// o 0 si todavia no paso ninguno. Los ticks se espacian hacia el final segun
        /// <see cref="DiceAnimChoreographer.TickTime"/>: el dado frena como uno fisico.
        /// </summary>
        public static int SpinTickAt(float elapsed, int tickCount)
        {
            var t = DiceAnimTimings.Defaults;
            int current = 0;
            for (int i = 1; i <= tickCount; i++)
            {
                if (DiceAnimChoreographer.TickTime(i, tickCount, t.SpinSeconds, t.SpinDecelerationPower) > elapsed)
                    break;
                current = i;
            }
            return current;
        }

        /// <summary>
        /// Intensidad 0..1 del feedback segun la banda. Sale de la banda y no del numero
        /// porque el GDD lo pide: en Riesgo la banda negativa es un buen resultado, y en
        /// Precision el 6 puede ser el peor.
        /// </summary>
        public static float Intensity01(ActiveItemBand band)
        {
            switch (band)
            {
                case ActiveItemBand.Negative: return 0.25f;
                case ActiveItemBand.Mixed: return 0.55f;
                case ActiveItemBand.Positive: return 1f;
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(band), band, "ActiveItemBand sin intensidad definida.");
            }
        }

        /// <summary>
        /// Escala de la ficha en el instante dado: un pop al asentarse y otro, mas corto,
        /// cuando el encantamiento corre el resultado. Vuelve a 1 en reposo.
        /// </summary>
        public static float ScaleAt(float elapsed, bool wasEnchanted, ActiveItemBand band,
            float maxPop = 0.35f)
        {
            var phase = PhaseAt(elapsed, wasEnchanted);
            float amplitude = maxPop * Intensity01(band);

            switch (phase)
            {
                case ActiveItemRollPhase.Settled:
                {
                    float local = elapsed - SpinSeconds;
                    float span = wasEnchanted ? RawHoldSeconds : ResultHoldSeconds;
                    return 1f + amplitude * Decay(local, span);
                }
                case ActiveItemRollPhase.Enchanted:
                {
                    float local = elapsed - SpinSeconds - RawHoldSeconds;
                    return 1f + amplitude * Decay(local, EnchantFlashSeconds);
                }
                default:
                    return 1f;
            }
        }

        /// <summary>Caida lineal de 1 a 0 sobre <paramref name="span"/>.</summary>
        private static float Decay(float local, float span)
            => span <= 0f ? 0f : Mathf.Clamp01(1f - local / span);

        /// <summary>
        /// <c>true</c> si esta banda amerita hit-stop. El GDD lo reserva para el mejor
        /// desenlace, que es la banda positiva.
        /// </summary>
        public static bool HitstopAllowed(ActiveItemBand band) => band == ActiveItemBand.Positive;
    }
}
