using UnityEngine;

namespace Rollgeon.Dice.Throw
{
    /// <summary>
    /// Settings de los dados arrojables (CNF-008): modo default + todo el tuning de
    /// los presenters 2D/3D. El modo RUNTIME vive en <see cref="DiceThrowService"/>
    /// (mutable via <c>dicemode</c> en la DevConsole); este SO define el arranque.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Dice/Dice Throw Settings",
        fileName = "DiceThrowSettings")]
    public class DiceThrowSettingsSO : ScriptableObject
    {
        [Header("Modo")]
        [Tooltip("Modo con el que arranca cada run. Classic = botón Roll actual.")]
        public DiceThrowMode DefaultMode = DiceThrowMode.Classic;

        [Header("2D — Grab (atracción al cursor)")]
        [Tooltip("Radio (px del canvas) alrededor del cursor que agarra dados al pasarles por encima.")]
        public float GrabRadius = 60f;

        [Tooltip("Rigidez del spring hacia el cursor. Más alto = persiguen más fuerte.")]
        public float SpringStiffness = 250f;

        [Tooltip("Amortiguación del spring. Cerca de crítico (~2*sqrt(k)) = sin oscilación.")]
        public float SpringDamping = 18f;

        [Header("2D — Flick (arrojar)")]
        [Tooltip("Constante de suavizado exponencial de la velocidad del mouse (segundos).")]
        public float VelocitySmoothTau = 0.05f;

        [Tooltip("Velocidad mínima del mouse (px/s) al soltar para que cuente como flick. " +
                 "Soltar más lento = cancel suave (los dados vuelven).")]
        public float FlickMinSpeed = 900f;

        [Tooltip("Multiplicador de la velocidad del mouse al convertirla en velocidad del dado.")]
        public float ThrowGain = 1.0f;

        [Header("2D — Vuelo y settle")]
        [Tooltip("Fricción del aire por segundo (proporción de velocidad perdida).")]
        public float FlightDrag = 1.0f;

        [Tooltip("Rebote contra los bordes de pantalla (0 = muerto, 1 = elástico perfecto).")]
        [Range(0f, 1f)] public float Restitution = 0.65f;

        [Tooltip("Velocidad (px/s) por debajo de la cual el dado cuenta como quieto.")]
        public float SettleSpeedEps = 25f;

        [Tooltip("Tiempo sostenido bajo el umbral para confirmar el settle (histéresis).")]
        public float SettleHoldSeconds = 0.15f;

        [Tooltip("Failsafe: un dado que siga volando pasado esto se asienta a la fuerza.")]
        public float MaxFlightSeconds = 5f;

        [Header("2D — Juice (motion cosmético — gateado por dicemotion)")]
        [Tooltip("Grados de giro del sprite por px/s de velocidad del flick. 0 = sin rotación en vuelo.")]
        public float FlightSpinDegreesPerUnit = 0.35f;

        [Tooltip("Tope de velocidad angular del sprite en vuelo (grados/s) para que no estrobe.")]
        public float FlightSpinMaxDegreesPerSecond = 1080f;

        [Tooltip("Decay exponencial por segundo de la rotación en vuelo (más alto = frena antes).")]
        public float FlightSpinDecay = 1.4f;

        [Tooltip("Tween de enderezado (rotación → 0) al asentarse. <= 0 = instantáneo.")]
        public float SettleUprightSeconds = 0.12f;

        [Tooltip("Drop-in al spawnear la sesión: scale 0→1 por dado. <= 0 = sin drop-in.")]
        public float SpawnDropSeconds = 0.18f;

        [Tooltip("Stagger entre dados del drop-in.")]
        public float SpawnDropStagger = 0.04f;

        [Header("2D — Tweens de retorno/alineado")]
        [Tooltip("Duración del tween de vuelta al cancelar el grab. <= 0 = instantáneo (tests).")]
        public float ReturnSeconds = 0.2f;

        [Tooltip("Duración del tween de alineado final hacia los slots.")]
        public float AlignSeconds = 0.35f;

        [Tooltip("Stagger entre dados en el alineado final.")]
        public float AlignStagger = 0.04f;

        [Header("3D — Grab/Flick")]
        [Tooltip("Fuerza de atracción del rigidbody agarrado hacia el punto del cursor.")]
        public float PullStrength = 12f;

        [Tooltip("Altura (m) del plano por el que viajan los dados agarrados.")]
        public float CarryHeight = 1.5f;

        [Tooltip("Multiplicador de la velocidad del mouse proyectada al plano al arrojar.")]
        public float FlickWorldGain = 0.02f;

        [Tooltip("Torque random proporcional a la velocidad del flick (tumbling).")]
        public float Tumble = 2.5f;

        [Header("3D — Settle y lectura de cara")]
        [Tooltip("Dot mínimo de la normal superior contra Vector3.up para aceptar la cara. " +
                 "Menos que esto = dado de canto → nudge.")]
        [Range(0.5f, 1f)] public float CockedDotThreshold = 0.92f;

        [Tooltip("Impulso del nudge para dados de canto.")]
        public float NudgeImpulse = 0.6f;

        [Tooltip("Nudges máximos antes del snap-rotate a la cara más cercana.")]
        public int MaxNudges = 2;

        [Tooltip("Failsafe: tiempo máximo de settle 3D antes del snap-rotate.")]
        public float MaxSettleSeconds = 6f;
    }
}
