using UnityEngine;

[System.Serializable]
public class EnemyState
{
    public EnemyData BaseData;
    public int CurrentHP;
    public int MaxHP;
    public Vector2Int GridPosition;
    public float CurrentEnergy;
    public SpeedDie SpeedDie;

    public bool IsAlive => CurrentHP > 0;
    public bool IsEnraged => CurrentEnergy >= BaseData.MaxEnergy;

    public static EnemyState Create(EnemyData data, Vector2Int position)
    {
        return new EnemyState
        {
            BaseData = data,
            CurrentHP = data.MaxHP,
            MaxHP = data.MaxHP,
            GridPosition = position,
            CurrentEnergy = 0,
            SpeedDie = new SpeedDie
            {
                MinValue = data.SpeedMin,
                MaxValue = data.SpeedMax
            }
        };
    }

    public void TakeDamage(int amount)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - amount);
    }

    public void GainEnergy()
    {
        CurrentEnergy = Mathf.Min(BaseData.MaxEnergy, CurrentEnergy + BaseData.EnergyPerRound);
    }
}
