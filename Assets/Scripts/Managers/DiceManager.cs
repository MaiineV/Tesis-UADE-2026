using UnityEngine;
using System.Collections.Generic;

public class DiceManager : MonoBehaviour
{
    public static DiceManager Instance;

    void Awake()
    {
        Instance = this;
    }

    public RollResult[] RollDice(List<DiceInstance> dice)
    {
        RollResult[] results = new RollResult[dice.Count];
        for (int i = 0; i < dice.Count; i++)
        {
            results[i] = dice[i].Roll();
        }
        return results;
    }

    public RollResult[] RerollDice(List<DiceInstance> dice, HashSet<string> diceIdsToReroll)
    {
        RollResult[] results = new RollResult[dice.Count];
        for (int i = 0; i < dice.Count; i++)
        {
            if (diceIdsToReroll.Contains(dice[i].Id))
            {
                results[i] = dice[i].Roll();
            }
        }
        return results;
    }
}
