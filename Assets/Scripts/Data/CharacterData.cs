using UnityEngine;

[CreateAssetMenu(menuName = "Game/CharacterData")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string CharacterName;
    public string ClassName;
    public string Description;
    public Sprite Portrait;
    public Color CharacterColor;

    [Header("Base Stats")]
    public int MaxHP;
    public int StartingPowerBudget;

    [Header("Speed Die")]
    public int SpeedMin;
    public int SpeedMax;

    [Header("Starting Dice")]
    public DiceLoadout[] StartingDice;

    [Header("Affinity")]
    public CombinationType AffinityCombo;
    public float AffinityDamageBonus;

    [Header("Unlock")]
    public bool UnlockedByDefault;
    public string UnlockDescription;
}
