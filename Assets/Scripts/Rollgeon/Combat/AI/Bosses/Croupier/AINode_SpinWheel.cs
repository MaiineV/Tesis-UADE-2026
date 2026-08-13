using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// "Hagan sus apuestas": el Croupier canta <see cref="ICroupierWheelService.NumbersPerTurn"/>
    /// número(s) del 1 al 6 y los deja flotando sobre él. Cada número es dos cosas a la vez — el
    /// sector del paño que va a caer el turno que viene y el dado de la bolsa que se confisca — así
    /// que este nodo no hace nada más que elegirlo: marcar el sector y confiscar el dado son otros dos
    /// nodos que leen de acá.
    /// </summary>
    /// <remarks>
    /// Va inmediatamente antes del nodo de confiscación y del de marcado en el Sequence raíz. Abre el
    /// windup: desde que este nodo corre hasta que el sector detona, pegarle al jefe corre la rueda.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_SpinWheel : AIActionNode
    {
        [Tooltip("Daño de la Represalia de mesa: lo que cuesta pegarle con un número impar en el aire. " +
                 "Con la rueda trucada (fase 2) no se cobra.")]
        [MinValue(0)]
        public int RetaliationDamage = 8;

        [Tooltip("Si está activo, nunca canta dos veces seguidas el mismo número: el paño se mueve " +
                 "todos los turnos. Apagalo para dejar que el azar repita.")]
        public bool AvoidRepeatingLastNumber = true;

        [NonSerialized] private int _lastNumber;

        public override string NodeName => "Spin Wheel (Croupier)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;

            var wheel = CroupierWheelService.ResolveOrCreate();
            if (wheel == null) return AIResult.Failed;

            wheel.Bind(context.SelfGuid);
            wheel.RetaliationDamage = RetaliationDamage;

            var numbers = PickNumbers(context, wheel.NumbersPerTurn);
            if (numbers.Count == 0) return AIResult.Failed;

            wheel.Sing(numbers);
            _lastNumber = numbers[0];
            return AIResult.Succeeded;
        }

        /// <summary>
        /// <paramref name="count"/> números distintos entre sí de 1..6. Distintos porque dos números
        /// iguales en fase 2 harían caer un solo sector y el turno se leería como fase 1.
        /// </summary>
        private List<int> PickNumbers(AIContext context, int count)
        {
            int total = ThreatAreaShape.RoomSectorCount;
            var pool = new List<int>(total);
            for (int n = 1; n <= total; n++)
            {
                // El descarte del número anterior es del pool, no un re-sorteo: así todos los números
                // restantes quedan equiprobables en vez de sesgar hacia el segundo intento.
                if (AvoidRepeatingLastNumber && n == _lastNumber && total > 1) continue;
                pool.Add(n);
            }

            int take = count < 1 ? 1 : count;
            if (take > pool.Count) take = pool.Count;

            var picked = new List<int>(take);
            for (int i = 0; i < take; i++)
            {
                int j = NextInt(context, pool.Count);
                picked.Add(pool[j]);
                pool.RemoveAt(j);
            }
            return picked;
        }

        private static int NextInt(AIContext context, int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 1) return 0;
            return context.Rng != null
                ? context.Rng.Next(exclusiveUpperBound)
                : UnityEngine.Random.Range(0, exclusiveUpperBound);
        }
    }
}
