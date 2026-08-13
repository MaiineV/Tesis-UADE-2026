using UnityEngine;

namespace Rollgeon.Dice.Throw
{
    /// <summary>
    /// Mappings puros del feel de los dados arrojables: velocidad/impulso → volumen,
    /// pitch e intervalo de rattle, y el decay de la rotación cosmética en vuelo.
    /// Sin estado ni tipos de escena — testeable en EditMode como data.
    /// </summary>
    public static class DiceThrowFeelMath
    {
        /// <summary>Intensidad normalizada de un impacto/gesto. refSpeed &lt;= 0 ⇒ 1 (sin referencia, todo es "fuerte").</summary>
        public static float Intensity01(float speed, float refSpeed)
        {
            if (refSpeed <= 0f) return 1f;
            return Mathf.Clamp01(speed / refSpeed);
        }

        /// <summary>Volumen de un impacto: piso audible + rampa lineal con la velocidad.</summary>
        public static float ImpactVolume(float speed, float refSpeed, float floor = 0.25f)
            => Mathf.Lerp(Mathf.Clamp01(floor), 1f, Intensity01(speed, refSpeed));

        /// <summary>Pitch de un impacto: golpes fuertes suenan apenas más agudos.</summary>
        public static float ImpactPitch(float speed, float refSpeed, float min = 0.9f, float max = 1.2f)
            => Mathf.Lerp(min, max, Intensity01(speed, refSpeed));

        /// <summary>
        /// Intervalo entre one-shots del rattle de la mano (no hay API de loop en
        /// IAudioService): mano rápida = rattle denso, con clamp mínimo para no
        /// apilar clips (un rattle real es ~10-15 Hz).
        /// </summary>
        public static float RattleInterval(float speed, float refSpeed, float min = 0.08f, float max = 0.28f)
            => Mathf.Lerp(max, Mathf.Max(min, 0.01f), Intensity01(speed, refSpeed));

        /// <summary>
        /// Decay exponencial de la velocidad angular cosmética del sprite en vuelo
        /// (frame-rate independiente). decayPerSecond &lt;= 0 ⇒ sin decay.
        /// </summary>
        public static float SpinDecayStep(float angularVel, float decayPerSecond, float dt)
        {
            if (decayPerSecond <= 0f || dt <= 0f) return angularVel;
            return angularVel * Mathf.Exp(-decayPerSecond * dt);
        }

        /// <summary>
        /// Velocidad angular inicial (grados/s) que un flick imprime al sprite:
        /// proporcional a la velocidad y con el signo del componente horizontal
        /// (flick a la derecha = giro horario), clampeada para que no estrobe.
        /// </summary>
        public static float FlickAngularVelocity(Vector2 flickVel, float degreesPerUnit, float maxDegreesPerSecond)
        {
            float raw = -flickVel.x * degreesPerUnit;
            if (maxDegreesPerSecond <= 0f) return raw;
            return Mathf.Clamp(raw, -maxDegreesPerSecond, maxDegreesPerSecond);
        }
    }
}
