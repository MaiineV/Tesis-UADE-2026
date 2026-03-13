using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DefensePhase
{
    public int AvailableRolls;
    public List<CombinationResult> DefenseResults = new List<CombinationResult>();
    public int FinalShieldValue;

    public DefensePhase(int rollsUsedForAttack)
    {
        AvailableRolls = 3 - rollsUsedForAttack;
    }

    public void PerformDefenseRoll(DiceBag bag, bool hasGeneralaThisRun)
    {
        if (DefenseResults.Count >= AvailableRolls) return;

        var results = bag.RollAll();
        int[] values = results.Select(r => r.Value).ToArray();
        var combo = CombinationDetector.Evaluate(values, hasGeneralaThisRun);
        DefenseResults.Add(combo);
    }

    public int CalculateShield()
    {
        if (DefenseResults.Count == 0)
        {
            FinalShieldValue = 0;
            return 0;
        }

        // Pick best defense result
        var best = DefenseResults.OrderByDescending(r => GetShieldValue(r)).First();
        FinalShieldValue = GetShieldValue(best);
        return FinalShieldValue;
    }

    private int GetShieldValue(CombinationResult combo)
    {
        int sum = combo.MatchingDice.Sum();
        switch (combo.Type)
        {
            case CombinationType.HighDie:
                return Mathf.RoundToInt(combo.MatchingDice[0] * 0.5f);
            case CombinationType.Pair:
                return Mathf.RoundToInt(sum * 0.75f);
            case CombinationType.TwoPair:
                return Mathf.RoundToInt(sum * 0.75f);
            case CombinationType.ThreeOfAKind:
                return sum;
            case CombinationType.Straight:
                return 15;
            case CombinationType.FullHouse:
                return 20;
            case CombinationType.FourOfAKind:
                return Mathf.RoundToInt(sum * 1.5f);
            case CombinationType.Generala:
            case CombinationType.DoubleGenerala:
                return Mathf.RoundToInt(sum * 2.5f);
            default:
                return 0;
        }
    }
}
