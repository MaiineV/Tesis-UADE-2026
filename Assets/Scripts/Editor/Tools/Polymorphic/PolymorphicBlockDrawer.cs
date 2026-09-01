using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.PreConditions;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
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
        /// <remarks>
        /// Raised from 6 to 8 when effects started nesting inside feedback sequences:
        /// <c>EffChain → ChainPhase → EffectData → EffPlaySequence → FeedbackSequenceStep →
        /// EffectData → EffDealDamage</c> is 7 levels on its own, so the warrior's deferred
        /// damage fell past the old cap and rendered through Odin's stock drawer — which hides
        /// the polymorphic picker by declared type, making it unauthorable.
        /// </remarks>
        public const int MAX_DEPTH = 8;

        /// <summary>Per-host toggles for parts of an <see cref="EffectData"/> that don't apply everywhere.</summary>
        public struct Options
        {
            /// <summary>
            /// Show <see cref="EffectData.TargetSelector"/>. Only <c>EnemyActionBehavior</c> reads it —
            /// items and hero actions ignore it, so surfacing it there invites authoring a no-op.
            /// </summary>
            public bool ShowTargetSelector;

            /// <summary>
            /// Filtro de tipos para los pickers de Eff/PC. El host enemigo esconde lo que en su
            /// contexto no hace nada (efectos/PCs que leen el roll del jugador — el validador ya
            /// los marca en assets viejos). Null = sin filtro (Item/Enchantment Editor).
            /// </summary>
            public System.Func<System.Type, bool> TypeFilter;

            public static Options Enemy => new Options
            {
                ShowTargetSelector = true,
                // global:: — dentro del struct, "Enemy" a secas resuelve a este mismo property.
                TypeFilter = t => !global::Rollgeon.Editor.Tools.Enemy.AITree.AITreeValidator.NeedsPlayerRollContext(t)
                                  && !global::Rollgeon.Editor.Tools.Enemy.AITree.AITreeValidator.PcUnusableInEnemyTree(t),
            };
            public static Options Default => new Options { ShowTargetSelector = false };
        }

        // ---- campos con dibujo propio -------------------------------------

        static readonly Dictionary<(System.Type Owner, string Member), System.Action<object>>
            MemberDrawers = new Dictionary<(System.Type, string), System.Action<object>>();

        /// <summary>
        /// Hace que un campo lo dibuje el dueño del dominio en vez de Odin.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Este drawer es genérico a propósito y no conoce ningún tipo del juego, pero hay campos que
        /// dibujados tal cual mienten: <c>ItemSO.DisplayName</c> no es lo que ve el jugador — es el
        /// respaldo de la tabla de localización. Un registro opt-in deja que la ventana de ítems
        /// ponga ahí el campo del idioma activo, en la misma categoría "Identity" donde el autor lo
        /// busca, sin que este archivo sepa que existe la localización.
        /// </para>
        /// <para>
        /// Solo aplica a <see cref="DrawNode"/> — el panel. La tab de Raw Data recorre el árbol por
        /// su cuenta y sigue mostrando el campo crudo, que es justo para lo que está.
        /// </para>
        /// </remarks>
        public static void RegisterMemberDrawer(
            System.Type ownerType, string memberName, System.Action<object> draw)
        {
            if (ownerType == null || string.IsNullOrEmpty(memberName)) return;

            var key = (ownerType, memberName);
            if (draw == null) MemberDrawers.Remove(key);
            else MemberDrawers[key] = draw;
        }

        static bool TryDrawMember(object owner, InspectorProperty child)
        {
            if (MemberDrawers.Count == 0 || owner == null) return false;
            if (!MemberDrawers.TryGetValue((owner.GetType(), child.Name), out var draw)) return false;

            draw(owner);
            return true;
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
            PolymorphicAuthoringContext ctx, string label, System.Type baseType, IList list,
            System.Func<System.Type, bool> filter = null)
        {
            PolymorphicPicker.DrawAddButton(
                label, baseType, list,
                onAdded: () => { ctx.MarkDirty(); ctx.Notify(); },
                onBeforeAdd: () => ctx.RecordUndo("Add " + label),
                filter: filter);
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
            DrawAddButton(ctx, "PreCondition", typeof(BasePreCondition), item.PreConditions, opts.TypeFilter);

            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Effects", EditorStyles.miniBoldLabel);
            if (item.Effects == null) item.Effects = new List<IEffect>();
            DrawEffectListItems(ctx, item.Effects, basePath + ".Effects", opts, depth);
            DrawAddButton(ctx, "Effect", typeof(IEffect), item.Effects, opts.TypeFilter);

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

        // ---- arbitrary node (graph side panel) -----------------------------

        /// <summary>
        /// Draws the fields that belong to the block the canvas has selected — and only those.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Anything the graph already draws as its own node is skipped here.</b> An effect group's
        /// effects and preconditions are nodes to its right, so re-serialising them in the panel
        /// shows the same data twice and buries the group's own fields; on an <c>EffChain</c> it's
        /// worse, because the panel renders the whole nested tree that the canvas is already
        /// showing. Structure is edited on the canvas (right-click a node); the panel edits values.
        /// </para>
        /// <para>
        /// The exception is a slot that is <b>null</b>: it produces no node, so the panel has to
        /// offer its picker or there'd be no way to fill it.
        /// </para>
        /// </remarks>
        public static void DrawNode(PolymorphicAuthoringContext ctx, object value, string path, Options opts)
        {
            if (value == null) return;

            var type = value.GetType();
            var pickers = PolymorphicMemberScanner.Scan(type);
            var blocks = PolymorphicMemberScanner.BlockMembersOf(type);

            var children = ChildrenAt(ctx, path);
            if (children == null)
            {
                ctx.Draw(path);
                return;
            }

            bool drewAnything = false;

            // Los campos se agrupan bajo el [Title] que los precede y cada grupo se puede plegar. Un
            // ItemSO tiene cinco categorias y en un panel angosto se leian como un muro continuo; el
            // titulo ya marcaba la division visualmente, pero no dejaba esconder lo que no se toca.
            string openSection = null;
            bool sectionVisible = true;
            bool sectionHasContent = true;

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (IsBlockNamed(blocks, child.Name)) continue;   // graphed
                if (IsBlockNamed(pickers, child.Name)) continue;  // graphed, or handled below

                var title = TitleOf(child);
                if (title != null && title != openSection)
                {
                    openSection = title;
                    sectionHasContent = SectionHasVisibleContent(children, i, title, blocks, pickers);
                    sectionVisible = sectionHasContent && DrawSectionHeader(type, title);
                }

                if (!sectionHasContent)
                {
                    // Se dibuja igual — no emite nada — porque Odin reevalua el ShowIf DENTRO del
                    // Draw de cada campo. Saltearlo dejaria la categoria escondida para siempre:
                    // al pasar el item de Passive a Active, sus campos nunca volverian a mirarse.
                    child.Draw();
                    continue;
                }

                if (!sectionVisible) { drewAnything = true; continue; }

                if (!TryDrawMember(value, child)) child.Draw();
                drewAnything = true;
            }

            // Null single slots have no node, so they'd be unreachable without a picker here.
            foreach (var picker in pickers)
            {
                if (picker.IsList) continue;
                if (IsHiddenTargetSelector(picker, opts)) continue;
                var current = picker.Field.GetValue(value);
                if (current != null) continue;

                var captured = picker;
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(picker.Title, EditorStyles.miniBoldLabel);
                DrawSingleSlot(
                    ctx, "Type", picker.BaseType, null, path + "." + picker.Name,
                    v => captured.Field.SetValue(value, v),
                    "Assign " + picker.Title);
                drewAnything = true;
            }

            if (!drewAnything)
                EditorGUILayout.HelpBox(
                    "This block has no fields of its own — everything it holds is a node on the canvas. " +
                    "Right-click it there to add or remove.",
                    MessageType.Info);
        }

        /// <summary>
        /// Only <c>EnemyActionBehavior</c> reads <see cref="EffectData.TargetSelector"/>. Offering it
        /// in the item or hero hosts would let an author configure something that never runs.
        /// </summary>
        static bool IsHiddenTargetSelector(PolymorphicMember member, Options opts) =>
            !opts.ShowTargetSelector && member.BaseType == typeof(BaseEnemyTargetSelector);

        /// <summary>
        /// Child properties at <paramref name="path"/>, or the asset's own top-level fields when the
        /// path is empty (the root node). Null when the path doesn't resolve.
        /// </summary>
        static List<InspectorProperty> ChildrenAt(PolymorphicAuthoringContext ctx, string path)
        {
            var result = new List<InspectorProperty>();

            if (string.IsNullOrEmpty(path))
            {
                if (ctx.Tree == null) return null;
                foreach (var p in ctx.Tree.EnumerateTree(false))
                {
                    if (IsOdinMachinery(p.Name)) continue;
                    result.Add(p);
                }
                return result;
            }

            var prop = ctx.At(path);
            if (prop == null) return null;
            for (int i = 0; i < prop.Children.Count; i++)
            {
                var child = prop.Children[i];
                if (IsOdinMachinery(child.Name)) continue;
                result.Add(child);
            }
            return result;
        }

        /// <summary>
        /// Odin surfaces its own plumbing as properties of a <c>SerializedScriptableObject</c>:
        /// the serialisation blob, and the hook it uses to run a type's custom inspector GUI.
        /// Neither is content, and drawing the latter re-entrantly renders the whole inspector.
        /// </summary>
        static bool IsOdinMachinery(string propertyName) =>
            propertyName == "serializationData" || propertyName == "InternalOnInspectorGUI";

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

        /// <summary>
        /// Si la categoria que abre <paramref name="start"/> tiene algun campo visible.
        /// </summary>
        /// <remarks>
        /// Una categoria puede quedar entera oculta por <c>ShowIf</c> — "Action economy" y "Active
        /// Effects" son solo de items Activos, asi que en un Pasivo no tienen un solo campo. Su
        /// cabecera desplegada la dibuja el <c>[Title]</c> de Odin, que sale junto al primer campo:
        /// sin campos no sale nada, y quedaba una categoria sin titulo ni linea con el triangulo
        /// flotando sobre la anterior. Desplegarla la hacia desaparecer y no habia como volver a
        /// plegarla.
        /// </remarks>
        static bool SectionHasVisibleContent(
            List<InspectorProperty> children, int start, string title,
            IReadOnlyList<PolymorphicMember> blocks, IReadOnlyList<PolymorphicMember> pickers)
        {
            for (int i = start; i < children.Count; i++)
            {
                var child = children[i];

                var childTitle = TitleOf(child);
                if (i > start && childTitle != null && childTitle != title) break;

                if (IsBlockNamed(blocks, child.Name)) continue;
                if (IsBlockNamed(pickers, child.Name)) continue;
                if (child.State.Visible) return true;
            }

            return false;
        }

        /// <summary>El <c>[Title]</c> del miembro, si abre una categoria; null si no.</summary>
        static string TitleOf(InspectorProperty property)
        {
            var member = property?.Info?.GetMemberInfo();
            var title = member?.GetCustomAttribute<TitleAttribute>(false);
            return string.IsNullOrEmpty(title?.Title) ? null : title.Title;
        }

        /// <summary>
        /// Cabecera plegable de una categoria. Devuelve si su contenido va dibujado.
        /// </summary>
        /// <remarks>
        /// El estado se guarda por tipo de asset y titulo, no por instancia: las categorias son las
        /// mismas para todos los items, asi que plegar una y cambiar de item deberia mantenerla
        /// plegada — si se reabriera en cada seleccion, plegarla no serviria de nada.
        /// </remarks>
        static bool DrawSectionHeader(System.Type ownerType, string title)
        {
            var key = SectionKeyOf(ownerType.Name, title);
            bool expanded = EditorPrefs.GetBool(key, true);
            bool next = SectionToggle(title, expanded, drawOwnTitle: !expanded);
            if (next != expanded) EditorPrefs.SetBool(key, next);
            return next;
        }

        internal static string SectionKeyOf(string ownerTypeName, string title) =>
            "Rollgeon.PolymorphicBlockDrawer.Section." + ownerTypeName + "." + title;

        /// <summary>
        /// Triángulo de plegado alineado a la derecha del encabezado de categoría.
        /// </summary>
        /// <remarks>
        /// El subrayado lo dibuja el <c>[Title]</c> de Odin, y queremos ese — no una segunda cabecera
        /// arriba, que era lo que se veía duplicado. Así que el triángulo se dibuja solo, alineado a
        /// la derecha, y se lo sube con un espacio negativo para que caiga sobre la misma línea que el
        /// título que Odin va a dibujar justo después.
        /// <para>
        /// Cuando la categoría está plegada, Odin no llega a dibujar nada — sus campos no se dibujan —
        /// así que ahí el título y su línea los pone <paramref name="drawOwnTitle"/>, replicando el
        /// mismo aspecto para que plegar y desplegar no cambie la cabecera.
        /// </para>
        /// </remarks>
        internal static bool SectionToggle(string title, bool expanded, bool drawOwnTitle)
        {
            EditorGUILayout.Space(6);

            if (drawOwnTitle)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    expanded = DrawArrow(expanded);
                }
                DrawUnderline();
                return expanded;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                expanded = DrawArrow(expanded);
            }

            // Sube la línea siguiente para que el título de Odin comparta fila con el triángulo.
            GUILayout.Space(-EditorGUIUtility.singleLineHeight - 2f);
            return expanded;
        }

        static bool DrawArrow(bool expanded)
        {
            var rect = GUILayoutUtility.GetRect(16f, EditorGUIUtility.singleLineHeight, GUILayout.Width(16f));
            if (GUI.Button(rect, expanded ? "▾" : "▸", EditorStyles.label)) expanded = !expanded;
            return expanded;
        }

        static void DrawUnderline()
        {
            var line = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1f));
            EditorGUI.DrawRect(line, new Color(0.35f, 0.35f, 0.35f, 1f));
            EditorGUILayout.Space(2);
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
