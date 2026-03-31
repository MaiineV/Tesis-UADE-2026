using UnityEngine;
using UnityEditor;

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

        CreateDie("d6",  6,  new[] { 1, 2, 3, 4, 5, 6 },                          1, HexColor("#42a5f5"));
        CreateDie("d8",  8,  new[] { 1, 2, 3, 4, 5, 6, 7, 8 },                    2, HexColor("#66bb6a"));
        CreateDie("d10", 10, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },             3, HexColor("#ff9800"));
        CreateDie("d12", 12, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 },     4, HexColor("#ab47bc"));
    }

    static void CreateDie(string name, int faces, int[] defaultFaces, float powerCost, Color color)
    {
        var die = ScriptableObject.CreateInstance<DiceData>();
        die.DiceName = name;
        die.NumberOfFaces = faces;
        die.DefaultFaces = defaultFaces;
        die.PowerCost = powerCost;
        die.DiceColor = color;
        CreateOrReplaceAsset(die, $"Assets/Data/Dice/{name}.asset");
    }

    static void CreateCharacters()
    {
        EnsureFolder("Assets/Data/Characters");

        var d6 = AssetDatabase.LoadAssetAtPath<DiceData>("Assets/Data/Dice/d6.asset");
        var d8 = AssetDatabase.LoadAssetAtPath<DiceData>("Assets/Data/Dice/d8.asset");

        // Warrior — 4×d6 + 2×d8, budget 8, Full House affinity
        var warrior = ScriptableObject.CreateInstance<CharacterData>();
        warrior.CharacterName = "Warrior";
        warrior.ClassName = "Warrior";
        warrior.Description = "A balanced fighter with strong dice combinations.";
        warrior.CharacterColor = HexColor("#4fc3f7");
        warrior.MaxHP = 100;
        warrior.StartingPowerBudget = 8f;
        warrior.StartingDice = new[]
        {
            new DiceLoadout { DiceType = d6, Quantity = 4 },
            new DiceLoadout { DiceType = d8, Quantity = 2 }
        };
        warrior.CombatDiceSlots = 6;
        warrior.AffinityCombo = CombinationType.FullHouse;
        warrior.AffinityDamageBonus = 1.2f;
        warrior.UnlockedByDefault = true;
        CreateOrReplaceAsset(warrior, "Assets/Data/Characters/Warrior.asset");

        // Mage — 2×d6 + 3×d8, budget 8, Straight affinity
        var mage = ScriptableObject.CreateInstance<CharacterData>();
        mage.CharacterName = "Mage";
        mage.ClassName = "Mage";
        mage.Description = "A glass cannon who chases Straights for devastating damage.";
        mage.CharacterColor = HexColor("#ab47bc");
        mage.MaxHP = 80;
        mage.StartingPowerBudget = 8f;
        mage.StartingDice = new[]
        {
            new DiceLoadout { DiceType = d6, Quantity = 2 },
            new DiceLoadout { DiceType = d8, Quantity = 3 }
        };
        mage.CombatDiceSlots = 5;
        mage.AffinityCombo = CombinationType.Straight;
        mage.AffinityDamageBonus = 1.2f;
        mage.UnlockedByDefault = false;
        mage.UnlockDescription = "Unlock by completing a run with the Warrior.";
        CreateOrReplaceAsset(mage, "Assets/Data/Characters/Mage.asset");

        // Rogue — 6×d6, budget 6, Pair affinity
        var rogue = ScriptableObject.CreateInstance<CharacterData>();
        rogue.CharacterName = "Rogue";
        rogue.ClassName = "Rogue";
        rogue.Description = "A swift gambler who squeezes massive value out of humble d6s.";
        rogue.CharacterColor = HexColor("#ff6f00");
        rogue.MaxHP = 90;
        rogue.StartingPowerBudget = 6f;
        rogue.StartingDice = new[]
        {
            new DiceLoadout { DiceType = d6, Quantity = 6 }
        };
        rogue.CombatDiceSlots = 6;
        rogue.AffinityCombo = CombinationType.Pair;
        rogue.AffinityDamageBonus = 1.3f;
        rogue.UnlockedByDefault = false;
        rogue.UnlockDescription = "Unlock by reaching Floor 2 with the Warrior.";
        CreateOrReplaceAsset(rogue, "Assets/Data/Characters/Rogue.asset");
    }

    static void CreateOrReplaceAsset(Object asset, string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(asset, path);
    }

    static void CreateEnemies()
    {
        EnsureFolder("Assets/Data/Enemies");

        CreateEnemy("Goblin",     40, 2, 6, 1, 3, 50f, 15f, EnemyBehavior.Aggressive, false, 0, 0, HexColor("#66bb6a"),  5, 10);
        CreateEnemy("Orc",        60, 2, 8, 1, 2, 40f, 12f, EnemyBehavior.Aggressive, false, 0, 0, HexColor("#ef5350"), 10, 20);
        CreateEnemy("CardArcher", 30, 1, 6, 1, 2, 40f, 10f, EnemyBehavior.Ranged,     true,  3, 80, HexColor("#ffd54f"),  5, 15);
    }

    static void CreateEnemy(string name, int hp, int atkCount, int atkFaces, int spdMin, int spdMax,
        float maxEnergy, float energyPerRound, EnemyBehavior behavior,
        bool isRanged, int preferredRange, int accuracy,
        Color color, int goldDropMin = 5, int goldDropMax = 15)
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
        enemy.IsRanged = isRanged;
        enemy.PreferredRange = preferredRange;
        enemy.Accuracy = accuracy;
        enemy.GoldDropMin = goldDropMin;
        enemy.GoldDropMax = goldDropMax;
        CreateOrReplaceAsset(enemy, $"Assets/Data/Enemies/{name}.asset");
    }

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var color);
        return color;
    }
}
