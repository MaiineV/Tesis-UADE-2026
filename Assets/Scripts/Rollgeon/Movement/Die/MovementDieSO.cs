using Rollgeon.Dice;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Movement.Die
{
    /// <summary>
    /// Dado de Movimiento de una clase (TECHNICAL.md §6.6). Entidad separada del
    /// <see cref="DiceBagSO"/> de combate: no ocupa slot de la build, no recibe
    /// encantamientos ni bloqueos de dados, y la build no lo modifica.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Dice/Movement Die", fileName = "AD_MovementDie")]
    public class MovementDieSO : ScriptableObject
    {
        public const DiceType DefaultType = DiceType.D4;

        [Tooltip("Tipo del dado. La cara tirada reemplaza el rango fijo del Movimiento en combate.")]
        [EnumToggleButtons]
        public DiceType Type = DefaultType;

        public int MaxFace => Type.MaxFace();
    }
}
