using UnityEngine;

public class ShieldPhase
{
    public int ShieldValue;

    /// Roll a d6 for shield. Returns the shield value.
    public int RollShield()
    {
        ShieldValue = Random.Range(1, 7);
        return ShieldValue;
    }
}
