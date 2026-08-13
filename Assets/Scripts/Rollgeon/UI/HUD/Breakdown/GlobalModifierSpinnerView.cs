using System;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Recuadro fijo de modificadores globales (items/pasivas) estilo spinner de
    /// matchmaking: muestra UNA entrada a la vez y rota al siguiente como un
    /// tambor. El 3D se fakea con dos slots alternantes — el saliente sube
    /// comprimiéndose en Y y el entrante llega desde abajo (<see cref="SpinnerDrumMath"/>).
    /// El pop y el vuelo del número al contador los maneja el director ANTES de
    /// llamar <see cref="AdvanceToNext"/>.
    /// </summary>
    public sealed class GlobalModifierSpinnerView : MonoBehaviour
    {
        /// <summary>Spin-in de la primera entrada al poblar (par del slide-in del cascade viejo).</summary>
        private const float SpinInSeconds = 0.18f;

        [SerializeField]
        [Tooltip("Slot A del tambor (ModifierEntryView bajo SlotsRoot).")]
        private ModifierEntryView _slotA;

        [SerializeField]
        [Tooltip("Slot B del tambor — arranca inactivo, alterna con A en cada rotación.")]
        private ModifierEntryView _slotB;

        [SerializeField]
        [Tooltip("Ventana del tambor (con RectMask2D): define el recorrido del spin.")]
        private RectTransform _slotsRoot;

        [SerializeField]
        [Tooltip("Icono para fuentes sin sprite propio (los ItemSO aún no tienen Icon autorado).")]
        private Sprite _fallbackIcon;

        [SerializeField]
        [Tooltip("Opcional — visibilidad por alpha. Sin CanvasGroup se togglea el GameObject.")]
        private CanvasGroup _group;

        private readonly List<(Sprite icon, string label)> _entries = new List<(Sprite, string)>();
        private int _index;
        private bool _aIsActive = true;
        private Tween _spinTween;

        /// <summary>Entradas restantes, incluida la visible. 0 = tambor vacío.</summary>
        public int Count => Mathf.Max(0, _entries.Count - _index);

        /// <summary>El slot activo en la ventana (origen del pop y del vuelo). Null sin entradas.</summary>
        public ModifierEntryView Current => Count > 0 ? ActiveSlot : null;

        private ModifierEntryView ActiveSlot => _aIsActive ? _slotA : _slotB;
        private ModifierEntryView InactiveSlot => _aIsActive ? _slotB : _slotA;

        /// <summary>
        /// Puebla el tambor. <paramref name="entries"/>[0] es la primera a resolver
        /// (queda visible en la ventana). Con <paramref name="animated"/>, la primera
        /// entrada entra rotando desde abajo.
        /// </summary>
        public void SetEntries(IReadOnlyList<(Sprite icon, string label)> entries, bool animated = false)
        {
            ClearEntries();
            if (entries == null || entries.Count == 0) return;

            for (int i = 0; i < entries.Count; i++) _entries.Add(entries[i]);

            var slot = ActiveSlot;
            if (slot == null) return;
            slot.Show(_entries[0].icon, _entries[0].label, _fallbackIcon);

            bool spin = animated && Application.isPlaying && !DiceAnim.DiceUiMotionPrefs.ReducedMotion;
            if (!spin)
            {
                ResetSlot(slot, visible: true);
                return;
            }

            float travel = TravelFor(slot);
            ApplyIncoming(slot, 0f, travel); // pose inicial antes del primer frame del tween
            _spinTween = Tween.Custom(0f, 1f, SpinInSeconds, t =>
            {
                float te = SpinnerDrumMath.EaseSpin(t);
                ApplyIncoming(slot, te, travel);
            }).OnComplete(() => ResetSlot(slot, visible: true));
        }

        /// <summary>
        /// Rota el tambor a la siguiente entrada: el slot activo sale hacia arriba
        /// comprimiéndose y el siguiente entra desde abajo; con la última entrada
        /// rota a vacío. <paramref name="onDone"/> se invoca EXACTAMENTE una vez al
        /// frenar (contrato del director — la secuencia se cuelga si falta).
        /// Los segundos llegan ya escalados por D()/ramp — acá no se re-dividen.
        /// </summary>
        public void AdvanceToNext(float spinSeconds, Action onDone)
        {
            if (Count == 0)
            {
                onDone?.Invoke();
                return;
            }

            if (_spinTween.isAlive) _spinTween.Stop();

            _index++;
            bool hasNext = _index < _entries.Count;
            var outgoing = ActiveSlot;
            var incoming = InactiveSlot;

            if (spinSeconds <= 0f || !Application.isPlaying || DiceAnim.DiceUiMotionPrefs.ReducedMotion)
            {
                ResetSlot(outgoing, visible: false);
                if (hasNext && incoming != null)
                {
                    incoming.Show(_entries[_index].icon, _entries[_index].label, _fallbackIcon);
                    ResetSlot(incoming, visible: true);
                    _aIsActive = !_aIsActive;
                }
                onDone?.Invoke();
                return;
            }

            // El punch del director pudo dejar el scale a mitad de camino bajo skip;
            // el tambor escribe scaleY absoluto por frame, pero X/Z quedan.
            if (outgoing != null) outgoing.transform.localScale = Vector3.one;

            float travel = TravelFor(outgoing);
            if (hasNext && incoming != null)
            {
                incoming.Show(_entries[_index].icon, _entries[_index].label, _fallbackIcon);
                ApplyIncoming(incoming, 0f, travel); // pose inicial antes del primer frame del tween
            }

            _spinTween = Tween.Custom(0f, 1f, spinSeconds, t =>
            {
                float te = SpinnerDrumMath.EaseSpin(t);
                ApplyOutgoing(outgoing, te, travel);
                if (hasNext) ApplyIncoming(incoming, te, travel);
            }).OnComplete(() =>
            {
                ResetSlot(outgoing, visible: false);
                if (hasNext)
                {
                    ResetSlot(incoming, visible: true);
                    _aIsActive = !_aIsActive;
                }
                onDone?.Invoke();
            });
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

        /// <summary>
        /// Corta cualquier rotación viva y deja ambos slots en reposo ocultos.
        /// Stop() no dispara OnComplete, así el onDone capturado no se invoca
        /// post-abort (EndSequence/ForceFinalState ya no lo esperan).
        /// </summary>
        public void ClearEntries()
        {
            if (_spinTween.isAlive) _spinTween.Stop();
            ResetSlot(_slotA, visible: false);
            ResetSlot(_slotB, visible: false);
            _entries.Clear();
            _index = 0;
            _aIsActive = true;
        }

        private static void ApplyOutgoing(ModifierEntryView slot, float te, float travel)
        {
            if (slot == null) return;
            var scale = slot.transform.localScale;
            scale.y = SpinnerDrumMath.OutgoingScaleY(te);
            slot.transform.localScale = scale;
            var pos = slot.Rect.anchoredPosition;
            pos.y = SpinnerDrumMath.OutgoingOffsetY(te, travel);
            slot.Rect.anchoredPosition = pos;
        }

        private static void ApplyIncoming(ModifierEntryView slot, float te, float travel)
        {
            if (slot == null) return;
            var scale = slot.transform.localScale;
            scale.y = SpinnerDrumMath.IncomingScaleY(te);
            slot.transform.localScale = scale;
            var pos = slot.Rect.anchoredPosition;
            pos.y = SpinnerDrumMath.IncomingOffsetY(te, travel);
            slot.Rect.anchoredPosition = pos;
        }

        private static void ResetSlot(ModifierEntryView slot, bool visible)
        {
            if (slot == null) return;
            slot.transform.localScale = Vector3.one;
            slot.Rect.anchoredPosition = Vector2.zero;
            if (!visible) slot.Hide();
        }

        private float TravelFor(ModifierEntryView slot)
        {
            float visible = _slotsRoot != null ? _slotsRoot.rect.height : 84f;
            float slotH = slot != null ? slot.Rect.rect.height : 56f;
            return SpinnerDrumMath.Travel(visible, slotH);
        }
    }
}
