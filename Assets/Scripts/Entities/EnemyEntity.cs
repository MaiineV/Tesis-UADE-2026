using UnityEngine;

public class EnemyEntity : MonoBehaviour
{
    public EnemyState State { get; private set; }
    public SpriteRenderer Visual;

    public void Initialize(EnemyData data, Vector2Int position)
    {
        State = EnemyState.Create(data, position);
        Visual.color = data.EnemyColor;
        transform.position = GridManager.Instance.GridToWorld(position); // TODO: Depends on US-03
    }

    /// Roll attack. Returns final damage value.
    public int RollAttack()
    {
        int totalDamage = 0;

        // Roll N dice of the enemy's attack type
        for (int i = 0; i < State.BaseData.AttackDiceCount; i++)
        {
            totalDamage += Random.Range(1, State.BaseData.AttackDiceFaces + 1);
        }

        // Apply enrage bonus
        if (State.IsEnraged)
        {
            bool crit = Random.value < 0.6f;
            State.CurrentEnergy = 0; // reset
            if (crit)
            {
                totalDamage *= 2;
            }
        }

        // Gain energy for next round
        State.GainEnergy();

        return totalDamage;
    }

    public void MoveTo(Vector2Int newPosition)
    {
        State.GridPosition = newPosition;
        transform.position = GridManager.Instance.GridToWorld(newPosition); // TODO: Depends on US-03
    }
}
