using System;
using Rollgeon.Combat.Cashier;
using Rollgeon.Economy;

namespace Rollgeon.Combat.AI.Tests
{
    internal sealed class FakeEconomyService : IEconomyService
    {
        public int CurrentGold { get; private set; }

        /// <summary>Total agregado vía <see cref="Add"/> — sirve para afirmar devoluciones/cobros.</summary>
        public int TotalAdded { get; private set; }

        public FakeEconomyService(int startingGold = 0) => CurrentGold = startingGold;

        public void Add(int amount)
        {
            if (amount <= 0) return;
            CurrentGold += amount;
            TotalAdded += amount;
        }

        public bool Spend(int amount)
        {
            if (amount <= 0) return true;
            if (CurrentGold < amount) return false;
            CurrentGold -= amount;
            return true;
        }

        public bool CanAfford(int amount) => amount <= 0 || CurrentGold >= amount;

        public void ResetTo(int amount) => CurrentGold = amount < 0 ? 0 : amount;
    }

    internal sealed class FakeCashierLedgerService : ICashierLedgerService
    {
        public int VaultedGold { get; set; }
        public int ChipValueMultiplier { get; set; } = 1;
        public int DamageStepDown { get; set; }

        // Derivado y no un campo suelto: los nodos leen DamageStepDown, así que el fake tenía que
        // poder setear el escalón sin acordarse de mover también el contador de rondas.
        public int BribeRoundsLeft => DamageStepDown > 0 ? BribeRounds : 0;

        public int DamageStepUp { get; set; }
        public int BribeCost { get; set; } = 35;
        public int BribeRounds { get; set; } = 3;
        public int RakeRoundsPerStep { get; set; } = 3;

        public bool DamageTaken { get; set; }
        public int CollectTaxCalls { get; private set; }
        public float LastTaxPercent { get; private set; }
        public int LastTaxMinimum { get; private set; }
        public int NextTaxAmount { get; set; }
        public int RegisteredChips { get; private set; }
        public int LastChipValue { get; private set; }
        public Guid LastChipOwner { get; private set; }

        public bool ConsumeDamageTaken(Guid entityGuid)
        {
            if (!DamageTaken) return false;
            DamageTaken = false;
            return true;
        }

        public int CollectTax(Guid ownerGuid, float percent, int minimum = 0)
        {
            CollectTaxCalls++;
            LastTaxPercent = percent;
            LastTaxMinimum = minimum;
            VaultedGold += NextTaxAmount;
            return NextTaxAmount;
        }

        public void SetChipValueMultiplier(int multiplier) => ChipValueMultiplier = multiplier < 1 ? 1 : multiplier;

        public CashierTierSnapshot? LastTier { get; private set; }

        public int ReportTierCalls { get; private set; }

        public void ReportTier(int rank, int damage, int gold, int stepUp, int stepDown)
        {
            ReportTierCalls++;
            LastTier = new CashierTierSnapshot(rank, damage, gold, stepUp, stepDown);
        }

        public bool TryBribe()
        {
            DamageStepDown = 1;
            return true;
        }

        public void RegisterChip(Guid hazardInstanceId, int value, Guid ownerGuid)
        {
            RegisteredChips++;
            LastChipValue = value;
            LastChipOwner = ownerGuid;
        }

        public int GetChipValue(Guid hazardInstanceId) => LastChipValue;
    }
}
