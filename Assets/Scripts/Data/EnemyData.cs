using UnityEngine;

[CreateAssetMenu(menuName = "Game/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string EnemyName;
    public Sprite Sprite;
    public Color EnemyColor;
    public GameObject ModelPrefab;

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

    [Header("Ranged")]
    public bool IsRanged;
    public int PreferredRange;
    public int Accuracy;
    public int Precision;
    public bool FiresFirst;

    [Header("Loot")]
    public int GoldDropMin;
    public int GoldDropMax;
}
