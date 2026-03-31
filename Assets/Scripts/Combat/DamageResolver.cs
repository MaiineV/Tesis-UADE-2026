using UnityEngine;

public class DamageResolver
{
    // Player attack: combo base damage + affinity bonus
    public static int ResolvePlayerAttack(CombinationResult combo, CharacterData character)
    {
        int damage = combo.BaseDamage;
        if (combo.Type == character.AffinityCombo)
            damage = Mathf.RoundToInt(damage * character.AffinityDamageBonus);
        return damage;
    }

    // Enemy attack: direct damage — shield is item territory, not a base mechanic
    public static int ResolveEnemyAttack(int enemyRawDamage)
    {
        return Mathf.Max(0, enemyRawDamage);
    }
}
