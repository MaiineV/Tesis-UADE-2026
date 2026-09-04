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

        /// <summary>
        /// Se asento la cara cruda del dado y espera la decision del jugador
        /// (aceptar o re-tirar). No termina por tiempo: dura lo que dure la decision.
        /// </summary>
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
    /// La tirada vive en <b>dos segmentos</b>, porque entre medio decide el jugador:
    /// <list type="number">
    ///   <item><b>Pendiente</b> (desde <c>OnRollPending</c>): giro y asentado sobre la
    ///         cara cruda. No vuelve al reposo por tiempo — la ficha sostiene la cara
    ///         mientras el jugador elige entre aceptar y re-tirar. Un reroll reinicia
    ///         este segmento.</item>
    ///   <item><b>Resolucion</b> (desde <c>OnResolved</c>): el destello del encantamiento
    ///         si lo hubo, el hold del resultado final y la vuelta al reposo.</item>
    /// </list>
    /// El GDD pide dos cosas que esta clase hace explicitas:
    /// <list type="bullet">
    ///   <item>El dado gira <b>dentro de la ficha</b>, nunca en la mesa de los 5 dados de
    ///         combate.</item>
    ///   <item>El feedback diferencia las tres bandas, y <b>alto no siempre es mejor</b>:
    ///         la intensidad sale de la banda, no del numero.</item>
    /// </list>
    /// La cara cruda siempre se ve antes que la final: el segmento pendiente la muestra
    /// todo el tiempo que dure la decision, y recien la resolucion salta a la ajustada —
    /// asi se lee que <i>intervino el encantamiento</i> y no que el dado salio distinto.
    /// </remarks>
    public static class ActiveItemRollFeelMath
    {
        /// <summary>
        /// Duracion del giro. Sale de <see cref="DiceAnimTimings.Defaults"/> para que el
        /// dado del item gire al mismo ritmo que los de combate — lo que cambia es DONDE
        /// gira (dentro de la ficha), no como.
        /// </summary>
        public static float SpinSeconds => DiceAnimTimings.Defaults.SpinSeconds;

        /// <summary>Cuanto dura el pop de asentado de la cara cruda pendiente.</summary>
        public const float RawHoldSeconds = 0.25f;

        /// <summary>Cuanto se sostiene el resultado final antes de volver al reposo.</summary>
        public const float ResultHoldSeconds = 0.9f;

        /// <summary>Destello corto al momento en que el encantamiento corre el resultado.</summary>
        public const float EnchantFlashSeconds = 0.18f;

        // ==================================================================
        // Segmento pendiente (OnRollPending → decision del jugador)
        // ==================================================================

        /// <summary>
        /// Fase del segmento pendiente segun el tiempo desde <c>OnRollPending</c>. Nunca
        /// llega a <see cref="ActiveItemRollPhase.Idle"/>: la cara cruda queda asentada
        /// hasta que el jugador acepte o re-tire.
        /// </summary>
        public static ActiveItemRollPhase PendingPhaseAt(float elapsed)
        {
            if (elapsed < 0f) return ActiveItemRollPhase.Idle;
            return elapsed < SpinSeconds ? ActiveItemRollPhase.Spinning : ActiveItemRollPhase.Settled;
        }

        /// <summary>
        /// Escala de la ficha en el segmento pendiente: un pop discreto al asentarse la
        /// cara cruda y vuelta a 1 mientras espera la decision. Discreto a proposito: la
        /// banda todavia no se resolvio (el encantamiento corre al aceptar), asi que no
        /// hay intensidad que comunicar — el payoff va en la resolucion.
        /// </summary>
        public static float PendingScaleAt(float elapsed, float settlePop = 0.12f)
        {
            if (PendingPhaseAt(elapsed) != ActiveItemRollPhase.Settled) return 1f;
            return 1f + settlePop * Decay(elapsed - SpinSeconds, RawHoldSeconds);
        }

        // ==================================================================
        // Segmento de resolucion (OnResolved → reposo)
        // ==================================================================

        /// <summary>
        /// Fase del segmento de resolucion segun el tiempo desde <c>OnResolved</c>.
        /// Arranca con la cara cruda ya asentada por el segmento pendiente.
        /// </summary>
        /// <param name="wasEnchanted">
        /// Si el encantamiento cambio el resultado. Sin encantamiento no existe el
        /// destello: seria un destello sin nada que comunicar.
        /// </param>
        public static ActiveItemRollPhase ResolvePhaseAt(float elapsed, bool wasEnchanted)
        {
            if (elapsed < 0f) return ActiveItemRollPhase.Idle;

            if (wasEnchanted)
            {
                if (elapsed < EnchantFlashSeconds) return ActiveItemRollPhase.Enchanted;
                if (elapsed < EnchantFlashSeconds + ResultHoldSeconds) return ActiveItemRollPhase.Holding;
                return ActiveItemRollPhase.Idle;
            }

            return elapsed < ResultHoldSeconds ? ActiveItemRollPhase.Holding : ActiveItemRollPhase.Idle;
        }

        /// <summary>Duracion total de la resolucion, para saber cuando volver al reposo.</summary>
        public static float ResolveTotalSeconds(bool wasEnchanted)
            => wasEnchanted ? EnchantFlashSeconds + ResultHoldSeconds : ResultHoldSeconds;

        /// <summary>
        /// Escala de la ficha en el segmento de resolucion: el pop del payoff, escalado
        /// por la banda. Vuelve a 1 en reposo.
        /// </summary>
        public static float ResolveScaleAt(float elapsed, bool wasEnchanted, ActiveItemBand band,
            float maxPop = 0.35f)
        {
            var phase = ResolvePhaseAt(elapsed, wasEnchanted);
            float amplitude = maxPop * Intensity01(band);

            switch (phase)
            {
                case ActiveItemRollPhase.Enchanted:
                    return 1f + amplitude * Decay(elapsed, EnchantFlashSeconds);
                case ActiveItemRollPhase.Holding:
                {
                    float local = wasEnchanted ? elapsed - EnchantFlashSeconds : elapsed;
                    return 1f + amplitude * Decay(local, ResultHoldSeconds);
                }
                default:
                    return 1f;
            }
        }

        // ==================================================================
        // Compartido
        // ==================================================================

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

        /// <summary>Caida lineal de 1 a 0 sobre <paramref name="span"/>.</summary>
        private static float Decay(float local, float span)
            => span <= 0f ? 0f : Mathf.Clamp01(1f - local / span);

        /// <summary>
        /// <c>true</c> si esta banda amerita hit-stop. El GDD lo reserva para el mejor
        /// desenlace, que es la banda positiva.
        /// </summary>
        public static bool HitstopAllowed(ActiveItemBand band) => band == ActiveItemBand.Positive;

        // ==================================================================
        // Feature#0085 — intensidad por estructura de resolucion
        // ==================================================================

        /// <summary>
        /// Intensidad 0..1 segun la estructura de resolucion completa. Bands usa la
        /// tabla de <see cref="Intensity01(ActiveItemBand)"/> tal cual. Binary no tiene
        /// banda mixta: Negative 0.55, Positive 1. Gradient/Hierarchy escalan de forma
        /// continua con <see cref="ActiveItemRollResolution.Magnitude01"/> — la cara mas
        /// alta siempre pega mas fuerte, sin el escalon de 3 bandas.
        /// </summary>
        public static float Intensity01(in ActiveItemRollResolution resolution)
        {
            switch (resolution.Structure)
            {
                case ActiveItemResolution.Binary:
                    return resolution.Band == ActiveItemBand.Positive ? 1f : 0.55f;
                case ActiveItemResolution.Gradient:
                case ActiveItemResolution.Hierarchy:
                    return 0.25f + 0.75f * resolution.Magnitude01;
                default:
                    return Intensity01(resolution.Band);
            }
        }

        /// <summary>
        /// <c>true</c> si esta resolucion amerita hit-stop. Bands/Binary: banda positiva,
        /// igual que <see cref="HitstopAllowed(ActiveItemBand)"/>. Gradient/Hierarchy no
        /// tienen banda positiva "de verdad" (todo corre por <c>OnPositiveBand</c>): el
        /// criterio pasa a ser la cara maxima del dado.
        /// </summary>
        public static bool HitstopAllowed(in ActiveItemRollResolution resolution)
        {
            if (resolution.Structure == ActiveItemResolution.Gradient
                || resolution.Structure == ActiveItemResolution.Hierarchy)
            {
                return resolution.Face == resolution.Faces;
            }
            return resolution.Band == ActiveItemBand.Positive;
        }
    }
}
