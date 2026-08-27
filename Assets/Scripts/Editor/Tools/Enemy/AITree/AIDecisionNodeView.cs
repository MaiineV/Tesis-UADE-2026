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
        Label _issueChip;

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

        // ---- issue badge ---------------------------------------------------

        /// <summary>Badge de diagnóstico (peor severidad del nodo); el mensaje completo va al tooltip.</summary>
        public void SetIssue(string message, IssueSeverity severity)
        {
            if (_issueChip == null)
            {
                _issueChip = new Label
                {
                    style =
                    {
                        fontSize = 9,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        color = new Color(0.10f, 0.10f, 0.10f),
                        paddingLeft = 4, paddingRight = 4, paddingTop = 1, paddingBottom = 1,
                        marginLeft = 2, marginRight = 4, marginTop = 2,
                        borderTopLeftRadius = 3, borderTopRightRadius = 3,
                        borderBottomLeftRadius = 3, borderBottomRightRadius = 3,
                    },
                };
                // titleButtonContainer vive a la derecha del título; así no compite con los chips
                // de categoría/raíz que se insertan por índice en titleContainer.
                titleButtonContainer.Insert(0, _issueChip);
            }

            Color bg; string text;
            switch (severity)
            {
                case IssueSeverity.Error:   bg = new Color(0.90f, 0.30f, 0.30f); text = "error"; break;
                case IssueSeverity.Warning: bg = new Color(0.95f, 0.72f, 0.20f); text = "aviso"; break;
                default:                    bg = new Color(0.55f, 0.70f, 0.90f); text = "info";  break;
            }
            _issueChip.text = text;
            _issueChip.tooltip = message;
            _issueChip.style.backgroundColor = bg;
            _issueChip.style.display = DisplayStyle.Flex;
        }

        public void ClearIssue()
        {
            if (_issueChip != null) _issueChip.style.display = DisplayStyle.None;
        }

        void BuildRootChip()
        {
            _rootChip = new Label("RAÍZ")
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

        public static string CategoryLabel(AINodeCategory category)
        {
            switch (category)
            {
                case AINodeCategory.Composite: return "Compuesto";
                case AINodeCategory.Branching: return "Ramificación";
                default:                       return "Hoja";
            }
        }

        void BuildHeader()
        {
            var meta = AINodeRegistry.Find(Data.GetType());
            var category = meta?.Category ?? AINodeCategory.Leaf;

            var chip = new Label(CategoryLabel(category));
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
            InputPort.portName = "entrada";
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
            port.portName = AITreeTopology.PortLabel(slot, null);
            port.userData = slotIndex;
            outputContainer.Add(port);
            _outputPorts.Add(port);
            return port;
        }

        /// <summary>Quita un puerto libre sobrante. Los puertos no son GraphElements: no dispara graphViewChanged.</summary>
        public void RemoveOutputPort(Port port)
        {
            if (port == null || !_outputPorts.Remove(port)) return;
            if (port.parent == outputContainer) outputContainer.Remove(port);
            RefreshPorts();
        }

        public void SetPortLabel(Port port, string label)
        {
            if (port != null && port.portName != label) port.portName = label;
        }

        /// <summary>Reordena los puertos de salida dados moviéndolos al final en ese orden (los edges siguen a su puerto).</summary>
        public void ReorderOutputPorts(IReadOnlyList<Port> ordered)
        {
            foreach (var p in ordered)
            {
                if (p.parent != outputContainer) continue;
                outputContainer.Remove(p);
                outputContainer.Add(p);
            }
            _outputPorts.Clear();
            foreach (var child in outputContainer.Children())
                if (child is Port cp) _outputPorts.Add(cp);
            RefreshPorts();
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
                    string mTarget = m.TargetSelector != null ? m.TargetSelector.GetType().Name : "jugador";
                    string mRange = m.DesiredRange != null ? Describe(m.DesiredRange) : (m.StopAdjacent ? "1" : "0");
                    return $"→ {mTarget} · distancia {mRange} · máx. {Describe(m.MaxSteps)} pasos{(m.Retreat ? " · kitea" : "")}";
                case AINode_KeepDistance k:
                    return $"máx. {Describe(k.MaxSteps)} pasos · ideal {Describe(k.IdealDistance)}";
                case AINode_If i:
                    int conds = i.Conditions != null ? i.Conditions.Count : 0;
                    string sel = i.TargetSelector != null ? i.TargetSelector.GetType().Name : "objetivo por defecto";
                    return conds == 0 ? $"sin condiciones (siempre pasa) · {sel}" : $"{conds} condición(es) · {sel}";
                case AINode_While w:
                    int wconds = w.Conditions != null ? w.Conditions.Count : 0;
                    string wsel = w.TargetSelector != null ? w.TargetSelector.GetType().Name : "objetivo por defecto";
                    return $"mientras {wconds} cond. · máx. {w.MaxIterations} · {wsel}";
                case AINode_Behavior b:
                    return b.Behavior != null ? b.Behavior.BehaviorName : "(sin behavior)";
                case AINode_TelegraphMark t:
                    return $"telegraph {t.Shape} · tamaño {t.Size} · daño {t.Damage}";
                case AINode_ExecuteTelegraph e:
                    return string.IsNullOrEmpty(e.WindupFeedbackId)
                        ? "resuelve el telegraph pendiente"
                        : $"resuelve el telegraph pendiente · {e.WindupFeedbackId}";
                case AINode_RotateBlock r:
                    return $"rota {r.Target} ×{r.Count}";
                case AINode_PromulgateRule p:
                    return $"regla cada {p.IntervalPhase1}/{p.IntervalPhase2} turnos · {(p.EnabledRules != null ? p.EnabledRules.Count : 0)} activas";
                case AINode_ApplyStatModifier a:
                    return $"ATQ {a.AttackDelta:+0;-0;0} · VEL {a.SpeedDelta:+0;-0;0} → fase {a.PhaseIndex}";
                case AINode_Once _:
                    return "ejecuta el hijo una sola vez";
                case AINode_Random r:
                    // Siempre texto no vacío: BuildSummary solo crea el label si el primer
                    // summary tiene contenido, y los pesos aparecen recién al conectar hijos.
                    if (r.Options == null || r.Options.Count == 0) return "sin opciones";
                    var parts = new List<string>(r.Options.Count);
                    foreach (var o in r.Options) parts.Add(o.Weight.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                    return "pesos: " + string.Join(" / ", parts);
                default:
                    return null;
            }
        }

        static string Describe(AIIntReader reader)
        {
            if (reader == null) return "?";
            if (reader is AIConstantInt c) return c.Value.ToString();
            if (reader is AIReadSelfStat self) return $"Propio.{self.Stat}";
            if (reader is AIReadPlayerStat player) return $"Jugador.{player.Stat}";
            return reader.GetType().Name;
        }

    }
}
