using System;
using System.Collections.Generic;
using System.Globalization;
using Rollgeon.Combat.AI.Decisions;

namespace Rollgeon.Editor.Tools.Enemy.AITree
{
    /// <summary>
    /// Per-subtype knowledge of how an <see cref="AIDecisionNode"/> connects to its children.
    /// Encapsulated here so the GraphView/serializer/auto-layout don't repeat type checks.
    /// </summary>
    /// <remarks>
    /// Cada compuesto nuevo tiene que entrar en los cuatro switches de abajo. Un compuesto que
    /// falte se dibuja como hoja sin puertos y sus hijos desaparecen del canvas (pasó con
    /// <see cref="AINode_Alternate"/> en tres jefes). El test por reflexión
    /// <c>AITreeSerializerTests.EveryNodeWithTopologyFields_HasSlots</c> lo detecta.
    /// </remarks>
    public static class AITreeTopology
    {
        public readonly struct Slot
        {
            public readonly string Name;     // identificador estable (tests, sidecar); no se muestra
            public readonly string Label;    // texto del puerto, en español
            public readonly bool IsDynamic;  // true → user can add more children of this slot kind
            public Slot(string name, string label, bool isDynamic) { Name = name; Label = label; IsDynamic = isDynamic; }
        }

        /// <summary>
        /// Texto de un puerto de salida. Slot fijo → su label. Slot dinámico: el puerto libre es
        /// "+"; el conectado muestra el ordinal (orden de ejecución) y, en Random, el peso.
        /// </summary>
        public static string PortLabel(Slot slot, int? ordinal, float? weight = null)
        {
            if (!slot.IsDynamic) return slot.Label;
            if (ordinal == null) return "+";
            return weight.HasValue
                ? $"{ordinal.Value} · peso {weight.Value.ToString("0.##", CultureInfo.InvariantCulture)}"
                : ordinal.Value.ToString(CultureInfo.InvariantCulture);
        }

        public static IReadOnlyList<Slot> SlotsOf(AIDecisionNode node)
        {
            switch (node)
            {
                case AINode_Selector _:  return _dynamicChildren;
                case AINode_Sequence _:  return _dynamicChildren;
                case AINode_Alternate _: return _dynamicChildren;
                case AINode_If _:        return _ifSlots;
                case AINode_Random _:    return _randomOptions;
                case AINode_While _:     return _whileSlots;
                case AINode_Once _:      return _onceSlot;
                default:                 return Array.Empty<Slot>(); // leaves
            }
        }

        public static IReadOnlyList<AIDecisionNode> ChildrenOf(AIDecisionNode node, out IReadOnlyList<int> slotIndices)
        {
            var children = new List<AIDecisionNode>();
            var slots = new List<int>();
            switch (node)
            {
                case AINode_Selector s:
                    if (s.Children != null) foreach (var c in s.Children) { children.Add(c); slots.Add(0); }
                    break;
                case AINode_Sequence s:
                    if (s.Children != null) foreach (var c in s.Children) { children.Add(c); slots.Add(0); }
                    break;
                case AINode_Alternate a:
                    if (a.Children != null) foreach (var c in a.Children) { children.Add(c); slots.Add(0); }
                    break;
                case AINode_If i:
                    children.Add(i.Then); slots.Add(0);
                    children.Add(i.Else); slots.Add(1);
                    break;
                case AINode_Random r:
                    if (r.Options != null) foreach (var o in r.Options) { children.Add(o.Node); slots.Add(0); }
                    break;
                case AINode_While w:
                    children.Add(w.Body); slots.Add(0);
                    break;
                case AINode_Once o:
                    children.Add(o.Child); slots.Add(0);
                    break;
            }
            slotIndices = slots;
            return children;
        }

        /// <summary>
        /// Guarda en <paramref name="into"/> el dato por-edge que <see cref="ClearChildren"/>
        /// destruye y <see cref="AppendChild"/> no puede reinventar. Hoy solo el peso de
        /// <see cref="AINode_Random"/>: el nodo es la fuente de verdad (el inspector lo edita
        /// in-place), así que se captura de ahí justo antes de reconstruir la topología.
        /// </summary>
        public static void CaptureEdgeWeights(
            AIDecisionNode node, Dictionary<(AIDecisionNode parent, AIDecisionNode child), float> into)
        {
            if (!(node is AINode_Random r) || r.Options == null) return;
            foreach (var o in r.Options)
            {
                if (o.Node == null) continue;
                into[(node, o.Node)] = o.Weight;
            }
        }

        public static void ClearChildren(AIDecisionNode node)
        {
            switch (node)
            {
                case AINode_Selector s: s.Children = new List<AIDecisionNode>(); break;
                case AINode_Sequence s: s.Children = new List<AIDecisionNode>(); break;
                case AINode_Alternate a: a.Children = new List<AIDecisionNode>(); break;
                case AINode_If i: i.Then = null; i.Else = null; break;
                case AINode_Random r: r.Options = new List<AINode_Random.Option>(); break;
                case AINode_While w: w.Body = null; break;
                case AINode_Once o: o.Child = null; break;
            }
        }

        /// <summary>
        /// Append <paramref name="child"/> into <paramref name="parent"/> at <paramref name="slotIndex"/>.
        /// For dynamic-children slots (Selector/Sequence/Alternate/Random), <paramref name="slotIndex"/>
        /// is always 0 and order is determined by call order — caller must invoke in left-to-right order.
        /// <paramref name="weight"/> solo lo consume <see cref="AINode_Random"/>.
        /// </summary>
        public static void AppendChild(AIDecisionNode parent, int slotIndex, AIDecisionNode child, float weight = 1f)
        {
            switch (parent)
            {
                case AINode_Selector s: s.Children.Add(child); break;
                case AINode_Sequence s: s.Children.Add(child); break;
                case AINode_Alternate a: a.Children.Add(child); break;
                case AINode_If i:
                    if (slotIndex == 0) i.Then = child;
                    else i.Else = child;
                    break;
                case AINode_Random r:
                    r.Options.Add(new AINode_Random.Option { Node = child, Weight = weight });
                    break;
                case AINode_While w:
                    w.Body = child;
                    break;
                case AINode_Once o:
                    o.Child = child;
                    break;
            }
        }

        /// <summary>True if the node accepts at least one outgoing connection.</summary>
        public static bool CanHaveChildren(AIDecisionNode node) => SlotsOf(node).Count > 0;

        /// <summary>
        /// <c>true</c> si un miembro con ese tipo declarado es topología (un hijo o una lista de
        /// hijos) y no un parámetro. El inspector genérico lo usa para no dibujar los hijos
        /// inline — esos se editan por los puertos del grafo.
        /// </summary>
        public static bool IsTopologyMember(Type declaredType)
        {
            if (declaredType == null) return false;
            if (typeof(AIDecisionNode).IsAssignableFrom(declaredType)) return true;
            if (typeof(IList<AIDecisionNode>).IsAssignableFrom(declaredType)) return true;
            if (declaredType == typeof(List<AINode_Random.Option>)) return true;
            return false;
        }

        // ---- canonical slot configurations -------------------------------

        static readonly Slot[] _dynamicChildren = { new Slot("Children", "Hijos", true) };
        static readonly Slot[] _ifSlots = { new Slot("Then", "Entonces", false), new Slot("Else", "Si no", false) };
        static readonly Slot[] _randomOptions = { new Slot("Options", "Opciones", true) };
        static readonly Slot[] _whileSlots = { new Slot("Body", "Cuerpo", false) };
        static readonly Slot[] _onceSlot = { new Slot("Child", "Hijo", false) };
    }
}
