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

    public DiceInstance GetLargestDie()
    {
        DiceInstance largest = null;
        int maxFaces = 0;
        for (int i = 0; i < Dice.Count; i++)
        {
            if (Dice[i].BaseData.NumberOfFaces > maxFaces)
            {
                maxFaces = Dice[i].BaseData.NumberOfFaces;
                largest = Dice[i];
            }
        }
        return largest;
    }

    public float CalculateAverageEV()
    {
        if (Dice.Count == 0) return 0f;
        float totalEV = 0f;
        for (int i = 0; i < Dice.Count; i++)
        {
            int[] faces = Dice[i].CurrentFaces;
            float sum = 0f;
            for (int j = 0; j < faces.Length; j++) sum += faces[j];
            totalEV += sum / faces.Length;
        }
        return totalEV / Dice.Count;
    }

    public int CalculateLargeDiceBonus()
    {
        // Find all dice with BonusDamage > 0, sorted by bonus desc
        int maxBonus = 0;
        int otherBonusSum = 0;
        for (int i = 0; i < Dice.Count; i++)
        {
            int bonus = Dice[i].BaseData.BonusDamage;
            if (bonus <= 0) continue;

            if (bonus > maxBonus)
            {
                otherBonusSum += maxBonus; // previous max becomes "other"
                maxBonus = bonus;
            }
            else
            {
                otherBonusSum += bonus;
            }
        }
        // Full bonus for the largest, 50% for the rest
        return maxBonus + (int)(otherBonusSum * 0.5f);
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
