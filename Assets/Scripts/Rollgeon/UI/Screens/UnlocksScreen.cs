using System.Collections.Generic;
using Patterns;
using Rollgeon.Meta;
using Rollgeon.UI.Unlocks;
using Sirenix.OdinInspector;
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

        private readonly List<UnlockEntryRowView> _rows = new List<UnlockEntryRowView>();

        public override string ScreenStringId => ScreenId;

        protected override void OnPushed(IScreenPayload payload)
        {
            if (_backButton != null) _backButton.onClick.AddListener(OnBackClicked);
            Rebuild();
        }

        protected override void OnPopped()
        {
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBackClicked);
            ClearRows();
        }

        private void Rebuild()
        {
            ClearRows();

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
                    def.DisplayName,
                    unlocked ? def.Description : def.HintText,
                    locked: !unlocked);
                _rows.Add(row);
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
