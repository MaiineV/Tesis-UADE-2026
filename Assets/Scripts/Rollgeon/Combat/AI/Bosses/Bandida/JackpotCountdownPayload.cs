using System;

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    /// <summary>
    /// Estado publicado de la cuenta regresiva del jackpot de La Bandida — el "número gigante"
    /// sobre la máquina, por canal tipado (<c>TypedEvent&lt;JackpotCountdownPayload&gt;</c>).
    /// </summary>
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
