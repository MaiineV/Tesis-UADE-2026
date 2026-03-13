using System;
using UnityEngine;

public class CrapsMode
{
    public bool IsActive { get; private set; }
    public CombinationType BetCombo { get; private set; }

    // Events
    public static event Action OnCrapsModeStarted;
    public static event Action<CombinationType> OnBetPlaced;
    public static event Action<bool, CrapsResult> OnCrapsResolved; // success, result

    public void Activate()
    {
        IsActive = true;
        OnCrapsModeStarted?.Invoke();
    }

    public void PlaceBet(CombinationType bet)
    {
        BetCombo = bet;
        OnBetPlaced?.Invoke(bet);
    }

    public CrapsResult Resolve(CombinationType actualCombo, int baseDamage)
    {
        bool success = IsMatchingBet(actualCombo);
        var result = new CrapsResult();
        result.Success = success;
        result.BetCombo = BetCombo;
        result.ActualCombo = actualCombo;

        if (success)
        {
            result.DamageMultiplier = GetSuccessMultiplier(BetCombo);
            result.HPChange = GetSuccessHeal(BetCombo);
        }
        else
        {
            result.DamageMultiplier = GetFailureMultiplier(BetCombo);
            result.HPChange = GetFailureDamage(BetCombo);
        }

        result.FinalDamage = Mathf.RoundToInt(baseDamage * result.DamageMultiplier);

        IsActive = false;
        OnCrapsResolved?.Invoke(success, result);
        return result;
    }

    private bool IsMatchingBet(CombinationType actual)
    {
        // Exact match or better counts as success
        // e.g., bet Pair but got Four of a Kind -> still success
        if (actual == BetCombo) return true;

        // "Or better" rules:
        int betRank = GetComboRank(BetCombo);
        int actualRank = GetComboRank(actual);

        // Special case: Straight and Full House don't upgrade to each other
        // Only N-of-a-kind combos upgrade within their line
        if (IsNOfAKind(BetCombo) && IsNOfAKind(actual) && actualRank >= betRank)
            return true;

        return false;
    }

    private bool IsNOfAKind(CombinationType type)
    {
        return type == CombinationType.Pair ||
               type == CombinationType.ThreeOfAKind ||
               type == CombinationType.FourOfAKind ||
               type == CombinationType.Generala;
    }

    private int GetComboRank(CombinationType type)
    {
        switch (type)
        {
            case CombinationType.Pair: return 1;
            case CombinationType.ThreeOfAKind: return 2;
            case CombinationType.Straight: return 3;
            case CombinationType.FullHouse: return 4;
            case CombinationType.FourOfAKind: return 5;
            case CombinationType.Generala: return 6;
            default: return 0;
        }
    }

    private float GetSuccessMultiplier(CombinationType bet)
    {
        switch (bet)
        {
            case CombinationType.Pair: return 1.25f;
            case CombinationType.ThreeOfAKind: return 1.5f;
            case CombinationType.Straight: return 1.5f;
            case CombinationType.FullHouse: return 1.75f;
            case CombinationType.FourOfAKind: return 2.0f;
            case CombinationType.Generala: return 3.0f;
            default: return 1.0f;
        }
    }

    private float GetFailureMultiplier(CombinationType bet)
    {
        switch (bet)
        {
            case CombinationType.Pair: return 0.9f;
            case CombinationType.ThreeOfAKind: return 0.85f;
            case CombinationType.Straight: return 0.85f;
            case CombinationType.FullHouse: return 0.8f;
            case CombinationType.FourOfAKind: return 0.75f;
            case CombinationType.Generala: return 0.5f;
            default: return 1.0f;
        }
    }

    private int GetSuccessHeal(CombinationType bet)
    {
        switch (bet)
        {
            case CombinationType.Straight: return 10;
            case CombinationType.Generala: return 20;
            default: return 0;
        }
    }

    private int GetFailureDamage(CombinationType bet)
    {
        switch (bet)
        {
            case CombinationType.FourOfAKind: return -5;
            case CombinationType.Generala: return -10;
            default: return 0;
        }
    }
}

[System.Serializable]
public struct CrapsResult
{
    public bool Success;
    public CombinationType BetCombo;
    public CombinationType ActualCombo;
    public float DamageMultiplier;
    public int HPChange;            // positive = heal, negative = self-damage
    public int FinalDamage;
}
