using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools
{
    [FilePath("UserSettings/Rollgeon/SceneSwitcher.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class SceneSwitcherSettings : ScriptableSingleton<SceneSwitcherSettings>
    {
        [SerializeField] private bool _playWithCustomConfig;
        [SerializeField] private string _targetSceneName = "02_Gameplay";
        [SerializeField] private string _heroGuid;
        [SerializeField] private string _diceBagGuid;
        [SerializeField] private string _rulesetGuid;
        [SerializeField] private List<string> _startingItemGuids = new List<string>();

        public bool PlayWithCustomConfig
        {
            get => _playWithCustomConfig;
            set { _playWithCustomConfig = value; SaveDirty(); }
        }

        public string TargetSceneName
        {
            get => _targetSceneName;
            set { _targetSceneName = value; SaveDirty(); }
        }

        public string HeroGuid
        {
            get => _heroGuid;
            set { _heroGuid = value; SaveDirty(); }
        }

        public string DiceBagGuid
        {
            get => _diceBagGuid;
            set { _diceBagGuid = value; SaveDirty(); }
        }

        public string RulesetGuid
        {
            get => _rulesetGuid;
            set { _rulesetGuid = value; SaveDirty(); }
        }

        public List<string> StartingItemGuids => _startingItemGuids;

        public void SaveDirty() => Save(true);
    }
}
