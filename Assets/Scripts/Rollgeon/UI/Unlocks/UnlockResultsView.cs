using System.Text;
using Patterns;
using Rollgeon.Meta;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.Unlocks
{
    /// <summary>
    /// Sección de desbloqueos en las pantallas de resultados (#164). Al activarse
    /// (push de Victory/DefeatScreen) lee <see cref="IUnlockProgressService.UnlocksThisRun"/>
    /// y lista lo conseguido en la run — incluye tanto los unlocks mid-run como los
    /// evaluados al cierre. Se oculta si la run no desbloqueó nada.
    /// </summary>
    /// <remarks>
    /// [SETUP] Hijo del panel de VictoryScreen y de DefeatScreen. Ver
    /// <c>docs/setup/0164_MetaProgression.md</c>.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Unlocks/Unlock Results View")]
    public class UnlockResultsView : MonoBehaviour
    {
        [Title("Unlock Results")]
        [Required("Arrastrar el GameObject raíz de la sección (header + lista).")]
        [SerializeField] private GameObject _sectionRoot;

        [Required("Arrastrar el TMP donde se listan los desbloqueos.")]
        [SerializeField] private TextMeshProUGUI _unlocksLabel;

        private void OnEnable()
        {
            Refresh();
        }

        /// <summary>Repuebla la lista desde el servicio. Expuesto para tests.</summary>
        public void Refresh()
        {
            if (!ServiceLocator.TryGetService<IUnlockProgressService>(out var progress) ||
                progress == null || progress.UnlocksThisRun.Count == 0)
            {
                if (_sectionRoot != null) _sectionRoot.SetActive(false);
                return;
            }

            var sb = new StringBuilder();
            foreach (var def in progress.UnlocksThisRun)
            {
                if (def == null) continue;
                if (sb.Length > 0) sb.AppendLine();
                sb.Append("• ").Append(string.IsNullOrEmpty(def.DisplayName) ? def.TargetId : def.DisplayName);
            }

            if (_unlocksLabel != null) _unlocksLabel.text = sb.ToString();
            if (_sectionRoot != null) _sectionRoot.SetActive(sb.Length > 0);
        }
    }
}
