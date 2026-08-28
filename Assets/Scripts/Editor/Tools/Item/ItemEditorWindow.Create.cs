using System.Collections.Generic;
using Rollgeon.Items;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// Fase 3 creation wizard (docs/tools/item-editor-spec.md §6.2, §3 rule 4). Intercepts the
    /// generic <c>BlockEditorWindow&lt;ItemSO&gt;</c> Create/Duplicate buttons so a new item always
    /// gets a real Display Name and a derived, collision-checked id instead of an empty
    /// <c>Item_New</c> stub or a copy that shares its source's id.
    /// </summary>
    public sealed partial class ItemEditorWindow
    {
        // ---- BlockEditorWindow<ItemSO> hooks -------------------------------------------------------

        protected override bool TryBeginCreate()
        {
            ItemCreationWizard.Open(this, null);
            return true;
        }

        /// <summary>
        /// Splits Duplicate per spec §3 rule 4: a family member offers "add variant" (the 90% case,
        /// derives id + structure, asks only for what changes) alongside "duplicate as new item"
        /// (opens the same wizard as Create, precargado, empty name required). A loose item has no
        /// family to add a variant to, so it skips straight to the wizard.
        /// </summary>
        protected override bool TryBeginDuplicate(ItemSO source)
        {
            if (source == null) return false;

            if (string.IsNullOrEmpty(source.FamilyId))
            {
                ItemCreationWizard.Open(this, source);
                return true;
            }

            int choice = EditorUtility.DisplayDialogComplex(
                "Duplicate Item",
                $"'{LabelOf(source)}' is part of family '{source.FamilyId}'.\n\n" +
                "\"Add variant to family\" derives the id and copies the structure — you only fill " +
                "in what changes. That's the common case.\n\n" +
                "\"Duplicate as new item\" opens the creation wizard precargado with this item's " +
                "data, starting as an unrelated item with an empty name.",
                "Add variant to family",
                "Cancel",
                "Duplicate as new item");

            switch (choice)
            {
                case 0: ItemAddVariantWizard.Open(this, source); break;
                case 2: ItemCreationWizard.Open(this, source); break;
                // 1 = Cancel — do nothing.
            }
            return true;
        }

        // ---- wizard callbacks -----------------------------------------------------------------------

        /// <summary>Hands control back to the shell once <see cref="ItemAuthoring.CreateItem"/> succeeded.</summary>
        internal void OnWizardItemCreated(ItemSO item)
        {
            RefreshAndSelect(item);
            Focus();
            LogUndoCaveat();
        }

        /// <summary>Same as <see cref="OnWizardItemCreated"/> for a family batch — selects the first variant.</summary>
        internal void OnWizardFamilyCreated(IReadOnlyList<ItemSO> items)
        {
            if (items == null || items.Count == 0) return;
            RefreshAndSelect(items[0]);
            Focus();
            LogUndoCaveat();
        }

        /// <summary>
        /// Spec §7.1 — measured limit of the create undo group: Ctrl+Z reverts the catalog entry,
        /// the ShopPool price and the ES/EN localization keys, but Unity never puts
        /// <c>AssetDatabase.CreateAsset</c> on the undo stack, so the <c>.asset</c> file itself
        /// survives. An undone create leaves an orphaned asset on disk — this is a heads-up, not
        /// something the tool can fix by forcing a delete (that would fight the user's own Ctrl+Z).
        /// </summary>
        static void LogUndoCaveat() =>
            Debug.Log(
                "[Item Editor] Created. Heads up: Ctrl+Z undoes the catalog/price/localization " +
                "writes but does NOT delete the .asset file (Unity limitation, spec §7.1) — undoing " +
                "a create leaves an orphaned asset you'll need to delete by hand.");
    }

    // ==================================================================================================
    // shared UI helper
    // ==================================================================================================

    /// <summary>
    /// Live id feedback (spec §6.2 item 3): while a Display Name is typed, shows the id it will
    /// derive to and flags instantly if it's already taken, naming the owner. Shared by both
    /// wizards below so the two forms give identical feedback.
    /// </summary>
    static class ItemIdPreview
    {
        public static void Draw(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                EditorGUILayout.HelpBox("Type a Display Name to see the id it will get.", MessageType.None);
                return;
            }

            var id = ItemIdSlug.FromDisplayName(displayName);
            if (string.IsNullOrEmpty(id))
            {
                EditorGUILayout.HelpBox(
                    "This Display Name doesn't derive a usable id (only separators/symbols).",
                    MessageType.Error);
                return;
            }

            if (ItemAuthoring.IsIdAvailable(id, out var owner))
            {
                EditorGUILayout.HelpBox($"Id: {id}", MessageType.Info);
                return;
            }

            var ownerLabel = owner != null
                ? (string.IsNullOrEmpty(owner.DisplayName) ? owner.name : owner.DisplayName)
                : "<unknown>";
            EditorGUILayout.HelpBox($"Id '{id}' is already used by '{ownerLabel}'.", MessageType.Error);
        }
    }

    // ==================================================================================================
    // "+ Create" / "Duplicate as new item" wizard
    // ==================================================================================================

    /// <summary>
    /// Asks for Display Name, description, icon, rarity and type, and whether the result is a loose
    /// item or a family of variants (spec §6.2). Confirm calls <see cref="ItemAuthoring"/> once — the
    /// four writes land in the service's single undo group, this window never touches assets itself.
    /// </summary>
    sealed class ItemCreationWizard : EditorWindow
    {
        enum Mode { Single, Family }

        sealed class VariantRow
        {
            public string DisplayName = string.Empty;
            public ItemRarity Rarity;
            public bool OverrideDescription;
            public string Description = string.Empty;
            public bool OverrideIcon;
            public Sprite Icon;
            public bool OverrideBasePrice;
            public int BasePrice;
        }

        ItemEditorWindow _owner;
        Mode _mode = Mode.Single;

        // single-item fields
        string _displayName = string.Empty;
        string _description = string.Empty;
        Sprite _icon;
        ItemRarity _rarity;
        ItemType _type;
        bool _overrideBasePrice;
        int _basePrice;
        string _targetFolder;

        // family fields
        string _familyId = string.Empty;
        ItemType _familyType;
        string _familyDescription = string.Empty;
        Sprite _familyIcon;
        string _familyTargetFolder;
        readonly List<VariantRow> _variants = new List<VariantRow>();

        List<string> _errors;
        Vector2 _scroll;

        /// <param name="prefillFrom">
        /// Null for a fresh "+ Create". Non-null for "Duplicate as new item" (spec §3 rule 4) —
        /// copies description/icon/rarity/type as a starting point but leaves Display Name empty,
        /// since a duplicate can't be confirmed without a new name.
        /// </param>
        public static void Open(ItemEditorWindow owner, ItemSO prefillFrom)
        {
            var w = CreateInstance<ItemCreationWizard>();
            w.titleContent = new GUIContent(prefillFrom == null ? "New Item" : "Duplicate → New Item");
            w._owner = owner;
            w._targetFolder = ItemAuthoring.DefaultFolder;
            w._familyTargetFolder = ItemAuthoring.DefaultFolder;

            if (prefillFrom != null)
            {
                w._description = prefillFrom.Description;
                w._icon = prefillFrom.Icon;
                w._rarity = prefillFrom.Rarity;
                w._type = prefillFrom.Type;
                w._familyDescription = prefillFrom.Description;
                w._familyIcon = prefillFrom.Icon;
                w._familyType = prefillFrom.Type;
            }

            w._variants.Add(new VariantRow { Rarity = w._rarity });
            w.minSize = new Vector2(440f, 420f);
            w.ShowUtility();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(4);
            var newMode = (Mode)GUILayout.Toolbar((int)_mode, new[] { "Ítem suelto", "Familia de variantes" });
            if (newMode != _mode)
            {
                _mode = newMode;
                _errors = null;
            }
            EditorGUILayout.Space(6);

            if (_mode == Mode.Single) DrawSingle();
            else DrawFamily();

            DrawErrors();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);
            DrawFooter();
        }

        void DrawSingle()
        {
            _displayName = EditorGUILayout.TextField("Display Name", _displayName);
            ItemIdPreview.Draw(_displayName);

            EditorGUILayout.LabelField("Description");
            _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(48f));

            _icon = (Sprite)EditorGUILayout.ObjectField("Icon", _icon, typeof(Sprite), false);
            _rarity = (ItemRarity)EditorGUILayout.EnumPopup("Rarity", _rarity);
            _type = (ItemType)EditorGUILayout.EnumPopup("Type", _type);

            DrawBasePriceOverride(ref _overrideBasePrice, ref _basePrice, _rarity);
            DrawFolderField("Target Folder", ref _targetFolder);
        }

        void DrawFamily()
        {
            _familyId = EditorGUILayout.TextField("Family Id", _familyId);
            _familyType = (ItemType)EditorGUILayout.EnumPopup("Type", _familyType);

            EditorGUILayout.LabelField("Default Description");
            _familyDescription = EditorGUILayout.TextArea(_familyDescription, GUILayout.MinHeight(36f));
            _familyIcon = (Sprite)EditorGUILayout.ObjectField("Default Icon", _familyIcon, typeof(Sprite), false);
            DrawFolderField("Target Folder", ref _familyTargetFolder);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Variants", EditorStyles.boldLabel);

            VariantRow toRemove = null;
            for (int i = 0; i < _variants.Count; i++)
            {
                var row = _variants[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Variant {i}", EditorStyles.miniBoldLabel);
                        GUILayout.FlexibleSpace();
                        if (_variants.Count > 1 && GUILayout.Button("Remove", GUILayout.Width(64f)))
                            toRemove = row;
                    }

                    row.DisplayName = EditorGUILayout.TextField("Display Name", row.DisplayName);
                    ItemIdPreview.Draw(row.DisplayName);
                    row.Rarity = (ItemRarity)EditorGUILayout.EnumPopup("Rarity", row.Rarity);

                    row.OverrideDescription = EditorGUILayout.ToggleLeft("Override description", row.OverrideDescription);
                    if (row.OverrideDescription)
                        row.Description = EditorGUILayout.TextArea(row.Description, GUILayout.MinHeight(32f));

                    row.OverrideIcon = EditorGUILayout.ToggleLeft("Override icon", row.OverrideIcon);
                    if (row.OverrideIcon)
                        row.Icon = (Sprite)EditorGUILayout.ObjectField(row.Icon, typeof(Sprite), false);

                    DrawBasePriceOverride(ref row.OverrideBasePrice, ref row.BasePrice, row.Rarity);
                }
            }
            if (toRemove != null) _variants.Remove(toRemove);

            if (GUILayout.Button("+ Add variant")) _variants.Add(new VariantRow());
        }

        static void DrawBasePriceOverride(ref bool overrideFlag, ref int price, ItemRarity rarity)
        {
            var defaultPrice = RarityPricing.BasePriceFor(rarity);
            overrideFlag = EditorGUILayout.ToggleLeft($"Override base price (default: {defaultPrice})", overrideFlag);
            using (new EditorGUI.DisabledScope(!overrideFlag))
                price = EditorGUILayout.IntField("Base Price", overrideFlag ? price : defaultPrice);
        }

        static void DrawFolderField(string label, ref string folder)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                folder = EditorGUILayout.TextField(label, folder);
                if (GUILayout.Button("...", GUILayout.Width(28f)))
                {
                    var picked = EditorUtility.OpenFolderPanel("Target Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(picked) && picked.StartsWith(Application.dataPath))
                        folder = "Assets" + picked.Substring(Application.dataPath.Length);
                }
            }
        }

        void DrawErrors()
        {
            if (_errors == null) return;
            foreach (var e in _errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        }

        void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(24f))) Close();

                bool canConfirm = _mode == Mode.Single
                    ? !string.IsNullOrWhiteSpace(_displayName)
                    : !string.IsNullOrWhiteSpace(_familyId) && _variants.Count > 0 &&
                      _variants.TrueForAll(v => !string.IsNullOrWhiteSpace(v.DisplayName));

                using (new EditorGUI.DisabledScope(!canConfirm))
                {
                    var label = _mode == Mode.Single ? "Create" : $"Create family ({_variants.Count})";
                    if (GUILayout.Button(label, GUILayout.Height(24f))) Confirm();
                }
            }
        }

        /// <remarks>
        /// Builds the spec and hands it to <see cref="ItemAuthoring"/> — no validation duplicated
        /// here beyond what gates the button (empty names): the service is the single source of
        /// truth for id derivation, uniqueness and folder checks, and returns <c>Errors</c> for all
        /// of it.
        /// </remarks>
        void Confirm()
        {
            _errors = null;

            if (_mode == Mode.Single)
            {
                var spec = new ItemCreationSpec
                {
                    DisplayName = _displayName,
                    Description = _description,
                    Icon = _icon,
                    Rarity = _rarity,
                    Type = _type,
                    BasePrice = _overrideBasePrice ? _basePrice : (int?)null,
                    TargetFolder = string.IsNullOrWhiteSpace(_targetFolder) ? null : _targetFolder,
                };

                var result = ItemAuthoring.CreateItem(spec);
                if (!result.Success)
                {
                    _errors = new List<string>(result.Errors);
                    return;
                }

                _owner.OnWizardItemCreated(result.Item);
                Close();
            }
            else
            {
                var variants = new List<ItemFamilyVariantSpec>(_variants.Count);
                foreach (var row in _variants)
                {
                    variants.Add(new ItemFamilyVariantSpec
                    {
                        DisplayName = row.DisplayName,
                        Description = row.OverrideDescription ? row.Description : null,
                        Icon = row.OverrideIcon ? row.Icon : null,
                        Rarity = row.Rarity,
                        BasePrice = row.OverrideBasePrice ? row.BasePrice : (int?)null,
                    });
                }

                var spec = new ItemFamilyCreationSpec
                {
                    FamilyId = _familyId,
                    Type = _familyType,
                    DefaultDescription = _familyDescription,
                    DefaultIcon = _familyIcon,
                    TargetFolder = string.IsNullOrWhiteSpace(_familyTargetFolder) ? null : _familyTargetFolder,
                    Variants = variants,
                };

                var result = ItemAuthoring.CreateFamily(spec);
                if (!result.Success)
                {
                    _errors = new List<string>(result.Errors);
                    return;
                }

                _owner.OnWizardFamilyCreated(result.Items);
                Close();
            }
        }
    }

    // ==================================================================================================
    // "Add variant to family" wizard — spec §3 rule 4, the 90% case
    // ==================================================================================================

    /// <summary>
    /// Lighter form than <see cref="ItemCreationWizard"/>: family, type and folder are fixed to
    /// <paramref name="_source"/>'s, only Display Name is required, everything else defaults to the
    /// source's values with an explicit override toggle per field.
    /// </summary>
    sealed class ItemAddVariantWizard : EditorWindow
    {
        ItemEditorWindow _owner;
        ItemSO _source;
        List<ItemSO> _familyMembers;

        string _displayName = string.Empty;
        ItemRarity _rarity;
        bool _overrideDescription;
        string _description = string.Empty;
        bool _overrideIcon;
        Sprite _icon;
        bool _overrideBasePrice;
        int _basePrice;

        List<string> _errors;

        public static void Open(ItemEditorWindow owner, ItemSO source)
        {
            var w = CreateInstance<ItemAddVariantWizard>();
            w.titleContent = new GUIContent($"Add variant → {source.FamilyId}");
            w._owner = owner;
            w._source = source;
            w._rarity = source.Rarity;
            w._familyMembers = FindFamilyMembers(source.FamilyId);
            w.minSize = new Vector2(400f, 380f);
            w.ShowUtility();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Family", _source.FamilyId, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Type", _source.Type.ToString());

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Existing variants", EditorStyles.miniBoldLabel);
            foreach (var member in _familyMembers)
                EditorGUILayout.LabelField($"  {member.VariantIndex} · {member.DisplayName}", EditorStyles.miniLabel);

            EditorGUILayout.Space(8);
            _displayName = EditorGUILayout.TextField("Display Name", _displayName);
            ItemIdPreview.Draw(_displayName);

            _rarity = (ItemRarity)EditorGUILayout.EnumPopup("Rarity", _rarity);

            _overrideDescription = EditorGUILayout.ToggleLeft("Override description", _overrideDescription);
            if (_overrideDescription)
                _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(36f));
            else
                EditorGUILayout.HelpBox(
                    string.IsNullOrEmpty(_source.Description)
                        ? "(inherits the family's default description)"
                        : _source.Description,
                    MessageType.None);

            _overrideIcon = EditorGUILayout.ToggleLeft("Override icon", _overrideIcon);
            if (_overrideIcon)
                _icon = (Sprite)EditorGUILayout.ObjectField(_icon, typeof(Sprite), false);

            var defaultPrice = RarityPricing.BasePriceFor(_rarity);
            _overrideBasePrice = EditorGUILayout.ToggleLeft($"Override base price (default: {defaultPrice})", _overrideBasePrice);
            using (new EditorGUI.DisabledScope(!_overrideBasePrice))
                _basePrice = EditorGUILayout.IntField("Base Price", _overrideBasePrice ? _basePrice : defaultPrice);

            if (_errors != null)
                foreach (var e in _errors) EditorGUILayout.HelpBox(e, MessageType.Error);

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(24f))) Close();

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_displayName)))
                    if (GUILayout.Button("Add variant", GUILayout.Height(24f))) Confirm();
            }
        }

        /// <remarks>
        /// Routes through <see cref="ItemAuthoring.CreateFamily"/> with a single variant rather than
        /// a dedicated "add one" entry point — a batch of one behaves identically (same validation,
        /// same one-undo-step write) and keeps <see cref="ItemAuthoring"/>'s surface at two methods
        /// instead of three.
        /// </remarks>
        void Confirm()
        {
            _errors = null;

            int nextIndex = 0;
            foreach (var m in _familyMembers)
                if (m.VariantIndex >= nextIndex) nextIndex = m.VariantIndex + 1;

            var folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(_source))?.Replace('\\', '/');

            var spec = new ItemFamilyCreationSpec
            {
                FamilyId = _source.FamilyId,
                Type = _source.Type,
                DefaultDescription = _source.Description,
                DefaultIcon = _source.Icon,
                TargetFolder = string.IsNullOrEmpty(folder) ? null : folder,
                Variants = new List<ItemFamilyVariantSpec>
                {
                    new ItemFamilyVariantSpec
                    {
                        DisplayName = _displayName,
                        Description = _overrideDescription ? _description : null,
                        Icon = _overrideIcon ? _icon : null,
                        Rarity = _rarity,
                        BasePrice = _overrideBasePrice ? _basePrice : (int?)null,
                        VariantIndex = nextIndex,
                    },
                },
            };

            var result = ItemAuthoring.CreateFamily(spec);
            if (!result.Success)
            {
                _errors = new List<string>(result.Errors);
                return;
            }

            _owner.OnWizardFamilyCreated(result.Items);
            Close();
        }

        static List<ItemSO> FindFamilyMembers(string familyId)
        {
            var list = new List<ItemSO>();
            if (string.IsNullOrEmpty(familyId)) return list;

            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(ItemSO)))
            {
                var so = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so != null && so.FamilyId == familyId) list.Add(so);
            }
            list.Sort((a, b) => a.VariantIndex.CompareTo(b.VariantIndex));
            return list;
        }
    }
}
