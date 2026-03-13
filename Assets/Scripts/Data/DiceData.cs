using UnityEngine;

[CreateAssetMenu(menuName = "Game/DiceData")]
public class DiceData : ScriptableObject
{
    public string DiceName;
    public int NumberOfFaces;
    public int[] DefaultFaces;
    public int PowerCost;
    public Sprite Icon;
    public Color DiceColor;
}
