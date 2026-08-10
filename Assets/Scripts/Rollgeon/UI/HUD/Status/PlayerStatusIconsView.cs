using System;
using System.Collections.Generic;
using Patterns;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Fila de estados del player (arriba a la izquierda). Instancia un
    /// <see cref="StatusEffectIconView"/> por estado que publiquen sus
    /// <see cref="IStatusIconProvider"/>. Hoy el único es la pasiva de clase; el sistema de
    /// efectos de estado que viene se suma agregando un provider en
    /// <see cref="BuildProviders"/>, sin tocar esta vista.
    /// </summary>
    /// <remarks>
    /// Vive en <c>Canvas_PlayerStatus</c>, que está siempre activo — se ve igual en
    /// exploración y en combate. Lo bindea <c>ExplorationHUDView</c>, que es la screen base
    /// y nunca se popea durante la run.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Player Status Icons View")]
    public class PlayerStatusIconsView : MonoBehaviour
    {
        [Title("Wiring")]
        [SerializeField, Required]
        [Tooltip("Prefab de un ícono de estado (StatusEffectIcon).")]
        private StatusEffectIconView _iconPrefab;

        [SerializeField, Required]
        [Tooltip("Contenedor de la fila — lleva el HorizontalLayoutGroup.")]
        private RectTransform _container;

        [ShowInInspector, ReadOnly] private Guid _playerGuid;
        [ShowInInspector, ReadOnly] private bool _bound;

        private readonly List<IStatusIconProvider> _providers = new();
        private readonly List<StatusIconState> _states = new();
        private readonly List<StatusEffectIconView> _slots = new();

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();

            _playerGuid = playerGuid;
            BuildProviders();

            // La pasiva se prende y apaga por modifiers sobre atributos, así que estos tres
            // cubren todo lo que hoy la puede cambiar. OnTurnStarted es el resync barato para
            // estados futuros que dependan de turno y no toquen modifiers.
            EventManager.Subscribe(EventName.OnModifierAdded, HandleOwnerEvent);
            EventManager.Subscribe(EventName.OnModifierRemoved, HandleOwnerEvent);
            EventManager.Subscribe(EventName.OnAttributeChanged, HandleOwnerEvent);
            EventManager.Subscribe(EventName.OnTurnStarted, HandleOwnerEvent);
            _bound = true;

            Refresh();
        }

        public void Unbind()
        {
            if (!_bound) return;

            EventManager.UnSubscribe(EventName.OnModifierAdded, HandleOwnerEvent);
            EventManager.UnSubscribe(EventName.OnModifierRemoved, HandleOwnerEvent);
            EventManager.UnSubscribe(EventName.OnAttributeChanged, HandleOwnerEvent);
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleOwnerEvent);
            _bound = false;
        }

        private void OnEnable()
        {
            // A diferencia del PassiveBadgeView viejo, re-bindeamos solos: este canvas puede
            // apagarse y prenderse sin que la screen que nos bindeó vuelva a correr su Bind,
            // y sin esto la fila quedaba congelada en el último estado que vio.
            if (!_bound && _playerGuid != Guid.Empty) Bind(_playerGuid);
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        // Todos los eventos que escuchamos traen el guid del dueño en args[0]; los de otras
        // entidades no nos mueven la fila.
        private void HandleOwnerEvent(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (!(args[0] is Guid entityGuid) || entityGuid != _playerGuid) return;
            Refresh();
        }

        /// <summary>Relee el estado real y repinta. Idempotente y barato.</summary>
        public void Refresh()
        {
            if (_container == null || _iconPrefab == null) return;

            _states.Clear();
            foreach (var provider in _providers)
                provider?.Collect(_playerGuid, _states);

            EnsureSlots(_states.Count);

            for (int i = 0; i < _slots.Count; i++)
            {
                bool used = i < _states.Count;
                _slots[i].gameObject.SetActive(used);
                if (used) _slots[i].Show(_states[i]);
            }
        }

        // Los slots se reusan y solo se apagan: la fila se repinta en cada cambio de HP y
        // destruir/instanciar por refresh sería churn de GC en pleno combate.
        private void EnsureSlots(int needed)
        {
            while (_slots.Count < needed)
                _slots.Add(Instantiate(_iconPrefab, _container));
        }

        private void BuildProviders()
        {
            _providers.Clear();
            _providers.Add(new ClassPassiveStatusProvider());
        }
    }
}
