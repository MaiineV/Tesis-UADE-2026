using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CombinationDetector
{
    /// Analyze a set of roll results and return the BEST combination found.
    /// "Best" = highest damage output.
    public static CombinationResult Evaluate(int[] diceValues, bool hasGeneralaThisRun)
    {
        if (diceValues == null || diceValues.Length == 0)
            return new CombinationResult { Type = CombinationType.HighDie, BaseDamage = 0,
                MatchingDice = new int[0], RemainingDice = new int[0] };

        // Build frequency map
        Dictionary<int, int> freq = new Dictionary<int, int>();
        foreach (int v in diceValues)
        {
            if (!freq.ContainsKey(v)) freq[v] = 0;
            freq[v]++;
        }

        // Check each combination from highest to lowest priority
        // We evaluate ALL and pick the one with highest damage

        List<CombinationResult> candidates = new List<CombinationResult>();

        // Generala: 5+ of a kind
        foreach (var kvp in freq)
        {
            if (kvp.Value >= 5)
            {
                int sum = kvp.Key * 5;
                var type = hasGeneralaThisRun
                    ? CombinationType.DoubleGenerala
                    : CombinationType.Generala;
                int multiplier = type == CombinationType.DoubleGenerala ? 10 : 5;
                candidates.Add(MakeResult(type, kvp.Key, 5, diceValues, sum * multiplier));
            }
        }

        // Four of a Kind
        foreach (var kvp in freq)
        {
            if (kvp.Value >= 4)
            {
                int sum = kvp.Key * 4;
                candidates.Add(MakeResult(CombinationType.FourOfAKind, kvp.Key, 4, diceValues, sum * 3));
            }
        }

        // Full House: need a 3-of-a-kind AND a separate pair
        var threes = freq.Where(f => f.Value >= 3).ToList();
        var twos = freq.Where(f => f.Value >= 2).ToList();
        foreach (var three in threes)
        {
            foreach (var two in twos)
            {
                if (three.Key != two.Key)
                {
                    int sum = three.Key * 3 + two.Key * 2;
                    candidates.Add(new CombinationResult
                    {
                        Type = CombinationType.FullHouse,
                        MatchingDice = Enumerable.Repeat(three.Key, 3)
                            .Concat(Enumerable.Repeat(two.Key, 2)).ToArray(),
                        RemainingDice = GetRemaining(diceValues,
                            Enumerable.Repeat(three.Key, 3)
                            .Concat(Enumerable.Repeat(two.Key, 2)).ToArray()),
                        BaseDamage = 35 + sum
                    });
                }
            }
        }

        // Straight: find longest consecutive sequence in the VALUES present
        candidates.AddRange(CheckStraights(diceValues));

        // Three of a Kind
        foreach (var kvp in freq)
        {
            if (kvp.Value >= 3)
            {
                int sum = kvp.Key * 3;
                candidates.Add(MakeResult(CombinationType.ThreeOfAKind, kvp.Key, 3, diceValues, sum * 2));
            }
        }

        // Two Pair
        var pairs = freq.Where(f => f.Value >= 2).OrderByDescending(f => f.Key).ToList();
        if (pairs.Count >= 2)
        {
            int sum = pairs[0].Key * 2 + pairs[1].Key * 2;
            candidates.Add(new CombinationResult
            {
                Type = CombinationType.TwoPair,
                MatchingDice = Enumerable.Repeat(pairs[0].Key, 2)
                    .Concat(Enumerable.Repeat(pairs[1].Key, 2)).ToArray(),
                RemainingDice = GetRemaining(diceValues,
                    Enumerable.Repeat(pairs[0].Key, 2)
                    .Concat(Enumerable.Repeat(pairs[1].Key, 2)).ToArray()),
                BaseDamage = Mathf.RoundToInt(sum * 1.5f)
            });
        }

        // Pair
        foreach (var kvp in freq)
        {
            if (kvp.Value >= 2)
            {
                int sum = kvp.Key * 2;
                candidates.Add(MakeResult(CombinationType.Pair, kvp.Key, 2, diceValues,
                    Mathf.RoundToInt(sum * 1.5f)));
            }
        }

        // High Die (always available as fallback)
        int highest = diceValues.Max();
        candidates.Add(new CombinationResult
        {
            Type = CombinationType.HighDie,
            MatchingDice = new int[] { highest },
            RemainingDice = GetRemaining(diceValues, new int[] { highest }),
            BaseDamage = highest
        });

        // Return the candidate with the highest damage
        return candidates.OrderByDescending(c => c.BaseDamage).First();
    }

    private static List<CombinationResult> CheckStraights(int[] diceValues)
    {
        var results = new List<CombinationResult>();
        var uniqueSorted = diceValues.Distinct().OrderBy(v => v).ToList();
        if (uniqueSorted.Count == 0) return results;

        // Find longest consecutive run
        int bestStart = uniqueSorted[0];
        int bestLength = 1;
        int currentStart = uniqueSorted[0];
        int currentLength = 1;

        for (int i = 1; i < uniqueSorted.Count; i++)
        {
            if (uniqueSorted[i] == uniqueSorted[i - 1] + 1)
            {
                currentLength++;
                if (currentLength > bestLength)
                {
                    bestLength = currentLength;
                    bestStart = currentStart;
                }
            }
            else
            {
                currentStart = uniqueSorted[i];
                currentLength = 1;
            }
        }

        // Need at least 5 consecutive for a straight
        if (bestLength >= 5)
        {
            int[] straightDice = Enumerable.Range(bestStart, bestLength).ToArray();
            int highestInStraight = straightDice.Max();
            results.Add(new CombinationResult
            {
                Type = CombinationType.Straight,
                MatchingDice = straightDice,
                RemainingDice = GetRemaining(diceValues, straightDice),
                BaseDamage = 30 + highestInStraight
            });
        }

        return results;
    }

    // Helper: given all dice and matching dice, return the leftover
    private static int[] GetRemaining(int[] allDice, int[] matching)
    {
        var remaining = new List<int>(allDice);
        foreach (int m in matching)
        {
            remaining.Remove(m); // removes first occurrence
        }
        return remaining.ToArray();
    }

    private static CombinationResult MakeResult(CombinationType type, int matchValue,
        int matchCount, int[] allDice, int damage)
    {
        int[] matching = Enumerable.Repeat(matchValue, matchCount).ToArray();
        return new CombinationResult
        {
            Type = type,
            MatchingDice = matching,
            RemainingDice = GetRemaining(allDice, matching),
            BaseDamage = damage
        };
    }
}
