using System.Collections.Generic;
using Patterns;
using PrimeTween;
using Rollgeon.Audio;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Motor genérico de una pila de fichas del HUD: pool de Images apiladas,
    /// drop-in al ganar, fade deslizando a la izquierda al perder y shake para
    /// el régimen "solo agitar" del oro. Sin lógica de recurso — las vistas
    /// (<see cref="HealthChipStackView"/> etc.) deciden QUÉ fichas mostrar y
    /// este componente resuelve el CÓMO.
    /// </summary>
    /// <remarks>
    /// El modelo lógico (<see cref="_ids"/>) se actualiza en el instante en que
    /// se llama <see cref="SetChips"/> — los bursts de eventos same-frame
    /// siempre diffean contra el modelo, nunca contra el estado de los tweens,
    /// así que nada se anima dos veces. Fichas saliendo viven en
    /// <see cref="_dying"/> hasta que su tween termina y vuelven al pool.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Chip Stack View")]
    public sealed class ChipStackView : MonoBehaviour
    {
        [SerializeField, Optional, Tooltip("Padre de las fichas y target del shake. Null = este RectTransform.")]
        private RectTransform _chipRoot;

        private ChipStackSettingsSO _settings;
        private Sprite[] _spritesById;
        private float _spacingY = 14f;

        private sealed class Chip
        {
            public RectTransform Rect;
            public Image Img;
            public CanvasGroup Cg;
        }

        private readonly List<int> _ids = new List<int>();
        private readonly List<Chip> _displayed = new List<Chip>();
        private readonly List<Chip> _dying = new List<Chip>();
        private readonly Stack<Chip> _pool = new Stack<Chip>();

        private Vector3 _chipRootRestPos;
        private bool _restPosCaptured;

        /// <summary>Cantidad de fichas del modelo lógico (no cuenta las que se están yendo).</summary>
        public int DisplayedCount => _ids.Count;

        public void Configure(ChipStackSettingsSO settings, Sprite[] spritesById, float spacingY)
        {
            _settings = settings;
            _spritesById = spritesById;
            _spacingY = spacingY;
            CaptureRestPos();
        }

        private RectTransform ChipRoot => _chipRoot != null ? _chipRoot : (RectTransform)transform;

        /// <summary>Padre real de las fichas — lo usa <see cref="HealthChipStackView"/>
        /// para colgar la pila fantasma del máximo exactamente en el mismo origen.</summary>
        public RectTransform Root => ChipRoot;

        private void CaptureRestPos()
        {
            if (_restPosCaptured) return;
            _chipRootRestPos = ChipRoot.localPosition;
            _restPosCaptured = true;
        }

        /// <summary>
        /// Lleva la pila al estado <paramref name="targetIds"/> (ids = índice en el
        /// array de sprites configurado). Anima el delta salvo que
        /// <paramref name="animate"/> sea false, rija reduced motion o el GO esté
        /// inactivo.
        /// </summary>
        public void SetChips(IReadOnlyList<int> targetIds, bool animate = true)
        {
            if (_settings == null || _spritesById == null || targetIds == null) return;

            var diff = ChipStackMath.ComputeDiff(_ids, targetIds);
            if (diff.IsEmpty) return;

            bool instant = !animate
                           || DiceAnim.DiceUiMotionPrefs.ReducedMotion
                           || !gameObject.activeInHierarchy;

            // Remover del tope hacia abajo (stagger top-down).
            int removeStagger = 0;
            for (int i = _ids.Count - 1; i >= diff.KeepCount; i--)
            {
                RemoveTopChip(i, instant, removeStagger++);
            }

            // Agregar encima del prefijo (stagger bottom-up).
            int addStagger = 0;
            for (int i = diff.KeepCount; i < targetIds.Count; i++)
            {
                AddChip(targetIds[i], i, instant, addStagger++);
            }

            _ids.Clear();
            for (int i = 0; i < targetIds.Count; i++) _ids.Add(targetIds[i]);

            if (!instant)
            {
                if (diff.RemoveCount > 0) PlayBatchSfx(_settings.ChipSpendClip, diff.RemoveCount);
                if (diff.AddCount > 0) PlayBatchSfx(_settings.ChipDropClip, diff.AddCount);
            }
        }

        /// <summary>Shake horizontal de la pila (régimen oro ≥5). No-op con reduced motion.</summary>
        public void Shake()
        {
            if (_settings == null || DiceAnim.DiceUiMotionPrefs.ReducedMotion) return;

            CaptureRestPos();
            var root = ChipRoot;

            // Reset antes de re-disparar: un shake sobre otro no debe acumular offset.
            Tween.StopAll(onTarget: root);
            root.localPosition = _chipRootRestPos;
            Tween.ShakeLocalPosition(root,
                strength: new Vector3(_settings.ShakeStrengthX, 0f, 0f),
                duration: _settings.ShakeDuration,
                frequency: _settings.ShakeFrequency,
                useUnscaledTime: true);
        }

        /// <summary>Aplica el estado actual sin animación y recicla todo lo que estaba en vuelo.</summary>
        public void SnapToTarget()
        {
            for (int i = _dying.Count - 1; i >= 0; i--) RecycleDying(_dying[i]);

            for (int i = 0; i < _displayed.Count; i++)
            {
                var chip = _displayed[i];
                StopChipTweens(chip);
                chip.Rect.anchoredPosition = RestPosition(i);
                chip.Cg.alpha = 1f;
            }

            if (_restPosCaptured)
            {
                var root = ChipRoot;
                Tween.StopAll(onTarget: root);
                root.localPosition = _chipRootRestPos;
            }
        }

        private Vector2 RestPosition(int index) => new Vector2(0f, index * _spacingY);

        private void AddChip(int id, int index, bool instant, int staggerIndex)
        {
            var chip = _pool.Count > 0 ? _pool.Pop() : CreateChip();
            var sprite = id >= 0 && id < _spritesById.Length ? _spritesById[id] : null;

            chip.Img.sprite = sprite;
            if (sprite != null)
            {
                chip.Rect.sizeDelta = sprite.rect.size * Mathf.Max(1f, _settings.ChipScale);
            }

            var rest = RestPosition(index);
            chip.Cg.alpha = 1f;
            chip.Rect.SetAsLastSibling();
            chip.Rect.gameObject.SetActive(true);
            _displayed.Add(chip);

            if (instant)
            {
                chip.Rect.anchoredPosition = rest;
                return;
            }

            chip.Rect.anchoredPosition = new Vector2(rest.x, rest.y + _settings.DropHeight);
            Tween.UIAnchoredPositionY(chip.Rect, rest.y, _settings.DropDuration, _settings.DropEase,
                startDelay: staggerIndex * _settings.StaggerPerChip, useUnscaledTime: true);
        }

        private void RemoveTopChip(int modelIndex, bool instant, int staggerIndex)
        {
            if (_displayed.Count == 0) return;

            // El modelo y _displayed van en paralelo: el tope del modelo es el último.
            var chip = _displayed[_displayed.Count - 1];
            _displayed.RemoveAt(_displayed.Count - 1);

            if (instant)
            {
                RecyclePooled(chip);
                return;
            }

            _dying.Add(chip);
            float delay = staggerIndex * _settings.StaggerPerChip;
            Tween.UIAnchoredPositionX(chip.Rect, chip.Rect.anchoredPosition.x - _settings.LoseSlideDistance,
                _settings.LoseDuration, _settings.LoseEase, startDelay: delay, useUnscaledTime: true);
            Tween.Alpha(chip.Cg, 0f, _settings.LoseDuration, _settings.LoseEase,
                    startDelay: delay, useUnscaledTime: true)
                .OnComplete(this, self => self.RecycleDying(chip));
        }

        private Chip CreateChip()
        {
            var go = new GameObject("Chip", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(ChipRoot, worldPositionStays: false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;

            return new Chip
            {
                Rect = rect,
                Img = img,
                Cg = go.GetComponent<CanvasGroup>(),
            };
        }

        private void RecycleDying(Chip chip)
        {
            if (!_dying.Remove(chip)) return;
            RecyclePooled(chip);
        }

        private void RecyclePooled(Chip chip)
        {
            if (chip.Rect == null) return; // destruida desde afuera (teardown de escena)
            StopChipTweens(chip);
            chip.Cg.alpha = 1f;
            chip.Rect.gameObject.SetActive(false);
            _pool.Push(chip);
        }

        private void StopChipTweens(Chip chip)
        {
            if (chip.Rect != null) Tween.StopAll(onTarget: chip.Rect);
            if (chip.Cg != null) Tween.StopAll(onTarget: chip.Cg);
        }

        private void PlayBatchSfx(AudioClip clip, int changedCount)
        {
            if (clip == null || _settings == null) return;
            if (!ServiceLocator.TryGetService<IAudioService>(out var audio) || audio == null) return;

            int plays = Mathf.Min(changedCount, Mathf.Max(1, _settings.MaxSfxPerBatch));
            for (int i = 0; i < plays; i++)
            {
                float delay = i * _settings.StaggerPerChip;
                if (delay <= 0f)
                {
                    PlayOneSfx(audio, clip);
                    continue;
                }

                Tween.Delay(this, delay, self =>
                {
                    if (ServiceLocator.TryGetService<IAudioService>(out var a) && a != null)
                        self.PlayOneSfx(a, clip);
                }, useUnscaledTime: true);
            }
        }

        private void PlayOneSfx(IAudioService audio, AudioClip clip)
        {
            float pitch = Random.Range(_settings.SfxPitchRange.x, _settings.SfxPitchRange.y);
            audio.PlaySfx2D(clip, _settings.SfxVolume, pitch);
        }

        private void OnDisable()
        {
            // Nada sobrevive al layer: matar tweens (incluidos los delays de SFX
            // targeteados a este componente) y dejar la pila en su estado final.
            Tween.StopAll(onTarget: this);
            SnapToTarget();
        }
    }
}
