using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.PreConditions;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Polymorphic
{
    /// <summary>
    /// Draws polymorphic authoring blocks — the picker Odin refuses to show, plus the list UI
    /// around it. Odin still draws every element's inner fields; this only owns what Odin can't.
    /// </summary>
    /// <remarks>
    /// Stateless by design: all state lives in the <see cref="PolymorphicAuthoringContext"/>, and
    /// every path is <b>absolute</b> from the asset root. Callers that track a selection (the AI
    /// tree inspector) compose their own prefix — path relativity is the host's business, not this
    /// drawer's.
    /// </remarks>
    public static class PolymorphicBlockDrawer
    {
        /// <summary>
        /// Nesting cap. <c>EffChain → ChainPhase → EffectData → EffChain</c> is legal at runtime, so
        /// the drawer needs a floor; past this depth it hands the subtree back to Odin rather than
        /// recursing forever.
        /// </summary>
        public const int MAX_DEPTH = 6;

        /// <summary>Per-host toggles for parts of an <see cref="EffectData"/> that don't apply everywhere.</summary>
        public struct Options
        {
            /// <summary>
            /// Show <see cref="EffectData.TargetSelector"/>. Only <c>EnemyActionBehavior</c> reads it —
            /// items and hero actions ignore it, so surfacing it there invites authoring a no-op.
            /// </summary>
            public bool ShowTargetSelector;

            public static Options Enemy => new Options { ShowTargetSelector = true };
            public static Options Default => new Options { ShowTargetSelector = false };
        }

        // ---- generic polymorphic list -------------------------------------

        /// <summary>
        /// Header row with the concrete type name + a ✕ button, then Odin draws the item's inner
        /// fields. Used for any <c>IList</c> of a polymorphic base.
        /// </summary>
        public static void DrawPolymorphicListItems(
            PolymorphicAuthoringContext ctx, IList list, string listPath, string undoLabel)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var item = list[i];
                        EditorGUILayout.LabelField(
                            item != null ? item.GetType().Name : "(null)",
                            EditorStyles.miniBoldLabel);
                        GUILayout.FlexibleSpace();
                        if (PolymorphicPicker.DrawClearButton())
                        {
                            ctx.RecordUndo("Remove " + undoLabel);
                            list.RemoveAt(i);
                            ctx.MarkDirty();
                            ctx.Notify();
                            // Mutating while enumerating corrupts the IMGUI layout — bail out and
                            // let the next repaint redraw the shortened list.
                            return;
                        }
                    }
                    if (list[i] != null) ctx.Draw(listPath + ".$" + i);
                }
            }
        }

        /// <summary>"+ Add" that records undo before mutating, then dirties and repaints.</summary>
        public static void DrawAddButton(
            PolymorphicAuthoringContext ctx, string label, System.Type baseType, IList list)
        {
            PolymorphicPicker.DrawAddButton(
                label, baseType, list,
                onAdded: () => { ctx.MarkDirty(); ctx.Notify(); },
                onBeforeAdd: () => ctx.RecordUndo("Add " + label));
        }

        /// <summary>Picker for a single polymorphic slot, followed by Odin drawing its fields.</summary>
        public static void DrawSingleSlot(
            PolymorphicAuthoringContext ctx, string label, System.Type baseType,
            object current, string absolutePath, System.Action<object> assign, string undoLabel)
        {
            PolymorphicPicker.DrawSingle(
                label, baseType, current,
                newInstance =>
                {
                    ctx.RecordUndo(undoLabel);
                    assign(newInstance);
                    ctx.MarkDirty();
                    ctx.Notify();
                });
            if (current != null)
            {
                EditorGUI.indentLevel++;
                ctx.Draw(absolutePath);
                EditorGUI.indentLevel--;
            }
        }

        // ---- EffectData ---------------------------------------------------

        /// <summary>
        /// A <c>List&lt;EffectData&gt;</c>: one collapsible box per group, labelled by
        /// <see cref="EffectData.Label"/>.
        /// </summary>
        public static void DrawEffectDataList(
            PolymorphicAuthoringContext ctx, IList<EffectData> list, string listPath, Options opts)
        {
            DrawEffectDataList(ctx, list, listPath, opts, 0);
        }

        static void DrawEffectDataList(
            PolymorphicAuthoringContext ctx, IList<EffectData> list, string listPath, Options opts, int depth)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var item = list[i];
                        string label = item != null && !string.IsNullOrEmpty(item.Label)
                            ? item.Label
                            : (item != null ? item.GetType().Name : "(null)");
                        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (PolymorphicPicker.DrawClearButton())
                        {
                            ctx.RecordUndo("Remove Effect Group");
                            list.RemoveAt(i);
                            ctx.MarkDirty();
                            ctx.Notify();
                            return;
                        }
                    }
                    if (list[i] != null)
                        DrawEffectData(ctx, list[i], listPath + ".$" + i, opts, depth);
                }
            }
        }

        /// <summary>
        /// One <see cref="EffectData"/>: its three polymorphic slots get custom pickers, everything
        /// else falls through to Odin.
        /// </summary>
        public static void DrawEffectData(
            PolymorphicAuthoringContext ctx, EffectData item, string basePath, Options opts)
        {
            DrawEffectData(ctx, item, basePath, opts, 0);
        }

        static void DrawEffectData(
            PolymorphicAuthoringContext ctx, EffectData item, string basePath, Options opts, int depth)
        {
            if (item == null) return;

            ctx.Draw(basePath + ".Label");
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("PreConditions (AND)", EditorStyles.miniBoldLabel);
            if (item.PreConditions == null) item.PreConditions = new List<BasePreCondition>();
            DrawPolymorphicListItems(ctx, item.PreConditions, basePath + ".PreConditions", "PreCondition");
            DrawAddButton(ctx, "PreCondition", typeof(BasePreCondition), item.PreConditions);

            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Effects", EditorStyles.miniBoldLabel);
            if (item.Effects == null) item.Effects = new List<IEffect>();
            DrawEffectListItems(ctx, item.Effects, basePath + ".Effects", opts, depth);
            DrawAddButton(ctx, "Effect", typeof(IEffect), item.Effects);

            if (!opts.ShowTargetSelector) return;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Target Override", EditorStyles.miniBoldLabel);
            DrawSingleSlot(
                ctx, "Type", typeof(BaseEnemyTargetSelector), item.TargetSelector,
                basePath + ".TargetSelector",
                v => item.TargetSelector = (BaseEnemyTargetSelector)v,
                "Change Effect Target Selector");
        }

        /// <summary>
        /// The <c>List&lt;IEffect&gt;</c> inside an <see cref="EffectData"/>. Same shape as
        /// <see cref="DrawPolymorphicListItems"/> plus the reader pickers each effect may need.
        /// </summary>
        static void DrawEffectListItems(
            PolymorphicAuthoringContext ctx, IList list, string listPath, Options opts, int depth)
        {
            for (int i = 0; i < list.Count; i++)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var item = list[i];
                        EditorGUILayout.LabelField(
                            item != null ? item.GetType().Name : "(null)",
                            EditorStyles.miniBoldLabel);
                        GUILayout.FlexibleSpace();
                        if (PolymorphicPicker.DrawClearButton())
                        {
                            ctx.RecordUndo("Remove Effect");
                            list.RemoveAt(i);
                            ctx.MarkDirty();
                            ctx.Notify();
                            return;
                        }
                    }

                    if (list[i] != null)
                    {
                        var effectPath = listPath + ".$" + i;
                        DrawBlockBody(ctx, list[i], effectPath, opts, depth);
                        DrawReaderPickers(ctx, list[i], effectPath);
                    }
                }
            }
        }

        // ---- recursion into nested containers ------------------------------

        /// <summary>
        /// Draws an arbitrary inline object. Odin draws it whole unless it holds containers with
        /// unauthorable content below — then this owns the traversal and hands Odin each leaf.
        /// </summary>
        /// <remarks>
        /// Only <c>EffChain</c> qualifies today (via <c>Phases → ChainPhase → Effects</c>), but the
        /// rule is structural, not a special case: without this, the nested <c>EffectData</c> inside
        /// a chain renders through Odin's stock drawer, whose PreCondition picker is missing — which
        /// is why nothing could be authored inside a chain in any tool.
        /// </remarks>
        static void DrawBlockBody(
            PolymorphicAuthoringContext ctx, object value, string path, Options opts, int depth)
        {
            var blocks = PolymorphicMemberScanner.BlockMembersOf(value.GetType());
            if (blocks.Count == 0 || depth >= MAX_DEPTH)
            {
                // The common case: no container underneath, so Odin renders the whole thing and the
                // panel looks exactly as it always has.
                ctx.Draw(path);
                return;
            }

            var prop = ctx.At(path);
            if (prop == null)
            {
                ctx.Draw(path);
                return;
            }

            // Odin draws every field that isn't a container we have to own.
            for (int i = 0; i < prop.Children.Count; i++)
            {
                var child = prop.Children[i];
                if (IsBlockNamed(blocks, child.Name)) continue;
                child.Draw();
            }

            foreach (var block in blocks)
                DrawBlockMember(ctx, block, value, path, opts, depth);
        }

        static bool IsBlockNamed(IReadOnlyList<PolymorphicMember> blocks, string name)
        {
            for (int i = 0; i < blocks.Count; i++)
                if (blocks[i].Name == name) return true;
            return false;
        }

        static void DrawBlockMember(
            PolymorphicAuthoringContext ctx, PolymorphicMember block,
            object owner, string ownerPath, Options opts, int depth)
        {
            var value = block.Field.GetValue(owner);
            if (value == null) return;

            string memberPath = ownerPath + "." + block.Name;
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(block.Title, EditorStyles.miniBoldLabel);

            if (!block.IsList)
            {
                DrawNestedBlock(ctx, value, memberPath, opts, depth + 1);
                return;
            }

            if (!(value is IList list)) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"{block.BaseType.Name} {i}", EditorStyles.miniLabel);
                    DrawNestedBlock(ctx, list[i], memberPath + ".$" + i, opts, depth + 1);
                }
            }
        }

        static void DrawNestedBlock(
            PolymorphicAuthoringContext ctx, object value, string path, Options opts, int depth)
        {
            if (value is EffectData nested)
            {
                DrawEffectData(ctx, nested, path, opts, depth);
                return;
            }
            DrawBlockBody(ctx, value, path, opts, depth);
        }

        /// <summary>
        /// Reader pickers for effects that read their value from an <see cref="EffectIntReader"/>.
        /// </summary>
        /// <remarks>
        /// This is the one place the generic drawer has to know a business rule: the reader only
        /// applies when a <c>DamageSource</c> field is set to <c>FromReader</c>, and reflection
        /// can't infer that. Expressing it as <c>[ShowIf]</c> on the runtime field would let Odin
        /// gate it and delete this method — worth doing when the runtime side is next touched.
        /// </remarks>
        static void DrawReaderPickers(PolymorphicAuthoringContext ctx, object effect, string effectPath)
        {
            var fields = effect.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

            bool hasFromReader = false;
            foreach (var f in fields)
            {
                if (f.FieldType == typeof(DamageSource)
                    && (DamageSource)f.GetValue(effect) == DamageSource.FromReader)
                {
                    hasFromReader = true;
                    break;
                }
            }
            if (!hasFromReader) return;

            foreach (var f in fields)
            {
                if (f.FieldType != typeof(EffectIntReader)) continue;
                var captured = f;
                PolymorphicPicker.DrawSingle(
                    "Reader Type", typeof(EffectIntReader), (EffectIntReader)f.GetValue(effect),
                    newInstance =>
                    {
                        ctx.RecordUndo("Change Effect Reader");
                        captured.SetValue(effect, newInstance);
                        ctx.MarkDirty();
                        ctx.Notify();
                    });
            }
        }
    }
}
