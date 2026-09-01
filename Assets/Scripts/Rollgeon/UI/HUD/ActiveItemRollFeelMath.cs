using Rollgeon.Items.Active;
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
        /// <summary>Duracion del giro, en segundos.</summary>
        public const float SpinSeconds = 0.45f;

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
        /// Cara a mostrar en un instante dado. Durante el giro es una cara arbitraria que
        /// cambia cada vez menos seguido; despues es el resultado que corresponda a la
        /// fase.
        /// </summary>
        /// <param name="seed">
        /// Semilla del giro, para que dos activaciones no muestren la misma secuencia. La
        /// cara del giro es puro adorno: el resultado real ya esta decidido.
        /// </param>
        public static int FaceAt(float elapsed, bool wasEnchanted, int rawRoll, int finalRoll,
            int faces, int seed)
        {
            var phase = PhaseAt(elapsed, wasEnchanted);
            switch (phase)
            {
                case ActiveItemRollPhase.Spinning:
                    return SpinFace(elapsed, faces, seed);
                case ActiveItemRollPhase.Settled:
                    return rawRoll;
                default:
                    return finalRoll;
            }
        }

        /// <summary>
        /// Cara del giro. Los cambios se espacian hacia el final (desaceleracion), asi la
        /// tirada "frena" en vez de cortarse de golpe.
        /// </summary>
        public static int SpinFace(float elapsed, int faces, int seed)
        {
            if (faces < 1) return 1;

            float t = Mathf.Clamp01(elapsed / SpinSeconds);

            // Ticks acumulados con densidad decreciente: la integral de (1 - t) da el
            // frenado. El factor fija cuantas caras pasan en total.
            const float TotalTicks = 14f;
            int tick = Mathf.FloorToInt(TotalTicks * (t * (2f - t)));

            // Hash barato y determinista: mismo seed y mismo tick, misma cara.
            int hash = (seed * 73856093) ^ (tick * 19349663);
            return Mathf.Abs(hash % faces) + 1;
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
