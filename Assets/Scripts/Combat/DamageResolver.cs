using UnityEngine;

public class DamageResolver
{
    /// Apply player attack to enemy
    public static int ResolvePlayerAttack(CombinationResult combo, CharacterData character)
    {
        int damage = combo.BaseDamage;

        // Apply affinity bonus
        if (combo.Type == character.AffinityCombo)
        {
            damage = Mathf.RoundToInt(damage * character.AffinityDamageBonus);
        }

        return damage;
    }

    /// Get the total combo-specific damage multiplier from player buffs
    public static float GetComboMultiplier(CombinationType comboType, PlayerState state)
    {
        return 1f + state.GetComboBuffTotal(comboType);
    }

    /// Apply player attack using the 3AP damage formula:
    /// daño_final = (base_combo × multiplicador_dado) + bonus_dados_grandes
    public static int ResolvePlayerAttack3AP(CombinationResult combo, CharacterData character, DiceBag bag)
    {
        float averageEV = bag.CalculateAverageEV();
        float multiplicador = averageEV / 3.5f;
        int bonus = bag.CalculateLargeDiceBonus();

        int damage = Mathf.RoundToInt(combo.BaseDamage * multiplicador) + bonus;

        // Apply affinity bonus
        if (combo.Type == character.AffinityCombo)
        {
            damage = Mathf.RoundToInt(damage * character.AffinityDamageBonus);
        }

        return Mathf.Max(0, damage);
    }

    /// Get damage breakdown for UI display
    public static void GetDamageBreakdown(CombinationResult combo, DiceBag bag,
        out int comboBase, out float multiplier, out int bonus, out int total)
    {
        float averageEV = bag.CalculateAverageEV();
        multiplier = averageEV / 3.5f;
        bonus = bag.CalculateLargeDiceBonus();
        comboBase = combo.BaseDamage;
        total = Mathf.RoundToInt(comboBase * multiplier) + bonus;
    }

    /// Apply enemy attack to player, considering shield
    public static int ResolveEnemyAttack(int enemyRawDamage, int playerShield)
    {
        int netDamage = Mathf.Max(0, enemyRawDamage - playerShield);
        return netDamage;
    }
}
