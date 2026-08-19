using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Pathing;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Status;
using Rollgeon.Entities.Traits;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Tiles;
using Rollgeon.Tiles.Authoring;
using Rollgeon.Tiles.Forced;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Tiles
{
    /// <summary>
    /// Installer del sistema de Casillas Especiales: crea/actualiza las 12 definiciones
    /// del catálogo (valores del GDD), los bootstraps de servicios y los registra en el
    /// <see cref="ServiceBootstrapSO"/>. Re-ejecutable: los assets existentes se
    /// actualizan en lugar de duplicarse.
    /// </summary>
    public static class SpecialTilesInstaller
    {
        private const string Root = "Assets/Rollgeon/Tiles";
        private const string BootstrapsFolder = Root + "/Bootstraps";

        [MenuItem("Rollgeon/Tiles/1 - Crear definiciones del catálogo")]
        public static void CreateDefinitions()
        {
            EnsureFolder(Root);

            foreach (var config in Catalog())
            {
                var path = $"{Root}/{config.FileName}.asset";
                var def = AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(path);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
                    AssetDatabase.CreateAsset(def, path);
                }
                config.Apply(def);
                EditorUtility.SetDirty(def);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[SpecialTilesInstaller] 12 definiciones creadas/actualizadas en " + Root);
        }

        [MenuItem("Rollgeon/Tiles/2 - Crear y registrar bootstraps")]
        public static void CreateAndRegisterBootstraps()
        {
            EnsureFolder(Root);
            EnsureFolder(BootstrapsFolder);

            var tuning = EnsureAsset<AIPathTuningSO>($"{BootstrapsFolder}/AIPathTuning.asset");

            var bootstraps = new List<ScriptableObject>
            {
                EnsureAsset<UnitTraitServiceBootstrap>($"{BootstrapsFolder}/UnitTraitServiceBootstrap.asset"),
                EnsureAsset<PoisonServiceBootstrap>($"{BootstrapsFolder}/PoisonServiceBootstrap.asset"),
                EnsureAsset<SpecialTileServiceBootstrap>($"{BootstrapsFolder}/SpecialTileServiceBootstrap.asset"),
                EnsureAsset<ForcedMovementServiceBootstrap>($"{BootstrapsFolder}/ForcedMovementServiceBootstrap.asset"),
                EnsureAsset<RoomSpecialTilesLoaderBootstrap>($"{BootstrapsFolder}/RoomSpecialTilesLoaderBootstrap.asset"),
            };

            var plannerBootstrap = EnsureAsset<AIPathPlannerBootstrap>($"{BootstrapsFolder}/AIPathPlannerBootstrap.asset");
            var serialized = new SerializedObject(plannerBootstrap);
            serialized.FindProperty("_tuning").objectReferenceValue = tuning;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            bootstraps.Add(plannerBootstrap);

            RegisterInServiceBootstrap(bootstraps);

            AssetDatabase.SaveAssets();
            Debug.Log("[SpecialTilesInstaller] Bootstraps creados y registrados en ServiceBootstrapSO.");
        }

        [MenuItem("Rollgeon/Tiles/Setup All")]
        public static void SetupAll()
        {
            CreateDefinitions();
            CreateAndRegisterBootstraps();
        }

        // ==================================================================
        // Bootstrap registration
        // ==================================================================

        private static void RegisterInServiceBootstrap(List<ScriptableObject> bootstraps)
        {
            var guids = AssetDatabase.FindAssets("t:ServiceBootstrapSO");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[SpecialTilesInstaller] No hay ServiceBootstrapSO en el proyecto — " +
                                 "los bootstraps quedaron creados pero SIN registrar.");
                return;
            }

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var hub = AssetDatabase.LoadAssetAtPath<ServiceBootstrapSO>(path);
                if (hub == null) continue;

                hub.ExtraServices ??= new List<IPreloadableService>();
                int added = 0;
                foreach (var bootstrap in bootstraps)
                {
                    if (!(bootstrap is IPreloadableService preloadable)) continue;
                    if (ContainsSameAsset(hub.ExtraServices, bootstrap)) continue;
                    hub.ExtraServices.Add(preloadable);
                    added++;
                }

                if (added > 0)
                {
                    EditorUtility.SetDirty(hub);
                    Debug.Log($"[SpecialTilesInstaller] {added} bootstraps agregados a '{path}'.");
                }
            }
        }

        private static bool ContainsSameAsset(List<IPreloadableService> services, ScriptableObject asset)
        {
            foreach (var service in services)
                if (ReferenceEquals(service, asset)) return true;
            return false;
        }

        // ==================================================================
        // Catálogo — valores del GDD
        // ==================================================================

        private readonly struct TileConfig
        {
            public readonly string FileName;
            public readonly Action<SpecialTileDefinitionSO> Apply;

            public TileConfig(string fileName, Action<SpecialTileDefinitionSO> apply)
            {
                FileName = fileName;
                Apply = apply;
            }
        }

        private static IEnumerable<TileConfig> Catalog()
        {
            yield return new TileConfig("Tile_Spikes", d =>
            {
                Identity(d, "TILE_SPIKES", "Pinchos", SpecialTileType.Spikes, "tile.spikes");
                d.Triggers = TileTrigger.OnEnter | TileTrigger.OnForcedMovementInto;
                d.Category = TileEffectCategory.Damage;
                d.Affinity = TileAffinity.GroundOnly;
                d.EnterDamage = 12;
                d.DisarmOnTrigger = true;
                d.RearmOnRoundWrap = true;
                d.EditorColor = new Color(0.30f, 0.30f, 0.34f);
            });

            yield return new TileConfig("Tile_Fire", d =>
            {
                Identity(d, "TILE_FIRE", "Fuego", SpecialTileType.Fire, "tile.fire");
                d.Triggers = TileTrigger.OnEnter | TileTrigger.OnTurnStart;
                d.Category = TileEffectCategory.Damage;
                d.Affinity = TileAffinity.All;
                d.EnterDamage = 8;
                d.TurnStartDamage = 12;
                d.EditorColor = new Color(1f, 0.35f, 0.1f);
            });

            yield return new TileConfig("Tile_FireTemp", d =>
            {
                Identity(d, "TILE_FIRE_TEMP", "Fuego Temporal", SpecialTileType.FireTemp, "tile.firetemp");
                d.Triggers = TileTrigger.OnEnter | TileTrigger.OnTurnStart;
                d.Category = TileEffectCategory.Damage;
                d.Affinity = TileAffinity.All;
                d.EnterDamage = 6;
                d.TurnStartDamage = 10;
                d.DefaultDurationRounds = 2;
                d.EditorColor = new Color(1f, 0.6f, 0.25f);
            });

            yield return new TileConfig("Tile_Ice", d =>
            {
                Identity(d, "TILE_ICE", "Hielo", SpecialTileType.Ice, "tile.ice");
                d.Triggers = TileTrigger.OnEnter | TileTrigger.OnPassThrough;
                d.Category = TileEffectCategory.ForcedSlide;
                d.Affinity = TileAffinity.GroundOnly;
                d.EditorColor = new Color(0.5f, 0.85f, 1f);
            });

            yield return new TileConfig("Tile_Portal", d =>
            {
                Identity(d, "TILE_PORTAL", "Portal", SpecialTileType.Portal, "tile.portal");
                d.Triggers = TileTrigger.OnEnter | TileTrigger.OnForcedMovementInto;
                d.Category = TileEffectCategory.Teleport;
                d.Affinity = TileAffinity.All;
                d.EditorColor = new Color(0.65f, 0.3f, 1f);
            });

            yield return new TileConfig("Tile_Poison", d =>
            {
                Identity(d, "TILE_POISON", "Veneno", SpecialTileType.Poison, "tile.poison");
                d.Triggers = TileTrigger.OnEnter;
                d.Category = TileEffectCategory.ApplyStatus;
                d.Affinity = TileAffinity.GroundOnly;
                d.StatusKind = TileStatusKind.Poison;
                d.StatusTurns = 3;
                d.StatusTickDamage = 5;
                d.EditorColor = new Color(0.4f, 1f, 0.2f);
            });

            yield return new TileConfig("Tile_Heal", d =>
            {
                Identity(d, "TILE_HEAL", "Curación", SpecialTileType.Heal, "tile.heal");
                d.Triggers = TileTrigger.OnEndTurn;
                d.Category = TileEffectCategory.Heal;
                d.Affinity = TileAffinity.All;
                d.HealAmount = 12;
                d.EditorColor = Color.white;
            });

            yield return new TileConfig("Tile_Strength", d =>
            {
                Identity(d, "TILE_STRENGTH", "Fortaleza", SpecialTileType.Strength, "tile.strength");
                d.Triggers = TileTrigger.OnRemainOn;
                d.Category = TileEffectCategory.StatModifier;
                d.Affinity = TileAffinity.All;
                d.ComboDamageBonus = 10;
                d.EditorColor = new Color(0.9f, 0.15f, 0.2f);
            });

            yield return new TileConfig("Tile_Boost", d =>
            {
                Identity(d, "TILE_BOOST", "Impulso", SpecialTileType.Boost, "tile.boost");
                d.Triggers = TileTrigger.OnMovementRoll;
                d.Category = TileEffectCategory.MoveRangeBonus;
                d.Affinity = TileAffinity.All;
                // INERTE por decisión de diseño: el valor queda autorado para cuando exista
                // tirada real de movimiento, pero hoy nada lo consume.
                d.MoveRangeBonus = 2;
                d.EditorColor = new Color(0.25f, 0.5f, 1f);
            });

            yield return new TileConfig("Tile_Telegraph", d =>
            {
                Identity(d, "TILE_TELEGRAPH", "Advertencia", SpecialTileType.Telegraph, "tile.telegraph");
                d.Triggers = TileTrigger.OnTelegraphExpire;
                d.Category = TileEffectCategory.Telegraph;
                d.Affinity = TileAffinity.All;
                d.DefaultDurationRounds = 1;
                d.TelegraphRounds = 1;
                // El payload lo asigna cada uso (variantes por asset o por código de jefe).
                d.EditorColor = new Color(1f, 0.8f, 0.2f);
            });

            yield return new TileConfig("Tile_ElectricPuddle", d =>
            {
                Identity(d, "TILE_ELECTRIC_PUDDLE", "Charco Eléctrico",
                    SpecialTileType.ElectricPuddle, "tile.electricpuddle");
                d.Triggers = TileTrigger.OnEnter | TileTrigger.OnForcedMovementInto;
                d.Category = TileEffectCategory.ApplyStatus;
                d.Affinity = TileAffinity.All;
                d.StatusKind = TileStatusKind.Stun;
                d.StatusTurns = 1;
                d.AIVirtualEnterDamage = 25;
                d.EditorColor = new Color(1f, 0.95f, 0.1f);
            });

            yield return new TileConfig("Tile_SafeZone", d =>
            {
                Identity(d, "TILE_SAFEZONE", "Zona de Seguridad", SpecialTileType.SafeZone, "tile.safezone");
                d.Triggers = TileTrigger.OnRemainOn;
                d.Category = TileEffectCategory.ConditionalProtection;
                d.Affinity = TileAffinity.All;
                // Cada zona declara de qué protege: este asset base queda vacío a propósito
                // — las variantes (por jefe / por sala) se autorean duplicándolo.
                d.ProtectedTileTypes = Array.Empty<SpecialTileType>();
                d.EditorColor = new Color(0.95f, 0.95f, 0.95f);
            });
        }

        private static void Identity(SpecialTileDefinitionSO d, string tileId, string displayName,
            SpecialTileType type, string localizationId)
        {
            d.TileId = tileId;
            d.DisplayName = displayName;
            d.TileType = type;
            d.NameKey = localizationId;
            d.DescriptionKey = localizationId;
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        private static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
