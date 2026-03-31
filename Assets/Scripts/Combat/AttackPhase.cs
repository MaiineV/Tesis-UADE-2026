using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Handles the Generala Phase of Pick & Roll.
// Flow: PerformRoll (initial roll) → SetMovementDice → PerformRoll (rerolls) → Commit
public class AttackPhase
{
    public int MaxRolls = 3;
    public int CurrentRoll = 0;
    public RollResult[] AllInitialResults;          // All dice from the initial roll
    public RollResult[] CurrentResults;             // Dice available for Generala (excludes movement dice)
    public HashSet<string> LockedDiceIds = new HashSet<string>();
    public HashSet<string> MovementDiceIds = new HashSet<string>();
    public CombinationResult FinalCombination;
    public int RollsUsed => CurrentRoll;

    // Called once at the start (PickAndRoll state) and again for rerolls (GeneralaPhase)
    public RollResult[] PerformRoll(DiceBag bag)
    {
        CurrentRoll++;

        if (CurrentRoll == 1)
        {
            // Initial roll: roll ALL dice
            AllInitialResults = bag.RollAll();
            CurrentResults = AllInitialResults;
        }
        else
        {
            // Reroll: only reroll unlocked dice from the Generala pool
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

    // Called after player confirms movement dice selection.
    // Filters out movement dice from Generala pool.
    public void SetMovementDice(IEnumerable<string> movementIds)
    {
        MovementDiceIds = new HashSet<string>(movementIds);
        LockedDiceIds.ExceptWith(MovementDiceIds);
        CurrentResults = AllInitialResults
            .Where(r => !MovementDiceIds.Contains(r.DiceId))
            .ToArray();
    }

    // Returns total movement tiles from the selected movement dice
    public int GetMovementSteps()
    {
        if (AllInitialResults == null) return 0;
        int steps = 0;
        foreach (var r in AllInitialResults)
            if (MovementDiceIds.Contains(r.DiceId))
                steps += r.Value;
        return steps;
    }

    public void ToggleLock(string diceId)
    {
        if (LockedDiceIds.Contains(diceId))
            LockedDiceIds.Remove(diceId);
        else
            LockedDiceIds.Add(diceId);
    }

    public bool CanRollAgain => CurrentRoll < MaxRolls;

    public CombinationResult Commit(bool hasGeneralaThisRun)
    {
        if (CurrentResults == null || CurrentResults.Length == 0)
        {
            FinalCombination = new CombinationResult
            {
                Type = CombinationType.HighDie,
                BaseDamage = 0,
                MatchingDice = new int[0]
            };
            return FinalCombination;
        }
        int[] values = CurrentResults.Select(r => r.Value).ToArray();
        FinalCombination = CombinationDetector.Evaluate(values, hasGeneralaThisRun);
        return FinalCombination;
    }
}
