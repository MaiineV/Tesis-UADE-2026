using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rollgeon.Editor.Tools.Polymorphic
{
    /// <summary>
    /// Declares a parameterless void method on a <see cref="BlockEditorWindow{T}"/> subclass as an
    /// extra tab, drawn with IMGUI next to the built-in Graph and Raw Data tabs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Discovery is by attribute rather than by an <c>OnRegisterTabs</c> override for one reason:
    /// <b>file ownership</b>. An override is a single member in a single file, so two people each
    /// adding a tab (family view, metrics) would collide inside it. With the attribute each tab is
    /// self-contained — a partial file of the host holding one decorated method — and nothing shared
    /// has to be edited to add or remove one.
    /// </para>
    /// <para>
    /// A decorated method with the wrong shape is reported to the console and skipped, rather than
    /// throwing at window-open time: a broken tab must not take the whole tool down.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class BlockEditorTabAttribute : Attribute
    {
        /// <param name="title">Tab button label.</param>
        /// <param name="order">Sort key among host tabs. Ties break by title, so the tab strip never
        /// reorders itself between domain reloads.</param>
        public BlockEditorTabAttribute(string title, int order = 0)
        {
            Title = title;
            Order = order;
        }

        public string Title { get; }

        public int Order { get; }
    }

    public abstract partial class BlockEditorWindow<T> where T : ScriptableObject
    {
        // ============================ Tab host ============================

        static readonly Color TAB_ACTIVE = new Color(0.30f, 0.40f, 0.55f);
        static readonly Color TAB_IDLE = new Color(0.20f, 0.20f, 0.20f);

        // The two built-ins sort ahead of every host tab no matter what order it asks for; Graph is
        // the tab the window opens on and Raw Data is its escape hatch, so their position is part of
        // the window's shape, not a preference.
        const int GRAPH_TAB_ORDER = int.MinValue;
        const int RAW_DATA_TAB_ORDER = int.MinValue + 1;

        sealed class TabEntry
        {
            public string Title;
            public int Order;
            public VisualElement Content;

            /// <summary>Non-null only for host tabs, which the shell has to repaint on its behalf.</summary>
            public IMGUIContainer Imgui;
        }

        readonly List<TabEntry> _tabs = new List<TabEntry>();
        readonly List<Button> _tabButtons = new List<Button>();
        int _tabIndex;

        /// <summary>Index of the visible tab in the sorted strip. 0 is the graph.</summary>
        protected int ActiveTabIndex => _tabIndex;

        void BuildTabBar(VisualElement rightCol)
        {
            _tabs.Clear();
            _tabButtons.Clear();

            _tabs.Add(new TabEntry { Title = "Graph", Order = GRAPH_TAB_ORDER, Content = _graphTab });
            _tabs.Add(new TabEntry { Title = "Raw Data", Order = RAW_DATA_TAB_ORDER, Content = _dataPanel, Imgui = _dataPanel });
            CollectDeclaredTabs();

            _tabs.Sort((a, b) =>
                a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Title, b.Title));

            var tabBar = new VisualElement { style = { flexDirection = FlexDirection.Row, height = 28, marginBottom = 4 } };
            for (int i = 0; i < _tabs.Count; i++)
            {
                int index = i; // captured per button, not per loop
                var button = MakeTab(_tabs[i].Title, () => SwitchTab(index));
                _tabButtons.Add(button);
                tabBar.Add(button);
            }
            rightCol.Add(tabBar);
        }

        void CollectDeclaredTabs()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var method in GetType().GetMethods(flags))
            {
                var attribute = method.GetCustomAttribute<BlockEditorTabAttribute>();
                if (attribute == null) continue;

                if (method.ReturnType != typeof(void) || method.GetParameters().Length != 0)
                {
                    Debug.LogError(
                        $"[{GetType().Name}] '{method.Name}' carries [BlockEditorTab] but is not a " +
                        "parameterless void method — tab skipped.");
                    continue;
                }

                var draw = (Action)method.CreateDelegate(typeof(Action), this);
                var container = new IMGUIContainer(draw) { style = { flexGrow = 1 } };
                _tabs.Add(new TabEntry
                {
                    Title = attribute.Title,
                    Order = attribute.Order,
                    Content = container,
                    Imgui = container,
                });
            }
        }

        static Button MakeTab(string label, Action onClick)
        {
            var b = new Button(onClick) { text = label };
            b.style.flexGrow = 1;
            b.style.height = 26;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            return b;
        }

        /// <summary>
        /// Activa la tab que se llame <paramref name="title"/>. Silencioso si no existe.
        /// </summary>
        /// <remarks>
        /// Por titulo y no por indice porque el orden lo decide el atributo de cada tab: un host que
        /// agregue una tab nueva correria cualquier indice que otro archivo tuviera hardcodeado.
        /// </remarks>
        protected void ActivateTab(string title)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].Title != title) continue;
                SwitchTab(i);
                return;
            }
        }

        void SwitchTab(int index)
        {
            if (index < 0 || index >= _tabs.Count) return;

            _tabIndex = index;
            _rightHost.Clear();
            _rightHost.Add(_tabs[index].Content);

            for (int i = 0; i < _tabButtons.Count; i++)
                _tabButtons[i].style.backgroundColor = i == _tabIndex ? TAB_ACTIVE : TAB_IDLE;
        }

        /// <summary>
        /// Repaints every IMGUI-backed tab. Host tabs read the same asset as the graph, so anything
        /// that invalidates the side panel invalidates them too — and unlike the built-ins they have
        /// no way to learn about it themselves.
        /// </summary>
        void MarkDeclaredTabsDirty()
        {
            for (int i = 0; i < _tabs.Count; i++) _tabs[i].Imgui?.MarkDirtyRepaint();
        }
    }
}
