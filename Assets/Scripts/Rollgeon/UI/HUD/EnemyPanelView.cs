using System;
using Patterns;
using Rollgeon.Combat.Weakness;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view que muestra nombre + HP + icono de debilidad del enemy targeteado.
    /// Plan §3.4 / §4.4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>HP</b>: suscribe <see cref="TypedEvent{T}"/> de <see cref="HealthChangedPayload"/>,
    /// filtra por <c>EntityGuid == _targetGuid</c>.
    /// </para>
    /// <para>
    /// <b>Weakness</b>: en <see cref="SetTarget"/> consulta <see cref="IWeaknessRegistry"/>
    /// (opcional) y muestra el icono si hay weakness registrada. Si no hay servicio
    /// o no hay entry, oculta el weakness root sin warning.
    /// </para>
    /// <para>
    /// <b>Destruccion</b>: al recibir <see cref="EventName.OnEntityDestroyed"/> del
    /// target, oculta el panel entero.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Enemy Panel View")]
    public class EnemyPanelView : MonoBehaviour
    {
        private const string LogPrefix = "[EnemyPanelView] ";

        [Title("Enemy Panel — Widget refs")]
        [SerializeField]
        [Tooltip("Root del panel. Se desactiva cuando no hay target o el target murio.")]
        private GameObject _panelRoot;

        [SerializeField]
        [Tooltip("Label del nombre del enemy. El nombre lo setea el CombatController via SetNameText.")]
        private TextMeshProUGUI _name;

        [SerializeField]
        [Tooltip("Slider de HP. Fill-only — sin Handle visible.")]
        private Slider _hpSlider;

        [SerializeField]
        [Tooltip("Label de HP (formato '{0}/{1}' default).")]
        private TextMeshProUGUI _hpText;

        [SerializeField]
        [Tooltip("Formato del label de HP. Default '{0}/{1}'.")]
        private string _hpTextFormat = "{0}/{1}";

        [Title("Enemy Panel — Weakness")]
        [SerializeField]
        [Tooltip("Root del indicador de weakness. Se apaga cuando el enemy no tiene debilidad.")]
        private GameObject _weaknessRoot;

        [SerializeField]
        [Tooltip("Image del icono de weakness. Sprite default viene del prefab; si hay " +
                 "catalog cableado, se reemplaza por el mapeado al comboId.")]
        private Image _weaknessIcon;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private Guid _targetGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private Action<HealthChangedPayload> _onHealthChanged;

        /// <summary>
        /// Guid del target actual. <see cref="Guid.Empty"/> si no hay target.
        /// </summary>
        public Guid CurrentTarget => _targetGuid;

        /// <summary>
        /// Bind standard — playerGuid se guarda por simetria con las otras sub-views
        /// pero no se usa para filtrar (el filter es por <see cref="_targetGuid"/>).
        /// </summary>
        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();

            _playerGuid = playerGuid;

            _onHealthChanged = HandleHealthChanged;
            TypedEvent<HealthChangedPayload>.Subscribe(_onHealthChanged);

            EventManager.Subscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);
            _bound = true;
        }

        /// <summary>Desuscribe. Idempotente.</summary>
        public void Unbind()
        {
            if (!_bound) return;
            if (_onHealthChanged != null)
            {
                TypedEvent<HealthChangedPayload>.Unsubscribe(_onHealthChanged);
                _onHealthChanged = null;
            }
            EventManager.UnSubscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);
            _bound = false;
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        // ======================================================================
        // API publica
        // ======================================================================

        /// <summary>
        /// Cambia el target. <see cref="Guid.Empty"/> esconde el panel.
        /// </summary>
        public void SetTarget(Guid enemyGuid)
        {
            _targetGuid = enemyGuid;

            if (enemyGuid == Guid.Empty)
            {
                SetPanelVisible(false);
                return;
            }

            SetPanelVisible(true);
            RefreshWeakness();
        }

        /// <summary>
        /// Setea el nombre mostrado (el pipeline actual no tiene <c>IEntityNameService</c> —
        /// el <c>CombatController</c> invoca este hook).
        /// </summary>
        public void SetNameText(string displayName)
        {
            if (_name != null)
            {
                _name.text = displayName ?? string.Empty;
            }
        }

        /// <summary>Pinta el slider + label con current/max. Publico para tests/tooling.</summary>
        public void SetHp(int current, int max)
        {
            if (_hpSlider != null)
            {
                _hpSlider.value = max > 0 ? (float)current / max : 0f;
            }
            if (_hpText != null)
            {
                _hpText.text = string.Format(_hpTextFormat, current, max);
            }
        }

        // ======================================================================
        // Internals
        // ======================================================================

        private void SetPanelVisible(bool visible)
        {
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(visible);
            }
        }

        private void RefreshWeakness()
        {
            if (_weaknessRoot == null) return;

            if (_targetGuid == Guid.Empty)
            {
                _weaknessRoot.SetActive(false);
                return;
            }

            // El servicio es opcional — si no esta, ocultamos sin warning.
            if (!ServiceLocator.TryGetService<IWeaknessRegistry>(out var registry) || registry == null)
            {
                _weaknessRoot.SetActive(false);
                return;
            }

            if (!registry.TryGet(_targetGuid, out var data) || string.IsNullOrEmpty(data.comboId))
            {
                _weaknessRoot.SetActive(false);
                return;
            }

            _weaknessRoot.SetActive(true);
            // El sprite default viene del prefab; un catalog combo->sprite se agregaria
            // a futuro (plan §3.4). Hoy sirve el Image.sprite autorado.
        }

        private void HandleHealthChanged(HealthChangedPayload payload)
        {
            if (payload.EntityGuid != _targetGuid) return;
            SetHp(payload.Current, payload.Max);
        }

        private void HandleEntityDestroyed(params object[] args)
        {
            if (args == null || args.Length < 1)
            {
                Debug.LogWarning(LogPrefix + "OnEntityDestroyed args malformed (len < 1).", this);
                return;
            }
            if (!(args[0] is Guid guid))
            {
                Debug.LogWarning(LogPrefix + "OnEntityDestroyed args[0] is not Guid.", this);
                return;
            }
            if (guid != _targetGuid) return;

            SetPanelVisible(false);
        }
    }
}
