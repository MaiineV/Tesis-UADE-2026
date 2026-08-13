namespace Rollgeon.Dice.Throw
{
    /// <summary>
    /// Estado POR DADO dentro de una sesión de throw. La máquina es por dado (no
    /// global) porque la spec permite tirar subconjuntos (agarrar 2, tirarlos,
    /// agarrar los otros) y el cancel devuelve cada dado "a su lugar previo".
    /// </summary>
    public enum DieThrowState
    {
        /// <summary>No participa del throw (held en reroll, o bloqueado). Queda en su slot.</summary>
        Excluded,

        /// <summary>Espera en el spot "sin tirar" (rollArea). Agarrable.</summary>
        NotThrown,

        /// <summary>Agarrado — sigue al cursor con atracción. Cancelable (vuelve a su estado previo).</summary>
        Grabbed,

        /// <summary>Arrojado — volando/rebotando hasta asentarse.</summary>
        Flying,

        /// <summary>Quieto con cara visible. Re-agarrable mientras la sesión no cierre.</summary>
        Settled,
    }
}
