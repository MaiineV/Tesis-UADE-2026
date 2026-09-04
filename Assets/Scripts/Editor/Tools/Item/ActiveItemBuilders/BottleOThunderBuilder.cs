using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Items;
using Rollgeon.Tiles;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item.ActiveItemBuilders
{
    /// <summary>
    /// Efectos de Bottle'o Thunder (Feature#0084 §7, Jerarquía D4). Un único grupo
    /// (<c>OnPositiveBand</c>): cadena de aturdimiento hasta la cara del dado, más 2 Charcos
    /// Eléctricos anclados cerca del objetivo primario.
    /// </summary>
    public static class BottleOThunderBuilder
    {
        private const string ElectricPuddlePath = "Assets/Rollgeon/Tiles/Tile_ElectricPuddle.asset";

        public static void Build(ItemSO item)
        {
            if (item == null) return;

            var chainStun = new EffChainStun
            {
                Turns = 1,
                BounceRange = 2,
                Selection = new SelectionSettings
                {
                    SlotState = SlotState.Occupied,
                    EntityFilter = EntityFilterMask.Enemies,
                    Range = 4,
                    RangeMode = RangeMode.Manhattan,
                    TargetMode = TargetMode.Single,
                },
            };

            var puddle = AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(ElectricPuddlePath);
            if (puddle == null)
            {
                Debug.LogWarning($"[BottleOThunderBuilder] no se encontró '{ElectricPuddlePath}' — " +
                                 "EffSpawnRuntimeTile queda sin Definition, autorarla a mano.");
            }

            var spawnTile = new EffSpawnRuntimeTile
            {
                Definition = puddle,
                Count = 2,
                DurationRounds = 2,
                MaxRadius = 3,
            };

            item.OnPositiveBand.Effects.Add(chainStun);
            item.OnPositiveBand.Effects.Add(spawnTile);
        }
    }
}
