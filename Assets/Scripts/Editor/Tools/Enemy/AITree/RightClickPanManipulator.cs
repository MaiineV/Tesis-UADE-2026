using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rollgeon.Editor.Tools.Enemy.AITree
{
    /// <summary>
    /// Right-click + drag panea el GraphView. Right-click sin drag deja pasar el evento
    /// normal — el <c>BuildContextualMenu</c> del GraphView fira con sus entries (Set as
    /// Root, Cut/Copy/Paste/Delete/Duplicate). Vive junto al <see cref="ContentDragger"/>
    /// default (left + Alt) — no lo reemplaza.
    /// </summary>
    /// <remarks>
    /// Mecánica: en <c>MouseDown</c> registra posición de inicio + viewTransform; en
    /// <c>MouseMove</c> con botón derecho activo, si la distancia recorrida supera
    /// <see cref="DragThreshold"/>, entra en modo pan y traslada el viewTransform. En
    /// <c>MouseUp</c>, si hubo drag, llama <c>StopImmediatePropagation</c> para suprimir
    /// el contextual menu — el click fue un pan, no una intención de abrir menu.
    /// </remarks>
    public sealed class RightClickPanManipulator : MouseManipulator
    {
        private const float DragThreshold = 5f;

        private bool _active;
        private bool _moved;
        private Vector2 _startMouse;
        private Vector3 _startViewPos;

        public RightClickPanManipulator()
        {
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.RightMouse });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnDown);
            target.RegisterCallback<MouseMoveEvent>(OnMove);
            target.RegisterCallback<MouseUpEvent>(OnUp);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMove);
            target.UnregisterCallback<MouseUpEvent>(OnUp);
        }

        private void OnDown(MouseDownEvent evt)
        {
            if (!CanStartManipulation(evt)) return;
            var gv = target as GraphView;
            if (gv == null) return;

            _active = true;
            _moved = false;
            _startMouse = evt.mousePosition;
            _startViewPos = gv.viewTransform.position;
        }

        private void OnMove(MouseMoveEvent evt)
        {
            if (!_active) return;
            var gv = target as GraphView;
            if (gv == null) return;

            Vector2 delta = evt.mousePosition - _startMouse;
            if (!_moved && delta.magnitude > DragThreshold) _moved = true;
            if (!_moved) return;

            var newPos = new Vector3(
                _startViewPos.x + delta.x,
                _startViewPos.y + delta.y,
                _startViewPos.z);
            gv.UpdateViewTransform(newPos, gv.viewTransform.scale);
            evt.StopPropagation();
        }

        private void OnUp(MouseUpEvent evt)
        {
            if (!_active) return;
            if (evt.button != (int)MouseButton.RightMouse) return;

            bool wasDrag = _moved;
            _active = false;
            _moved = false;

            if (wasDrag)
            {
                // Right-click fue un pan, no una intención de abrir menu — suprimir el
                // contextual menu para que no aparezca al soltar.
                evt.StopImmediatePropagation();
            }
        }
    }
}
