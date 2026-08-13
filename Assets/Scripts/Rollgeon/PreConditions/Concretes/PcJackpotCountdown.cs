using System;
using Patterns;
using Rollgeon.Combat.AI.Bosses.Bandida;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// Compara la cuenta regresiva del jackpot de La Bandida contra <see cref="Value"/>.
    /// <c>== 0</c> es el gate del jackpot: la cuenta llegó al final y este turno se marca el 7×7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Lee el contador, no la vida de los rodillos.</b> Con el mínimo del jugador en 6 contra
    /// rodillos de 3 de vida el estado "dañado pero vivo" no existe, así que una PC que compare
    /// vidas nunca vería la cancelación. La cancelación la escribe el hook de daño del servicio;
    /// esta PC solo lee el número.
    /// </para>
    /// <para>
    /// <b><see cref="RequireCounting"/> es la salvaguarda de la cancelación.</b> Cancelar congela el
    /// número donde estaba; sin este flag, un <c>== 0</c> pasaría igual con la cuenta cancelada y el
    /// jackpot dispararía después de que el jugador desarmó la bomba.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcJackpotCountdown : BasePreCondition
    {
        public IntComparison Comparison = IntComparison.Equal;

        [Tooltip("Valor contra el que se compara la cuenta. 0 = la cuenta llegó al final.")]
        [MinValue(0)]
        public int Value = 0;

        [Tooltip("Si está activo, la PC es false cuando la cuenta está cancelada (rodillo roto), " +
                 "sin importar el número congelado.")]
        public bool RequireCounting = true;

        public override string ConditionName => $"Jackpot countdown {Comparison} {Value}";

        public override bool Evaluate(PreConditionContext context)
        {
            if (!ServiceLocator.TryGetService<IBandidaJackpotService>(out var service) || service == null)
                return false;

            if (RequireCounting && !service.IsCounting) return false;

            int countdown = service.Countdown;
            return Comparison switch
            {
                IntComparison.Equal => countdown == Value,
                IntComparison.NotEqual => countdown != Value,
                IntComparison.Less => countdown < Value,
                IntComparison.LessOrEqual => countdown <= Value,
                IntComparison.Greater => countdown > Value,
                IntComparison.GreaterOrEqual => countdown >= Value,
                _ => false,
            };
        }
    }
}
