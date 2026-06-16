using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades
{
    /// <summary>
    /// Base abstracta del Sistema de Mejoras In-Run. Identidad común a los tres
    /// canales (<see cref="UpgradeChannel"/>): id estable, display, descripción
    /// e icono. Los datos canal-específicos viven en las subclases concretas
    /// (<c>EnchantmentSO</c>, <c>ComboPassiveSO</c>, <c>CharacterRewardSO</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hereda <see cref="SerializedScriptableObject"/></b> (Odin) porque las
    /// subclases serializan listas polimórficas de triggers/filters/readers que
    /// Unity native <c>[SerializeField]</c> no maneja.
    /// </para>
    /// <para>
    /// <b>Data-driven.</b> Los diseñadores crean .asset desde el menú contextual
    /// de Unity sin tocar código. La validación cross-canal (id único, asset
    /// referenciado en pools) se hace via Odin attributes en las subclases.
    /// </para>
    /// </remarks>
    public abstract class UpgradeSO : SerializedScriptableObject
    {
        [Title("Identity")]
        [Tooltip("Id estable del upgrade. Convención: '<channel>.<snake_case>' " +
                 "(ej. 'ench.only_evens', 'combo_pass.par_plus_10', 'char_rew.hp_plus_2').")]
        [Required]
        [OdinSerialize]
        protected string _upgradeId;

        [Tooltip("Nombre legible para UI (tienda, altar, pantalla de reward).")]
        [OdinSerialize]
        protected string _displayName;

        [TextArea(2, 5)]
        [Tooltip("Descripción para tooltips y UI. El texto que ve el jugador al hover.")]
        [OdinSerialize]
        protected string _description;

        [PreviewField(48, ObjectFieldAlignment.Left)]
        [OdinSerialize]
        protected Sprite _icon;

        [Title("Stat Grants (canal único)")]
        [InfoBox("Boosts PERMANENTES (toda la run) a stats del jugador que se aplican al adquirir " +
                 "este upgrade. Attack = daño base del PJ. Compartido por rewards de personaje y " +
                 "pasivas/ítems de tienda. Dejá la lista vacía si el upgrade no toca stats.")]
        [ListDrawerSettings(ShowFoldout = false)]
        [OdinSerialize]
        protected List<StatGrant> _statGrants = new List<StatGrant>();

        /// <summary>Canal al que pertenece este upgrade. Implementado por cada subclase.</summary>
        public abstract UpgradeChannel Channel { get; }

        /// <summary>Boosts permanentes de stat que otorga este upgrade al adquirirse. Puede estar vacía.</summary>
        public IReadOnlyList<StatGrant> StatGrants => _statGrants;

        /// <summary>Id estable. Format <c>'&lt;channel&gt;.&lt;snake_case&gt;'</c>.</summary>
        public string UpgradeId => _upgradeId;

        /// <summary>Nombre legible para UI.</summary>
        public string DisplayName => _displayName;

        /// <summary>Descripción legible para tooltips/UI.</summary>
        public string Description => _description;

        /// <summary>Icono opcional. Puede quedar null hasta el pipeline de arte.</summary>
        public Sprite Icon => _icon;
    }
}
