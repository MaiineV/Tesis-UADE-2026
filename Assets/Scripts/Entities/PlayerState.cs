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
    public List<DiceInstance> FullInventory;
    public DiceBag Bag;

    // Energy
    public float CurrentEnergy;
    public float MaxEnergy;

    // Position
    public Vector2Int GridPosition;

    // Combat state
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
        state.MaxEnergy = 100f;
        state.CurrentEnergy = 0f;
        state.CrapsModeAvailable = false;
        state.Gold = 0;
        state.HasPotion = true;
        state.PotionCount = 1;
        state.FullInventory = new List<DiceInstance>();
        foreach (var loadout in data.StartingDice)
        {
            for (int i = 0; i < loadout.Quantity; i++)
                state.FullInventory.Add(DiceInstance.Create(loadout.DiceType));
        }

        state.Bag = new DiceBag { MaxPower = data.StartingPowerBudget };

        return state;
    }

    public bool IsAlive => CurrentHP > 0;

    // Direct damage — no base shield mechanic (shield is item territory)
    public void TakeDamage(int damage)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - damage);
    }

    public void Heal(int amount)
    {
        CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
    }
}
