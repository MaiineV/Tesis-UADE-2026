using System;
using Rollgeon.Combat.Cashier;
using Rollgeon.Economy;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Economía fake para los tests del Cajero: contador de oro sin save system, sin
    /// <c>OnRunStart</c> y sin eventos. Lo que interesa es qué lee el jefe y cuánto le saca.
    /// </summary>
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

    /// <summary>
    /// Ledger fake: sólo expone las palancas que leen los nodos (<see cref="DamageStepDown"/>,
    /// <see cref="ChipValueMultiplier"/>) y registra las llamadas, sin suscribirse a eventos
    /// globales. Los tests del servicio real usan <c>CashierLedgerService</c> directo.
    /// </summary>
    internal sealed class FakeCashierLedgerService : ICashierLedgerService
    {
        public int VaultedGold { get; set; }
        public int ChipValueMultiplier { get; set; } = 1;
        public int DamageStepDown { get; set; }
        public int DamageStepUp { get; set; }
        public int BribeCost { get; set; } = 35;
        public int BribeRounds { get; set; } = 3;
        public int RakeRoundsPerStep { get; set; } = 3;

        public bool DamageTaken { get; set; }
        public int CollectTaxCalls { get; private set; }
        public float LastTaxPercent { get; private set; }
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

        public int CollectTax(Guid ownerGuid, float percent)
        {
            CollectTaxCalls++;
            LastTaxPercent = percent;
            VaultedGold += NextTaxAmount;
            return NextTaxAmount;
        }

        public void SetChipValueMultiplier(int multiplier) => ChipValueMultiplier = multiplier < 1 ? 1 : multiplier;

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
