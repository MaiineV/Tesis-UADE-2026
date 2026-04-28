using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Effects;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Items
{
    [CreateAssetMenu(menuName = "Rollgeon/Items/Item")]
    public class ItemSO : SerializedScriptableObject
    {
        [Title("Identity")]
        [ValueDropdown(nameof(GetItemIds))]
        public string ItemId;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;
        public ItemRarity Rarity;

        [Title("Type")]
        [EnumToggleButtons]
        public ItemType Type;

        [Title("Passive Effects")]
        [InfoBox("Se aplican automaticamente al obtener el item. Se remueven si el item se pierde.")]
        [ShowIf("@Type == ItemType.Passive")]
        [ListDrawerSettings(ShowFoldout = false)]
        [OdinSerialize]
        public List<PassiveItemHook> PassiveHooks = new();

        [Title("Active Effects")]
        [InfoBox("Se ejecutan cuando el jugador activa el item. Pueden tener cooldown.")]
        [ShowIf("@Type == ItemType.Active")]
        [OdinSerialize]
        public EffectData OnActivate = new();

        [ShowIf("@Type == ItemType.Active")]
        [InfoBox("Cooldown en turnos. 0 = usable cada turno.")]
        [MinValue(0)]
        public int Cooldown = 0;

        [Title("Action economy")]
        [ShowIf("@Type == ItemType.Active")]
        [InfoBox("Si true, usar este item activo consume un slot del turno.")]
        public bool ConsumesAction = true;

        [ShowIf("@Type == ItemType.Active && ConsumesAction")]
        [InfoBox("ActionId que se registra en action economy. Default: item.<ItemId>.")]
        public string ActionId;

        public string ResolvedActionId => string.IsNullOrEmpty(ActionId) ? $"item.{ItemId}" : ActionId;

        [Title("Visual")]
        [InfoBox("Prefab opcional para la representacion 3D del item en el mundo (pedestal, drop).")]
        public GameObject WorldPrefab;

#if UNITY_EDITOR
        private static IEnumerable<string> GetItemIds() =>
            ServiceLocator.TryGetService<ItemCatalogSO>(out var cat) ? cat.AllIds : Array.Empty<string>();
#endif
    }
}
