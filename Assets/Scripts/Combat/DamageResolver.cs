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

    /// Apply enemy attack to player, considering shield
    public static int ResolveEnemyAttack(int enemyRawDamage, int playerShield)
    {
        int netDamage = Mathf.Max(0, enemyRawDamage - playerShield);
        return netDamage;
    }
}
