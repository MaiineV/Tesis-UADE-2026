using System.Collections.Generic;
using Patterns;
using Rollgeon.Meta;
using Rollgeon.UI.Unlocks;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Pantalla de desbloqueos (#164), accesible desde el menú principal. Lista
    /// todas las <see cref="UnlockDefinitionSO"/> del catálogo: las cumplidas con
    /// nombre, descripción y efecto completo; las pendientes con candado + el
    /// texto de pista configurado en la Unlock Condition Tool.
    /// </summary>
    /// <remarks>
    /// [SETUP] GameObject hijo del Canvas de <c>01_MainMenu.unity</c>, registrado
    /// por el ScreenHost. Ver <c>docs/setup/0164_MetaProgression.md</c>.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Unlocks Screen")]
    public class UnlocksScreen : BaseScreen
    {
        private const string LogPrefix = "[UnlocksScreen] ";
        private const string ScreenId = "UnlocksScreen";

        [Title("Screen — Unlocks")]
        [Required("Arrastrar el container (Content del ScrollView) donde se instancian las filas.")]
        [SerializeField] private Transform _entriesContainer;

        [Required("Arrastrar el prefab de fila (UnlockEntryRowView).")]
        [SerializeField] private UnlockEntryRowView _entryRowPrefab;

        [Required("Arrastrar el Button de volver.")]
        [SerializeField] private Button _backButton;

        [SerializeField]
        [Tooltip("TMP opcional del título. Se setea por código (lane B) para poder " +
                 "envolverlo en <wave> cuando el label tiene Text Animator.")]
        private TextMeshProUGUI _titleLabel;

        private readonly List<UnlockEntryRowView> _rows = new List<UnlockEntryRowView>();
        private bool _titleHasTextAnimator;
        private bool _titleAnimatorChecked;

        public override string ScreenStringId => ScreenId;

        protected override void OnPushed(IScreenPayload payload)
        {
            if (_backButton != null) _backButton.onClick.AddListener(OnBackClicked);

            Rollgeon.Localization.LocalizationRefresh.Subscribe(Rebuild);
            Rebuild();
        }

        protected override void OnPopped()
        {
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBackClicked);

            Rollgeon.Localization.LocalizationRefresh.Unsubscribe(Rebuild);
            ClearRows();
        }

        private void Rebuild()
        {
            ClearRows();
            RefreshTitle();

            if (_entriesContainer == null || _entryRowPrefab == null)
            {
                Debug.LogWarning(LogPrefix + "Container o prefab de fila sin cablear.", this);
                return;
            }

            if (!ServiceLocator.TryGetService<IMetaProgressionService>(out var meta) || meta == null)
            {
                Debug.LogWarning(LogPrefix + "IMetaProgressionService no registrado — lista vacía.", this);
                return;
            }

            foreach (var def in meta.Definitions)
            {
                if (def == null) continue;

                bool unlocked = meta.IsDefinitionCompleted(def);
                var row = Instantiate(_entryRowPrefab, _entriesContainer);
                row.Bind(
                    Rollgeon.Localization.LocalizedContent.Name(def.UnlockId, def.DisplayName),
                    unlocked
                        ? Rollgeon.Localization.LocalizedContent.Description(def.UnlockId, def.Description)
                        : Rollgeon.Localization.LocalizedContent.Hint(def.UnlockId, def.HintText),
                    locked: !unlocked);
                _rows.Add(row);
            }

            PlayRowsEntrance();
        }

        // Lane B (code-set): el installer remueve el LocalizeStringEvent del título —
        // el binding estático pisaría los tags de <wave> en cada refresh de locale.
        private void RefreshTitle()
        {
            if (_titleLabel == null) return;

            var text = Rollgeon.Localization.LocalizedContent.Ui("unlocks.title", "DESBLOQUEOS");

            if (!_titleAnimatorChecked)
            {
                // Por nombre: el runtime no referencia el assembly de Febucci — el
                // installer agrega el componente (mismo truco que ContractDisplayView).
                _titleHasTextAnimator = _titleLabel.GetComponent("TextAnimator_TMP") != null;
                _titleAnimatorChecked = true;
            }
            _titleLabel.text = _titleHasTextAnimator ? $"<wave>{text}</wave>" : text;
        }

        // Pop escalonado de las cards. Gated por isPlaying (los tests EditMode invocan
        // Rebuild y PrimeTween no corre ahí) y por la preferencia de reduced motion.
        private void PlayRowsEntrance()
        {
            if (!Application.isPlaying || Rollgeon.UI.HUD.DiceAnim.DiceUiMotionPrefs.ReducedMotion)
                return;

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row == null) continue;

                float delay = i * 0.04f;
                var rect = (RectTransform)row.transform;
                rect.localScale = Vector3.one * 0.92f;
                PrimeTween.Tween.Scale(rect, 1f, 0.18f, PrimeTween.Ease.OutBack,
                    startDelay: delay, useUnscaledTime: true);

                if (row.TryGetComponent<CanvasGroup>(out var group))
                {
                    group.alpha = 0f;
                    PrimeTween.Tween.Alpha(group, 1f, 0.18f, PrimeTween.Ease.OutQuad,
                        startDelay: delay, useUnscaledTime: true);
                }
            }
        }

        private void ClearRows()
        {
            foreach (var row in _rows)
            {
                if (row == null) continue;
                if (Application.isPlaying) Destroy(row.gameObject);
                else DestroyImmediate(row.gameObject);
            }
            _rows.Clear();
        }

        private void OnBackClicked()
        {
            if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                screens.PopCurrent();
            }
        }
    }
}
