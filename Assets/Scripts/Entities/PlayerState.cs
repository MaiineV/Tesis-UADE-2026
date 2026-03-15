using System.Collections.Generic;
using UnityEngine;

public class PlayerState
{
    // Identity
    public CharacterData BaseData;

    // Health
    public int CurrentHP;
    public int MaxHP;

    // Dice
    public List<DiceInstance> FullInventory; // all dice the player owns
    public int CombatDiceSlots;              // how many dice to select for combat
    public DiceBag Bag;       // selected combat dice (subset of FullInventory)
    public SpeedDie SpeedDie; // TODO: Depends on US-02

    // Energy
    public float CurrentEnergy;
    public float MaxEnergy;

    // Position
    public Vector2Int GridPosition;

    // Combat state
    public int ShieldValue;
    public bool CrapsModeAvailable;

    public static PlayerState Create(CharacterData data)
    {
        var state = new PlayerState();
        state.BaseData = data;
        state.CurrentHP = data.MaxHP;
        state.MaxHP = data.MaxHP;
        state.MaxEnergy = 100f;
        state.CurrentEnergy = 0f;
        state.ShieldValue = 0;
        state.CrapsModeAvailable = false;

        // Build full inventory from starting dice
        state.FullInventory = new List<DiceInstance>();
        foreach (var loadout in data.StartingDice)
        {
            for (int i = 0; i < loadout.Quantity; i++)
                state.FullInventory.Add(DiceInstance.Create(loadout.DiceType));
        }
        // CombatDiceSlots defined in CharacterData; fallback to full inventory count
        state.CombatDiceSlots = data.CombatDiceSlots > 0
            ? data.CombatDiceSlots
            : state.FullInventory.Count;

        // Bag starts empty — filled by inventory builder before combat
        state.Bag = new DiceBag { MaxPower = data.StartingPowerBudget };

        // Build speed die
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
