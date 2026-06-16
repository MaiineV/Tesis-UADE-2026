using System;
using System.Collections.Generic;
using System.Linq;
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
        [Tooltip("Tipos de dado disponibles para esta clase y el tope de copias permitidas.")]
        public List<DicePoolEntry> Offerings = new();

        /// <summary>
        /// <c>true</c> si el pool puede generar bolsas validas (suma de
        /// <see cref="DicePoolEntry.MaxInBag"/> &gt;= <see cref="RequiredBagSize"/> y ningun
        /// override excede <see cref="DiceTypeExt.MaxPerBag"/>).
        /// </summary>
        public bool Validate(out string error)
        {
            if (Offerings == null || Offerings.Count == 0)
            {
                error = "Pool sin Offerings — no se puede armar una bolsa.";
                return false;
            }

            foreach (var entry in Offerings)
            {
                int hardCap = entry.Type.MaxPerBag();
                if (entry.MaxInBag <= 0)
                {
                    error = $"{entry.Type}: MaxInBag debe ser > 0.";
                    return false;
                }
                if (entry.MaxInBag > hardCap)
                {
                    error = $"{entry.Type}: MaxInBag {entry.MaxInBag} excede MaxPerBag global ({hardCap}).";
                    return false;
                }
            }

            int totalCapacity = Offerings.Sum(o => o.MaxInBag);
            if (totalCapacity < RequiredBagSize)
            {
                error = $"Suma de MaxInBag ({totalCapacity}) < RequiredBagSize ({RequiredBagSize}) — el jugador no podria llenar la bolsa.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>Cap efectivo para un tipo: 0 si no esta en el pool.</summary>
        public int MaxFor(DiceType type)
        {
            if (Offerings == null) return 0;
            for (int i = 0; i < Offerings.Count; i++)
                if (Offerings[i].Type == type) return Offerings[i].MaxInBag;
            return 0;
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

        [Tooltip("Maximo de copias de este dado que el jugador puede meter en su bolsa. " +
                 "No puede exceder DiceType.MaxPerBag().")]
        [MinValue(1)]
        public int MaxInBag;
    }
}
