using UnityEngine;

[CreateAssetMenu(menuName = "Game/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string EnemyName;
    public Sprite Sprite;
    public Color EnemyColor;

    [Header("Stats")]
    public int MaxHP;
    public int AttackDiceCount;
    public int AttackDiceFaces;

    [Header("Movement")]
    public int SpeedMin;
    public int SpeedMax;

    [Header("Energy")]
    public float MaxEnergy;
    public float EnergyPerRound;

    [Header("Behavior")]
    public EnemyBehavior Behavior;
}
