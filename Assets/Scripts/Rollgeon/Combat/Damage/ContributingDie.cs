using Rollgeon.Dice;

namespace Rollgeon.Combat.Damage
{
    /// <summary>
    /// Un dado que contribuye al combo matcheado: slot del bag (para que la UI pueda
    /// apuntar al dado físico en pantalla), cara tirada (término de N en la fórmula v3)
    /// y tipo (arte/color). Viaja junto para que las tres piezas no puedan desalinearse
    /// entre fronteras (payload de preview vs. contexto de impacto).
    /// </summary>
    public readonly struct ContributingDie
    {
        /// <summary>Slot del bag del jugador; -1 si el mapeo a slot no aplica.</summary>
        public readonly int BagSlot;

        /// <summary>Cara tirada que contribuye al combo.</summary>
        public readonly int Face;

        public readonly DiceType Type;

        public ContributingDie(int bagSlot, int face, DiceType type)
        {
            BagSlot = bagSlot;
            Face = face;
            Type = type;
        }

        public override string ToString() => $"{Type}[{Face}]@slot{BagSlot}";
    }
}
