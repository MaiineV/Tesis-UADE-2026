using System;
using System.Collections.Generic;
using System.Linq;
using Rollgeon.UI.Utility;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.TMPSpriteAtlas
{
    /// <summary>
    /// GUI del generador de atlas de iconos: arrastrar sprites, nombrarlos, empaquetar,
    /// y cablear el resultado a TMP Settings + el <see cref="IconPlaceholderMapSO"/>.
    /// Toda la lógica vive en <see cref="TMPSpriteAtlasBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Portado del proyecto Bot-Game. La diferencia es el <b>nombre editable por fila</b>:
    /// nuestros PNG de UI están importados en <c>SpriteImportMode.Multiple</c>, así que el
    /// sub-sprite se llama <c>Energy_0</c> y ese sería el nombre del glifo. El nombre es lo
    /// que después se escribe a mano en el rich text, así que tiene que poder limpiarse.
    /// </remarks>
    public class TMPSpriteAtlasGeneratorWindow : EditorWindow
    {
        private enum Tab { CreateAtlas, AssignToProject }

        private class SpriteRow
        {
            public Sprite Sprite;
            public string Name;
        }

        private Tab _currentTab = Tab.CreateAtlas;

        // --- Create Atlas tab ---
        private string _atlasName = "RollgeonIcons";
        private readonly List<SpriteRow> _rows = new();
        private int _padding = 2;
        private int _maxAtlasSize = 512;
        private string _outputFolder = "Assets/Art/UI/Icons";
        private string _statusMessage = "";
        private MessageType _statusType = MessageType.Info;
        private Vector2 _spriteListScroll;

        // --- Assign to Project tab ---
        private TMP_SpriteAsset _selectedSpriteAsset;
        private IconPlaceholderMapSO _selectedPlaceholderMap;
        private Vector2 _assignScroll;

        private static readonly int[] AtlasSizes = { 256, 512, 1024, 2048, 4096 };

        [MenuItem("Rollgeon/UI/TMP Sprite Atlas Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<TMPSpriteAtlasGeneratorWindow>();
            window.titleContent = new GUIContent("TMP Sprite Atlas");
            window.minSize = new Vector2(460, 520);
            window.Show();
        }

        private void OnGUI()
        {
            _currentTab = (Tab)GUILayout.Toolbar((int)_currentTab, new[] { "Create Atlas", "Assign to Project" });
            EditorGUILayout.Space(8);

            switch (_currentTab)
            {
                case Tab.CreateAtlas:
                    DrawCreateAtlasTab();
                    break;
                case Tab.AssignToProject:
                    DrawAssignToProjectTab();
                    break;
            }
        }

        #region Create Atlas Tab

        private void DrawCreateAtlasTab()
        {
            _atlasName = EditorGUILayout.TextField("Atlas Name", _atlasName);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Sprites", EditorStyles.boldLabel);
            if (GUILayout.Button("+", GUILayout.Width(25)))
                _rows.Add(new SpriteRow());
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
                _rows.Clear();
            EditorGUILayout.EndHorizontal();

            // El rect completo de la lista es la drop zone — arrastrar sobre cualquier
            // parte del box agrega, no solo sobre el placeholder de "vacío".
            var listRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinHeight(180));

            _spriteListScroll = EditorGUILayout.BeginScrollView(_spriteListScroll,
                GUILayout.MinHeight(140), GUILayout.MaxHeight(300));

            if (_rows.Count == 0)
            {
                EditorGUILayout.Space(40);
                var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 12 };
                EditorGUILayout.LabelField("Drag & Drop Sprites Here", style);
                EditorGUILayout.LabelField("(select multiple in Project and drag them all at once)",
                    EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.Space(40);
            }
            else
            {
                DrawSpriteRows();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            HandleDragAndDrop(listRect);

            EditorGUILayout.Space(8);

            _padding = EditorGUILayout.IntSlider("Padding (px)", _padding, 0, 8);

            int sizeIndex = Array.IndexOf(AtlasSizes, _maxAtlasSize);
            if (sizeIndex < 0) sizeIndex = 1;
            sizeIndex = EditorGUILayout.Popup("Max Atlas Size", sizeIndex,
                AtlasSizes.Select(s => s.ToString()).ToArray());
            _maxAtlasSize = AtlasSizes[sizeIndex];

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Output Folder");
            EditorGUILayout.LabelField(_outputFolder, EditorStyles.textField);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
                BrowseOutputFolder();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(12);

            var inputs = BuildInputs();
            string validationError = TMPSpriteAtlasBuilder.Validate(inputs);

            // "No hay sprites" no es un error que valga mostrar en rojo con la lista vacía —
            // el botón deshabilitado ya lo dice.
            if (validationError != null && inputs.Count > 0)
                EditorGUILayout.HelpBox(validationError, MessageType.Warning);

            GUI.enabled = validationError == null && !string.IsNullOrWhiteSpace(_atlasName);
            if (GUILayout.Button("Generate Atlas & TMP Sprite Asset", GUILayout.Height(30)))
            {
                var result = TMPSpriteAtlasBuilder.Build(
                    _atlasName, inputs, _outputFolder, _padding, _maxAtlasSize);

                _statusMessage = result.Message;
                _statusType = result.Success ? MessageType.Info : MessageType.Error;

                if (result.Success)
                {
                    // Pre-cargar el tab de asignación con lo recién generado: el flujo
                    // siempre sigue con "y ahora enchufalo al proyecto".
                    _selectedSpriteAsset = result.Asset;
                    EditorGUIUtility.PingObject(result.Asset);
                }
            }
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(_statusMessage, _statusType);
            }
        }

        private void DrawSpriteRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                var row = _rows[i];
                var picked = (Sprite)EditorGUILayout.ObjectField(row.Sprite, typeof(Sprite), false,
                    GUILayout.Width(140));
                if (picked != row.Sprite)
                {
                    row.Sprite = picked;
                    row.Name = TMPSpriteAtlasBuilder.DefaultGlyphName(picked);
                }

                row.Name = EditorGUILayout.TextField(row.Name);

                if (row.Sprite != null)
                {
                    var rect = row.Sprite.rect;
                    EditorGUILayout.LabelField($"{(int)rect.width}x{(int)rect.height}", GUILayout.Width(64));
                }

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    _rows.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private List<TMPSpriteAtlasBuilder.SpriteInput> BuildInputs()
            => _rows.Where(r => r.Sprite != null)
                    .Select(r => new TMPSpriteAtlasBuilder.SpriteInput(r.Sprite, r.Name))
                    .ToList();

        private void BrowseOutputFolder()
        {
            string selected = EditorUtility.OpenFolderPanel("Select Output Folder", _outputFolder, "");
            if (string.IsNullOrEmpty(selected)) return;

            if (selected.StartsWith(Application.dataPath))
                _outputFolder = "Assets" + selected.Substring(Application.dataPath.Length);
            else
                EditorUtility.DisplayDialog("Error", "Folder must be inside the Assets directory.", "OK");
        }

        private void HandleDragAndDrop(Rect dropArea)
        {
            var evt = Event.current;
            if (!dropArea.Contains(evt.mousePosition)) return;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.Use();
                    break;

                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is Sprite sprite)
                        {
                            AddSprite(sprite);
                        }
                        else if (obj is Texture2D tex)
                        {
                            // Arrastrar el PNG entero trae todos sus sub-sprites.
                            string path = AssetDatabase.GetAssetPath(tex);
                            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                                AddSprite(sub);
                        }
                    }
                    evt.Use();
                    break;
            }
        }

        private void AddSprite(Sprite sprite)
        {
            if (sprite == null || _rows.Any(r => r.Sprite == sprite)) return;
            _rows.Add(new SpriteRow { Sprite = sprite, Name = TMPSpriteAtlasBuilder.DefaultGlyphName(sprite) });
        }

        #endregion

        #region Assign to Project Tab

        private void DrawAssignToProjectTab()
        {
            _assignScroll = EditorGUILayout.BeginScrollView(_assignScroll);

            EditorGUILayout.LabelField("TMP Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _selectedSpriteAsset = (TMP_SpriteAsset)EditorGUILayout.ObjectField(
                "TMP Sprite Asset", _selectedSpriteAsset, typeof(TMP_SpriteAsset), false);

            var currentDefault = TMP_Settings.defaultSpriteAsset;
            EditorGUILayout.LabelField("Current Default", currentDefault != null ? currentDefault.name : "(none)");

            EditorGUILayout.Space(4);

            GUI.enabled = _selectedSpriteAsset != null;

            if (GUILayout.Button("Set as Default Sprite Asset"))
                PromptSetAsDefault(_selectedSpriteAsset);

            if (GUILayout.Button("Add as Fallback Sprite Asset"))
                TMPSpriteAtlasBuilder.AddAsFallbackSpriteAsset(_selectedSpriteAsset);

            GUI.enabled = true;

            EditorGUILayout.Space(12);

            DrawFallbackList();

            EditorGUILayout.Space(16);
            DrawSeparator();
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Icon Placeholder Map", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _selectedPlaceholderMap = (IconPlaceholderMapSO)EditorGUILayout.ObjectField(
                "Placeholder Map", _selectedPlaceholderMap, typeof(IconPlaceholderMapSO), false);

            EditorGUILayout.Space(4);

            GUI.enabled = _selectedPlaceholderMap != null;
            if (GUILayout.Button("Rebuild Mappings from TMP Settings"))
                TMPSpriteAtlasBuilder.RebuildMappingsFromSettings(_selectedPlaceholderMap);
            GUI.enabled = true;

            EditorGUILayout.HelpBox(
                "Clears all mappings and regenerates from Default + Fallback sprite assets configured in TMP Settings.",
                MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        private static void PromptSetAsDefault(TMP_SpriteAsset spriteAsset)
        {
            // Reemplazar el default sin más perdería los glifos del anterior (ej. EmojiOne)
            // para todo el proyecto; se ofrece degradarlo a fallback en vez de tirarlo.
            var currentDefault = TMP_Settings.defaultSpriteAsset;
            bool demote = currentDefault != null
                          && currentDefault != spriteAsset
                          && EditorUtility.DisplayDialog(
                              "Add Fallback?",
                              $"The current default '{currentDefault.name}' will be replaced.\n" +
                              "Add it as a fallback sprite asset?",
                              "Yes", "No");

            TMPSpriteAtlasBuilder.SetAsDefaultSpriteAsset(spriteAsset, demote);
        }

        private static void DrawFallbackList()
        {
            var defaultAsset = TMP_Settings.defaultSpriteAsset;
            if (defaultAsset == null)
            {
                EditorGUILayout.LabelField("Fallback Sprite Assets", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("(no default sprite asset set)", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            var so = new SerializedObject(defaultAsset);
            var fallbackProp = so.FindProperty("fallbackSpriteAssets");

            EditorGUILayout.LabelField($"Fallbacks on '{defaultAsset.name}'", EditorStyles.boldLabel);

            if (fallbackProp.arraySize == 0)
            {
                EditorGUILayout.LabelField("(none)", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            int removeIndex = -1;
            for (int i = 0; i < fallbackProp.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();

                var asset = fallbackProp.GetArrayElementAtIndex(i).objectReferenceValue as TMP_SpriteAsset;
                EditorGUILayout.LabelField($"  {i + 1}. {(asset != null ? asset.name : "(missing)")}");

                if (GUILayout.Button("X", GUILayout.Width(20)))
                    removeIndex = i;

                EditorGUILayout.EndHorizontal();
            }

            if (removeIndex >= 0)
            {
                // Nulear antes de borrar: en arrays de object reference el primer
                // DeleteArrayElementAtIndex solo pone null, no achica el array.
                fallbackProp.GetArrayElementAtIndex(removeIndex).objectReferenceValue = null;
                fallbackProp.DeleteArrayElementAtIndex(removeIndex);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(defaultAsset);
                AssetDatabase.SaveAssets();
            }
        }

        private static void DrawSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        }

        #endregion
    }
}
