using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class RewardGenerator
{
    /// Generate N unique upgrade offers for the player's current bag
    public static List<FaceUpgradeOffer> GenerateOffers(DiceBag bag, int count)
    {
        var offers = new List<FaceUpgradeOffer>();
        if (bag == null || bag.Dice == null || bag.Dice.Count == 0)
            return offers;

        var usedDieFacePairs = new HashSet<string>(); // prevent duplicate targets

        int attempts = 0;
        while (offers.Count < count && attempts < 50)
        {
            attempts++;

            // Pick random die
            var die = bag.Dice[Random.Range(0, bag.Dice.Count)];
            int faceIdx = Random.Range(0, die.CurrentFaces.Length);
            string key = $"{die.Id}_{faceIdx}";

            if (usedDieFacePairs.Contains(key)) continue;
            usedDieFacePairs.Add(key);

            // Generate upgrade
            var upgrade = GenerateUpgrade(die, faceIdx);
            offers.Add(new FaceUpgradeOffer
            {
                TargetDiceId = die.Id,
                TargetDiceName = die.BaseData.DiceName,
                Upgrade = upgrade
            });
        }

        return offers;
    }

    private static FaceUpgrade GenerateUpgrade(DiceInstance die, int faceIdx)
    {
        int currentValue = die.CurrentFaces[faceIdx];
        float roll = Random.value;

        if (roll < 0.5f)
        {
            int increase = Random.Range(2, 5);
            return new FaceUpgrade
            {
                Type = FaceUpgradeType.ValueIncrease,
                TargetFaceIndex = faceIdx,
                Value = increase,
                Description = $"{die.BaseData.DiceName}: face [{currentValue}] gains +{increase} -> [{currentValue + increase}]"
            };
        }
        else if (roll < 0.8f)
        {
            int maxFace = die.CurrentFaces.Max();
            int newVal = maxFace + Random.Range(1, 4);
            return new FaceUpgrade
            {
                Type = FaceUpgradeType.ValueSet,
                TargetFaceIndex = faceIdx,
                Value = newVal,
                Description = $"{die.BaseData.DiceName}: face [{currentValue}] becomes [{newVal}]"
            };
        }
        else
        {
            if (die.CurrentFaces.Length <= 3)
            {
                // Safety: if die has few faces, do value increase instead
                return new FaceUpgrade
                {
                    Type = FaceUpgradeType.ValueIncrease,
                    TargetFaceIndex = faceIdx,
                    Value = 3,
                    Description = $"{die.BaseData.DiceName}: face [{currentValue}] gains +3 -> [{currentValue + 3}]"
                };
            }
            return new FaceUpgrade
            {
                Type = FaceUpgradeType.FaceRemoval,
                TargetFaceIndex = faceIdx,
                Value = 0,
                Description = $"{die.BaseData.DiceName}: REMOVE face [{currentValue}] ({die.CurrentFaces.Length} -> {die.CurrentFaces.Length - 1} faces)"
            };
        }
    }
}
