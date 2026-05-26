using System;
using System.Collections.Generic;
using System.IO;
using Rollgeon.Balance;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Items;
using Rollgeon.Patterns.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rollgeon.Editor.Tools
{
    public sealed class SceneSwitcherWindow : EditorWindow
    {
        private const string BootstrapScenePath = "Assets/Scenes/00_Bootstrap.unity";

        private Vector2 _scroll;

        [MenuItem("Tools/Scene Switcher %#l")]
        public static void Open()
        {
            var window = GetWindow<SceneSwitcherWindow>(utility: false, title: "Scene Switcher");
            window.minSize = new Vector2(280f, 240f);
            window.Show();
        }

        [InitializeOnLoadMethod]
        private static void RegisterPlayModeHook()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                BootstrapRunOverride.Consume();
                EditorSceneManager.playModeStartScene = null;
            }
        }

        private void OnEnable()
        {
            EditorBuildSettings.sceneListChanged += Repaint;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        }

        private void OnDisable()
        {
            EditorBuildSettings.sceneListChanged -= Repaint;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        }

        private void OnActiveSceneChanged(Scene previous, Scene current) => Repaint();

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawSwitchSection();
            EditorGUILayout.Space(10);
            DrawPlayConfigSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSwitchSection()
        {
            EditorGUILayout.LabelField("Switch Scene", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                var scenes = EditorBuildSettings.scenes;
                if (scenes.Length == 0)
                {
                    EditorGUILayout.HelpBox("No scenes registered in Build Settings.", MessageType.Info);
                    return;
                }

                string activeName = EditorSceneManager.GetActiveScene().name;

                foreach (var scene in scenes)
                {
                    if (string.IsNullOrEmpty(scene.path) || !scene.path.StartsWith("Assets"))
                        continue;

                    string name = Path.GetFileNameWithoutExtension(scene.path);
                    bool isActive = name == activeName;

                    using (new EditorGUI.DisabledScope(isActive))
                    {
                        if (GUILayout.Button(name, GUILayout.Height(22f)))
                        {
                            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
                        }
                    }
                }
            }
        }

        private void DrawPlayConfigSection()
        {
            var s = SceneSwitcherSettings.instance;

            EditorGUILayout.LabelField("Play With Config", EditorStyles.boldLabel);

            bool toggle = EditorGUILayout.Toggle("Enabled", s.PlayWithCustomConfig);
            if (toggle != s.PlayWithCustomConfig) s.PlayWithCustomConfig = toggle;
            if (!toggle) return;

            EditorGUI.BeginChangeCheck();

            DrawAssetField<ClassHeroSO>("Hero", s.HeroGuid, g => s.HeroGuid = g);
            DrawAssetField<DiceBagSO>("Dice Bag", s.DiceBagGuid, g => s.DiceBagGuid = g);
            DrawAssetField<RulesetSO>("Ruleset", s.RulesetGuid, g => s.RulesetGuid = g);

            DrawTargetSceneDropdown(s);

            EditorGUILayout.Space(4);
            DrawStartingItemsList(s);

            if (EditorGUI.EndChangeCheck())
                s.SaveDirty();

            EditorGUILayout.Space(8);

            string blockReason = ResolvePlayBlockReason(s);
            using (new EditorGUI.DisabledScope(blockReason != null))
            {
                if (GUILayout.Button("Play With Config", GUILayout.Height(28f)))
                    StartPlayWithConfig(s);
            }
            if (blockReason != null)
                EditorGUILayout.HelpBox(blockReason, MessageType.Info);
        }

        private static string ResolvePlayBlockReason(SceneSwitcherSettings s)
        {
            if (Application.isPlaying) return "Already in Play mode.";
            if (string.IsNullOrEmpty(s.HeroGuid)) return "Pick a hero.";
            if (string.IsNullOrEmpty(s.TargetSceneName)) return "Pick a target scene.";
            return null;
        }

        private static void DrawAssetField<T>(string label, string guid, Action<string> setGuid) where T : UnityEngine.Object
        {
            var current = LoadFromGuid<T>(guid);
            var picked = (T)EditorGUILayout.ObjectField(label, current, typeof(T), false);
            if (picked != current)
                setGuid(GuidFor(picked));
        }

        private static string GuidFor(UnityEngine.Object asset)
        {
            if (asset == null) return string.Empty;
            var path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        private static T LoadFromGuid<T>(string guid) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(guid)) return null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void DrawTargetSceneDropdown(SceneSwitcherSettings s)
        {
            var scenes = EditorBuildSettings.scenes;
            var names = new List<string>();
            foreach (var sc in scenes)
            {
                if (!string.IsNullOrEmpty(sc.path) && sc.path.StartsWith("Assets"))
                    names.Add(Path.GetFileNameWithoutExtension(sc.path));
            }

            if (names.Count == 0)
            {
                EditorGUILayout.HelpBox("No scenes in Build Settings.", MessageType.Info);
                return;
            }

            int currentIdx = names.IndexOf(s.TargetSceneName);
            if (currentIdx < 0) currentIdx = 0;
            int pickedIdx = EditorGUILayout.Popup("Target Scene", currentIdx, names.ToArray());
            string pickedName = names[pickedIdx];
            if (pickedName != s.TargetSceneName) s.TargetSceneName = pickedName;
        }

        private static void DrawStartingItemsList(SceneSwitcherSettings s)
        {
            EditorGUILayout.LabelField("Starting Items", EditorStyles.boldLabel);
            var guids = s.StartingItemGuids;

            int removeAt = -1;
            for (int i = 0; i < guids.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                var current = LoadFromGuid<ItemSO>(guids[i]);
                var picked = (ItemSO)EditorGUILayout.ObjectField(current, typeof(ItemSO), false);
                if (picked != current) guids[i] = GuidFor(picked);
                if (GUILayout.Button("X", GUILayout.Width(22f))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }

            if (removeAt >= 0) guids.RemoveAt(removeAt);
            if (GUILayout.Button("+ Add Item")) guids.Add(string.Empty);
        }

        private static void StartPlayWithConfig(SceneSwitcherSettings s)
        {
            if (LoadFromGuid<ClassHeroSO>(s.HeroGuid) == null)
            {
                Debug.LogError("[SceneSwitcher] Hero asset not found — pick a valid ClassHeroSO.");
                return;
            }

            var bootstrapAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);
            if (bootstrapAsset == null)
            {
                Debug.LogError($"[SceneSwitcher] Bootstrap scene not found at {BootstrapScenePath}.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            BootstrapRunOverride.StashForPlayMode(
                s.TargetSceneName,
                s.HeroGuid,
                s.DiceBagGuid,
                s.RulesetGuid,
                s.StartingItemGuids);

            EditorSceneManager.playModeStartScene = bootstrapAsset;
            EditorApplication.isPlaying = true;
        }
    }
}
