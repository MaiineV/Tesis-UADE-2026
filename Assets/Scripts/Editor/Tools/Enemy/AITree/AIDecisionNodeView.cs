using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rollgeon.Editor.Tools.Enemy.AITree
{
    public sealed class AIDecisionNodeView : Node
    {
        public AIDecisionNode Data { get; }
        public Port InputPort { get; private set; }
        public IReadOnlyList<Port> OutputPorts => _outputPorts;

        readonly List<Port> _outputPorts = new List<Port>();
        Label _summary;
        Label _rootChip;

        public AIDecisionNodeView(AIDecisionNode data)
        {
            Data = data;

            title = data.NodeName;
            BuildHeader();
            BuildPorts();
            BuildSummary();

            RefreshExpandedState();
            RefreshPorts();
        }

        public void RefreshSummary()
        {
            title = Data.NodeName;
            if (_summary != null) _summary.text = SummaryFor(Data);
        }

        /// <summary>
        /// Toggle the ROOT indicator chip. Owned by the GraphView — call from
        /// <c>RefreshRootIndicators()</c> whenever <c>GraphSnapshot.Root</c> changes.
        /// </summary>
        public void SetIsRoot(bool isRoot)
        {
            if (_rootChip == null && isRoot) BuildRootChip();
            if (_rootChip != null) _rootChip.style.display = isRoot ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void BuildRootChip()
        {
            _rootChip = new Label("ROOT")
            {
                style =
                {
                    fontSize = 9,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.10f, 0.10f, 0.10f),
                    backgroundColor = new Color(1f, 0.85f, 0.30f),
                    paddingLeft = 4, paddingRight = 4, paddingTop = 1, paddingBottom = 1,
                    marginLeft = 4, marginRight = 4, marginTop = 2,
                    borderTopLeftRadius = 3, borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3, borderBottomRightRadius = 3,
                },
            };
            // Insert after the category chip (which lives at index 0) so it sits next to it.
            titleContainer.Insert(1, _rootChip);
        }

        // ---- header (category chip + colour) ------------------------------

        void BuildHeader()
        {
            var meta = AINodeRegistry.Find(Data.GetType());
            var category = meta?.Category ?? AINodeCategory.Leaf;

            var chip = new Label(category.ToString());
            chip.style.fontSize = 9;
            chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            chip.style.color = new Color(0.9f, 0.9f, 0.9f);
            chip.style.marginLeft = 6;
            chip.style.marginRight = 6;
            chip.style.marginTop = 2;
            titleContainer.Insert(0, chip);

            Color tint;
            switch (category)
            {
                case AINodeCategory.Composite: tint = new Color(0.30f, 0.55f, 0.85f); break;
                case AINodeCategory.Branching: tint = new Color(0.85f, 0.65f, 0.25f); break;
                default:                       tint = new Color(0.35f, 0.70f, 0.40f); break;
            }
            titleContainer.style.backgroundColor = tint;
        }

        // ---- ports --------------------------------------------------------

        void BuildPorts()
        {
            InputPort = InstantiatePort(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(bool));
            InputPort.portName = "in";
            inputContainer.Add(InputPort);

            var slots = AITreeTopology.SlotsOf(Data);
            for (int i = 0; i < slots.Count; i++)
            {
                AddOutputPortForSlot(slots[i], i);
            }
        }

        public Port AddOutputPortForSlot(AITreeTopology.Slot slot, int slotIndex)
        {
            var port = InstantiatePort(Orientation.Vertical, Direction.Output, Port.Capacity.Single, typeof(bool));
            port.portName = slot.Name;
            port.userData = slotIndex;
            outputContainer.Add(port);
            _outputPorts.Add(port);
            return port;
        }

        // ---- summary (read-only chip showing the node's key params) -------

        void BuildSummary()
        {
            string text = SummaryFor(Data);
            if (string.IsNullOrEmpty(text)) return;

            _summary = new Label(text)
            {
                style =
                {
                    fontSize = 10,
                    color = new Color(0.85f, 0.85f, 0.85f),
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 4,
                    paddingBottom = 4,
                    whiteSpace = WhiteSpace.Normal,
                    minWidth = 180,
                },
            };
            extensionContainer.Add(_summary);
        }

        /// <summary>
        /// Cheap one-liner for in-canvas glance. Real editing happens in the side panel.
        /// </summary>
        public static string SummaryFor(AIDecisionNode node)
        {
            switch (node)
            {
                case AINode_Move m:
                    return $"max {Describe(m.MaxSteps)} steps · stop adj {m.StopAdjacent}";
                case AINode_KeepDistance k:
                    return $"max {Describe(k.MaxSteps)} steps · ideal {Describe(k.IdealDistance)}";
                case AINode_If i:
                    int conds = i.Conditions != null ? i.Conditions.Count : 0;
                    string sel = i.TargetSelector != null ? i.TargetSelector.GetType().Name : "default target";
                    return $"{conds} condition(s) · {sel}";
                case AINode_While w:
                    int wconds = w.Conditions != null ? w.Conditions.Count : 0;
                    string wsel = w.TargetSelector != null ? w.TargetSelector.GetType().Name : "default target";
                    return $"while {wconds} cond(s) · max {w.MaxIterations} · {wsel}";
                case AINode_Behavior b:
                    return b.Behavior != null ? b.Behavior.BehaviorName : "(no behavior)";
                default:
                    return null;
            }
        }

        static string Describe(AIIntReader reader)
        {
            if (reader == null) return "?";
            if (reader is AIConstantInt c) return c.Value.ToString();
            if (reader is AIReadSelfStat self) return $"Self.{self.Stat}";
            if (reader is AIReadPlayerStat player) return $"Player.{player.Stat}";
            return reader.GetType().Name;
        }

        // ---- per-subtype field lists (used by the side inspector) ---------

        public static string[] InlineFieldsOf(AIDecisionNode node)
        {
            switch (node)
            {
                case AINode_If _:           return _ifFields;
                case AINode_While _:        return _whileFields;
                case AINode_Behavior _:     return _behaviorFields;
                case AINode_Move _:         return _moveFields;
                case AINode_KeepDistance _: return _keepDistanceFields;
                default:                    return Array.Empty<string>();
            }
        }

        static readonly string[] _ifFields = { "TargetSelector", "Conditions" };
        static readonly string[] _whileFields = { "TargetSelector", "Conditions", "MaxIterations" };
        static readonly string[] _behaviorFields = { "Behavior" };
        static readonly string[] _moveFields = { "MaxSteps", "StopAdjacent" };
        static readonly string[] _keepDistanceFields = { "MaxSteps", "IdealDistance" };
    }
}
