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
using Rollgeon.Loot;
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
        private const string LootFolder = "Assets/Rollgeon/Loot";
        private const string ConfigPath = ChestFolder + "/ChestConfig.asset";
        private const string LootPoolPath = ChestFolder + "/ChestLootPool.asset";
        private const string BootstrapPath = ChestFolder + "/ChestManagerBootstrap.asset";
        private const string ChestPrefabPath = ChestFolder + "/PF_ChestPlaceholder.prefab";
        private const string ServiceBootstrapPath = "Assets/Rollgeon/ServiceBootstrap.asset";
        private const string MeleeEnemyPath = "Assets/Rollgeon/Enemies/ED_MeleeCardEnemy.asset";

        private const string ChestArtPrefabPath = "Assets/Prefabs/Enemies/Chest_Prefab.prefab";
        private const string MimicArtPrefabPath = "Assets/Prefabs/Enemies/ChestMimic_Prefab.prefab";
        private const string MimicAttackClipPath =
            "Assets/Art/3D/Animations/Enemies/ChestMimic/Anim_ChestMimic_Attack.anim";

        [MenuItem("Rollgeon/Chests/Setup All")]
        public static void SetupAll()
        {
            CreateChestPrefab();
            CreateAssets();
            EnablePlayerTargetingOfChests();
            SetupArtPrefabs();
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
        // 4 - Prefabs de arte (Chest_Prefab + ChestMimic_Prefab)
        // ================================================================

        /// <summary>
        /// Los prefabs del artista vienen "pelados" (modelo + Animator). Este paso
        /// los deja aptos para gameplay: <see cref="EntityPawn"/> con HP bar clonada
        /// del Melee, <see cref="Rollgeon.Feedback.PawnRegistryBinding"/> (sin él
        /// FeedbackManager no resuelve el pawn ⇒ ni anim de Attack ni VFX/impulse),
        /// <see cref="Rollgeon.Feedback.AnimationFeedbackEvent"/> junto al Animator
        /// del Mimic, y el Animation Event <c>hit</c> en el clip de ataque (sin él
        /// el step del feedback termina por timeout en vez del frame de impacto).
        /// </summary>
        [MenuItem("Rollgeon/Chests/4 - Setup Art Prefabs")]
        public static void SetupArtPrefabs()
        {
            SetupArtPrefab(ChestArtPrefabPath);
            SetupArtPrefab(MimicArtPrefabPath);
            EnsureMimicAttackHitEvent();
            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + "Prefabs de arte listos para gameplay.");
        }

        private static void SetupArtPrefab(string prefabPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                Debug.LogError(LogPrefix + prefabPath + " no encontrado — ¿falta mergear el arte?");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var pawn = root.GetComponent<EntityPawn>();
                if (pawn == null) pawn = root.AddComponent<EntityPawn>();

                if (root.GetComponent<Rollgeon.Feedback.PawnRegistryBinding>() == null)
                    root.AddComponent<Rollgeon.Feedback.PawnRegistryBinding>();

                var healthBar = root.GetComponentInChildren<WorldSpaceHealthBar>(true);
                if (healthBar == null)
                    healthBar = TryCloneMeleeHealthBar(root.transform);

                if (healthBar != null)
                {
                    var so = new SerializedObject(pawn);
                    so.FindProperty("_healthBar").objectReferenceValue = healthBar;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    Debug.LogWarning(LogPrefix + prefabPath + ": sin HP bar del Melee para clonar — queda sin barra.");
                }

                // El receptor de Animation Events tiene que convivir con el Animator
                // (los eventos del clip se despachan sobre ese GameObject).
                var animator = root.GetComponentInChildren<Animator>(true);
                if (animator != null
                    && animator.GetComponent<Rollgeon.Feedback.AnimationFeedbackEvent>() == null)
                {
                    animator.gameObject.AddComponent<Rollgeon.Feedback.AnimationFeedbackEvent>();
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log(LogPrefix + prefabPath + " cableado (pawn + binding + HP bar + anim events).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Espejo del evento del Goblin (Anim_Goblin_Attack: PushFeedbackEvent("hit")
        // a mitad de clip): el step 'anim.enemy.melee.attack' de los melee termina
        // con EndOnEventKey "hit", que marca el frame de impacto.
        private static void EnsureMimicAttackHitEvent()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MimicAttackClipPath);
            if (clip == null)
            {
                Debug.LogError(LogPrefix + MimicAttackClipPath + " no encontrado.");
                return;
            }

            foreach (var existing in AnimationUtility.GetAnimationEvents(clip))
            {
                if (existing.functionName == "PushFeedbackEvent" && existing.stringParameter == "hit")
                    return;
            }

            var events = new List<AnimationEvent>(AnimationUtility.GetAnimationEvents(clip))
            {
                new AnimationEvent
                {
                    time = clip.length * 0.5f,
                    functionName = "PushFeedbackEvent",
                    stringParameter = "hit",
                },
            };
            AnimationUtility.SetAnimationEvents(clip, events.ToArray());
            EditorUtility.SetDirty(clip);
            Debug.Log(LogPrefix + $"Animation Event 'hit' agregado a {clip.name} en t={clip.length * 0.5f:0.00}s.");
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
                //
                // Solo 4 tiers A PROPÓSITO: item-editor-spec.md §5.3 — el tier Dios NO
                // está confirmado como mecánica de cofre por Diseño (el GDD del Cofre
                // sigue en 4 tiers, y ese conflicto todavía no se comunicó al equipo).
                // ChestConfigSO.RollTier ya no puede sortear un ItemRarity sin entry acá
                // (ver su comentario), así que dejar afuera a God es seguro: ningún cofre
                // va a salir tageado Dios sin un ChestTierDef real.
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

            // Reseed si está vacío o si quedó algún bucket sin LootPool wireado (formato
            // legacy con lista inline, o migración a medias). Data placeholder TBD-01 —
            // el tuning real vive en los LootPool_Chest* que SÍ se respetan si existen.
            bool needsReseed = pool.Buckets == null || pool.Buckets.Count == 0
                               || pool.Buckets.Exists(b => b == null || b.Pool == null);
            if (needsReseed)
            {
                if (pool.Buckets != null && pool.Buckets.Count > 0)
                    Debug.Log(LogPrefix + "Buckets legacy/incompletos — se reseedean referenciando LootPools.");
                var catalog = FindItemCatalog();
                // Mismos 4 tiers que config.Tiers arriba — sin bucket God no hay tier
                // Dios para tenerlo (ver comentario de config.Tiers).
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

        // Bucket del tier: referencia su LootPool genérico + slot de oro. GoldChance
        // queda en el default del campo (~ el viejo slot uniforme 1/(N+1)).
        private static ChestLootBucket NewBucket(ItemCatalogSO catalog, ItemRarity tier, int goldMin, int goldMax)
        {
            return new ChestLootBucket
            {
                Tier = tier,
                Pool = EnsureTierLootPool(catalog, tier),
                GoldMin = goldMin,
                GoldMax = goldMax,
            };
        }

        // LootPool placeholder EDITABLE del tier (se respeta si ya existe): todo el
        // catálogo como candidatos y pesos que sesgan la categoría según el tier —
        // de acá sale la variedad de rarezas del reel (TBD-01: Balance ajusta pesos
        // e ítems en el asset, no acá).
        private static LootPoolSO EnsureTierLootPool(ItemCatalogSO catalog, ItemRarity tier)
        {
            if (!AssetDatabase.IsValidFolder(LootFolder))
                AssetDatabase.CreateFolder("Assets/Rollgeon", "Loot");

            string path = LootFolder + "/LootPool_Chest" + tier + ".asset";
            var lootPool = AssetDatabase.LoadAssetAtPath<LootPoolSO>(path);
            if (lootPool != null) return lootPool;

            lootPool = ScriptableObject.CreateInstance<LootPoolSO>();
            lootPool.Items = new List<ItemSO>();
            if (catalog != null)
            {
                foreach (var entry in catalog.Entries)
                {
                    if (entry != null) lootPool.Items.Add(entry);
                }
            }
            lootPool.Weights = TierWeights(tier);
            AssetDatabase.CreateAsset(lootPool, path);
            Debug.Log(LogPrefix + path + " creado (placeholder editable).");
            return lootPool;
        }

        /// <summary>
        /// Distribución de rarezas del LootPool placeholder de cada tier de cofre.
        /// Switch exhaustivo A PROPÓSITO: antes el <c>default:</c> cubría Common Y
        /// cualquier valor no listado — con God agregado al enum eso significaba
        /// heredar la tabla de Common en silencio si algún día se llamaba con ese
        /// tier. El tier Dios no está seedeado desde <see cref="CreateAssets"/>
        /// (ver su comentario), así que esta rama no debería ejecutarse hoy; si
        /// alguna vez se llama, falla fuerte en vez de degradar.
        /// </summary>
        private static RarityWeights TierWeights(ItemRarity tier)
        {
            switch (tier)
            {
                case ItemRarity.Common:
                    return new RarityWeights { Common = 70f, Uncommon = 25f, Rare = 5f, Legendary = 0f, God = 0f };
                case ItemRarity.Uncommon:
                    return new RarityWeights { Common = 40f, Uncommon = 40f, Rare = 18f, Legendary = 2f, God = 0f };
                case ItemRarity.Rare:
                    return new RarityWeights { Common = 15f, Uncommon = 40f, Rare = 35f, Legendary = 10f, God = 0f };
                case ItemRarity.Legendary:
                    return new RarityWeights { Common = 5f, Uncommon = 20f, Rare = 40f, Legendary = 35f, God = 0f };
                case ItemRarity.God:
                    throw new System.NotSupportedException(
                        "TierWeights(God): el tier de cofre Dios no está confirmado por Diseño " +
                        "(item-editor-spec.md §5.3). Definir la escalera acá antes de sumarlo a " +
                        "config.Tiers/pool.Buckets en CreateAssets().");
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(tier), tier, "ItemRarity sin tabla de pesos en ChestSetupTools.");
            }
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
