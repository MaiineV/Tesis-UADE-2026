using UnityEngine;
using UnityEngine.UIElements;

namespace Rollgeon.Editor.Tools.Polymorphic.Graph
{
    /// <summary>
    /// Turns "drag from a node's output connector" into "open the add menu" — never a real
    /// connection. This manipulator never constructs an
    /// <see cref="UnityEditor.Experimental.GraphView.Edge"/> and never calls
    /// <c>GraphView.AddElement</c> on one, so node-to-node wiring isn't just discouraged by
    /// <c>BlockGraphView.GetCompatiblePorts</c> returning empty — it's structurally impossible
    /// from this code path, independent of that guard.
    /// </summary>
    /// <remarks>
    /// Release over empty canvas → <see cref="BlockNodeView.OnAddMenuRequested"/> fires and the
    /// host shows the same add menu as right-click (<c>BlockGraphView.AppendAddItems</c>).
    /// Release over any node — including the one the drag started from — is a deliberate no-op,
    /// per spec §6.4: "soltar sobre un nodo no hace nada".
    /// </remarks>
    public sealed class PortAddDragManipulator : MouseManipulator
    {
        const float DragThreshold = 6f;

        readonly BlockNodeView _owner;
        Vector2 _startPosition;
        bool _dragging;

        public PortAddDragManipulator(BlockNodeView owner)
        {
            _owner = owner;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnDown);
            target.RegisterCallback<MouseMoveEvent>(OnMove);
            target.RegisterCallback<MouseUpEvent>(OnUp);
            target.RegisterCallback<MouseCaptureOutEvent>(OnCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMove);
            target.UnregisterCallback<MouseUpEvent>(OnUp);
            target.UnregisterCallback<MouseCaptureOutEvent>(OnCaptureOut);
        }

        void OnDown(MouseDownEvent evt)
        {
            if (!CanStartManipulation(evt)) return;
            _startPosition = evt.mousePosition;
            _dragging = false;
            target.CaptureMouse();
            evt.StopPropagation();
        }

        void OnMove(MouseMoveEvent evt)
        {
            if (!target.HasMouseCapture()) return;
            if (!_dragging && (evt.mousePosition - _startPosition).magnitude >= DragThreshold)
                _dragging = true;
        }

        void OnUp(MouseUpEvent evt)
        {
            if (!target.HasMouseCapture()) return;
            target.ReleaseMouse();

            bool wasDrag = _dragging;
            _dragging = false;
            if (!wasDrag) return;

            // Empty canvas only. Landing on any node (a Pick that resolves to a BlockNodeView
            // ancestor) is a no-op, whether it's another block or the one the drag started from.
            var picked = target.panel?.Pick(evt.mousePosition);
            var landedNode = picked?.GetFirstAncestorOfType<BlockNodeView>();
            if (landedNode != null) return;

            _owner.RaiseAddMenuRequested();
        }

        void OnCaptureOut(MouseCaptureOutEvent evt) => _dragging = false;
    }
}
