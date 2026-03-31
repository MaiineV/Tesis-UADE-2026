using UnityEngine;

public static class ExplorationActions
{
    // Bow: d20 flat roll — item mechanic placeholder
    // Hit if roll >= 11 (~50% base). Dexterity removed (not a GDD stat).
    public static (bool hit, int roll) AttemptBow()
    {
        int roll = Random.Range(1, 21); // d20
        return (roll >= 11, roll);
    }

    // Bow damage: flat value based on roll
    public static int CalculateBowDamage(int roll)
    {
        return Mathf.Max(1, roll);
    }

    // Potion: d10 flat roll → heal amount (item mechanic placeholder)
    // Dexterity removed (not a GDD stat).
    public static (int healAmount, int roll) AttemptPotion(int maxHP)
    {
        int roll = Random.Range(1, 11); // d10
        int healAmount = Mathf.Max(1, roll * 2);
        return (healAmount, roll);
    }

    // Flee: d10 flat roll — success if roll >= 6 (~50% base).
    // GDD note: leaving adjacency triggers Opportunity Attack (1d6 both sides).
    // Speed removed (not a player GDD stat).
    public static (bool success, int roll) AttemptFlee()
    {
        int roll = Random.Range(1, 11); // d10
        return (roll >= 6, roll);
    }

    // Force Door: d10 flat roll — success if roll >= 7 (~40% base).
    // Dexterity removed (not a GDD stat).
    public static (bool success, int roll) AttemptForceDoor()
    {
        int roll = Random.Range(1, 11); // d10
        return (roll >= 7, roll);
    }
}
