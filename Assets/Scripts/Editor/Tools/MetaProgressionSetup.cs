using System.Collections.Generic;
using System.Linq;
using Rollgeon.Dice;
using Rollgeon.Meta;
using Rollgeon.Meta.Conditions;
using Rollgeon.Patterns.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools
{
    /// <summary>
    /// Setup idempotente de la meta-progresión (#164). Crea el
    /// <see cref="UnlockCatalogSO"/> + las definiciones del árbol base (D8, D10,
    /// Berserker, Gambler), las registra en el catálogo, agrega catálogo y
    /// servicios al <see cref="ServiceBootstrapSO"/> y suma D8/D10 a los
    /// <see cref="DiceBagPoolSO"/> existentes (quedan gateados por unlock).
    /// Re-ejecutable: no duplica nada.
    /// </summary>
    public static class MetaProgressionSetup
    {
        private const string LogPrefix = "[MetaProgressionSetup] ";
        private const string MetaFolder = "Assets/Rollgeon/Meta";
        private const string UnlocksFolder = "Assets/Rollgeon/Meta/Unlocks";

        [MenuItem("Tools/Rollgeon/Setup Meta-Progression (#164)")]
        public static void Run()
        {
            EnsureFolder("Assets/Rollgeon", "Meta");
            EnsureFolder(MetaFolder, "Unlocks");

            var catalog = LoadOrCreate<UnlockCatalogSO>($"{MetaFolder}/UnlockCatalog.asset");

            // ---- Árbol de progresión base (#164) — las condiciones exactas
            // pueden cambiar después desde la Unlock Condition Tool.

            var d8 = LoadOrCreate<UnlockDefinitionSO>($"{UnlocksFolder}/Unlock_Dice_D8.asset", def =>
            {
                def.UnlockId = "unlock.dice.d8";
                def.DisplayName = "Dado D8";
                def.Category = UnlockableCategory.Dice;
                def.TargetId = DiceType.D8.ToString();
                def.Description = "El D8 queda disponible en la pantalla de armado de build.";
                def.HintText = "Dominá el dado estándar: ganá una run confiando solo en el clásico de seis caras.";
                def.AppliesTo = UnlockOutcomeFilter.Won;
                def.Condition = new DiceCountOfTypeCondition
                {
                    Type = DiceType.D6,
                    Count = 5,
                    Comparison = DiceCountComparison.Exactly,
                };
            });

            var d10 = LoadOrCreate<UnlockDefinitionSO>($"{UnlocksFolder}/Unlock_Dice_D10.asset", def =>
            {
                def.UnlockId = "unlock.dice.d10";
                def.DisplayName = "Dado D10";
                def.Category = UnlockableCategory.Dice;
                def.TargetId = DiceType.D10.ToString();
                def.Description = "El D10 queda disponible en la pantalla de armado de build.";
                def.HintText = "Hay una receta exacta de dados que abre esta puerta. Experimentá con la mezcla.";
                def.AppliesTo = UnlockOutcomeFilter.Won;
                def.Condition = new DiceCombinationCondition
                {
                    Combination = new List<DiceType>
                    {
                        DiceType.D4, DiceType.D4, DiceType.D6, DiceType.D6, DiceType.D8,
                    },
                };
            });

            var berserker = LoadOrCreate<UnlockDefinitionSO>($"{UnlocksFolder}/Unlock_Class_Berserker.asset", def =>
            {
                def.UnlockId = "unlock.class.berserker";
                def.DisplayName = "Berserker";
                def.Category = UnlockableCategory.HeroClass;
                def.TargetId = "Berserker";
                def.Description = "El Berserker queda seleccionable en la pantalla de selección de personaje.";
                def.HintText = "Demostrá fuerza de ocho caras: llevá el dado nuevo a una victoria.";
                def.AppliesTo = UnlockOutcomeFilter.Won;
                def.Condition = new DiceCountOfTypeCondition
                {
                    Type = DiceType.D8,
                    Count = 1,
                    Comparison = DiceCountComparison.AtLeast,
                };
            });

            var gambler = LoadOrCreate<UnlockDefinitionSO>($"{UnlocksFolder}/Unlock_Class_Gambler.asset", def =>
            {
                def.UnlockId = "unlock.class.gambler";
                def.DisplayName = "Gambler";
                def.Category = UnlockableCategory.HeroClass;
                def.TargetId = "Gambler";
                def.Description = "El Gambler queda seleccionable en la pantalla de selección de personaje.";
                def.HintText = "Un verdadero apostador no deja ninguna jugada del Contrato sin cobrar.";
                def.AppliesTo = UnlockOutcomeFilter.Won;
                def.Condition = new AllContractCombosExecutedCondition();
            });

            foreach (var def in new[] { d8, d10, berserker, gambler })
            {
                catalog.AddEntry(def);
            }
            EditorUtility.SetDirty(catalog);

            WireBootstrap(catalog);
            AddDiceOfferings();

            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + "Setup completo: catálogo + 4 unlocks base + bootstrap + pools de dados.");
        }

        // ------------------------------------------------------------------

        private static void WireBootstrap(UnlockCatalogSO catalog)
        {
            var guids = AssetDatabase.FindAssets("t:ServiceBootstrapSO");
            if (guids.Length == 0)
            {
                Debug.LogWarning(LogPrefix + "No se encontró ServiceBootstrapSO — agregar catálogo y servicios a mano.");
                return;
            }

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var bootstrap = AssetDatabase.LoadAssetAtPath<ServiceBootstrapSO>(path);
                if (bootstrap == null) continue;

                bool dirty = false;

                if (!bootstrap.Catalogs.Contains(catalog))
                {
                    bootstrap.Catalogs.Add(catalog);
                    dirty = true;
                }

                if (!bootstrap.ExtraServices.Any(s => s is MetaProgressionService))
                {
                    bootstrap.ExtraServices.Add(new MetaProgressionService());
                    dirty = true;
                }

                if (!bootstrap.ExtraServices.Any(s => s is UnlockProgressService))
                {
                    bootstrap.ExtraServices.Add(new UnlockProgressService());
                    dirty = true;
                }

                if (dirty)
                {
                    EditorUtility.SetDirty(bootstrap);
                    Debug.Log(LogPrefix + $"ServiceBootstrap '{path}' cableado (catálogo + 2 servicios).");
                }
            }
        }

        private static void AddDiceOfferings()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:DiceBagPoolSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var pool = AssetDatabase.LoadAssetAtPath<DiceBagPoolSO>(path);
                if (pool == null || pool.Offerings == null) continue;

                bool dirty = false;
                foreach (var type in new[] { DiceType.D8, DiceType.D10 })
                {
                    if (pool.Offerings.Any(o => o.Type == type)) continue;
                    pool.Offerings.Add(new DicePoolEntry { Type = type, MaxInBag = type.MaxPerBag() });
                    dirty = true;
                }

                if (dirty)
                {
                    EditorUtility.SetDirty(pool);
                    Debug.Log(LogPrefix + $"DiceBagPool '{path}': D8/D10 agregados (gateados por unlock).");
                }
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static T LoadOrCreate<T>(string path, System.Action<T> initialize = null) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            initialize?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log(LogPrefix + $"Creado {path}");
            return asset;
        }
    }
}
