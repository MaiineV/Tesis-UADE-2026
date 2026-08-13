using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.Cashier
{
    /// <summary>
    /// Un escalón de la columna del Cajero: desde cuánto oro aplica, qué ancho tiene la
    /// franja y cuánto pega al detonar. La ficha del jefe define tres
    /// (&lt;100 ⇒ Size 1 / 14, 100-249 ⇒ Size 3 / 28, ≥250 ⇒ Size 3 / 35).
    /// </summary>
    /// <remarks>
    /// Es data pura y serializable inline en el árbol (<c>AINode_TelegraphMarkGoldScaled.Tiers</c>):
    /// re-balancear el anzuelo económico es editar el asset del jefe, sin tocar código.
    /// </remarks>
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
