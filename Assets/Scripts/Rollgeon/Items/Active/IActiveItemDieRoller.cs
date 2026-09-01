using Rollgeon.Dice;

namespace Rollgeon.Items.Active
{
    /// <summary>
    /// Tira el dado propio de un item activo. El GDD lo separa a proposito de la bolsa
    /// de combate: "fuente de RNG: dado propio del ítem activo, independiente de la bolsa
    /// de dados de combate", y hasta el SFX debe ser distinto.
    /// </summary>
    /// <remarks>
    /// Interface propia (y no <see cref="IDiceRoller"/>) porque aquel es bag-oriented:
    /// tira los 5 dados de una <c>DiceBagSO</c>. Aca hace falta una sola tirada de N caras.
    /// </remarks>
    public interface IActiveItemDieRoller
    {
        /// <summary>Cara obtenida, en <c>[1, die.MaxFace()]</c>.</summary>
        int Roll(DiceType die);
    }

    /// <summary>Implementacion por defecto sobre <c>UnityEngine.Random</c>.</summary>
    public sealed class ActiveItemDieRoller : IActiveItemDieRoller
    {
        public int Roll(DiceType die) => UnityEngine.Random.Range(1, die.MaxFace() + 1);
    }
}
