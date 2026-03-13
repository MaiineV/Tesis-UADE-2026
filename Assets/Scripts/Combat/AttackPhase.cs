using System.Collections.Generic;
using System.Linq;

public class AttackPhase
{
    public int MaxRolls = 3;
    public int CurrentRoll = 0;
    public RollResult[] CurrentResults;
    public HashSet<string> LockedDiceIds = new HashSet<string>();
    public CombinationResult FinalCombination;
    public int RollsUsed => CurrentRoll;

    // Called when player clicks "Roll" or "Reroll"
    public RollResult[] PerformRoll(DiceBag bag)
    {
        CurrentRoll++;

        if (CurrentRoll == 1)
        {
            // First roll: roll everything
            CurrentResults = bag.RollAll();
        }
        else
        {
            // Subsequent rolls: only reroll unlocked dice
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

    // Player commits to current dice — evaluate best combo
    public CombinationResult Commit(bool hasGeneralaThisRun)
    {
        int[] values = CurrentResults.Select(r => r.Value).ToArray();
        FinalCombination = CombinationDetector.Evaluate(values, hasGeneralaThisRun);
        return FinalCombination;
    }
}
