using System;
using Patterns;
using PrimeTween;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Espada + daño base del player a la izquierda del dice board. Muestra
    /// <c>Attack.ModifiedValue</c> (base + bonos persistentes de items) — el mismo
    /// término que la secuencia hace volar hacia N.
    /// </summary>
    public sealed class PlayerBaseDamageView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _valueLabel;

        [SerializeField]
        [Tooltip("Origen del vuelo del '+ATQ'. Si queda vacío se usa el propio transform.")]
        private RectTransform _flyAnchor;

        private Guid _playerGuid;
        private bool _bound;
        private Action<ComboMatchedPayload> _onMatched;
        private Tween _punch;

        public RectTransform Anchor => _flyAnchor != null ? _flyAnchor : (RectTransform)transform;

        public int CurrentValue { get; private set; }

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();
            _playerGuid = playerGuid;
            // El ATQ cambia poco (items, buffs de pasiva) y el payload de match dispara en
            // cada toggle de hold: refrescar ahí alcanza sin un canal de eventos nuevo.
            _onMatched = _ => Refresh();
            TypedEvent<ComboMatchedPayload>.Subscribe(_onMatched);
            _bound = true;
            Refresh();
        }

        public void Unbind()
        {
            if (!_bound) return;
            if (_onMatched != null)
            {
                TypedEvent<ComboMatchedPayload>.Unsubscribe(_onMatched);
                _onMatched = null;
            }
            _bound = false;
        }

        /// <summary>Squash al disparar su vuelo en la secuencia.</summary>
        public void Punch()
        {
            if (_punch.isAlive) _punch.Stop();
            transform.localScale = Vector3.one;
            _punch = Tween.PunchScale(transform, new Vector3(0.12f, -0.15f, 0f), 0.16f, frequency: 1);
        }

        private void Refresh()
        {
            CurrentValue = 0;
            if (ServiceLocator.TryGetService<AttributesManager>(out var attrs) && attrs != null)
                CurrentValue = attrs.GetAttribute<Attack>(_playerGuid)?.ModifiedValue ?? 0;
            if (_valueLabel != null) _valueLabel.text = CurrentValue.ToString();
        }

        private void OnDisable()
        {
            if (_punch.isAlive) _punch.Stop();
            transform.localScale = Vector3.one;
        }
    }
}
