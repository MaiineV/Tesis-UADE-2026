using System;
using Patterns;
using PrimeTween;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Damage;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Espada + daño base del player a la izquierda del dice board. Muestra el MISMO
    /// término que la fórmula usa como <c>dmg_base_PJ + bonos_PJ</c>: con un base damage
    /// override activo (Furia Contenida, Egoísta), <c>override + bonos</c> con decimales;
    /// sin override, <c>Attack.ModifiedValue</c> como siempre.
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
        private EventManager.EventReceiver _onStreakChanged;
        private Tween _punch;

        public RectTransform Anchor => _flyAnchor != null ? _flyAnchor : (RectTransform)transform;

        public float CurrentValue { get; private set; }

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();
            _playerGuid = playerGuid;
            // El ATQ cambia poco (items, buffs de pasiva) y el payload de match dispara en
            // cada toggle de hold: refrescar ahí cubre lo estático. El override de Furia
            // cambia ENTRE tiradas (racha por ronda), por eso además se escucha el evento
            // de racha — sin él, la espada mostraba "5" congelado todo el combate (QA).
            _onMatched = _ => Refresh();
            TypedEvent<ComboMatchedPayload>.Subscribe(_onMatched);
            _onStreakChanged = _ => Refresh();
            EventManager.Subscribe(EventName.OnCleanTurnStreakChanged, _onStreakChanged);
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
            if (_onStreakChanged != null)
            {
                EventManager.UnSubscribe(EventName.OnCleanTurnStreakChanged, _onStreakChanged);
                _onStreakChanged = null;
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
            CurrentValue = 0f;
            if (ServiceLocator.TryGetService<AttributesManager>(out var attrs) && attrs != null)
            {
                var attack = attrs.GetAttribute<Attack>(_playerGuid);
                int baseValue = attack?.Value ?? 0;
                int bonos = (attack?.ModifiedValue ?? 0) - baseValue;

                // Paridad con PlayerComboDamage.Resolve: el override pisa SOLO dmg_base_PJ;
                // bonos_PJ (los +Attack de otros items) sobrevive encima.
                if (ServiceLocator.TryGetService<IBaseDamageOverrideService>(out var overrides)
                    && overrides != null
                    && overrides.TryGetBaseDamage(_playerGuid, out var overridden))
                    CurrentValue = overridden + bonos;
                else
                    CurrentValue = baseValue + bonos;
            }
            if (_valueLabel != null) _valueLabel.text = CurrentValue.ToString("0.##");
        }

        private void OnDisable()
        {
            if (_punch.isAlive) _punch.Stop();
            transform.localScale = Vector3.one;
        }
    }
}
