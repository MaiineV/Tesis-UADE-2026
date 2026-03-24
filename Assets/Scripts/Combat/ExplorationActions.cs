using UnityEngine;

public static class ExplorationActions
{
    // Bow: d20 + Dexterity → hit%
    public static (bool hit, int roll, int hitChance) AttemptBow(int dexterity, int dexterityMax)
    {
        int roll = Random.Range(1, 21); // d20
        int total = roll + dexterity;
        int hitChance = Mathf.RoundToInt((total / (float)(20 + dexterityMax)) * 100f);
        int check = Random.Range(1, 101);
        return (check <= hitChance, total, hitChance);
    }

    // Bow damage: flat value based on roll
    public static int CalculateBowDamage(int roll)
    {
        return Mathf.Max(1, roll);
    }

    // Potion: d10 + Dexterity → heal% of max HP (never fails)
    public static (int healAmount, int roll, float healPercent) AttemptPotion(int dexterity, int dexterityMax, int maxHP)
    {
        int roll = Random.Range(1, 11); // d10
        int total = roll + dexterity;
        float healPercent = total / (float)(10 + dexterityMax) * 100f;
        int healAmount = Mathf.Max(1, Mathf.RoundToInt(maxHP * healPercent / 100f));
        return (healAmount, total, healPercent);
    }

    // Flee: d10 + Speed → success%
    public static (bool success, int roll, int successChance) AttemptFlee(int speed)
    {
        int roll = Random.Range(1, 11); // d10
        int total = roll + speed;
        int successChance = Mathf.Min(95, total * 5);
        int check = Random.Range(1, 101);
        return (check <= successChance, total, successChance);
    }

    // Force Door: d10 + Dexterity → success%
    public static (bool success, int roll, int successChance) AttemptForceDoor(int dexterity, int dexterityMax)
    {
        int roll = Random.Range(1, 11); // d10
        int total = roll + dexterity;
        int successChance = Mathf.RoundToInt((total / (float)(10 + dexterityMax)) * 100f);
        int check = Random.Range(1, 101);
        return (check <= successChance, total, successChance);
    }
}
