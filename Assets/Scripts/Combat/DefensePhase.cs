using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DefensePhase
{
    public int MaxRolls;
    public int CurrentRoll = 0;
    public RollResult[] CurrentResults;
    public HashSet<string> LockedDiceIds = new HashSet<string>();
    public CombinationResult FinalCombination;
    public int FinalShieldValue;
    public int RollsUsed => CurrentRoll;

    public DefensePhase(int rollsUsedForAttack)
    {
        MaxRolls = Mathf.Max(0, 3 - rollsUsedForAttack);
    }

    public RollResult[] PerformRoll(DiceBag bag)
    {
        CurrentRoll++;

        if (CurrentRoll == 1)
        {
            CurrentResults = bag.RollAll();
        }
        else
        {
            for (int i = 0; i < CurrentResults.Length; i++)
            {
                if (!LockedDiceIds.Contains(CurrentResults[i].DiceId))
                {
                    var die = bag.Dice.First(d => d.Id == CurrentResults[i].DiceId);
                    CurrentResults[i] = die.Roll();
                }
            }
        }

        return CurrentResults;
    }

    public void ToggleLock(string diceId)
    {
        if (LockedDiceIds.Contains(diceId))
            LockedDiceIds.Remove(diceId);
        else
            LockedDiceIds.Add(diceId);
    }

    public bool CanRollAgain => CurrentRoll < MaxRolls;
    public bool HasRolled => CurrentRoll > 0;

    public int Commit(bool hasGeneralaThisRun)
    {
        if (CurrentResults == null || CurrentResults.Length == 0)
        {
            FinalShieldValue = 0;
            return 0;
        }

        int[] values = CurrentResults.Select(r => r.Value).ToArray();
        FinalCombination = CombinationDetector.Evaluate(values, hasGeneralaThisRun);
        FinalShieldValue = GetShieldValue(FinalCombination);
        return FinalShieldValue;
    }

    private int GetShieldValue(CombinationResult combo)
    {
        int sum = combo.MatchingDice.Sum();
        switch (combo.Type)
        {
            case CombinationType.HighDie:        return Mathf.RoundToInt(combo.MatchingDice[0] * 0.5f);
            case CombinationType.Pair:           return Mathf.RoundToInt(sum * 0.75f);
            case CombinationType.TwoPair:        return Mathf.RoundToInt(sum * 0.75f);
            case CombinationType.ThreeOfAKind:   return sum;
            case CombinationType.Straight:       return 15;
            case CombinationType.FullHouse:      return 20;
            case CombinationType.FourOfAKind:    return Mathf.RoundToInt(sum * 1.5f);
            case CombinationType.Generala:
            case CombinationType.DoubleGenerala: return Mathf.RoundToInt(sum * 2.5f);
            default:                             return 0;
        }
    }
}
