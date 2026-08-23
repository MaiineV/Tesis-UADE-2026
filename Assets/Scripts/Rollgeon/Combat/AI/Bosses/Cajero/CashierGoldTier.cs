using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.Cashier
{
    [Serializable, HideReferenceObjectPicker]
    public sealed class CashierGoldTier
    {
        [Tooltip("Oro mínimo (inclusive) del jugador para que este escalón aplique. " +
                 "El escalón más bajo debería tener 0 para que siempre haya uno elegible.")]
        [MinValue(0)]
        public int MinGold;

        [Tooltip("Ancho en casillas de la columna marcada (1 = la columna del jugador, 3 = ±1).")]
        [MinValue(0)]
        public int ColumnSize = 1;

        [Tooltip("Daño que cobra la columna el turno siguiente si el jugador sigue adentro.")]
        [MinValue(0)]
        public int Damage;
    }
}
