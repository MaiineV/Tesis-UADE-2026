using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.Unlocks
{
    /// <summary>
    /// Fila de la pantalla de desbloqueos (#164). Desbloqueado: nombre +
    /// descripción/efecto completo. Bloqueado: candado visual + texto de pista
    /// configurado en la Unlock Condition Tool.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/Unlocks/Unlock Entry Row View")]
    public class UnlockEntryRowView : MonoBehaviour
    {
        [Title("Unlock Entry Row")]
        [Required("Arrastrar el TMP del nombre.")]
        [SerializeField] private TextMeshProUGUI _nameLabel;

        [Required("Arrastrar el TMP del cuerpo (descripción o pista).")]
        [SerializeField] private TextMeshProUGUI _bodyLabel;

        [Tooltip("Icono de candado mostrado cuando el elemento está bloqueado. Opcional.")]
        [SerializeField] private GameObject _lockIcon;

        /// <summary>Puebla la fila. Con <paramref name="locked"/> el nombre se enmascara y el cuerpo muestra la pista.</summary>
        public void Bind(string displayName, string body, bool locked)
        {
            if (_nameLabel != null) _nameLabel.text = locked ? "???" : displayName;
            if (_bodyLabel != null) _bodyLabel.text = body ?? string.Empty;
            if (_lockIcon != null) _lockIcon.SetActive(locked);
        }
    }
}
