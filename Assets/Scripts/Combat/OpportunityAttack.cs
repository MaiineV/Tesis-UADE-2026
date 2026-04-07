using UnityEngine;

/// Bilateral Opportunity Attack system for 3AP combat.
/// OA triggers when an entity moves AWAY from another entity that has it in attack range.
public static class OpportunityAttack
{
    /// Chebyshev distance (8-directional adjacency)
    public static int Distance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    /// Check if moving from fromPos to toPos triggers an OA from the attacker at attackerPos.
    /// OA triggers if: was in attacker's range AND moved out of range.
    public static bool ShouldTrigger(Vector2Int moverFrom, Vector2Int moverTo,
        Vector2Int attackerPos, int attackRange)
    {
        int distBefore = Distance(moverFrom, attackerPos);
        int distAfter = Distance(moverTo, attackerPos);
        return distBefore <= attackRange && distAfter > attackRange;
    }

    /// Enemy OA damage against player: fixed value from EnemyData.
    public static int EnemyOADamage(EnemyData data)
    {
        return data.OADamage;
    }

    /// Player OA damage against enemy: roll the largest die in the bag.
    public static int PlayerOADamage(DiceBag bag)
    {
        var largest = bag.GetLargestDie();
        if (largest == null) return 0;
        return largest.Roll().Value;
    }
}
