using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Acumulador por recurso para un evento. Colapsa las 4 operaciones a tres buckets
    /// (suma, producto, set) de modo que el orden de resolución sea fijo y determinista:
    /// <c>(base + Add) × Mult</c>, con <see cref="HasSet"/> pisando la base. La suma
    /// conmuta entre triggers y la multiplicación se compone después, así no importa en
    /// qué dado/slot quedó cada encantamiento.
    /// </summary>
    public struct ResourceAccumulator
    {
        public int Add;
        public float Mult;
        public bool HasSet;
        public int SetValue;

        public static ResourceAccumulator Identity =>
            new ResourceAccumulator { Add = 0, Mult = 1f, HasSet = false, SetValue = 0 };

        public ResourceAccumulator Apply(ResourceOperation op, int amount)
        {
            switch (op)
            {
                case ResourceOperation.Add:      Add += amount; break;
                case ResourceOperation.Subtract: Add -= amount; break;
                case ResourceOperation.Multiply: Mult *= amount; break;
                case ResourceOperation.Set:      HasSet = true; SetValue = amount; break;
            }
            return this;
        }

        /// <summary>Resuelve el valor final dado el valor actual del jugador.</summary>
        public int Resolve(int currentValue)
        {
            int b = HasSet ? SetValue : currentValue;
            return Mathf.RoundToInt((b + Add) * Mult);
        }
    }
}
