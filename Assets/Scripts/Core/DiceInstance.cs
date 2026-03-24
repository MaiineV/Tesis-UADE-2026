using UnityEngine;

[System.Serializable]
public class DiceInstance
{
    public string Id;
    public DiceData BaseData;
    public int[] CurrentFaces;
    public float PowerCost;

    public static DiceInstance Create(DiceData data)
    {
        return new DiceInstance
        {
            Id = System.Guid.NewGuid().ToString(),
            BaseData = data,
            CurrentFaces = (int[])data.DefaultFaces.Clone(),
            PowerCost = data.PowerCost
        };
    }

    public RollResult Roll()
    {
        int faceIndex = Random.Range(0, CurrentFaces.Length);
        return new RollResult
        {
            DiceId = this.Id,
            FaceIndex = faceIndex,
            Value = CurrentFaces[faceIndex]
        };
    }
}
