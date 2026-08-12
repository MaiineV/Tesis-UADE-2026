using System;
using System.Collections.Generic;
using Rollgeon.Entities;
using Rollgeon.Items;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Chests
{
    /// <summary>
    /// Pesos de tier de cofre a partir de un piso dado (TBD-02 del GDD como data:
    /// el mecanismo de asignación quedó pendiente de Diseño, así que es una tabla
    /// editable — se aplica la entry con el <see cref="FloorNumber"/> más alto
    /// que sea &lt;= al piso actual).
    /// </summary>
    [Serializable]
    public class ChestFloorTierWeights
    {
        [MinValue(1)]
        public int FloorNumber = 1;

        [MinValue(0f)] public float Common = 1f;
        [MinValue(0f)] public float Uncommon = 0f;
        [MinValue(0f)] public float Rare = 0f;
        [MinValue(0f)] public float Legendary = 0f;

        public float WeightFor(ItemRarity tier)
        {
            switch (tier)
            {
                case ItemRarity.Uncommon: return Uncommon;
                case ItemRarity.Rare: return Rare;
                case ItemRarity.Legendary: return Legendary;
                default: return Common;
            }
        }
    }

    /// <summary>
    /// Config global de la mecánica de Cofre (GDD §21). Todos los valores marcados
    /// como "de prueba" en el GDD viven acá para que Balance los ajuste sin código.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Chests/Chest Config", fileName = "ChestConfig")]
    public sealed class ChestConfigSO : SerializedScriptableObject
    {
        [Title("Spawn")]
        [Range(0f, 1f)]
        [Tooltip("SpawnFrequency (GDD §21): probabilidad de que una sala de combate genere cofre. Valor de prueba.")]
        public float SpawnFrequency = 0.33f;

        [Range(0f, 1f)]
        [Tooltip("MimicSpawnChance (GDD §21): probabilidad de que un cofre generado sea Mimic. Valor de prueba.")]
        public float MimicSpawnChance = 0.15f;

        [MinValue(0)]
        [Tooltip("MimicClampHP (GDD §21): piso de HP del Mimic ante daño no-jugador.")]
        public int MimicClampHP = 1;

        [Title("Visual")]
        [Tooltip("Prefab del cofre. Debe tener EntityPawn con WorldSpaceHealthBar; " +
                 "renderers llamados 'Body' y 'Fittings' reciben el tint por tier.")]
        public GameObject ChestPrefab;

        [Tooltip("Color de herrajes/bisagras/cerradura, común a todos los tiers (#5F737A).")]
        public Color FittingsColor = new Color32(0x5F, 0x73, 0x7A, 0xFF);

        [Title("Tiers")]
        [ListDrawerSettings(ShowFoldout = false)]
        [Tooltip("Config por tier. Debe haber exactamente una entry por ItemRarity.")]
        public List<ChestTierDef> Tiers = new List<ChestTierDef>();

        [Tooltip("Pesos de tier según el piso (TBD-02 como data). Vacío = uniforme.")]
        [ListDrawerSettings(ShowFoldout = false)]
        public List<ChestFloorTierWeights> TierWeightsByFloor = new List<ChestFloorTierWeights>();

        [Title("Mimic")]
        [Tooltip("Arquetipo del Mimic activado. El GDD lo fija al Melee existente " +
                 "(ED_MeleeCardEnemy): misma IA y behaviors, stats escaladas por tier.")]
        public EnemyDataSO MimicEnemy;

        [Tooltip("Hint del Mimic dormido (GDD §18, 'leve movimiento'): segundos mínimos " +
                 "entre twitches del cofre. Valor de prueba.")]
        [MinValue(0f)]
        public float MimicHintMinSeconds = 6f;

        [Tooltip("Segundos máximos entre twitches del hint.")]
        [MinValue(0f)]
        public float MimicHintMaxSeconds = 12f;

        [Tooltip("Duración de cada twitch, en segundos.")]
        [MinValue(0f)]
        public float MimicHintDuration = 0.4f;

        [Tooltip("Amplitud del twitch, en grados de roll del modelo.")]
        [MinValue(0f)]
        public float MimicHintAngleDegrees = 4f;

        /// <summary>Def del tier pedido, o la primera como fallback defensivo.</summary>
        public ChestTierDef GetTierDef(ItemRarity tier)
        {
            for (int i = 0; i < Tiers.Count; i++)
            {
                if (Tiers[i] != null && Tiers[i].Tier == tier) return Tiers[i];
            }
            return Tiers.Count > 0 ? Tiers[0] : null;
        }

        /// <summary>
        /// Entry de pesos aplicable al piso (la de <c>FloorNumber</c> más alto que sea
        /// &lt;= <paramref name="floorNumber"/>). <c>null</c> = tabla vacía o ninguna
        /// aplica ⇒ el caller rolea uniforme.
        /// </summary>
        public ChestFloorTierWeights ResolveWeights(int floorNumber)
        {
            ChestFloorTierWeights best = null;
            for (int i = 0; i < TierWeightsByFloor.Count; i++)
            {
                var entry = TierWeightsByFloor[i];
                if (entry == null || entry.FloorNumber > floorNumber) continue;
                if (best == null || entry.FloorNumber > best.FloorNumber) best = entry;
            }
            return best;
        }

        /// <summary>
        /// Rolea el tier del cofre para el piso dado. Tabla vacía / pesos en 0 ⇒
        /// uniforme entre los 4 tiers. Determinista respecto del <paramref name="rng"/>.
        /// </summary>
        public ItemRarity RollTier(System.Random rng, int floorNumber)
        {
            var weights = ResolveWeights(floorNumber);
            var tiers = (ItemRarity[])Enum.GetValues(typeof(ItemRarity));

            if (weights == null) return tiers[rng.Next(tiers.Length)];

            float total = 0f;
            for (int i = 0; i < tiers.Length; i++) total += weights.WeightFor(tiers[i]);
            if (total <= 0f) return tiers[rng.Next(tiers.Length)];

            float pick = (float)rng.NextDouble() * total;
            float cursor = 0f;
            for (int i = 0; i < tiers.Length; i++)
            {
                cursor += weights.WeightFor(tiers[i]);
                if (pick <= cursor) return tiers[i];
            }
            return tiers[tiers.Length - 1];
        }
    }
}
