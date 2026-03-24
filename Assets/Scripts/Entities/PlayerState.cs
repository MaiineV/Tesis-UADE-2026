using System.Collections.Generic;
using UnityEngine;

public class PlayerState
{
    // Identity
    public CharacterData BaseData;

    // Health
    public int CurrentHP;
    public int MaxHP;

    // Stats
    public int Dexterity;
    public int Speed;

    // Dice
    public List<DiceInstance> FullInventory;
    public int CombatDiceSlots;
    public DiceBag Bag;
    public SpeedDie SpeedDie;

    // Energy
    public float CurrentEnergy;
    public float MaxEnergy;

    // Position
    public Vector2Int GridPosition;

    // Combat state
    public int ShieldValue;
    public bool CrapsModeAvailable;

    // Economy
    public int Gold;

    // Items
    public bool HasPotion;
    public int PotionCount;

    public static PlayerState Create(CharacterData data)
    {
        var state = new PlayerState();
        state.BaseData = data;
        state.CurrentHP = data.MaxHP;
        state.MaxHP = data.MaxHP;
        state.Dexterity = data.Dexterity;
        state.Speed = data.Speed;
        state.MaxEnergy = 100f;
        state.CurrentEnergy = 0f;
        state.ShieldValue = 0;
        state.CrapsModeAvailable = false;
        state.Gold = 0;
        state.HasPotion = true;
        state.PotionCount = 1;

        // Build full inventory from starting dice
        state.FullInventory = new List<DiceInstance>();
        foreach (var loadout in data.StartingDice)
        {
            for (int i = 0; i < loadout.Quantity; i++)
                state.FullInventory.Add(DiceInstance.Create(loadout.DiceType));
        }
        state.CombatDiceSlots = data.CombatDiceSlots > 0
            ? data.CombatDiceSlots
            : state.FullInventory.Count;

        state.Bag = new DiceBag { MaxPower = data.StartingPowerBudget };

        state.SpeedDie = new SpeedDie
        {
            MinValue = data.SpeedMin,
            MaxValue = data.SpeedMax
        };

        return state;
    }

    public bool IsAlive => CurrentHP > 0;

    public void TakeDamage(int rawDamage)
    {
        int blocked = Mathf.Min(ShieldValue, rawDamage);
        int actualDamage = rawDamage - blocked;
        ShieldValue = Mathf.Max(0, ShieldValue - blocked);
        CurrentHP = Mathf.Max(0, CurrentHP - actualDamage);
    }

    public void Heal(int amount)
    {
        CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
    }
}
