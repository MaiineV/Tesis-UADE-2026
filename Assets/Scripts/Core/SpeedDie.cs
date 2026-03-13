using UnityEngine;

[System.Serializable]
public class SpeedDie
{
    public int MinValue;
    public int MaxValue;

    public int Roll()
    {
        return Random.Range(MinValue, MaxValue + 1);
    }
}
