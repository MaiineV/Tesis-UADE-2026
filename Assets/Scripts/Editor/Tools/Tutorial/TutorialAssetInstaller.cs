using System.Collections.Generic;
using System.IO;
using Rollgeon.Combos;
using Rollgeon.Dungeon;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.Entities;
using Rollgeon.Heroes;
using Rollgeon.Items;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Shop;
using Rollgeon.Tutorial;
using Rollgeon.Tutorial.UI;
using Rollgeon.Upgrades.Dice;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools
{
    /// <summary>
    /// Instalador one-shot de los assets del Tutorial Mode
    /// (<c>Rollgeon → Tutorial → Install Tutorial Assets</c>). Idempotente: re-crea
    /// los assets bajo <c>Assets/Rollgeon/Tutorial/</c> si ya existen y re-wirea el
    /// <c>ServiceBootstrap.asset</c>. Ver <c>docs/setup/tutorial.md</c>.
    /// </summary>
    public static class TutorialAssetInstaller
    {
        private const string Root = "Assets/Rollgeon/Tutorial";
        private const string LogPrefix = "[TutorialAssetInstaller] ";

        // Fuentes existentes.
        private const string WarriorPath = "Assets/Rollgeon/Classes/CH_Warrior.asset";
        private const string MeleePath = "Assets/Rollgeon/Enemies/ED_MeleeCardEnemy.asset";
        private const string StartRoomPath = "Assets/Rollgeon/Rooms/Start_01.asset";
        private const string CombatRoomBPath = "Assets/Rollgeon/Rooms/Room_Combat01.asset";
        private const string CombatRoomCPath = "Assets/Rollgeon/Rooms/Room_Combat02.asset";
        private const string ShopRoomPath = "Assets/Rollgeon/Rooms/Room_Shop.asset";
        private const string EnchantRoomPath = "Assets/Rollgeon/Rooms/Room_Enchantment_01.asset";
        // Fuente del item de la tienda: item real de +daño al Par (item-first desde
        // que la tienda dejó de entregar ComboPassiveSO — vender la pasiva cobraba
        // el oro y no aplicaba NADA ni aparecía en el inventario).
        private const string PairItemPath = "Assets/Rollgeon/Items/Item_GuantesApostadorPar.asset";
        private const string ShopConfigPath = "Assets/Rollgeon/Rooms/Shop/ShopConfig.asset";
        private const string EnchantConfigPath = "Assets/Rollgeon/Upgrades/Dice/EnchantmentConfig.asset";
        private const string ServiceBootstrapPath = "Assets/Rollgeon/ServiceBootstrap.asset";

        // Precio del item de la tienda del tutorial (= drop del enemigo B).
        private const int ShopItemPrice = 20;
        // HP tuneados: B no muere de 1 golpe del Warrior inicial; los de C tampoco.
        private const int EnemyBHp = 60;
        private const int EnemyCHp = 30;

        [MenuItem("Rollgeon/Tutorial/Install Tutorial Assets")]
        public static void Install()
        {
            var warrior = Load<ClassHeroSO>(WarriorPath);
            var melee = Load<EnemyDataSO>(MeleePath);
            var startRoom = Load<RoomSO>(StartRoomPath);
            var combatRoomB = Load<RoomSO>(CombatRoomBPath);
            var combatRoomC = Load<RoomSO>(CombatRoomCPath);
            var shopRoom = Load<RoomSO>(ShopRoomPath);
            var enchantRoom = Load<RoomSO>(EnchantRoomPath);
            var pairItem = Load<ItemSO>(PairItemPath);
            var shopConfig = Load<ShopConfigSO>(ShopConfigPath);
            var enchantConfig = Load<EnchantmentConfigSO>(EnchantConfigPath);
            var serviceBootstrap = Load<ServiceBootstrapSO>(ServiceBootstrapPath);
            if (warrior == null || melee == null || startRoom == null || combatRoomB == null
                || combatRoomC == null || shopRoom == null || enchantRoom == null
                || pairItem == null || shopConfig == null || enchantConfig == null
                || serviceBootstrap == null)
            {
                Debug.LogError(LogPrefix + "Faltan assets fuente (ver errores previos) — instalación abortada.");
                return;
            }

            EnsureFolder();

            // --- Héroe fijo con Force Door de éxito garantizado -------------
            var hero = CloneAsset(warrior, "CH_Warrior_Tutorial", h =>
            {
                h.EntityId = "warrior_tutorial";
                h.DisplayName = "Guerrero (Tutorial)";
                TuneForceDoor(h);
                TuneAttackRolls(h);
            });
            EditorUtility.SetDirty(hero);

            // --- Enemigos tuneados -------------------------------------------
            // B dropea exactamente el precio del item; los dos de C financian
            // exactamente 1 encantamiento (ResolveCost(0) = costo base del altar).
            int enchantCost = enchantConfig.ResolveCost(0);
            int goldPerEnemyC = Mathf.CeilToInt(enchantCost / 2f);

            var enemyB = CloneAsset(melee, "ED_Tutorial_MeleeB", e =>
            {
                e.EntityId = "enemy_tutorial_melee_b";
                e.DisplayName = "Recluta (Tutorial)";
                e.BaseHP = EnemyBHp;
                e.MinGoldDrop = ShopItemPrice;
                e.MaxGoldDrop = ShopItemPrice;
            });
            EditorUtility.SetDirty(enemyB);

            var enemyC = CloneAsset(melee, "ED_Tutorial_MeleeC", e =>
            {
                e.EntityId = "enemy_tutorial_melee_c";
                e.DisplayName = "Matón (Tutorial)";
                e.BaseHP = EnemyCHp;
                e.MinGoldDrop = goldPerEnemyC;
                e.MaxGoldDrop = goldPerEnemyC;
            });
            EditorUtility.SetDirty(enemyC);

            // --- Setups (1 enemigo en B, 2 en C) ------------------------------
            var setupB = CreateOrReplace<EnemySetupSO>("ES_Tutorial_B");
            setupB.SetupName = "Tutorial B (1 melee)";
            setupB.Slots = new List<SetupSlot> { new SetupSlot { SpawnPointIndex = 0, Enemy = enemyB } };
            EditorUtility.SetDirty(setupB);

            var setupC = CreateOrReplace<EnemySetupSO>("ES_Tutorial_C");
            setupC.SetupName = "Tutorial C (2 melee)";
            setupC.Slots = new List<SetupSlot>
            {
                new SetupSlot { SpawnPointIndex = 0, Enemy = enemyC },
                new SetupSlot { SpawnPointIndex = 1, Enemy = enemyC },
            };
            EditorUtility.SetDirty(setupC);

            // --- Rooms (clones con setup fijo) --------------------------------
            // ForcePossibleSetups: el prefab compartido puede traer SpawnPointConfigs
            // (que normalmente ganan) — el tutorial exige EXACTAMENTE su setup
            // (1 enemigo en B, 2 en C).
            var roomB = CloneAsset(combatRoomB, "Room_Tutorial_CombatB", r =>
            {
                r.RoomId = "room_tutorial_combat_b";
                r.DisplayName = "Tutorial — Combate 1";
                r.PossibleSetups = new List<EnemySetupSO> { setupB };
                r.ForcePossibleSetups = true;
            });
            EditorUtility.SetDirty(roomB);

            var roomC = CloneAsset(combatRoomC, "Room_Tutorial_CombatC", r =>
            {
                r.RoomId = "room_tutorial_combat_c";
                r.DisplayName = "Tutorial — Combate 2";
                r.PossibleSetups = new List<EnemySetupSO> { setupC };
                r.ForcePossibleSetups = true;
            });
            EditorUtility.SetDirty(roomC);

            // --- Floor plan ----------------------------------------------------
            var plan = CreateOrReplace<TutorialFloorPlanSO>("TutorialFloorPlan");
            plan.SetEntries(new List<TutorialFloorPlanSO.Entry>
            {
                new TutorialFloorPlanSO.Entry { Cell = new Vector2Int(0, 0), Room = startRoom },
                new TutorialFloorPlanSO.Entry { Cell = new Vector2Int(0, 1), Room = roomB },
                new TutorialFloorPlanSO.Entry { Cell = new Vector2Int(0, 2), Room = roomC },
                new TutorialFloorPlanSO.Entry { Cell = new Vector2Int(-1, 1), Room = shopRoom, StartLocked = true },
                new TutorialFloorPlanSO.Entry { Cell = new Vector2Int(1, 2), Room = enchantRoom },
            });
            EditorUtility.SetDirty(plan);

            // --- Item de la tienda (ItemSO real: aparece en el inventario) ----
            // Clon del item de +daño al Par con el hook re-autorado a +50 fijo.
            // El clone hereda Icon y WorldPrefab; identidad y hook se pisan enteros.
            var tutorialPairItem = CloneAsset(pairItem, "Item_Tutorial_Par50", i =>
            {
                i.ItemId = "item.tutorial.par50";
                i.DisplayName = "Bonus de Par";
                i.Description = "+50 de daño al armar un Par.";
                i.Type = ItemType.Passive;
                i.PassiveHooks = new List<PassiveItemHook>
                {
                    new PassiveItemHook
                    {
                        Kind = PassiveHookKind.ComboPlayed,
                        ComboFilter = new ComboFilter
                        {
                            Mode = ComboFilterMode.ComboIds,
                            ComboIds = new List<string> { ComboId.Par },
                        },
                        Effect = new EffectData
                        {
                            Label = "Tutorial Par +50",
                            Effects = new List<IEffect>
                            {
                                new EffAddComboBonus { Amount = new ReadConstantInt { Value = 50 } },
                            },
                        },
                    },
                };
            });
            EditorUtility.SetDirty(tutorialPairItem);

            // --- Shop override (1 slot, precio exacto, solo el item de Par) ---
            var tutorialShopPool = CreateOrReplace<ShopPoolSO>("SP_Tutorial");
            tutorialShopPool.Items = new List<WeightedShopItem>
            {
                new WeightedShopItem { Item = tutorialPairItem, Weight = 1f, BasePrice = ShopItemPrice, MinFloorDepth = 0 },
            };
            EditorUtility.SetDirty(tutorialShopPool);

            var tutorialShopConfig = CloneAsset(shopConfig, "SC_Tutorial", c =>
            {
                c.MaxItemSlots = 1;
                c.PriceMultiplier = 1f;
                c.PriceVariance = 0f;
            });
            EditorUtility.SetDirty(tutorialShopConfig);

            // --- Overlay (material + flecha + settings + bootstrap) -----------
            var dimMaterial = CreateDimMaterial();
            var arrowSprite = CreateArrowSprite();

            var overlaySettings = CreateOrReplace<TutorialOverlaySettingsSO>("TutorialOverlaySettings");
            overlaySettings.DimMaterial = dimMaterial;
            overlaySettings.ArrowSprite = arrowSprite;

            // Fuente del proyecto para el popup (el asset existente la tenía en null
            // → caía al font default de TMP, fuera del estilo del juego).
            if (overlaySettings.PopupFont == null)
            {
                overlaySettings.PopupFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
                    "Assets/Fonts/m6x11plus SDF.asset");
                if (overlaySettings.PopupFont == null)
                    Debug.LogWarning(LogPrefix + "No se encontró 'Assets/Fonts/m6x11plus SDF.asset' — " +
                                     "el popup sigue con el font default de TMP.");
            }

            // Campos agregados después de serializado el asset: deserializan 0 y el
            // overlay los guardea, pero saneados acá quedan visibles/editables.
            if (overlaySettings.PopupFooterAlpha <= 0f) overlaySettings.PopupFooterAlpha = 0.9f;
            if (overlaySettings.PopupAnchorGapPx <= 0f) overlaySettings.PopupAnchorGapPx = 90f;
            if (string.IsNullOrEmpty(overlaySettings.ContinueFooterText)
                || overlaySettings.ContinueFooterText == "Hacé click para continuar")
                overlaySettings.ContinueFooterText = "Haz clic para continuar";
            EditorUtility.SetDirty(overlaySettings);

            var overlayBootstrap = CreateOrReplace<TutorialOverlayBootstrap>("TutorialOverlayBootstrap");
            SetPrivateField(overlayBootstrap, "_settings", overlaySettings);

            // --- Config central ------------------------------------------------
            var config = CreateOrReplace<TutorialConfigSO>("TutorialConfig");
            SetPrivateField(config, "_tutorialHero", hero);
            SetPrivateField(config, "_floorPlan", plan);
            SetPrivateField(config, "_shopConfig", tutorialShopConfig);
            SetPrivateField(config, "_shopPool", tutorialShopPool);

            // --- Registro en el ServiceBootstrap -------------------------------
            RegisterExtraService(serviceBootstrap, config);
            RegisterExtraService(serviceBootstrap, overlayBootstrap);
            EditorUtility.SetDirty(serviceBootstrap);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(LogPrefix + $"Tutorial instalado en {Root}. Enemigos C dropean {goldPerEnemyC} c/u " +
                      $"(encantamiento = {enchantCost}). Falta wiring manual: botón 'Tutorial' en el " +
                      "MainMenu (opcional) — ver docs/setup/tutorial.md.");
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError(LogPrefix + $"No se encontró '{path}' ({typeof(T).Name}).");
            return asset;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Root))
                AssetDatabase.CreateFolder("Assets/Rollgeon", "Tutorial");
        }

        private static string PathFor(string assetName) => $"{Root}/{assetName}.asset";

        /// <summary>
        /// Clon profundo de un SO (Instantiate serializa/deserializa también la data
        /// Odin, así los effects inline quedan con instancias propias). Reemplaza el
        /// asset si ya existía, para que el installer sea re-ejecutable.
        /// <para>
        /// El tuning va en <paramref name="configure"/> y corre ANTES de CreateAsset:
        /// la primera serialización ya sale completa. Mutar después dependía del
        /// SetDirty+SaveAssets del final, y un import intermedio del AssetDatabase
        /// podía escribir el clon CRUDO y perder el tuning (visto ago 2026: rooms sin
        /// ForcePossibleSetups → spawns random en B; enemigo B sin drop de 20 → softlock
        /// de la tienda).
        /// </para>
        /// </summary>
        private static T CloneAsset<T>(T source, string assetName, System.Action<T> configure = null)
            where T : ScriptableObject
        {
            var path = PathFor(assetName);
            AssetDatabase.DeleteAsset(path);
            var clone = Object.Instantiate(source);
            clone.name = assetName;
            configure?.Invoke(clone);
            AssetDatabase.CreateAsset(clone, path);
            return clone;
        }

        private static T CreateOrReplace<T>(string assetName) where T : ScriptableObject
        {
            var path = PathFor(assetName);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            created.name = assetName;
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        /// <summary>
        /// Force Door de éxito garantizado, por data: threshold 1 (cualquier tirada
        /// pasa) y sin heal de enemigos al escapar (economía de HP determinista).
        /// </summary>
        private static void TuneForceDoor(ClassHeroSO hero)
        {
            int patched = 0;
            if (hero.PhaseBehaviors != null)
            {
                foreach (var behavior in hero.PhaseBehaviors)
                {
                    if (behavior?.Effects == null) continue;
                    foreach (var group in behavior.Effects)
                    {
                        if (group?.Effects == null) continue;
                        foreach (var effect in group.Effects)
                        {
                            if (effect is not EffForceDoor forceDoor) continue;
                            forceDoor.RequiredValue = 1;
                            forceDoor.EnemyHealPercentOnSuccess = 0;
                            patched++;
                        }
                    }
                }
            }

            if (patched == 0)
                Debug.LogWarning(LogPrefix + "El héroe clonado no tiene ningún EffForceDoor inline — " +
                                 "el escape del tutorial va a usar el threshold normal (25).");
        }

        /// <summary>
        /// El tutorial enseña "hasta 3 tiradas" en el ataque (generala: 1 roll +
        /// 2 rerolls gratis) — se fuerza por data en el clone.
        /// </summary>
        private static void TuneAttackRolls(ClassHeroSO hero)
        {
            int patched = 0;
            if (hero.PhaseBehaviors != null)
            {
                foreach (var behavior in hero.PhaseBehaviors)
                {
                    if (behavior == null || behavior.Slot != HeroBehaviorSlot.BaseAttack) continue;
                    behavior.FreeRollCount = 3;
                    patched++;
                }
            }

            if (patched == 0)
                Debug.LogWarning(LogPrefix + "El héroe clonado no tiene behaviors en el slot BaseAttack — " +
                                 "la lección de dados va a prometer 3 tiradas que el data no da.");
        }

        private static void SetPrivateField(ScriptableObject target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError(LogPrefix + $"Campo '{fieldName}' no encontrado en {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterExtraService(ServiceBootstrapSO bootstrap, IPreloadableService service)
        {
            bootstrap.ExtraServices ??= new List<IPreloadableService>();
            if (!bootstrap.ExtraServices.Contains(service))
                bootstrap.ExtraServices.Add(service);
        }

        private static Material CreateDimMaterial()
        {
            var path = $"{Root}/TutorialDim.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Rollgeon/UI/TutorialDim");
            if (shader == null)
            {
                Debug.LogError(LogPrefix + "Shader 'Rollgeon/UI/TutorialDim' no encontrado.");
                return null;
            }
            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Arte real de la flecha del overlay (dorada, apunta a la DERECHA).</summary>
        private const string ArrowArtPath = "Assets/Art/UI/Arrow/Arrow.png";

        /// <summary>
        /// Devuelve el sprite de flecha del overlay: el arte dorado si está en el proyecto,
        /// y si no un triángulo blanco generado por código (64×64) como último recurso —
        /// el overlay esconde la flecha cuando el sprite es null, así que sin fallback un
        /// proyecto sin el arte perdería el indicador entero.
        /// </summary>
        private static Sprite CreateArrowSprite()
        {
            var art = LoadFirstSprite(ArrowArtPath);
            if (art != null) return art;

            Debug.LogWarning($"[TutorialAssetInstaller] No se encontró {ArrowArtPath} — " +
                             "la flecha del overlay cae al triángulo blanco generado.");

            var pngPath = $"{Root}/T_TutorialArrow.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            if (existing != null) return existing;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Triángulo: punta en x=size-4, base vertical en x=8.
                    float t = Mathf.InverseLerp(8f, size - 4f, x);
                    float halfHeight = Mathf.Lerp(size * 0.38f, 1f, t);
                    float dy = Mathf.Abs(y - size * 0.5f);
                    float alpha = x < 8 ? 0f : Mathf.Clamp01(halfHeight - dy);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(pngPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
        }

        /// <summary>
        /// Primer sub-sprite de una textura. Necesario porque el arte de la flecha está en
        /// sprite mode Multiple: ahí el asset principal es la <c>Texture2D</c> y
        /// <c>LoadAssetAtPath&lt;Sprite&gt;</c> devuelve null.
        /// </summary>
        private static Sprite LoadFirstSprite(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite) return sprite;
            }
            return null;
        }
    }
}
