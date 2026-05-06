using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;
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
                    return $"max {m.MaxSteps} steps · stop adj {m.StopAdjacent}";
                case AINode_KeepDistance k:
                    return $"max {k.MaxSteps} steps · ideal {k.IdealDistance}";
                case AINode_If i:
                    int conds = i.Conditions != null ? i.Conditions.Count : 0;
                    string sel = i.TargetSelector != null ? i.TargetSelector.GetType().Name : "default target";
                    return $"{conds} condition(s) · {sel}";
                case AINode_Behavior b:
                    return b.Behavior != null ? b.Behavior.BehaviorName : "(no behavior)";
                default:
                    return null;
            }
        }

        // ---- per-subtype field lists (used by the side inspector) ---------

        public static string[] InlineFieldsOf(AIDecisionNode node)
        {
            switch (node)
            {
                case AINode_If _:           return _ifFields;
                case AINode_Behavior _:     return _behaviorFields;
                case AINode_Move _:         return _moveFields;
                case AINode_KeepDistance _: return _keepDistanceFields;
                default:                    return Array.Empty<string>();
            }
        }

        static readonly string[] _ifFields = { "TargetSelector", "Conditions" };
        static readonly string[] _behaviorFields = { "Behavior" };
        static readonly string[] _moveFields = { "MaxSteps", "StopAdjacent" };
        static readonly string[] _keepDistanceFields = { "MaxSteps", "IdealDistance" };
    }
}
