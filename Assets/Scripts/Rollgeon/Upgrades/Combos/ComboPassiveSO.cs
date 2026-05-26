using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Effects.Readers;
using Rollgeon.Shop;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Combos
{
    /// <summary>
    /// Pasiva de combo del Sistema de Mejoras In-Run. Se compra en la tienda
    /// (Phase 8 wiring) y se aplica al <see cref="TargetComboId"/> del Contrato.
    /// Combina un bonus plano (<see cref="FlatDamageBonus"/>) con triggers
    /// composables (<see cref="ExtraTriggers"/>) para efectos más ricos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Stacking.</b> Multiples pasivas pueden tener el mismo <see cref="TargetComboId"/> —
    /// se suman en el dispatch. El <c>ComboPassiveService</c> almacena
    /// <c>Dictionary&lt;comboId, List&lt;ComboPassiveSO&gt;&gt;</c>.
    /// </para>
    /// <para>
    /// <b>Scaling Balatro-style.</b> Para el caso "+X daño por cada nivel de Par",
    /// el designer setea <see cref="FlatDamageBonus"/> como un
    /// <c>ReadComboCounter(ComboId='combo.par')</c> multiplicado por una constante.
    /// El service no implementa scaling explícito — todo via readers.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Upgrades/Combos/Combo Passive",
        fileName = "ComboPassive")]
    public class ComboPassiveSO : UpgradeSO, IShopRewardEntry
    {
        // ---- IShopRewardEntry (UpgradeSO provee DisplayName/Description/Icon ya como
        // properties que satisfacen la interface implícitamente; EntryId mapea
        // a UpgradeId via explicit impl).
        string IShopRewardEntry.EntryId => UpgradeId;

        [Title("Target Combo")]
        [ValueDropdown(nameof(GetComboIds))]
        [Tooltip("ID canónico del combo al que esta pasiva aplica (formato 'combo.<snake_case>'). " +
                 "Cuando ese combo matchee, esta pasiva se activa.")]
        [Required]
        [OdinSerialize]
        protected string _targetComboId;

        [Title("Flat Damage Bonus")]
        [InfoBox("Camino simple: suma plana al daño del combo cuando matchea. " +
                 "Usá ReadConstantInt(5) o, para escalar Balatro-style, ReadComboCounter('combo.par').")]
        [OdinSerialize, SerializeReference]
        protected EffectIntReader _flatDamageBonus;

        [Title("Extra Triggers")]
        [InfoBox("Hooks composables para condiciones avanzadas tipo 'cada vez que matchea " +
                 "escalera, ganás +3 oro'. Cada trigger consume el EffectIntReader que el " +
                 "designer prefiera.")]
        [OdinSerialize, SerializeReference]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        protected List<IComboPassiveTrigger> _extraTriggers = new List<IComboPassiveTrigger>();

        [Title("Shop Pricing")]
        [Tooltip("Oro requerido para comprar esta pasiva en la tienda. La integración con shop " +
                 "lee este valor (Phase 8).")]
        [MinValue(0)]
        [OdinSerialize]
        protected int _shopCost = 50;

        [Title("Visual")]
        [InfoBox("Prefab 3D que se instancia ENCIMA del pedestal en la tienda. " +
                 "Mismo rol que ItemSO.WorldPrefab. Null = pedestal solo (sin visual extra).")]
        [OdinSerialize]
        protected GameObject _worldPrefab;

        /// <inheritdoc />
        public override UpgradeChannel Channel => UpgradeChannel.Combo;

        /// <summary>ID del combo target (key del <see cref="ComboCatalogSO"/>).</summary>
        public string TargetComboId => _targetComboId;

        /// <summary>Bonus plano. Null = sin bonus base (solo triggers).</summary>
        public EffectIntReader FlatDamageBonus => _flatDamageBonus;

        /// <summary>Triggers polimórficos extra.</summary>
        public IReadOnlyList<IComboPassiveTrigger> ExtraTriggers => _extraTriggers;

        /// <summary>Costo en tienda. Phase 8 lo consume.</summary>
        public int ShopCost => _shopCost;

        /// <summary>Prefab 3D del visual sobre el pedestal en tienda. Null = sin visual.</summary>
        public GameObject WorldPrefab => _worldPrefab;

        // ---- Odin dropdown source — mismo patrón que BaseComboSO.GetComboIds ----
        //
        // Runtime: usa el ComboCatalogSO registrado.
        // Edit mode: escanea BaseComboSO assets del proyecto via AssetDatabase
        // (el ServiceLocator esta vacio fuera de Play).

        private static IEnumerable<string> GetComboIds()
        {
            if (Application.isPlaying)
            {
                if (ServiceLocator.TryGetService<ComboCatalogSO>(out var cat) && cat != null)
                    return cat.AllIds;
                return Array.Empty<string>();
            }

#if UNITY_EDITOR
            var ids = new SortedSet<string>();
            var guids = UnityEditor.AssetDatabase.FindAssets("t:BaseComboSO");
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<BaseComboSO>(path);
                if (asset != null && !string.IsNullOrEmpty(asset.ComboId))
                    ids.Add(asset.ComboId);
            }
            return ids;
#else
            return Array.Empty<string>();
#endif
        }
    }
}
