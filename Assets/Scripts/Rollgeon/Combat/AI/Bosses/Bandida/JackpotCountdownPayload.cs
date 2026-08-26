using System;

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    public readonly struct JackpotCountdownPayload
    {
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
