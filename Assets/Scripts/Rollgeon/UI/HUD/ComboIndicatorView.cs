using System;
using System.Collections.Generic;
using Patterns;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view que muestra el combo matcheado actual y la lista de combos del
    /// contrato con su estado "bloqueado" (mecanica del boss, T103).
    /// Plan §3.3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Combo actual</b>: suscribe <see cref="TypedEvent{T}"/> de
    /// <see cref="ComboMatchedPayload"/>, filtra por <c>SourceGuid == _playerGuid</c>,
    /// y pinta el <see cref="_currentComboLabel"/>.
    /// </para>
    /// <para>
    /// <b>Combos bloqueados</b>: suscribe <see cref="EventName.OnComboBlocked"/> y
    /// <see cref="EventName.OnComboUnblocked"/> (stubs de T103). El designer cablea
    /// una <see cref="ComboRow"/> por combo del contrato del guerrero (8 entradas);
    /// el view togglea el overlay "blocked" en el row correspondiente.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Combo Indicator View")]
    public class ComboIndicatorView : MonoBehaviour
    {
        private const string LogPrefix = "[ComboIndicatorView] ";

        /// <summary>
        /// Row del contrato: mapea <c>comboId</c> a su label + overlay de bloqueo.
        /// </summary>
        [Serializable]
        public struct ComboRow
        {
            [Tooltip("Id canonico del combo (ej. 'combo.par', 'combo.generala').")]
            public string ComboId;

            [Tooltip("Label del row (nombre display del combo). Puede quedar null si " +
                     "solo interesa el overlay.")]
            public TextMeshProUGUI Label;

            [Tooltip("GameObject que se activa cuando el combo esta bloqueado.")]
            public GameObject BlockedOverlay;
        }

        [Title("Combo Indicator — Widget refs")]
        [SerializeField]
        [Tooltip("Label del combo actual (se actualiza al matchear). Opcional.")]
        private TextMeshProUGUI _currentComboLabel;

        [SerializeField]
        [Tooltip("Color del texto cuando se highlighta el combo nuevo. El reset " +
                 "a idle lo hace el designer via tween / timer externo.")]
        private Color _highlightColor = new Color(1f, 0.87f, 0.27f, 1f);

        [SerializeField]
        [Tooltip("Color idle del label (estado default). Si es blanco puro, dejarlo " +
                 "en white.")]
        private Color _idleColor = Color.white;

        [Title("Combo Indicator — Contract rows")]
        [InfoBox("Una fila por combo del contrato del guerrero (8 filas esperadas).")]
        [SerializeField]
        private List<ComboRow> _rows = new List<ComboRow>();

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private Action<ComboMatchedPayload> _onComboMatched;

        /// <summary>Suscribe al bus legacy + al <c>TypedEvent</c>.</summary>
        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();

            _playerGuid = playerGuid;

            _onComboMatched = HandleComboMatched;
            TypedEvent<ComboMatchedPayload>.Subscribe(_onComboMatched);

            EventManager.Subscribe(EventName.OnComboBlocked, HandleComboBlocked);
            EventManager.Subscribe(EventName.OnComboUnblocked, HandleComboUnblocked);
            _bound = true;

            // Reset visual: todos los blocked overlays apagados al bindear.
            for (int i = 0; i < _rows.Count; i++)
            {
                SetRowBlocked(_rows[i], false);
            }
        }

        /// <summary>Desuscribe. Idempotente.</summary>
        public void Unbind()
        {
            if (!_bound) return;
            if (_onComboMatched != null)
            {
                TypedEvent<ComboMatchedPayload>.Unsubscribe(_onComboMatched);
                _onComboMatched = null;
            }
            EventManager.UnSubscribe(EventName.OnComboBlocked, HandleComboBlocked);
            EventManager.UnSubscribe(EventName.OnComboUnblocked, HandleComboUnblocked);
            _bound = false;
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        // ======================================================================
        // API publica (tooling T95T + tests)
        // ======================================================================

        /// <summary>Setea el label del combo actual. Publico para tooling / tests.</summary>
        public void SetCurrentComboText(string comboDisplay)
        {
            if (_currentComboLabel == null) return;
            _currentComboLabel.text = comboDisplay ?? string.Empty;
            _currentComboLabel.color = _highlightColor;
        }

        /// <summary>
        /// Togglea el overlay "blocked" del combo <paramref name="comboId"/>. Public
        /// para tests y tooling. <paramref name="turnsRemaining"/> es informativo
        /// (lo puede pintar el designer en el overlay).
        /// </summary>
        public void SetBlocked(string comboId, bool blocked, int turnsRemaining)
        {
            if (string.IsNullOrEmpty(comboId)) return;
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (!string.Equals(row.ComboId, comboId, StringComparison.Ordinal)) continue;
                SetRowBlocked(row, blocked);
                return;
            }
            // Combo no cableado en _rows — ignore silenciosamente (no todos los combos
            // del enum tienen row UI).
        }

        // ======================================================================
        // Handlers
        // ======================================================================

        private void HandleComboMatched(ComboMatchedPayload payload)
        {
            if (payload.SourceGuid != _playerGuid) return;
            SetCurrentComboText(payload.ComboId);
        }

        private void HandleComboBlocked(params object[] args)
        {
            // schema: [Guid affectedGuid, string comboId, int turnsRemaining]
            if (args == null || args.Length < 3)
            {
                Debug.LogWarning(LogPrefix + "OnComboBlocked args malformed (len < 3).", this);
                return;
            }
            if (!(args[0] is Guid guid))
            {
                Debug.LogWarning(LogPrefix + "OnComboBlocked args[0] is not Guid.", this);
                return;
            }
            if (guid != _playerGuid) return;

            if (!(args[1] is string comboId))
            {
                Debug.LogWarning(LogPrefix + "OnComboBlocked args[1] is not string.", this);
                return;
            }
            int turns = args[2] is int t ? t : 0;
            SetBlocked(comboId, true, turns);
        }

        private void HandleComboUnblocked(params object[] args)
        {
            // schema: [Guid affectedGuid, string comboId]
            if (args == null || args.Length < 2)
            {
                Debug.LogWarning(LogPrefix + "OnComboUnblocked args malformed (len < 2).", this);
                return;
            }
            if (!(args[0] is Guid guid))
            {
                Debug.LogWarning(LogPrefix + "OnComboUnblocked args[0] is not Guid.", this);
                return;
            }
            if (guid != _playerGuid) return;

            if (!(args[1] is string comboId))
            {
                Debug.LogWarning(LogPrefix + "OnComboUnblocked args[1] is not string.", this);
                return;
            }
            SetBlocked(comboId, false, 0);
        }

        private static void SetRowBlocked(ComboRow row, bool blocked)
        {
            if (row.BlockedOverlay != null)
            {
                row.BlockedOverlay.SetActive(blocked);
            }
            if (row.Label != null)
            {
                // Dim el label cuando el combo esta bloqueado.
                var c = row.Label.color;
                c.a = blocked ? 0.45f : 1f;
                row.Label.color = c;
            }
        }
    }
}
