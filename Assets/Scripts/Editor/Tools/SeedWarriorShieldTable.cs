using System.Collections.Generic;
using Rollgeon.Heroes;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Seedea <c>ShieldBaseTable</c> de CH_Warrior con la tabla propuesta del plan
    /// 09/07/2026 (Spec Escudo v2). Valores iniciales para tuning con Maine/Bocco —
    /// re-ejecutar pisa la tabla completa (idempotente).
    /// </summary>
    public static class SeedWarriorShieldTable
    {
        private const string WarriorPath = "Assets/Rollgeon/Classes/CH_Warrior.asset";

        [MenuItem("Rollgeon/Tools/Seed Warrior Shield Table")]
        public static void Seed()
        {
            var hero = AssetDatabase.LoadAssetAtPath<ClassHeroSO>(WarriorPath);
            if (hero == null || hero.Sheet == null)
            {
                Debug.LogError($"[SeedShield] No se pudo cargar {WarriorPath} o su Sheet es null.");
                return;
            }

            hero.Sheet.ShieldBaseTable = new List<ComboShieldBaseEntry>
            {
                new ComboShieldBaseEntry { ComboId = "combo.pair", ShieldBase = 2 },
                new ComboShieldBaseEntry { ComboId = "combo.higher_number", ShieldBase = 2 },
                new ComboShieldBaseEntry { ComboId = "combo.double_pair", ShieldBase = 3 },
                new ComboShieldBaseEntry { ComboId = "combo.trio", ShieldBase = 4 },
                new ComboShieldBaseEntry { ComboId = "combo.full_house", ShieldBase = 5 },
                new ComboShieldBaseEntry { ComboId = "combo.ladder", ShieldBase = 5 },
                new ComboShieldBaseEntry { ComboId = "combo.poker", ShieldBase = 6 },
                new ComboShieldBaseEntry { ComboId = "combo.generala", ShieldBase = 8 },
            };

            EditorUtility.SetDirty(hero);
            AssetDatabase.SaveAssets();
            Debug.Log("[SeedShield] ShieldBaseTable de CH_Warrior seedeada: 8 entradas (pair 2 → generala 8, cap 8).");
        }
    }
}
