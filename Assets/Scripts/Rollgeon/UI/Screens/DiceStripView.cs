using System;
using System.Collections.Generic;
using Patterns;
using PrimeTween;
using Rollgeon.Audio;
using Rollgeon.Dice;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Tira horizontal de la bolsa en armado (<see cref="BuildSelectionScreen"/>):
    /// un dado clickeable por entrada, siempre ordenada de menor a mayor. Agregar
    /// cae desde arriba con settle; quitar se encoge y desvanece; en ambos casos
    /// los vecinos se deslizan a su nueva posición. Click en un dado emite
    /// <see cref="OnDieClicked"/> (la screen decide quitarlo).
    /// </summary>
    /// <remarks>
    /// Modelo sincrónico + diff (patrón <c>ChipStackView</c>): la vista diffea la
    /// bolsa ordenada entrante contra su modelo y anima solo la edición única
    /// (inserción/remoción); cambios mayores (limpiar) reconstruyen staggered.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Dice Strip View")]
    public sealed class DiceStripView : MonoBehaviour
    {
        private DiceBuildUiSettingsSO _settings;

        private sealed class DieEntry
        {
            public RectTransform Rect;
            public Image Image;
            public CanvasGroup Group;
            public Button Button;
            public DiceType Type;
        }

        private readonly List<DiceType> _model = new List<DiceType>();
        private readonly List<DieEntry> _entries = new List<DieEntry>();
        private readonly List<DieEntry> _dying = new List<DieEntry>();
        private readonly Stack<DieEntry> _pool = new Stack<DieEntry>();

        /// <summary>Click en un dado de la tira. Payload = tipo del dado clickeado.</summary>
        public event Action<DiceType> OnDieClicked;

        /// <summary>Cantidad de dados del modelo lógico.</summary>
        public int DisplayedCount => _model.Count;

        public void Configure(DiceBuildUiSettingsSO settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Lleva la tira al estado <paramref name="sortedBag"/> (YA ordenado
        /// ascendente por el caller). Anima la edición salvo estado instantáneo.
        /// </summary>
        public void SetDice(IReadOnlyList<DiceType> sortedBag, bool animate = true)
        {
            if (_settings == null || sortedBag == null) return;

            var diff = DiceStripMath.ComputeDiff(_model, sortedBag);
            if (diff.Change == DiceStripMath.StripChange.None) return;

            bool instant = !animate
                           || !Application.isPlaying
                           || !gameObject.activeInHierarchy
                           || Rollgeon.UI.HUD.DiceAnim.DiceUiMotionPrefs.ReducedMotion;

            switch (diff.Change)
            {
                case DiceStripMath.StripChange.Insert:
                    InsertAt(diff.Index, sortedBag[diff.Index], instant);
                    PlaySfx(_settings.AddClip);
                    break;

                case DiceStripMath.StripChange.Remove:
                    RemoveAt(diff.Index, instant);
                    PlaySfx(_settings.RemoveClip);
                    break;

                case DiceStripMath.StripChange.Rebuild:
                    RebuildAll(sortedBag, instant);
                    if (sortedBag.Count == 0 && _entries.Count == 0) PlaySfx(_settings.ClearClip);
                    break;
            }

            _model.Clear();
            for (int i = 0; i < sortedBag.Count; i++) _model.Add(sortedBag[i]);

            Reflow(instant);
        }

        /// <summary>Estado final sin animación; recicla todo lo que estaba en vuelo.</summary>
        public void SnapToTarget()
        {
            for (int i = _dying.Count - 1; i >= 0; i--) Recycle(_dying[i], fromDying: true);
            Reflow(instant: true);
        }

        // -----------------------------------------------------------------
        // Ediciones
        // -----------------------------------------------------------------

        private void InsertAt(int index, DiceType type, bool instant)
        {
            var entry = _pool.Count > 0 ? _pool.Pop() : CreateEntry();
            entry.Type = type;
            entry.Image.sprite = _settings.GetSprite(type);
            entry.Rect.sizeDelta = new Vector2(_settings.DieSize, _settings.DieSize);
            entry.Group.alpha = 1f;
            entry.Rect.localScale = Vector3.one;
            entry.Rect.gameObject.SetActive(true);
            entry.Rect.SetSiblingIndex(index);
            _entries.Insert(index, entry);

            float targetX = DiceStripMath.SlotX(index, _entries.Count, _settings.Spacing);
            if (instant)
            {
                entry.Rect.anchoredPosition = new Vector2(targetX, 0f);
                return;
            }

            // Cae desde arriba y asienta con OutBack (settle).
            entry.Rect.anchoredPosition = new Vector2(targetX, _settings.DropHeight);
            Tween.UIAnchoredPositionY(entry.Rect, 0f, _settings.DropDuration,
                _settings.DropEase, useUnscaledTime: true);
        }

        private void RemoveAt(int index, bool instant)
        {
            if (index < 0 || index >= _entries.Count) return;
            var entry = _entries[index];
            _entries.RemoveAt(index);

            if (instant)
            {
                Recycle(entry, fromDying: false);
                return;
            }

            _dying.Add(entry);
            entry.Button.interactable = false;
            Tween.Scale(entry.Rect, 0f, _settings.RemoveDuration, _settings.RemoveEase, useUnscaledTime: true);
            Tween.Alpha(entry.Group, 0f, _settings.RemoveDuration, useUnscaledTime: true)
                .OnComplete(this, self => self.RecycleFromDying(entry));
        }

        private void RebuildAll(IReadOnlyList<DiceType> target, bool instant)
        {
            // Salida de lo existente (staggered al limpiar).
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                _entries.RemoveAt(i);

                if (instant)
                {
                    Recycle(entry, fromDying: false);
                    continue;
                }

                _dying.Add(entry);
                entry.Button.interactable = false;
                float delay = i * _settings.ClearStagger;
                Tween.Scale(entry.Rect, 0f, _settings.RemoveDuration, _settings.RemoveEase,
                    startDelay: delay, useUnscaledTime: true);
                Tween.Alpha(entry.Group, 0f, _settings.RemoveDuration,
                        startDelay: delay, useUnscaledTime: true)
                    .OnComplete(this, self => self.RecycleFromDying(entry));
            }

            // Entrada del estado nuevo (resync — normalmente vacío al limpiar).
            for (int i = 0; i < target.Count; i++)
            {
                InsertAt(i, target[i], instant: true);
            }
        }

        private void Reflow(bool instant)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var rect = _entries[i].Rect;
                float targetX = DiceStripMath.SlotX(i, _entries.Count, _settings.Spacing);
                if (Mathf.Approximately(rect.anchoredPosition.x, targetX)) continue;

                if (instant)
                {
                    rect.anchoredPosition = new Vector2(targetX, rect.anchoredPosition.y);
                }
                else
                {
                    Tween.UIAnchoredPositionX(rect, targetX, _settings.SlideDuration,
                        _settings.SlideEase, useUnscaledTime: true);
                }
            }
        }

        // -----------------------------------------------------------------
        // Pool de entries
        // -----------------------------------------------------------------

        private DieEntry CreateEntry()
        {
            var go = new GameObject("Die", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, worldPositionStays: false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;

            var entry = new DieEntry
            {
                Rect = rect,
                Image = image,
                Group = go.GetComponent<CanvasGroup>(),
                Button = button,
                Type = DiceType.D4,
            };
            button.onClick.AddListener(() => OnDieClicked?.Invoke(entry.Type));
            return entry;
        }

        private void RecycleFromDying(DieEntry entry) => Recycle(entry, fromDying: true);

        private void Recycle(DieEntry entry, bool fromDying)
        {
            if (fromDying && !_dying.Remove(entry)) return;
            if (entry.Rect == null) return; // destruida por teardown de escena

            Tween.StopAll(onTarget: entry.Rect);
            Tween.StopAll(onTarget: entry.Group);
            entry.Rect.localScale = Vector3.one;
            entry.Group.alpha = 1f;
            entry.Button.interactable = true;
            entry.Rect.gameObject.SetActive(false);
            _pool.Push(entry);
        }

        private void PlaySfx(AudioClip clip)
        {
            if (clip == null || _settings == null || !Application.isPlaying) return;
            if (!ServiceLocator.TryGetService<IAudioService>(out var audio) || audio == null) return;
            float pitch = UnityEngine.Random.Range(_settings.SfxPitchRange.x, _settings.SfxPitchRange.y);
            audio.PlaySfx2D(clip, _settings.SfxVolume, pitch);
        }

        private void OnDisable()
        {
            Tween.StopAll(onTarget: this);
            foreach (var entry in _entries)
            {
                if (entry.Rect == null) continue;
                Tween.StopAll(onTarget: entry.Rect);
                Tween.StopAll(onTarget: entry.Group);
                entry.Rect.localScale = Vector3.one;
                entry.Group.alpha = 1f;
            }
            SnapToTarget();
        }
    }
}
