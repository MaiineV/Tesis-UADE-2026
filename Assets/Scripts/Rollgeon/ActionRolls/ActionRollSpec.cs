using Rollgeon.UI.HUD;

namespace Rollgeon.ActionRolls
{
    /// <summary>
    /// Parametros de una tirada de accion (Forzar Puerta, Curarse). Lo arma el
    /// effect que implementa <see cref="IActionRollEffect"/> y lo consume el
    /// <see cref="IActionRollService"/> para orquestar confirm dialog, charge,
    /// roll, reroll y resolucion.
    /// </summary>
    public struct ActionRollSpec
    {
        /// <summary>
        /// True si cada tirada (inicial y rerolls) cobra 1 roll del pool
        /// (Feature#0050 — GDD Turn System). False = flujo gratis: la variante
        /// de exploracion de Heal/pocion no consume rolls.
        /// </summary>
        public bool CostsRolls;

        /// <summary>
        /// Suma minima de las caras que se considera "exito" o que activa el
        /// excedente (heal). Si la primera tirada queda por debajo, el servicio
        /// ofrece un reroll opcional (siempre que <see cref="AllowReroll"/> y
        /// haya rolls en el pool).
        /// </summary>
        public int Threshold;

        /// <summary>
        /// True si la accion necesita un dialogo de confirmacion antes de cobrar
        /// + tirar (Forzar Puerta muestra umbral + costo). False = roll
        /// directo (Curarse).
        /// </summary>
        public bool RequireConfirm;

        /// <summary>Texto humano que la UI muestra en confirm/result (ej. "Forzar Puerta").</summary>
        public string ActionLabel;

        /// <summary>Habilita el reroll extra cuando la tirada queda &lt; Threshold.</summary>
        public bool AllowReroll;

        /// <summary>
        /// True si la accion no tiene estado de "fallo" — ej. Curarse: la primera
        /// tirada bajo el umbral cura el monto base, no falla. La UI usa este flag
        /// para no mostrar "Falló" en rojo cuando <c>FinalSum &lt; Threshold</c>.
        /// </summary>
        public bool AlwaysSucceeds;

        /// <summary>
        /// Skin del tablero de dados que usa esta tirada (default / ataque / defensa). Lo
        /// consume <see cref="Rollgeon.UI.HUD.DiceBoardSkinView"/> para swappear el sprite.
        /// </summary>
        public DiceBoardType BoardType;

        public static ActionRollSpec Default => new ActionRollSpec
        {
            CostsRolls = false,
            Threshold = 10,
            RequireConfirm = false,
            ActionLabel = string.Empty,
            AllowReroll = true,
            AlwaysSucceeds = false,
            BoardType = DiceBoardType.Default,
        };
    }
}
