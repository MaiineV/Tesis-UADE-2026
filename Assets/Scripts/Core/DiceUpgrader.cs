using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class DiceUpgrader
{
    public static void ApplyUpgrade(DiceInstance die, FaceUpgrade upgrade)
    {
        switch (upgrade.Type)
        {
            case FaceUpgradeType.ValueIncrease:
                die.CurrentFaces[upgrade.TargetFaceIndex] += upgrade.Value;
                break;

            case FaceUpgradeType.ValueSet:
                die.CurrentFaces[upgrade.TargetFaceIndex] = upgrade.Value;
                break;

            case FaceUpgradeType.FaceRemoval:
                if (die.CurrentFaces.Length <= 2) return;
                var faces = new List<int>(die.CurrentFaces);
                faces.RemoveAt(upgrade.TargetFaceIndex);
                die.CurrentFaces = faces.ToArray();
                break;
        }
    }
}
