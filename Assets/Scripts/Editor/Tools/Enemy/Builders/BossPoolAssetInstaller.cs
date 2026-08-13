using System.Collections.Generic;
using Rollgeon.Dungeon;
using Rollgeon.Entities;
using Rollgeon.Entities.Bosses;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools
{
    /// <summary>
    /// Instalador one-shot de los pools de bosses por piso
    /// (<c>Tools → Rollgeon → Bosses → Build Floor Pools</c>). Idempotente: re-escribe
    /// las entries de <c>BP_Floor1/2/3</c> y las asigna al <c>BossPool</c> de cada
    /// <c>FloorLayoutSO</c>. Correr DESPUÉS de los builders de cada boss
    /// (<c>Tools → Rollgeon → Bosses → Build *</c>): los ED_Boss_* nuevos que falten
    /// se saltean con warning y el pool queda utilizable igual.
    /// </summary>
    public static class BossPoolAssetInstaller
    {
        private const string LogPrefix = "[BossPoolAssetInstaller] ";
        private const string PoolFolder = "Assets/Rollgeon/Floor";

        private const string Floor1LayoutPath = "Assets/Rollgeon/Floor/FloorLayout.asset";
        private const string Floor2LayoutPath = "Assets/Rollgeon/Floor/Floor2_Layout.asset";
        private const string Floor3LayoutPath = "Assets/Rollgeon/Floor/Floor3_Layout.asset";

        private const string SunkenGrandPath = "Assets/Rollgeon/Enemies/ED_Boss_Sunken_Grand.asset";
        private const string SecurityBossPath = "Assets/Rollgeon/Enemies/ED_Boss_Security_Boss.asset";
        private const string GeneralDirectorPath = "Assets/Rollgeon/Enemies/ED_Boss_GeneralDirector.asset";
        private const string CroupierPath = "Assets/Rollgeon/Enemies/ED_Boss_Croupier.asset";
        private const string BandidaPath = "Assets/Rollgeon/Enemies/ED_Boss_Bandida.asset";
        private const string CajeroPath = "Assets/Rollgeon/Enemies/ED_Boss_Cajero.asset";
        private const string AnotadorPath = "Assets/Rollgeon/Enemies/ED_Boss_Anotador.asset";
        private const string GeneralaPath = "Assets/Rollgeon/Enemies/ED_Boss_Generala.asset";
        private const string TahurPath = "Assets/Rollgeon/Enemies/ED_Boss_Tahur.asset";

        [MenuItem("Tools/Rollgeon/Bosses/Build Floor Pools")]
        public static void Install()
        {
            // Piso 1: el boss actual queda + los dos nuevos, pesos iguales.
            // Pisos 2 y 3: los nuevos suplantan al viejo — que queda en el pool
            // desactivado (Enabled = off) para poder re-activarlo desde el Inspector.
            var bp1 = BuildPool("BP_Floor1", new[]
            {
                Entry(SunkenGrandPath, 1f, true),
                Entry(CroupierPath, 1f, true),
                Entry(BandidaPath, 1f, true),
            });
            var bp2 = BuildPool("BP_Floor2", new[]
            {
                Entry(CajeroPath, 1f, true),
                Entry(AnotadorPath, 1f, true),
                Entry(SecurityBossPath, 1f, false),
            });
            var bp3 = BuildPool("BP_Floor3", new[]
            {
                Entry(GeneralaPath, 1f, true),
                Entry(TahurPath, 1f, true),
                Entry(GeneralDirectorPath, 1f, false),
            });

            AssignToLayout(Floor1LayoutPath, bp1);
            AssignToLayout(Floor2LayoutPath, bp2);
            AssignToLayout(Floor3LayoutPath, bp3);

            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + "Pools de piso instalados y asignados a los layouts.");
        }

        private static WeightedBoss Entry(string path, float weight, bool enabled)
        {
            var boss = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
            if (boss == null)
            {
                Debug.LogWarning(LogPrefix + $"Falta '{path}' — entry salteada. " +
                                 "Correr el builder de ese boss y re-correr este instalador.");
                return null;
            }
            return new WeightedBoss { Boss = boss, Weight = weight, Enabled = enabled };
        }

        private static BossPoolSO BuildPool(string assetName, IEnumerable<WeightedBoss> entries)
        {
            string path = $"{PoolFolder}/{assetName}.asset";
            var pool = AssetDatabase.LoadAssetAtPath<BossPoolSO>(path);
            if (pool == null)
            {
                pool = ScriptableObject.CreateInstance<BossPoolSO>();
                AssetDatabase.CreateAsset(pool, path);
            }

            pool.Entries.Clear();
            foreach (var entry in entries)
            {
                if (entry != null) pool.Entries.Add(entry);
            }
            EditorUtility.SetDirty(pool);
            return pool;
        }

        private static void AssignToLayout(string layoutPath, BossPoolSO pool)
        {
            var layout = AssetDatabase.LoadAssetAtPath<FloorLayoutSO>(layoutPath);
            if (layout == null)
            {
                Debug.LogError(LogPrefix + $"No se encontró el layout '{layoutPath}' — pool sin asignar.");
                return;
            }
            layout.BossPool = pool;
            EditorUtility.SetDirty(layout);
        }
    }
}
