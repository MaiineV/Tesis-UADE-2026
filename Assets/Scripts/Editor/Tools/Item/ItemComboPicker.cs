using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// El control para elegir a qué combos escucha un hook: un botón que resume la selección y
    /// abre un desplegable de selección múltiple.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vive suelto y no dentro de la ventana porque lo usan dos: el panel del ítem y el asistente
    /// de creación, que es otra clase.
    /// </para>
    /// <para>
    /// No es un <c>MaskField</c> porque el de Unity se cierra en cada clic: marcar tres combos son
    /// tres aperturas. Este queda abierto hasta que se hace clic afuera.
    /// </para>
    /// </remarks>
    public static class ItemComboPicker
    {
        /// <summary>"combo.full_house" → "full house". El prefijo es igual en todos y sólo gasta ancho.</summary>
        public static string ShortLabel(string id) =>
            string.IsNullOrEmpty(id) ? id : id.Replace("combo.", string.Empty).Replace('_', ' ');

        /// <summary>Qué dice el botón cerrado. Con más de dos, el detalle no entra y no aporta.</summary>
        public static string Summary(IReadOnlyList<string> selected, int total)
        {
            if (selected == null || selected.Count == 0) return "Ningún combo";
            if (selected.Count == total) return "Todos los combos";
            if (selected.Count <= 2) return string.Join(", ", selected.Select(ShortLabel));
            return selected.Count + " combos";
        }

        /// <summary>
        /// Dibuja el botón y, al apretarlo, abre el desplegable.
        /// </summary>
        /// <param name="onToggle">Se llama por cada tilde, no al cerrar.</param>
        /// <param name="onSetAll">"Todos" (<c>true</c>) o "Ninguno" (<c>false</c>).</param>
        /// <param name="onClosed">Al cerrar el popup. Acá va el aviso caro (reconstruir el grafo).</param>
        public static void Draw(
            IReadOnlyList<string> allIds, IReadOnlyList<string> selected,
            System.Action<string, bool> onToggle,
            System.Action<bool> onSetAll,
            System.Action onClosed = null)
        {
            var rect = EditorGUILayout.GetControlRect();
            if (!EditorGUI.DropdownButton(
                    rect, new GUIContent(Summary(selected, allIds.Count)), FocusType.Keyboard))
                return;

            PopupWindow.Show(rect, new Dropdown(
                allIds, rect.width, id => selected.Contains(id), onToggle, onSetAll, onClosed));
        }

        /// <summary>
        /// Desplegable de selección múltiple que no se cierra al marcar.
        /// </summary>
        /// <remarks>
        /// Escribe a medida que se clickea, no al cerrar: cerrar un popup de Unity es hacer clic
        /// afuera, y un cambio que solo se guarda ahí se pierde si el usuario aprieta Escape.
        /// </remarks>
        sealed class Dropdown : PopupWindowContent
        {
            const float RowHeight = 18f;
            const float HeaderHeight = 24f;
            const float MaxHeight = 320f;

            readonly IReadOnlyList<string> _ids;
            readonly float _width;
            readonly System.Func<string, bool> _isOn;
            readonly System.Action<string, bool> _toggle;
            readonly System.Action<bool> _setAll;
            readonly System.Action _onClosed;
            Vector2 _scroll;

            public Dropdown(
                IReadOnlyList<string> ids, float width,
                System.Func<string, bool> isOn,
                System.Action<string, bool> toggle,
                System.Action<bool> setAll,
                System.Action onClosed)
            {
                _ids = ids;
                _width = Mathf.Max(180f, width);
                _isOn = isOn;
                _toggle = toggle;
                _setAll = setAll;
                _onClosed = onClosed;
            }

            /// <summary>Recién acá se le avisa al grafo: una reconstrucción, no una por tilde.</summary>
            public override void OnClose() => _onClosed?.Invoke();

            public override Vector2 GetWindowSize() =>
                new Vector2(_width, Mathf.Min(MaxHeight, HeaderHeight + _ids.Count * RowHeight + 8f));

            public override void OnGUI(Rect rect)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    if (GUILayout.Button("Todos", EditorStyles.toolbarButton)) _setAll(true);
                    if (GUILayout.Button("Ninguno", EditorStyles.toolbarButton)) _setAll(false);
                }

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                foreach (var id in _ids)
                {
                    bool on = _isOn(id);
                    bool next = EditorGUILayout.ToggleLeft(
                        ShortLabel(id), on, GUILayout.Height(RowHeight));
                    if (next != on) _toggle(id, next);
                }
                EditorGUILayout.EndScrollView();
            }
        }

    }
}
