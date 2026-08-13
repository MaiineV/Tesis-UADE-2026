using System;

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    /// <summary>
    /// Estado publicado de la cuenta regresiva del jackpot de La Bandida — el "número gigante"
    /// sobre la máquina. Canal tipado (<c>TypedEvent&lt;JackpotCountdownPayload&gt;</c>) en vez de
    /// una entry nueva en <c>EventName</c>: el enum es compartido por seis ramas de jefes en
    /// paralelo y no hace falta tocarlo para esto.
    /// </summary>
    /// <remarks>
    /// El riesgo #1 de este jefe es que la cuenta no se vea: sin el número en pantalla el jackpot
    /// es un golpe sorpresa de 25. Este payload es el único contrato que necesita la UI —
    /// <see cref="IsCounting"/> <c>false</c> significa "cuenta cancelada, no mostrar número".
    /// </remarks>
    public readonly struct JackpotCountdownPayload
    {
        /// <summary>Jefe dueño de la cuenta (una sola Bandida por combate, pero se publica igual).</summary>
        public readonly Guid BossGuid;

        /// <summary>Valor a mostrar: 2 → 1 → 0. En 0 el jefe marca el jackpot ese mismo turno.</summary>
        public readonly int Value;

        /// <summary><c>false</c> = la cuenta está cancelada (rodillo roto) y no baja.</summary>
        public readonly bool IsCounting;

        public JackpotCountdownPayload(Guid bossGuid, int value, bool isCounting)
        {
            BossGuid = bossGuid;
            Value = value;
            IsCounting = isCounting;
        }
    }
}
