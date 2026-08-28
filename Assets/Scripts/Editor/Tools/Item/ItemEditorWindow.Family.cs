using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.Effects;
using Rollgeon.Items;
using Sirenix.Serialization;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// "Family" tab (item-editor-spec.md §6.3): the tiers of a family side by side as an editable
    /// table — one column per variant, one row per value — so authoring the GDD's Botas
    /// Ligeras → Botas del Viento → Botas del Rayo → Alas de Hermes chain never means opening four
    /// separate assets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One <see cref="PolymorphicAuthoringContext"/> per variant.</b> The shell's <c>Context</c>
    /// (<c>BlockEditorWindow&lt;T&gt;.Context</c>) is bound to the single selected asset — this tab
    /// edits every variant in the family at once, so it owns its own pool of contexts, one per
    /// <see cref="ItemSO"/> column, created lazily and pruned whenever the visible family changes.
    /// </para>
    /// <para>
    /// <b>"Propagate down" copies structure, not numbers.</b> The button clones the lowest-
    /// <c>VariantIndex</c> variant's <c>PassiveHooks</c>/<c>OnActivate</c> subtree (hook kind, event
    /// filters, the concrete <c>IEffect</c>/<c>BasePreCondition</c> types and their list shape) via
    /// <c>SerializationUtility.CreateCopy</c>, then walks the clone alongside each target's previous
    /// value and writes back every numeric leaf (int/float/…) the target already had at the same
    /// position — see <see cref="MergeNumericLeaves"/>. A leaf that is new (an effect the target
    /// didn't have before) keeps the source's number as a starting point; that is the expected
    /// outcome of a structural change, not a bug.
    /// </para>
    /// <para>
    /// <b>Structural edits are deferred to the end of the IMGUI pass</b> (<see cref="_familyPendingAction"/>).
    /// Add/Remove Hook and both Propagate buttons reassign a <c>List&lt;&gt;</c> or a whole
    /// <c>EffectData</c> — doing that mid-frame, before every later <c>ctx.Draw</c> call in the same
    /// column has run, would desync Odin's control count for the rest of that repaint (the failure
    /// mode <c>PolymorphicBlockDrawer.DrawPolymorphicListItems</c> avoids by returning early). Running
    /// the mutation strictly after the whole tab has finished drawing sidesteps that without needing
    /// to unwind out of nested <c>using</c> layout scopes.
    /// </para>
    /// </remarks>
    public sealed partial class ItemEditorWindow
    {
        const float FAMILY_COLUMN_WIDTH = 280f;

        string _familySelectedId;
        ItemSO _familyLastFollowed;
        Vector2 _familyScroll;
        Action _familyPendingAction;
        readonly Dictionary<ItemSO, PolymorphicAuthoringContext> _familyContexts =
            new Dictionary<ItemSO, PolymorphicAuthoringContext>();

        [BlockEditorTab("Family", 10)]
        void DrawFamilyTab()
        {
            SyncFollowedSelection();

            var families = ItemQuery.GetFamilies();
            if (families.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No families yet — set FamilyId on an item to group it with its tiers " +
                    "(spec §6.3). Loose items (empty FamilyId) don't show up here.",
                    MessageType.Info);
                return;
            }

            if (string.IsNullOrEmpty(_familySelectedId) || families.All(f => f.FamilyId != _familySelectedId))
                _familySelectedId = families[0].FamilyId;

            var family = families.First(f => f.FamilyId == _familySelectedId);
            PruneFamilyContexts(family.Variants);

            DrawFamilyPicker(families);

            if (family.Variants.Count < 2)
                EditorGUILayout.HelpBox(
                    "This family has a single variant — give another item the same FamilyId to " +
                    "compare them here.", MessageType.Info);

            EditorGUILayout.Space(4);
            DrawFamilyPropagateToolbar(family);
            EditorGUILayout.Space(4);

            _familyScroll = EditorGUILayout.BeginScrollView(_familyScroll);
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var variant in family.Variants)
                    if (variant != null)
                        DrawFamilyColumn(variant);
            }
            EditorGUILayout.EndScrollView();

            // Structural edits (add/remove hook, propagate) apply here — see remarks on deferral.
            if (_familyPendingAction != null)
            {
                var action = _familyPendingAction;
                _familyPendingAction = null;
                action();
            }
        }

        // ============================ Family selection ============================

        /// <summary>Jumps the tab to the selected item's family the first time selection lands on a new asset — spec §6.3 "seguir la familia del ítem seleccionado". The dropdown can still override it until the next selection change.</summary>
        void SyncFollowedSelection()
        {
            if (SelectedAsset == null || ReferenceEquals(SelectedAsset, _familyLastFollowed)) return;
            _familyLastFollowed = SelectedAsset;
            if (!string.IsNullOrEmpty(SelectedAsset.FamilyId))
                _familySelectedId = SelectedAsset.FamilyId;
        }

        void DrawFamilyPicker(IReadOnlyList<ItemQuery.ItemFamily> families)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Family", GUILayout.Width(50));

                var ids = families.Select(f => f.FamilyId).ToArray();
                int current = Array.IndexOf(ids, _familySelectedId);
                if (current < 0) current = 0;
                int picked = EditorGUILayout.Popup(current, ids);
                _familySelectedId = ids[picked];

                GUILayout.FlexibleSpace();
                var count = families.First(f => f.FamilyId == _familySelectedId).Variants.Count;
                EditorGUILayout.LabelField($"{count} variant(s)", EditorStyles.miniLabel, GUILayout.Width(90));
            }
        }

        // ============================ Per-variant contexts ============================

        /// <summary>Lazily creates the <see cref="PolymorphicAuthoringContext"/> a column mutates its variant through — see class remarks.</summary>
        PolymorphicAuthoringContext GetFamilyContext(ItemSO variant)
        {
            if (!_familyContexts.TryGetValue(variant, out var ctx))
            {
                ctx = new PolymorphicAuthoringContext(variant);
                _familyContexts[variant] = ctx;
            }
            return ctx;
        }

        /// <summary>Disposes contexts for variants that dropped out of the visible family (family switch, item removed from it) — keeps the live set bounded to what's on screen.</summary>
        void PruneFamilyContexts(IReadOnlyList<ItemSO> keep)
        {
            if (_familyContexts.Count == 0) return;

            List<ItemSO> stale = null;
            foreach (var key in _familyContexts.Keys)
                if (!keep.Any(v => ReferenceEquals(v, key)))
                    (stale ??= new List<ItemSO>()).Add(key);

            if (stale == null) return;
            foreach (var key in stale)
            {
                _familyContexts[key].Dispose();
                _familyContexts.Remove(key);
            }
        }

        // ============================ Table ============================

        void DrawFamilyColumn(ItemSO variant)
        {
            var ctx = GetFamilyContext(variant);
            ctx.UpdateTree();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(FAMILY_COLUMN_WIDTH)))
            {
                DrawFamilyColumnHeader(variant);
                EditorGUILayout.Space(4);

                EditorGUI.BeginChangeCheck();
                string newName = EditorGUILayout.DelayedTextField("Name", variant.DisplayName ?? string.Empty);
                if (EditorGUI.EndChangeCheck())
                    ctx.Mutate("Edit Display Name", () => variant.DisplayName = newName);

                // ItemId drives catalog lookups (§0) — kept read-only here; renaming goes through
                // the main panel's slug ceremony (ItemIdSlug), not this table.
                EditorGUILayout.LabelField("Item Id", variant.ItemId ?? string.Empty, EditorStyles.miniLabel);

                EditorGUI.BeginChangeCheck();
                var newRarity = (ItemRarity)EditorGUILayout.EnumPopup("Rarity", variant.Rarity);
                if (EditorGUI.EndChangeCheck())
                    ctx.Mutate("Edit Rarity", () => variant.Rarity = newRarity);

                EditorGUI.BeginChangeCheck();
                int newVariantIndex = EditorGUILayout.DelayedIntField("Variant Index", variant.VariantIndex);
                if (EditorGUI.EndChangeCheck())
                    ctx.Mutate("Edit Variant Index", () => variant.VariantIndex = Mathf.Max(0, newVariantIndex));

                DrawFamilyPriceRow(variant);

                EditorGUILayout.Space(6);
                DrawFamilyStructureRows(variant, ctx);
            }

            // Odin queues nested list "+"/"✕" clicks (PreConditions, Effects…) on their own resolver;
            // this flushes them for THIS variant's tree. See PolymorphicAuthoringContext remarks.
            ctx.ApplyChanges();
        }

        void DrawFamilyColumnHeader(ItemSO variant)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = RarityPalette.BodyColor(variant.Rarity);
            GUILayout.Box(RarityPalette.DisplayName(variant.Rarity), GUILayout.ExpandWidth(true));
            GUI.backgroundColor = prevBg;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(LabelOf(variant), EditorStyles.boldLabel);
                if (GUILayout.Button("Ping", GUILayout.Width(40)))
                    EditorGUIUtility.PingObject(variant);
            }
        }

        /// <summary>Price lives on the ShopPool, not the item (§2/§3) — read/write it through the bridge instead of duplicating a field here.</summary>
        void DrawFamilyPriceRow(ItemSO variant)
        {
            var pool = ItemShopPriceBridge.LoadDefaultPool();
            if (pool == null)
            {
                EditorGUILayout.HelpBox("No ShopPool at the default path.", MessageType.None);
                return;
            }

            if (ItemShopPriceBridge.TryGetPrice(pool, variant, out int price))
            {
                EditorGUI.BeginChangeCheck();
                int newPrice = EditorGUILayout.DelayedIntField("Price", price);
                if (EditorGUI.EndChangeCheck() && newPrice != price)
                    ItemShopPriceBridge.SetPrice(pool, variant, Mathf.Max(0, newPrice));
            }
            else
            {
                int suggested = RarityPricing.BasePriceFor(variant.Rarity);
                if (GUILayout.Button($"Add to Shop Pool ({suggested}g)"))
                    ItemShopPriceBridge.AddToPool(pool, variant, suggested);
            }
        }

        /// <summary>
        /// The row that actually differs by item Type: a passive's hook list, or an active's single
        /// effect. Drawn full-depth via <see cref="PolymorphicAuthoringContext.Draw"/> so every field
        /// — including nested polymorphic effects — is editable right here, no asset switch (§6.3).
        /// </summary>
        void DrawFamilyStructureRows(ItemSO variant, PolymorphicAuthoringContext ctx)
        {
            if (variant.Type == ItemType.Passive)
            {
                EditorGUILayout.LabelField("Passive Hooks", EditorStyles.boldLabel);

                var hooks = variant.PassiveHooks;
                int count = hooks?.Count ?? 0;
                for (int i = 0; i < count; i++)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField($"Hook {i} — {hooks[i].Kind}", EditorStyles.miniBoldLabel);
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("✕", GUILayout.Width(20)))
                            {
                                int removeIndex = i;
                                _familyPendingAction = () =>
                                    ctx.Mutate("Remove Hook", () => variant.PassiveHooks.RemoveAt(removeIndex));
                            }
                        }
                        ctx.Draw($"PassiveHooks.${i}");
                    }
                }

                if (GUILayout.Button("+ Add Hook"))
                {
                    _familyPendingAction = () => ctx.Mutate("Add Hook", () =>
                    {
                        variant.PassiveHooks ??= new List<PassiveItemHook>();
                        variant.PassiveHooks.Add(new PassiveItemHook());
                    });
                }
            }
            else if (variant.Type == ItemType.Active)
            {
                EditorGUILayout.LabelField("Active Effect", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                int cooldown = EditorGUILayout.DelayedIntField("Cooldown", variant.Cooldown);
                if (EditorGUI.EndChangeCheck())
                    ctx.Mutate("Edit Cooldown", () => variant.Cooldown = Mathf.Max(0, cooldown));

                EditorGUI.BeginChangeCheck();
                bool consumed = EditorGUILayout.Toggle("Consumed On Use", variant.ConsumedOnUse);
                if (EditorGUI.EndChangeCheck())
                    ctx.Mutate("Edit Consumed On Use", () => variant.ConsumedOnUse = consumed);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    ctx.Draw("OnActivate");
            }
            else
            {
                EditorGUILayout.HelpBox($"Unhandled item type: {variant.Type}", MessageType.Warning);
            }
        }

        // ============================ Propagate structure down ============================

        /// <summary>
        /// One toolbar, not one button per row: the source is always <c>family.Variants[0]</c> (the
        /// lowest <c>VariantIndex</c>) — matching the GDD's authored order (Botas Ligeras is tier 0)
        /// and the "hacia abajo" direction the spec asks for. A family with heterogeneous Types
        /// (unusual, but not forbidden) just skips the variants that don't match the source's Type.
        /// </summary>
        void DrawFamilyPropagateToolbar(ItemQuery.ItemFamily family)
        {
            var source = family.Variants[0];

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField($"Structure source: {LabelOf(source)}", EditorStyles.miniLabel, GUILayout.Width(240));

                bool hasHooks = source.Type == ItemType.Passive && source.PassiveHooks != null && source.PassiveHooks.Count > 0;
                using (new EditorGUI.DisabledScope(!hasHooks))
                {
                    if (GUILayout.Button("Propagate Hooks ↓", EditorStyles.toolbarButton, GUILayout.Width(150)))
                        _familyPendingAction = () => PropagateHooksDown(family, source);
                }

                bool hasActive = source.Type == ItemType.Active && source.OnActivate != null;
                using (new EditorGUI.DisabledScope(!hasActive))
                {
                    if (GUILayout.Button("Propagate Active Effect ↓", EditorStyles.toolbarButton, GUILayout.Width(180)))
                        _familyPendingAction = () => PropagateActiveDown(family, source);
                }

                GUILayout.FlexibleSpace();
            }
        }

        /// <summary>Clones <paramref name="source"/>'s hook list onto every other Passive variant, keeping each target's own numbers (§7, class remarks). One undo group for the whole family.</summary>
        void PropagateHooksDown(ItemQuery.ItemFamily family, ItemSO source)
        {
            using (PolymorphicAuthoringContext.UndoGroup($"Propagate Family Hooks: {family.FamilyId}"))
            {
                foreach (var target in family.Variants)
                {
                    if (target == null || ReferenceEquals(target, source) || target.Type != ItemType.Passive) continue;

                    var clone = CloneHooksKeepingNumbers(source.PassiveHooks, target.PassiveHooks);
                    var tctx = GetFamilyContext(target);
                    tctx.Mutate("Propagate Hooks", () => target.PassiveHooks = clone);
                }
            }
        }

        /// <summary>Same as <see cref="PropagateHooksDown"/> for <c>OnActivate</c> on Active variants.</summary>
        void PropagateActiveDown(ItemQuery.ItemFamily family, ItemSO source)
        {
            using (PolymorphicAuthoringContext.UndoGroup($"Propagate Family Active Effect: {family.FamilyId}"))
            {
                foreach (var target in family.Variants)
                {
                    if (target == null || ReferenceEquals(target, source) || target.Type != ItemType.Active) continue;

                    var clone = CloneEffectKeepingNumbers(source.OnActivate, target.OnActivate);
                    var tctx = GetFamilyContext(target);
                    tctx.Mutate("Propagate Active Effect", () => target.OnActivate = clone);
                }
            }
        }

        static List<PassiveItemHook> CloneHooksKeepingNumbers(List<PassiveItemHook> source, List<PassiveItemHook> existingTarget)
        {
            var clone = new List<PassiveItemHook>();
            if (source == null) return clone;

            foreach (var hook in source)
                clone.Add(Sirenix.Serialization.SerializationUtility.CreateCopy(hook) as PassiveItemHook);

            if (existingTarget != null)
            {
                int n = Mathf.Min(clone.Count, existingTarget.Count);
                for (int i = 0; i < n; i++)
                    MergeNumericLeaves(clone[i], existingTarget[i], new HashSet<object>());
            }
            return clone;
        }

        static EffectData CloneEffectKeepingNumbers(EffectData source, EffectData existingTarget)
        {
            if (source == null) return null;
            var clone = Sirenix.Serialization.SerializationUtility.CreateCopy(source) as EffectData;
            if (existingTarget != null) MergeNumericLeaves(clone, existingTarget, new HashSet<object>());
            return clone;
        }

        // ---- numeric-leaf merge (structure from `into`, numbers backfilled from `from`) ----

        static readonly HashSet<Type> NumericLeafTypes = new HashSet<Type>
        {
            typeof(int), typeof(float), typeof(double), typeof(long),
            typeof(short), typeof(byte), typeof(uint), typeof(ulong), typeof(ushort), typeof(sbyte),
        };

        /// <summary>
        /// Walks <paramref name="into"/> (the fresh structural clone) and <paramref name="from"/>
        /// (the target's previous value) in lockstep, overwriting every numeric field/list-of-numbers
        /// in <paramref name="into"/> with the value <paramref name="from"/> had at the same position.
        /// Stops descending wherever the two disagree on type — that's exactly the boundary of the
        /// structural change, and nothing below it in the old tree corresponds to anything in the new
        /// one, so there's nothing correct to preserve there.
        /// </summary>
        static void MergeNumericLeaves(object into, object from, HashSet<object> visited)
        {
            if (into == null || from == null) return;
            if (!into.GetType().IsValueType && !visited.Add(into)) return; // cycle guard, mirrors BlockGraphModel

            // UnityEngine.Object references (Sprite, GameObject…) are asset links, not inline data —
            // recursing into one would use reflection to mutate a SHARED asset in place. Never walk
            // past this boundary; same rule PolymorphicMemberScanner.IsInlineSerializableClass uses.
            if (typeof(UnityEngine.Object).IsAssignableFrom(into.GetType())) return;

            if (into is IList intoList)
            {
                if (!(from is IList fromList)) return;
                int n = Math.Min(intoList.Count, fromList.Count);
                var elementType = ElementTypeOfList(into.GetType());
                for (int i = 0; i < n; i++)
                {
                    if (elementType != null && NumericLeafTypes.Contains(elementType))
                    {
                        intoList[i] = fromList[i];
                    }
                    else if (intoList[i] != null && fromList[i] != null && intoList[i].GetType() == fromList[i].GetType())
                    {
                        MergeNumericLeaves(intoList[i], fromList[i], visited);
                    }
                }
                return;
            }

            var type = into.GetType();
            if (from.GetType() != type) return;
            if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return;

            foreach (var field in SerializedFieldsOf(type))
            {
                if (NumericLeafTypes.Contains(field.FieldType))
                {
                    field.SetValue(into, field.GetValue(from));
                    continue;
                }
                if (field.FieldType.IsPrimitive || field.FieldType.IsEnum || field.FieldType == typeof(string)) continue;
                if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)) continue; // asset link — never recurse into it

                var intoVal = field.GetValue(into);
                var fromVal = field.GetValue(from);
                if (intoVal == null || fromVal == null) continue;
                MergeNumericLeaves(intoVal, fromVal, visited);
            }
        }

        static Type ElementTypeOfList(Type listType)
        {
            if (listType.IsArray) return listType.GetElementType();
            if (listType.IsGenericType) return listType.GetGenericArguments()[0];
            return null;
        }

        /// <summary>Same "what does Odin actually serialize" rule as <c>PolymorphicMemberScanner.SerializedFieldsOf</c> — duplicated locally so this file stays self-contained (single-owner constraint, §A7 task).</summary>
        static IEnumerable<FieldInfo> SerializedFieldsOf(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var f in type.GetFields(flags))
            {
                if (f.IsDefined(typeof(NonSerializedAttribute), false)
                    && !f.IsDefined(typeof(OdinSerializeAttribute), false)) continue;

                bool optedIn = f.IsDefined(typeof(OdinSerializeAttribute), false)
                               || f.IsDefined(typeof(SerializeField), false)
                               || f.IsDefined(typeof(SerializeReference), false);

                if (f.IsPublic || optedIn) yield return f;
            }
        }
    }
}
