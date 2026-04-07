using UnityEngine;
using UnityEditor;
using System.IO;

public static class DataCreator
{
    [MenuItem("Tools/Create All Game Data")]
    public static void CreateAllData()
    {
        CreateDice();
        CreateCharacters();
        CreateEnemies();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("All game data created successfully!");
    }

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }

    static void CreateDice()
    {
        EnsureFolder("Assets/Data/Dice");

        CreateDie("d6", 6, new[] { 1, 2, 3, 4, 5, 6 }, 1, HexColor("#42a5f5"));
        CreateDie("d10", 10, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 2, HexColor("#ff7043"), 4);
        CreateDie("d12", 12, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, 3, HexColor("#ab47bc"), 8);
    }

    static void CreateDie(string name, int faces, int[] defaultFaces, float powerCost, Color color, int bonusDamage = 0)
    {
        string path = $"Assets/Data/Dice/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<DiceData>(path) != null)
            AssetDatabase.DeleteAsset(path);

        var die = ScriptableObject.CreateInstance<DiceData>();
        die.DiceName = name;
        die.NumberOfFaces = faces;
        die.DefaultFaces = defaultFaces;
        die.PowerCost = powerCost;
        die.DiceColor = color;
        die.BonusDamage = bonusDamage;
        AssetDatabase.CreateAsset(die, path);
    }

    static void CreateCharacters()
    {
        EnsureFolder("Assets/Data/Characters");

        var warrior = ScriptableObject.CreateInstance<CharacterData>();
        warrior.CharacterName = "Warrior";
        warrior.ClassName = "Warrior";
        warrior.Description = "A balanced fighter with strong dice combinations.";
        warrior.CharacterColor = HexColor("#4fc3f7");
        warrior.MaxHP = 100;
        warrior.StartingPowerBudget = 5f;
        warrior.SpeedMin = 2;
        warrior.SpeedMax = 5;

        var d6 = AssetDatabase.LoadAssetAtPath<DiceData>("Assets/Data/Dice/d6.asset");
        var d10 = AssetDatabase.LoadAssetAtPath<DiceData>("Assets/Data/Dice/d10.asset");
        warrior.StartingDice = new[]
        {
            new DiceLoadout { DiceType = d6, Quantity = 4 },
            new DiceLoadout { DiceType = d10, Quantity = 1 }
        };
        warrior.CombatDiceSlots = 5;

        warrior.AffinityCombo = CombinationType.FourOfAKind;
        warrior.AffinityDamageBonus = 1.25f;
        warrior.UnlockedByDefault = true;

        string warriorPath = "Assets/Data/Characters/Warrior.asset";
        if (AssetDatabase.LoadAssetAtPath<CharacterData>(warriorPath) != null)
            AssetDatabase.DeleteAsset(warriorPath);
        AssetDatabase.CreateAsset(warrior, warriorPath);
    }

    static void CreateEnemies()
    {
        EnsureFolder("Assets/Data/Enemies");

        CreateEnemy("Goblin", 40, 2, 6, 1, 3, 50f, 15f, EnemyBehavior.Aggressive, HexColor("#66bb6a"), 5, 10);
        CreateEnemy("Orc", 60, 2, 8, 1, 2, 40f, 12f, EnemyBehavior.Aggressive, HexColor("#ef5350"), 10, 20);
        CreateArcher();
    }

    static void CreateArcher()
    {
        var archer = ScriptableObject.CreateInstance<EnemyData>();
        archer.EnemyName = "Archer";
        archer.EnemyColor = HexColor("#ffa726");
        archer.MaxHP = 18;
        archer.AttackDiceCount = 1;
        archer.AttackDiceFaces = 6;
        archer.SpeedMin = 1;
        archer.SpeedMax = 3;
        archer.MaxEnergy = 40f;
        archer.EnergyPerRound = 10f;
        archer.Behavior = EnemyBehavior.Ranged;
        archer.IsRanged = true;
        archer.PreferredRange = 3;
        archer.Accuracy = 60;
        archer.Precision = 3;
        archer.FiresFirst = true;
        archer.GoldDropMin = 10;
        archer.GoldDropMax = 10;
        archer.HasComboResistance = false;

        string path = "Assets/Data/Enemies/Archer.asset";
        if (AssetDatabase.LoadAssetAtPath<EnemyData>(path) != null)
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(archer, path);
    }

    static void CreateEnemy(string name, int hp, int atkCount, int atkFaces, int spdMin, int spdMax,
        float maxEnergy, float energyPerRound, EnemyBehavior behavior, Color color,
        int goldDropMin = 5, int goldDropMax = 15)
    {
        var enemy = ScriptableObject.CreateInstance<EnemyData>();
        enemy.EnemyName = name;
        enemy.EnemyColor = color;
        enemy.MaxHP = hp;
        enemy.AttackDiceCount = atkCount;
        enemy.AttackDiceFaces = atkFaces;
        enemy.SpeedMin = spdMin;
        enemy.SpeedMax = spdMax;
        enemy.MaxEnergy = maxEnergy;
        enemy.EnergyPerRound = energyPerRound;
        enemy.Behavior = behavior;
        enemy.GoldDropMin = goldDropMin;
        enemy.GoldDropMax = goldDropMax;
        string enemyPath = $"Assets/Data/Enemies/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<EnemyData>(enemyPath) != null)
            AssetDatabase.DeleteAsset(enemyPath);
        AssetDatabase.CreateAsset(enemy, enemyPath);
    }

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var color);
        return color;
    }
}
