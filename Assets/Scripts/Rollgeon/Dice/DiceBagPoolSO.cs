using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Dice
{
    /// <summary>
    /// Oferta de dados disponibles para una clase. El jugador elige
    /// <see cref="RequiredBagSize"/> dados de este pool en
    /// <c>BuildSelectionScreen</c> antes de empezar la run. TECHNICAL.md §6.2 +
    /// diseno (cada clase un pool propio, jugador arma su bolsa).
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Dice Bag Pool", fileName = "DiceBagPool")]
    public class DiceBagPoolSO : ScriptableObject
    {
        [Tooltip("Cantidad exacta de dados que el jugador debe seleccionar para empezar la run.")]
        [MinValue(1)]
        public int RequiredBagSize = DiceBagSO.RequiredSize;

        [ListDrawerSettings(ShowFoldout = false)]
        [Tooltip("Tipos de dado disponibles para esta clase. Sin tope de copias: cualquier tipo ofrecido puede llenar la bolsa entera.")]
        public List<DicePoolEntry> Offerings = new();

        /// <summary>
        /// <c>true</c> si el pool puede generar bolsas validas (al menos un tipo
        /// ofrecido — sin topes por tipo, un solo tipo alcanza para llenar la bolsa).
        /// </summary>
        public bool Validate(out string error)
        {
            if (Offerings == null || Offerings.Count == 0)
            {
                error = "Pool sin Offerings — no se puede armar una bolsa.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary><c>true</c> si el tipo esta ofrecido en este pool.</summary>
        public bool Offers(DiceType type)
        {
            if (Offerings == null) return false;
            for (int i = 0; i < Offerings.Count; i++)
                if (Offerings[i].Type == type) return true;
            return false;
        }

        private void OnValidate()
        {
            if (Offerings == null) return;
            if (!Validate(out var error))
            {
                Debug.LogWarning($"{name}: {error}", this);
            }
        }
    }

    /// <summary>Una oferta de dado dentro de un <see cref="DiceBagPoolSO"/>.</summary>
    [Serializable]
    public struct DicePoolEntry
    {
        public DiceType Type;
    }
}
