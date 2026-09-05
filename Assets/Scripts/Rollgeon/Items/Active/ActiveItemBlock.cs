namespace Rollgeon.Items.Active
{
    /// <summary>
    /// Por que el slot de item activo no se puede usar ahora. GDD "Ítems Activos" §6 y §7.
    /// </summary>
    /// <remarks>
    /// Enum propio y no <see cref="ItemActivationBlock"/>: aquel gatea el camino viejo
    /// (<c>OnActivate</c> con cooldown y accion), y sus motivos no son los de este
    /// sistema. Cuando el catalogo se migre por completo, el viejo se retira.
    /// </remarks>
    public enum ActiveItemBlock
    {
        /// <summary>Se puede activar.</summary>
        None = 0,

        /// <summary>
        /// Fuera de combate. El GDD es tajante: "completamente oculta fuera de combate",
        /// el sistema "no existe ni se acumula durante la exploración".
        /// </summary>
        NotInCombat = 1,

        /// <summary>Slot vacio (PRE-02). El HUD lo muestra sin dado ni tabla.</summary>
        NoItemEquipped = 2,

        /// <summary>No es el turno del jugador (PRE-01).</summary>
        NotYourTurn = 3,

        /// <summary>Pool de rolls en 0 (PRE-03).</summary>
        NotEnoughRolls = 4,

        /// <summary>No hay ningun target valido para lo que pide el item (PRE-04).</summary>
        NoValidTarget = 5,

        /// <summary>
        /// Hay una activacion en curso: el dado esta girando (la resolucion llega sola al
        /// asentarse) o un efecto de banda espera una eleccion post-tirada. No se puede
        /// abrir otra activacion hasta que termine.
        /// </summary>
        Resolving = 6,
    }
}
