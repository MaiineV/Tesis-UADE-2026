using System.Collections.Generic;

[System.Serializable]
public class DiceBag
{
    public List<DiceInstance> Dice = new List<DiceInstance>();
    public float MaxPower;

    public float CurrentPower
    {
        get
        {
            float total = 0;
            foreach (var d in Dice) total += d.PowerCost;
            return total;
        }
    }

    public float RemainingPower => MaxPower - CurrentPower;

    public bool CanAdd(DiceInstance die)
    {
        return CurrentPower + die.PowerCost <= MaxPower;
    }

    public bool TryAdd(DiceInstance die)
    {
        if (!CanAdd(die)) return false;
        Dice.Add(die);
        return true;
    }

    public void Remove(string diceId)
    {
        Dice.RemoveAll(d => d.Id == diceId);
    }

    public RollResult[] RollAll()
    {
        RollResult[] results = new RollResult[Dice.Count];
        for (int i = 0; i < Dice.Count; i++)
        {
            results[i] = Dice[i].Roll();
        }
        return results;
    }
}
