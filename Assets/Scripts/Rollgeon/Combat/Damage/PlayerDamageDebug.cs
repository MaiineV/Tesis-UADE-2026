namespace Rollgeon.Combat.Damage
{
    /// <summary>
    /// Multiplicador de playtest sobre el daño del jugador. Apagado por default
    /// (<see cref="Multiplier"/> = 1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Para qué existe.</b> Probar la mecánica de un jefe de piso 3 exige llegar a él, y llegar
    /// exige un build armado que una sesión de prueba no tiene tiempo de juntar. La alternativa
    /// —bajarle los números al jefe hasta que una run floja gane— arruina justo lo que se quiere
    /// medir: el jefe queda tuneado contra un jugador que no existe.
    /// </para>
    /// <para>
    /// <b>Por qué acá y no en la clase.</b> Meterlo en el passive del Warrior lo haría permanente y
    /// movería el baseline contra el que están medidos los seis jefes. Esto es una palanca de
    /// afuera: se prende para la sesión, se apaga, y el kit del jugador nunca cambió.
    /// </para>
    /// <para>
    /// <b>No es contenido.</b> Nada de gameplay lo lee ni lo escribe — sólo
    /// <see cref="PlayerComboDamage"/> lo aplica al final de la fórmula. Si algún día alguien lo
    /// deja prendido, el log de daño lo canta en cada golpe (ver <see cref="DamageDebugLogger"/>).
    /// </para>
    /// </remarks>
    public static class PlayerDamageDebug
    {
        /// <summary>
        /// Valor de playtest: ×1 (bajó ×3 → ×1.5 el 2026-08-17 → ×1 el 2026-08-18). En ×1 la
        /// palanca no altera nada: los jefes están medidos contra el kit real del jugador y ese es
        /// el número contra el que se playtestea. Subirla es una decisión de sesión, no un default.
        /// </summary>
        public const float PlaytestMultiplier = 1f;

        /// <summary>
        /// El factor configurado. Se lee por <see cref="Multiplier"/>, que es el que decide si
        /// llega a aplicarse. Para una sesión que necesite llegar lejos sin build, se sube a mano
        /// desde acá y se vuelve a 1 al terminar — no se commitea prendida.
        /// </summary>
        public static float Configured = PlaytestMultiplier;

        /// <summary>
        /// Factor que la fórmula aplica de verdad. <b>Sólo existe en Play mode</b>: fuera de él
        /// siempre vale 1.
        /// </summary>
        /// <remarks>
        /// El gate no es comodidad, es lo que hace que esto sea seguro de commitear. Los ~4100
        /// tests de EditMode afirman la fórmula real, y una palanca que los alcanzara los obligaría
        /// a conocerla: cada fixture de daño tendría que acordarse de apagarla, y el día que uno se
        /// olvide el número medido deja de ser el número del juego. Atado a Play mode, un test
        /// nunca puede medir un daño inflado, y la palanca sigue estando donde se la necesita —
        /// jugando.
        /// </remarks>
        public static float Multiplier =>
            UnityEngine.Application.isPlaying ? Configured : 1f;

        /// <summary><c>true</c> si está alterando el daño — lo consulta el log para poder avisarlo.</summary>
        public static bool IsOn => !UnityEngine.Mathf.Approximately(Multiplier, 1f);

        /// <summary>Vuelve al daño real. El camino de una línea para dejar de testear.</summary>
        public static void Off() => Configured = 1f;
    }
}
