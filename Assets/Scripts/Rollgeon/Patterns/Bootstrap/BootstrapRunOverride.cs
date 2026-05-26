using System;
using System.Collections.Generic;
using Rollgeon.Balance;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Items;
using UnityEngine;

namespace Rollgeon.Patterns.Bootstrap
{
    public static class BootstrapRunOverride
    {
        public static bool HasOverride { get; private set; }
        public static string TargetScene { get; private set; }
        public static ClassHeroSO Hero { get; private set; }
        public static DiceBagSO DiceBag { get; private set; }
        public static RulesetSO Ruleset { get; private set; }
        public static IReadOnlyList<ItemSO> StartingItems { get; private set; }

        public static void Set(
            string targetScene,
            ClassHeroSO hero,
            DiceBagSO diceBag,
            RulesetSO ruleset,
            IReadOnlyList<ItemSO> startingItems)
        {
            TargetScene = targetScene;
            Hero = hero;
            DiceBag = diceBag;
            Ruleset = ruleset;
            StartingItems = startingItems;
            HasOverride = true;
        }

        public static void Consume()
        {
            HasOverride = false;
            TargetScene = null;
            Hero = null;
            DiceBag = null;
            Ruleset = null;
            StartingItems = null;
#if UNITY_EDITOR
            UnityEditor.SessionState.EraseString(SessionKey);
#endif
        }

#if UNITY_EDITOR
        private const string SessionKey = "Rollgeon.SceneSwitcher.OverridePayload";

        [Serializable]
        private class SerializedPayload
        {
            public string TargetScene;
            public string HeroGuid;
            public string DiceBagGuid;
            public string RulesetGuid;
            public List<string> ItemGuids;
        }

        public static void StashForPlayMode(
            string targetScene,
            string heroGuid,
            string diceBagGuid,
            string rulesetGuid,
            IList<string> itemGuids)
        {
            var payload = new SerializedPayload
            {
                TargetScene = targetScene,
                HeroGuid = heroGuid,
                DiceBagGuid = diceBagGuid,
                RulesetGuid = rulesetGuid,
                ItemGuids = itemGuids != null ? new List<string>(itemGuids) : new List<string>(),
            };
            UnityEditor.SessionState.SetString(SessionKey, JsonUtility.ToJson(payload));
        }

        [UnityEditor.InitializeOnEnterPlayMode]
        private static void RestoreFromSessionState()
        {
            var json = UnityEditor.SessionState.GetString(SessionKey, null);
            if (string.IsNullOrEmpty(json)) return;

            var payload = JsonUtility.FromJson<SerializedPayload>(json);
            if (payload == null) return;

            var hero = LoadAsset<ClassHeroSO>(payload.HeroGuid);
            if (hero == null)
            {
                Debug.LogError("[BootstrapRunOverride] Hero asset missing for stashed override — skipping.");
                UnityEditor.SessionState.EraseString(SessionKey);
                return;
            }

            var items = new List<ItemSO>();
            if (payload.ItemGuids != null)
            {
                foreach (var g in payload.ItemGuids)
                {
                    var item = LoadAsset<ItemSO>(g);
                    if (item != null) items.Add(item);
                }
            }

            Set(
                payload.TargetScene,
                hero,
                LoadAsset<DiceBagSO>(payload.DiceBagGuid),
                LoadAsset<RulesetSO>(payload.RulesetGuid),
                items);
        }

        private static T LoadAsset<T>(string guid) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(guid)) return null;
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        }
#endif
    }
}
