using System;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Columna derecha de modificadores globales (items/pasivas). Se resuelve en cascada
    /// desde abajo: la entrada inferior popea, su número vuela al contador, se retira y
    /// las demás caen un slot. SIN LayoutGroup a propósito — las posiciones son por código
    /// para poder animar la caída.
    /// </summary>
    public sealed class GlobalModifierCascadeView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Template de entrada (hijo desactivado de EntriesRoot); se clona al pool.")]
        private ModifierEntryView _entryTemplate;

        [SerializeField]
        [Tooltip("Root de las entradas, pivot abajo — el índice 0 queda abajo de todo.")]
        private RectTransform _entriesRoot;

        [SerializeField] private float _entryHeight = 56f;
        [SerializeField] private float _spacing = 8f;

        [SerializeField]
        [Tooltip("Icono para fuentes sin sprite propio (los ItemSO aún no tienen Icon autorado).")]
        private Sprite _fallbackIcon;

        [SerializeField]
        [Tooltip("Opcional — visibilidad por alpha. Sin CanvasGroup se togglea el GameObject.")]
        private CanvasGroup _group;

        private readonly List<ModifierEntryView> _active = new List<ModifierEntryView>();
        private readonly Stack<ModifierEntryView> _pool = new Stack<ModifierEntryView>();

        public int Count => _active.Count;

        /// <summary>La próxima entrada a resolver (la de abajo). Null sin entradas.</summary>
        public ModifierEntryView Bottom => _active.Count > 0 ? _active[0] : null;

        /// <summary>
        /// Puebla el cascade. <paramref name="entries"/>[0] es la PRIMERA a resolver
        /// (queda abajo de todo).
        /// </summary>
        public void SetEntries(IReadOnlyList<(Sprite icon, string label)> entries)
        {
            ClearEntries();
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                var view = Rent();
                view.Show(entries[i].icon, entries[i].label, _fallbackIcon);
                view.Rect.anchoredPosition = SlotPosition(i);
                _active.Add(view);
            }
        }

        /// <summary>
        /// Retira la entrada inferior y deja caer las demás un slot. El pop/vuelo del
        /// número lo maneja el director ANTES de llamar acá.
        /// </summary>
        public void RemoveBottom(float fallSeconds, Action onDone)
        {
            if (_active.Count == 0)
            {
                onDone?.Invoke();
                return;
            }

            var removed = _active[0];
            _active.RemoveAt(0);
            Release(removed);

            for (int i = 0; i < _active.Count; i++)
            {
                var rect = _active[i].Rect;
                if (fallSeconds <= 0f) rect.anchoredPosition = SlotPosition(i);
                else Tween.UIAnchoredPosition(rect, SlotPosition(i), fallSeconds, Ease.OutQuad);
            }

            if (fallSeconds <= 0f || onDone == null) { onDone?.Invoke(); return; }
            Tween.Delay(fallSeconds, onDone);
        }

        public void SetVisible(bool visible)
        {
            if (_group != null)
            {
                _group.alpha = visible ? 1f : 0f;
                _group.blocksRaycasts = false;
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }

        public void ClearEntries()
        {
            for (int i = 0; i < _active.Count; i++) Release(_active[i]);
            _active.Clear();
        }

        private Vector2 SlotPosition(int index)
            => new Vector2(0f, index * (_entryHeight + _spacing));

        private ModifierEntryView Rent()
        {
            if (_pool.Count > 0) return _pool.Pop();
            var view = Instantiate(_entryTemplate, _entriesRoot != null ? _entriesRoot : (RectTransform)transform);
            return view;
        }

        private void Release(ModifierEntryView view)
        {
            if (view == null) return;
            view.Hide();
            _pool.Push(view);
        }
    }
}
