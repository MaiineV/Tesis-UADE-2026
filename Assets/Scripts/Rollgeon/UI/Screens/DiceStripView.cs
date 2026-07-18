using System;
using System.Collections.Generic;
using Patterns;
using PrimeTween;
using Rollgeon.Audio;
using Rollgeon.Dice;
using UnityEngine;
using UnityEngine.EventSystems;
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
            public bool Removing;
        }

        /// <summary>Relay de hover por dado — la view anima la escala.</summary>
        private sealed class DieHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Action<bool> HoverChanged;
            public void OnPointerEnter(PointerEventData eventData) => HoverChanged?.Invoke(true);
            public void OnPointerExit(PointerEventData eventData) => HoverChanged?.Invoke(false);
        }

        private sealed class BurstParticle
        {
            public RectTransform Rect;
            public Image Image;
        }

        private readonly List<DiceType> _model = new List<DiceType>();
        private readonly List<DieEntry> _entries = new List<DieEntry>();
        private readonly List<DieEntry> _dying = new List<DieEntry>();
        private readonly Stack<DieEntry> _pool = new Stack<DieEntry>();
        private readonly Stack<BurstParticle> _particlePool = new Stack<BurstParticle>();

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
            // Cae rotado y se endereza; al aterrizar: squash + mini burst.
            entry.Rect.anchoredPosition = new Vector2(targetX, _settings.DropHeight);
            float tilt = UnityEngine.Random.Range(-_settings.DropRotation, _settings.DropRotation);
            entry.Rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
            Tween.LocalRotation(entry.Rect, Quaternion.identity, _settings.DropDuration,
                Ease.OutCubic, useUnscaledTime: true);
            Tween.UIAnchoredPositionY(entry.Rect, 0f, _settings.DropDuration,
                    _settings.DropEase, useUnscaledTime: true)
                .OnComplete(this, self => self.OnDieLanded(entry, targetX));
        }

        private void OnDieLanded(DieEntry entry, float landX)
        {
            if (entry.Rect == null || entry.Removing || !_entries.Contains(entry)) return;

            // Squash al aterrizar y vuelta con rebote.
            Tween.Scale(entry.Rect, new Vector3(1.12f, _settings.LandSquashScale, 1f),
                    _settings.LandSquashDuration, Ease.OutQuad, useUnscaledTime: true)
                .OnComplete(this, self =>
                {
                    if (entry.Rect != null && !entry.Removing)
                        Tween.Scale(entry.Rect, Vector3.one, 0.16f, Ease.OutBack, useUnscaledTime: true);
                });

            SpawnBurst(new Vector2(landX, -_settings.DieSize * 0.35f), _settings.AddBurstColor);
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
            entry.Removing = true;
            entry.Button.interactable = false;
            SpawnBurst(entry.Rect.anchoredPosition, _settings.RemoveBurstColor);
            Tween.Scale(entry.Rect, 0f, _settings.RemoveDuration, _settings.RemoveEase, useUnscaledTime: true);
            Tween.Alpha(entry.Group, 0f, _settings.RemoveDuration, useUnscaledTime: true)
                .OnComplete(this, self => self.RecycleFromDying(entry));
        }

        /// <summary>
        /// Ola de celebración al completar la bolsa: cada dado salta con stagger
        /// de izquierda a derecha. La dispara la screen al llegar al target.
        /// </summary>
        public void PlayCompleteWave()
        {
            if (_settings == null || !Application.isPlaying) return;
            if (Rollgeon.UI.HUD.DiceAnim.DiceUiMotionPrefs.ReducedMotion) return;

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Rect == null || entry.Removing) continue;
                Tween.UIAnchoredPositionY(entry.Rect, _settings.WaveJumpHeight,
                    _settings.WaveJumpDuration, Ease.OutQuad,
                    cycles: 2, cycleMode: CycleMode.Yoyo,
                    startDelay: i * _settings.WaveStagger, useUnscaledTime: true);
            }
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

            // Hover clickeable: el dado se agranda bajo el mouse.
            var hover = go.AddComponent<DieHoverRelay>();
            hover.HoverChanged = hovered => OnDieHover(entry, hovered);
            return entry;
        }

        private void OnDieHover(DieEntry entry, bool hovered)
        {
            if (_settings == null || entry.Rect == null || entry.Removing) return;
            if (!Application.isPlaying) return;
            if (Rollgeon.UI.HUD.DiceAnim.DiceUiMotionPrefs.ReducedMotion) return;
            if (!_entries.Contains(entry)) return;

            float target = hovered ? _settings.HoverScale : 1f;
            Tween.Scale(entry.Rect, target, _settings.HoverDuration, Ease.OutQuad, useUnscaledTime: true);
        }

        // -----------------------------------------------------------------
        // Mini burst de impacto (cuadraditos pooled, patrón DiceThrowImpactBurst)
        // -----------------------------------------------------------------

        private void SpawnBurst(Vector2 origin, Color color)
        {
            if (_settings == null || _settings.BurstCount <= 0) return;
            if (!Application.isPlaying || !gameObject.activeInHierarchy) return;
            if (Rollgeon.UI.HUD.DiceAnim.DiceUiMotionPrefs.ReducedMotion) return;

            for (int i = 0; i < _settings.BurstCount; i++)
            {
                var particle = _particlePool.Count > 0 ? _particlePool.Pop() : CreateParticle();
                particle.Image.color = color;
                particle.Rect.sizeDelta = Vector2.one * _settings.BurstParticleSize;
                particle.Rect.anchoredPosition = origin;
                particle.Rect.localScale = Vector3.one;
                particle.Rect.SetAsLastSibling();
                particle.Rect.gameObject.SetActive(true);

                // Abanico hacia arriba con algo de spread lateral.
                float angle = UnityEngine.Random.Range(30f, 150f) * Mathf.Deg2Rad;
                float distance = UnityEngine.Random.Range(_settings.BurstDistanceMin, _settings.BurstDistanceMax);
                var target = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

                Tween.UIAnchoredPosition(particle.Rect, target, _settings.BurstDuration,
                    Ease.OutCubic, useUnscaledTime: true);
                Tween.Scale(particle.Rect, 0.2f, _settings.BurstDuration, Ease.InQuad, useUnscaledTime: true);
                var captured = particle;
                Tween.Alpha(particle.Image, 0f, _settings.BurstDuration, Ease.InQuad, useUnscaledTime: true)
                    .OnComplete(this, self => self.RecycleParticle(captured));
            }
        }

        private BurstParticle CreateParticle()
        {
            var go = new GameObject("BurstParticle", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, worldPositionStays: false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return new BurstParticle { Rect = rect, Image = image };
        }

        private void RecycleParticle(BurstParticle particle)
        {
            if (particle.Rect == null) return;
            Tween.StopAll(onTarget: particle.Rect);
            Tween.StopAll(onTarget: particle.Image);
            var c = particle.Image.color;
            particle.Image.color = new Color(c.r, c.g, c.b, 1f);
            particle.Rect.gameObject.SetActive(false);
            _particlePool.Push(particle);
        }

        private void RecycleFromDying(DieEntry entry) => Recycle(entry, fromDying: true);

        private void Recycle(DieEntry entry, bool fromDying)
        {
            if (fromDying && !_dying.Remove(entry)) return;
            if (entry.Rect == null) return; // destruida por teardown de escena

            Tween.StopAll(onTarget: entry.Rect);
            Tween.StopAll(onTarget: entry.Group);
            entry.Rect.localScale = Vector3.one;
            entry.Rect.localRotation = Quaternion.identity;
            entry.Group.alpha = 1f;
            entry.Button.interactable = true;
            entry.Removing = false;
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
                entry.Rect.localRotation = Quaternion.identity;
                entry.Rect.anchoredPosition = new Vector2(entry.Rect.anchoredPosition.x, 0f);
                entry.Group.alpha = 1f;
            }
            SnapToTarget();
        }
    }
}
