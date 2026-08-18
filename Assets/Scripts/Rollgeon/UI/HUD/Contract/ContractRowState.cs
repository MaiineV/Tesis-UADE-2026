using Rollgeon.Localization;

namespace Rollgeon.UI.HUD.Contract
{
    /// <summary>
    /// Qué le hicieron a una fila del contrato. Es la lista cerrada de la tabla
    /// "Reglas invisibles · dónde las ve el jugador" del documento de jefes: cada marca
    /// tiene un solo dibujo, y nada que cambie el contrato puede quedar sin marca.
    /// </summary>
    public enum ContractRowMark
    {
        None = 0,

        /// <summary>El jefe lo sacó de la hoja por N turnos: ni siquiera entra a la detección.</summary>
        Blocked = 1,

        /// <summary>Se puede armar pero paga 0.</summary>
        Forbidden = 2,

        /// <summary>Corrido al valor de otra fila (la "tacha" del Anotador).</summary>
        Shifted = 3,

        /// <summary>Paga más que lo que dice la hoja.</summary>
        Buffed = 4,

        /// <summary>Paga menos.</summary>
        Nerfed = 5,
    }

    /// <summary>
    /// Una fila del contrato ANTES de los modificadores: lo que dice la hoja de la clase.
    /// Es la entrada del resolver, y es lo que le permite decir a qué fila fue corrida otra.
    /// </summary>
    public readonly struct ContractRowBase
    {
        public readonly string ComboId;
        public readonly string DisplayName;
        public readonly int BaseDamage;

        public ContractRowBase(string comboId, string displayName, int baseDamage)
        {
            ComboId = comboId;
            DisplayName = displayName;
            BaseDamage = baseDamage;
        }
    }

    /// <summary>
    /// Lo que una fila tiene que dibujar ahora mismo. Struct de sólo lectura: la vista lo
    /// recibe armado y no consulta servicios — así el mismo estado pinta la tabla del drawer
    /// y la planilla persistente sin que puedan discrepar.
    /// </summary>
    public readonly struct ContractRowState
    {
        public readonly ContractRowMark Mark;

        /// <summary>Lo que dice la hoja de la clase.</summary>
        public readonly int BaseDamage;

        /// <summary>Lo que paga hoy, con los modificadores encima.</summary>
        public readonly int EffectiveDamage;

        public readonly string ShiftedToComboId;
        public readonly string ShiftedToDisplayName;

        /// <summary>Turnos que le quedan al bloqueo. 0 si la fila no está bloqueada.</summary>
        public readonly int BlockedTurns;

        public ContractRowState(ContractRowMark mark, int baseDamage, int effectiveDamage,
            string shiftedToComboId, string shiftedToDisplayName, int blockedTurns)
        {
            Mark = mark;
            BaseDamage = baseDamage;
            EffectiveDamage = effectiveDamage;
            ShiftedToComboId = shiftedToComboId;
            ShiftedToDisplayName = shiftedToDisplayName;
            BlockedTurns = blockedTurns;
        }

        /// <summary>Fila intacta: paga lo que dice la hoja y no lleva marca.</summary>
        public static ContractRowState Unmodified(int baseDamage)
            => new ContractRowState(ContractRowMark.None, baseDamage, baseDamage, null, null, 0);

        public bool IsAltered => Mark != ContractRowMark.None;

        public int Delta => EffectiveDamage - BaseDamage;

        /// <summary>
        /// La fila va tachada: el combo ya no vale lo que está escrito. Cubre las tres
        /// marcas que el documento pide tachar — prohibido, bloqueado y corrido.
        /// </summary>
        public bool IsStruckThrough => Mark == ContractRowMark.Blocked
                                       || Mark == ContractRowMark.Forbidden
                                       || Mark == ContractRowMark.Shifted;

        /// <summary>
        /// Verde o rojo. Un corrimiento hacia arriba también favorece: el Anotador sortea
        /// dirección, y esconder los corrimientos buenos haría ilegible la mitad de su pelea.
        /// </summary>
        public bool IsFavorable => Mark == ContractRowMark.Buffed
                                   || (Mark == ContractRowMark.Shifted && Delta > 0);

        /// <summary>
        /// Texto del badge sobre la fila. Sin placeholders de formato: los textos autorados
        /// viajan por la tabla UI y un <c>{0}</c> mal escrito ahí tiraría en pantalla.
        /// </summary>
        public string BadgeText()
        {
            switch (Mark)
            {
                case ContractRowMark.Blocked:
                    var blocked = LocalizedContent.Ui(ContractTextKeys.RuleBlocked, "BLOQUEADO");
                    return BlockedTurns > 0 ? blocked + " " + BlockedTurns : blocked;

                case ContractRowMark.Forbidden:
                    return LocalizedContent.Ui(ContractTextKeys.RuleForbidden, "PROHIBIDO");

                case ContractRowMark.Shifted:
                    return LocalizedContent.Ui(ContractTextKeys.RuleShifted, "PAGA COMO")
                           + " " + (ShiftedToDisplayName ?? string.Empty);

                case ContractRowMark.Buffed:
                    return "+" + Delta;

                case ContractRowMark.Nerfed:
                    return Delta.ToString(); // ya viene con el signo

                default:
                    return string.Empty;
            }
        }
    }
}
