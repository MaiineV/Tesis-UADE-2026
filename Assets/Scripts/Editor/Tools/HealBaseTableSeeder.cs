using System.Collections.Generic;
using Rollgeon.Combos;
using Rollgeon.Heroes;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Puebla la <c>HealBaseTable</c> de <c>CH_Warrior</c> con los valores de arranque
    /// del Spec Heal N×M (upsert idempotente — re-correrlo no duplica entradas y respeta
    /// tuning manual posterior solo si se borra la entrada primero).
    /// El asset es Odin-serializado: SIEMPRE por acá, nunca editar el YAML a mano.
    /// El clon del tutorial (<c>CH_Warrior_Tutorial</c>) se regenera re-corriendo
    /// "Rollgeon/Tutorial/Install Tutorial Assets" DESPUÉS de este seeder.
    /// </summary>
    public static class HealBaseTableSeeder
    {
        private const string WarriorPath = "Assets/Rollgeon/Classes/CH_Warrior.asset";

        // Valores de arranque para playtest — misma escala 100 que las bases de daño
        // (Par 8 … Generala 90). El tuning fino vive en el inspector del asset.
        private static readonly (string comboId, int healBase)[] Seed =
        {
            (ComboId.Par, 8),
            (ComboId.HigherNumber, 5),
            (ComboId.DoublePair, 15),
            (ComboId.Triple, 22),
            (ComboId.FullHouse, 35),
            (ComboId.Straight, 45),
            (ComboId.Poker, 60),
            (ComboId.Generala, 90),
            // En el contrato del warrior y con base de escudo (5) desde el balance del
            // 21/08 — sin entrada acá, el gate lo dejaba curando 0.
            (ComboId.BruteForce, 5),
        };

        [MenuItem("Rollgeon/Tools/Seed Heal Base Table (CH_Warrior)")]
        public static void SeedWarrior()
        {
            var hero = AssetDatabase.LoadAssetAtPath<ClassHeroSO>(WarriorPath);
            if (hero == null || hero.Sheet == null)
            {
                Debug.LogError($"[HealBaseTableSeeder] No se pudo cargar '{WarriorPath}' o su Sheet es null.");
                return;
            }

            hero.Sheet.HealBaseTable ??= new List<ComboHealBaseEntry>();
            var table = hero.Sheet.HealBaseTable;

            int added = 0, updated = 0;
            foreach (var (comboId, healBase) in Seed)
            {
                int idx = table.FindIndex(e => e.ComboId == comboId);
                if (idx >= 0)
                {
                    if (table[idx].HealBase == healBase) continue;
                    table[idx] = new ComboHealBaseEntry { ComboId = comboId, HealBase = healBase };
                    updated++;
                }
                else
                {
                    table.Add(new ComboHealBaseEntry { ComboId = comboId, HealBase = healBase });
                    added++;
                }
            }

            EditorUtility.SetDirty(hero);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HealBaseTableSeeder] '{WarriorPath}' → HealBaseTable con {table.Count} entradas " +
                      $"({added} nuevas, {updated} actualizadas). Recordá re-correr " +
                      "'Rollgeon/Tutorial/Install Tutorial Assets' para propagar al clon del tutorial.");
        }
    }
}
