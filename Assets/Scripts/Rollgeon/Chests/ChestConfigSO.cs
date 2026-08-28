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

        // Dios (item-editor-spec.md §5.1/§5.3): default 0 — ningún cofre pesa Dios
        // hasta que Diseño confirme el tier (el GDD del Cofre sigue en 4 tiers).
        // Field nuevo en una clase ya serializada: Odin lo completa con este
        // default en las entries existentes de ChestConfig.asset, sin migración.
        [MinValue(0f)] public float God = 0f;

        /// <summary>
        /// Peso del tier. Switch exhaustivo A PROPÓSITO: antes de agregar
        /// <see cref="ItemRarity.God"/> el <c>default:</c> devolvía <see cref="Common"/>
        /// para cualquier valor no listado — con 5 rarezas eso pesaba un cofre Dios
        /// como Normal sin un solo error. Ahora cada caso es explícito y un valor
        /// realmente inesperado revienta en vez de degradar en silencio.
        /// </summary>
        public float WeightFor(ItemRarity tier)
        {
            switch (tier)
            {
                case ItemRarity.Common: return Common;
                case ItemRarity.Uncommon: return Uncommon;
                case ItemRarity.Rare: return Rare;
                case ItemRarity.Legendary: return Legendary;
                case ItemRarity.God: return God;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(tier), tier, "ItemRarity sin peso definido en ChestFloorTierWeights.");
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
        [Tooltip("Prefab del cofre (todos los tiers, salvo tier con ChestPrefabOverride). " +
                 "Debe tener EntityPawn con WorldSpaceHealthBar; los slots de material " +
                 "'Wood'/'Frame' (fallback: renderers 'Body'/'Fittings') reciben el visual por tier.")]
        public GameObject ChestPrefab;

        [Tooltip("Color de herrajes/bisagras/cerradura, común a todos los tiers (#5F737A).")]
        public Color FittingsColor = new Color32(0x5F, 0x73, 0x7A, 0xFF);

        [Title("Tiers")]
        [ListDrawerSettings(ShowFoldout = false)]
        [Tooltip("Config por tier. Idealmente una entry por ItemRarity, pero el tier Dios " +
                 "(item-editor-spec.md §5.3) NO está confirmado como mecánica de cofre por " +
                 "Diseño todavía — hoy la lista tiene 4 entries a propósito. Un ItemRarity " +
                 "sin entry acá cae a Tiers[0] en GetTierDef (fallback defensivo documentado, " +
                 "no un bug) y RollTier ya no puede sortearlo (ver su comentario).")]
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
        /// uniforme entre los tiers CONFIGURADOS. Determinista respecto del
        /// <paramref name="rng"/>.
        /// </summary>
        /// <remarks>
        /// El universo de tiers roleables sale de <see cref="Tiers"/>, no de
        /// <c>Enum.GetValues(typeof(ItemRarity))</c>. Con 5 valores de rareza y el
        /// tier Dios sin <see cref="ChestTierDef"/> confirmado (ver comentario en
        /// <see cref="Tiers"/>), sortear directo del enum podía devolver
        /// <see cref="ItemRarity.God"/> en el fallback uniforme aunque no hubiera
        /// def ni bucket de loot para ese tier: <see cref="GetTierDef"/> caía a
        /// Tiers[0] en silencio (cofre visualmente Normal pero tageado Dios) y
        /// <c>ChestLootPoolSO.Roll</c> degradaba a oro CERO (sin bucket God ⇒
        /// oro 0) — el peor de los dos silencios posibles. Ahora un ItemRarity sin
        /// entry en <see cref="Tiers"/> nunca puede salir sorteado.
        /// </remarks>
        public ItemRarity RollTier(System.Random rng, int floorNumber)
        {
            var weights = ResolveWeights(floorNumber);
            var tiers = ConfiguredTierValues();
            if (tiers.Length == 0) return ItemRarity.Common; // Config sin Tiers — no debería pasar en producción.

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

        // Tiers válidos (no-null) en el orden autorado de la lista — el orden no
        // afecta la probabilidad (uniforme o por peso acumulado), solo importa
        // para que el fallback por drift de punto flotante sea determinista.
        private ItemRarity[] ConfiguredTierValues()
        {
            var result = new List<ItemRarity>(Tiers.Count);
            for (int i = 0; i < Tiers.Count; i++)
            {
                if (Tiers[i] != null) result.Add(Tiers[i].Tier);
            }
            return result.ToArray();
        }
    }
}
