using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Rollgeon.Chests;
using Rollgeon.Effects;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Entities.Visuals;
using Rollgeon.Heroes;
using Rollgeon.Items;
using Rollgeon.Patterns.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Chests
{
    /// <summary>
    /// Installer de la mecánica de Cofre (Feature#0046). Idempotente — re-correr
    /// actualiza sin duplicar. "Setup All" deja el sistema jugable: assets de
    /// config/pool/bootstrap cableados al ServiceBootstrap, prefab placeholder del
    /// cofre con HP bar, y los SelectionSettings de ataque del jugador ampliados a
    /// <c>Enemies | Props</c> (sin esto el jugador no puede targetear el cofre —
    /// el paso más olvidable del feature, por eso está automatizado).
    /// </summary>
    public static class ChestSetupTools
    {
        private const string LogPrefix = "[ChestSetup] ";

        private const string ChestFolder = "Assets/Rollgeon/Chests";
        private const string ConfigPath = ChestFolder + "/ChestConfig.asset";
        private const string LootPoolPath = ChestFolder + "/ChestLootPool.asset";
        private const string BootstrapPath = ChestFolder + "/ChestManagerBootstrap.asset";
        private const string ChestPrefabPath = ChestFolder + "/PF_ChestPlaceholder.prefab";
        private const string ServiceBootstrapPath = "Assets/Rollgeon/ServiceBootstrap.asset";
        private const string MeleeEnemyPath = "Assets/Rollgeon/Enemies/ED_MeleeCardEnemy.asset";

        [MenuItem("Rollgeon/Chests/Setup All")]
        public static void SetupAll()
        {
            CreateChestPrefab();
            CreateAssets();
            EnablePlayerTargetingOfChests();
            Debug.Log(LogPrefix + "Setup completo.");
        }

        // ================================================================
        // 1 - Prefab placeholder (Body + Fittings + EntityPawn + HP bar)
        // ================================================================

        [MenuItem("Rollgeon/Chests/1 - Create Chest Prefab")]
        public static void CreateChestPrefab()
        {
            EnsureFolder();

            var root = new GameObject("PF_ChestPlaceholder");
            try
            {
                // Cuerpo (madera) — recibe el tint del tier por nombre 'Body'.
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                body.transform.localScale = new Vector3(0.9f, 0.7f, 0.6f);

                // Herrajes — banda superior, tint 'Fittings'.
                var fittings = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fittings.name = "Fittings";
                fittings.transform.SetParent(root.transform, false);
                fittings.transform.localPosition = new Vector3(0f, 0.72f, 0f);
                fittings.transform.localScale = new Vector3(0.95f, 0.12f, 0.65f);

                var pawn = root.AddComponent<EntityPawn>();
                root.AddComponent<Rollgeon.Feedback.PawnRegistryBinding>();

                // HP bar: se clona la del visual del Melee para heredar sprites y layout
                // en vez de reconstruir el widget a mano.
                var healthBar = TryCloneMeleeHealthBar(root.transform);
                if (healthBar != null)
                {
                    var so = new SerializedObject(pawn);
                    so.FindProperty("_healthBar").objectReferenceValue = healthBar;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    Debug.LogWarning(LogPrefix + "No se pudo clonar la HP bar del Melee — el cofre queda sin barra visible.");
                }

                PrefabUtility.SaveAsPrefabAsset(root, ChestPrefabPath);
                Debug.Log(LogPrefix + ChestPrefabPath + " creado/actualizado.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static WorldSpaceHealthBar TryCloneMeleeHealthBar(Transform parent)
        {
            var melee = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(MeleeEnemyPath);
            var sourceBar = melee != null && melee.VisualPrefab != null
                ? melee.VisualPrefab.GetComponentInChildren<WorldSpaceHealthBar>(true)
                : null;
            if (sourceBar == null) return null;

            var clone = Object.Instantiate(sourceBar.gameObject, parent, worldPositionStays: false);
            clone.name = sourceBar.gameObject.name;
            clone.transform.localPosition = sourceBar.transform.localPosition;
            return clone.GetComponent<WorldSpaceHealthBar>();
        }

        // ================================================================
        // 2 - Assets de datos + wiring al ServiceBootstrap
        // ================================================================

        [MenuItem("Rollgeon/Chests/2 - Create Assets")]
        public static void CreateAssets()
        {
            EnsureFolder();

            // ---- Config -------------------------------------------------
            var config = AssetDatabase.LoadAssetAtPath<ChestConfigSO>(ConfigPath);
            bool freshConfig = config == null;
            if (freshConfig)
            {
                config = ScriptableObject.CreateInstance<ChestConfigSO>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            if (config.Tiers == null || config.Tiers.Count == 0)
            {
                // Placeholders del GDD: HP por tier a escala del daño actual, stats de
                // Mimic 40/55/70/85% (TBD-15), oro de Mimic/fallback (TBD-16/TBD-01).
                config.Tiers = new List<ChestTierDef>
                {
                    NewTier(ItemRarity.Common, 20, 0.40f, 8, 12, 8),
                    NewTier(ItemRarity.Uncommon, 30, 0.55f, 12, 18, 12),
                    NewTier(ItemRarity.Rare, 40, 0.70f, 18, 26, 18),
                    NewTier(ItemRarity.Legendary, 55, 0.85f, 28, 40, 28),
                };
            }

            if (config.ChestPrefab == null)
                config.ChestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefabPath);
            if (config.MimicEnemy == null)
                config.MimicEnemy = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(MeleeEnemyPath);
            EditorUtility.SetDirty(config);

            // ---- Loot pool ---------------------------------------------
            var pool = AssetDatabase.LoadAssetAtPath<ChestLootPoolSO>(LootPoolPath);
            if (pool == null)
            {
                pool = ScriptableObject.CreateInstance<ChestLootPoolSO>();
                AssetDatabase.CreateAsset(pool, LootPoolPath);
            }

            if (pool.Buckets == null || pool.Buckets.Count == 0)
            {
                var catalog = FindItemCatalog();
                pool.Buckets = new List<ChestLootBucket>
                {
                    NewBucket(catalog, ItemRarity.Common, 8, 14),
                    NewBucket(catalog, ItemRarity.Uncommon, 14, 22),
                    NewBucket(catalog, ItemRarity.Rare, 22, 34),
                    NewBucket(catalog, ItemRarity.Legendary, 34, 50),
                };
            }
            EditorUtility.SetDirty(pool);

            // ---- Manager bootstrap -------------------------------------
            var bootstrap = AssetDatabase.LoadAssetAtPath<ChestManagerBootstrap>(BootstrapPath);
            if (bootstrap == null)
            {
                bootstrap = ScriptableObject.CreateInstance<ChestManagerBootstrap>();
                AssetDatabase.CreateAsset(bootstrap, BootstrapPath);
            }
            var bootstrapSo = new SerializedObject(bootstrap);
            bootstrapSo.FindProperty("_config").objectReferenceValue = config;
            bootstrapSo.FindProperty("_lootPool").objectReferenceValue = pool;
            bootstrapSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- ServiceBootstrap wiring -------------------------------
            var serviceBootstrap = AssetDatabase.LoadAssetAtPath<ServiceBootstrapSO>(ServiceBootstrapPath);
            if (serviceBootstrap == null)
            {
                Debug.LogError(LogPrefix + ServiceBootstrapPath + " no encontrado — agregar el bootstrap a mano.");
            }
            else
            {
                serviceBootstrap.ExtraServices ??= new List<IPreloadableService>();
                if (!serviceBootstrap.ExtraServices.Any(s => s is ChestManagerBootstrap))
                {
                    serviceBootstrap.ExtraServices.Add(bootstrap);
                    EditorUtility.SetDirty(serviceBootstrap);
                    Debug.Log(LogPrefix + "ChestManagerBootstrap → ServiceBootstrap.ExtraServices.");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + "Assets de datos creados/cableados.");
        }

        private static ChestTierDef NewTier(
            ItemRarity tier, int maxHp, float mimicScale, int goldMin, int goldMax, int fallbackGold)
        {
            return new ChestTierDef
            {
                Tier = tier,
                MaxHP = maxHp,
                BodyColor = RarityPalette.BodyColor(tier),
                MimicStatScale = mimicScale,
                MimicGoldMin = goldMin,
                MimicGoldMax = goldMax,
                FallbackGold = fallbackGold,
            };
        }

        // Bucket seedeado desde el catálogo por rareza (placeholder TBD-01: el pool
        // real por tier lo define Balance; mientras, cada tier ofrece sus ítems de
        // esa rareza).
        private static ChestLootBucket NewBucket(ItemCatalogSO catalog, ItemRarity tier, int goldMin, int goldMax)
        {
            var bucket = new ChestLootBucket { Tier = tier, GoldMin = goldMin, GoldMax = goldMax };
            if (catalog != null)
            {
                foreach (var item in catalog.GetByRarity(tier))
                {
                    if (item != null) bucket.Items.Add(item);
                }
            }
            return bucket;
        }

        private static ItemCatalogSO FindItemCatalog()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ItemCatalogSO"))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (catalog != null) return catalog;
            }
            Debug.LogWarning(LogPrefix + "ItemCatalogSO no encontrado — buckets sin ítems (solo oro).");
            return null;
        }

        // ================================================================
        // 3 - SelectionSettings del jugador: Enemies → Enemies | Props
        // ================================================================

        /// <summary>
        /// Recorre los grafos Odin de todos los <see cref="ClassHeroSO"/> buscando
        /// <see cref="BaseEffect.Selection"/> ofensivos (filter con <c>Enemies</c>)
        /// y les suma el bit <c>Props</c> para que el ataque pueda targetear cofres.
        /// </summary>
        [MenuItem("Rollgeon/Chests/3 - Enable Player Targeting Of Chests")]
        public static void EnablePlayerTargetingOfChests()
        {
            int patched = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:ClassHeroSO"))
            {
                var hero = AssetDatabase.LoadAssetAtPath<ClassHeroSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (hero == null) continue;

                int before = patched;
                foreach (var effect in CollectEffects(hero))
                {
                    var selection = effect.Selection;
                    if (selection == null) continue;
                    if ((selection.EntityFilter & EntityFilterMask.Enemies) == 0) continue;
                    if ((selection.EntityFilter & EntityFilterMask.Props) != 0) continue;
                    selection.EntityFilter |= EntityFilterMask.Props;
                    patched++;
                }

                if (patched > before) EditorUtility.SetDirty(hero);
            }

            if (patched > 0) AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + $"SelectionSettings ofensivos parcheados a Enemies|Props: {patched}.");
        }

        // Walker genérico del grafo Odin del hero: junta todos los BaseEffect
        // alcanzables por campos (listas anidadas incluidas). Editor-only, así que
        // el costo de reflexión no importa.
        private static IEnumerable<BaseEffect> CollectEffects(object root)
        {
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var stack = new Stack<object>();
            stack.Push(root);
            var results = new List<BaseEffect>();

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (current == null || !visited.Add(current)) continue;

                if (current is BaseEffect effect) results.Add(effect);
                if (current is string) continue;

                if (current is System.Collections.IEnumerable enumerable)
                {
                    foreach (var element in enumerable) stack.Push(element);
                    continue;
                }

                var type = current.GetType();
                if (type.IsPrimitive) continue;
                if (current is UnityEngine.Object && current is not ScriptableObject) continue;
                // No cruzar a OTROS assets (catálogos, prefabs) — solo el grafo del hero.
                if (current is ScriptableObject so && !ReferenceEquals(so, root)) continue;

                for (var t = type; t != null && t != typeof(object); t = t.BaseType)
                {
                    foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (field.FieldType.IsPrimitive || field.FieldType.IsEnum || field.FieldType == typeof(string)) continue;
                        object value;
                        try { value = field.GetValue(current); }
                        catch { continue; }
                        if (value == null) continue;
                        if (value is UnityEngine.Object uo && uo is not ScriptableObject) continue;
                        stack.Push(value);
                    }
                }
            }
            return results;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);
            int IEqualityComparer<object>.GetHashCode(object obj) =>
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(ChestFolder))
                AssetDatabase.CreateFolder("Assets/Rollgeon", "Chests");
        }
    }
}
