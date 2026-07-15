using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Effects.Concretes;
using Rollgeon.Player;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Badge que se prende al lado de la barra de vida mientras la pasiva de HP bajo del
    /// Warrior (<see cref="EffLowHpAttackBuff"/>) esté activa. Mismo patrón que
    /// <see cref="ShieldBarView"/>: <c>Bind/Unbind</c> + bus legacy + <c>_container.SetActive</c>.
    /// </summary>
    /// <remarks>
    /// No trackea "¿el buff está prendido?" en un campo propio — cada evento relevante
    /// (<c>OnModifierAdded</c>/<c>OnModifierRemoved</c> sobre el player) dispara
    /// <see cref="Refresh"/>, que relee el estado real vía
    /// <see cref="EffLowHpAttackBuff.IsActiveFor"/>. Barato e idempotente, igual que el
    /// effect mismo — no hace falta guardar el modifier id a mano (y <c>OnModifierRemoved</c>
    /// ni siquiera trae el <see cref="Type"/> del atributo para poder filtrar por Attack ahí).
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Passive Badge View")]
    public class PassiveBadgeView : MonoBehaviour
    {
        [Title("Passive Badge — Widget refs")]
        [SerializeField]
        [Tooltip("GameObject raiz a mostrar/ocultar segun la pasiva esté activa.")]
        private GameObject _container;

        [SerializeField]
        [Tooltip("Label opcional. Si está asignado, se le pone el DisplayName de la pasiva del " +
                 "hero actual en Bind. Si es null, el container puede llevar un texto fijo " +
                 "puesto a mano en el prefab (ej. 'Pasiva activa').")]
        private TextMeshProUGUI _text;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();

            _playerGuid = playerGuid;
            ResolvePassiveLabel();

            EventManager.Subscribe(EventName.OnModifierAdded, OnModifierAdded);
            EventManager.Subscribe(EventName.OnModifierRemoved, OnModifierRemoved);
            _bound = true;

            Refresh();
        }

        public void Unbind()
        {
            if (!_bound) return;

            EventManager.UnSubscribe(EventName.OnModifierAdded, OnModifierAdded);
            EventManager.UnSubscribe(EventName.OnModifierRemoved, OnModifierRemoved);
            _bound = false;
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        private void OnModifierAdded(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            if (!(args[0] is Guid entityGuid) || entityGuid != _playerGuid) return;
            if (!(args[1] is Type attributeType) || attributeType != typeof(Attack)) return;

            Refresh();
        }

        private void OnModifierRemoved(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (!(args[0] is Guid entityGuid) || entityGuid != _playerGuid) return;

            // OnModifierRemoved no trae el Type del atributo — Refresh() es barato e
            // idempotente, así que re-chequear de más (por un modifier de otro stat) es inofensivo.
            Refresh();
        }

        private void Refresh()
        {
            bool active = ServiceLocator.TryGetService<AttributesManager>(out var attrs) && attrs != null
                          && EffLowHpAttackBuff.IsActiveFor(attrs, _playerGuid);
            SetVisible(active);
        }

        private void SetVisible(bool visible)
        {
            if (_container != null) _container.SetActive(visible);
        }

        private void ResolvePassiveLabel()
        {
            if (_text == null) return;
            if (!ServiceLocator.TryGetService<IPlayerService>(out var ps) || ps == null) return;

            var passive = ps.CurrentHero?.Passive;
            if (passive != null && !string.IsNullOrEmpty(passive.DisplayName))
                _text.text = Rollgeon.Localization.LocalizedContent.Name(passive.PassiveId, passive.DisplayName);
        }
    }
}
