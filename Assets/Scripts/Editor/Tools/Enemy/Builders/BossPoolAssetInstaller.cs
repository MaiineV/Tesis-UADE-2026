using System.Collections.Generic;
using Rollgeon.Combat.Status;
using Rollgeon.Combat.Threat;
using Rollgeon.Dungeon;
using Rollgeon.Entities;
using Rollgeon.Entities.Bosses;
using Rollgeon.Patterns.Bootstrap;
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

        // Salas propias, las genera 'Rollgeon → Bosses → Build Boss Rooms'. Los jefes viejos no
        // tienen: se quedan con la sala compartida del piso.
        private const string CroupierRoom = "Assets/Rollgeon/Rooms/Room_Boss_Croupier.asset";
        private const string BandidaRoom = "Assets/Rollgeon/Rooms/Room_Boss_Bandida.asset";
        private const string CajeroRoom = "Assets/Rollgeon/Rooms/Room_Boss_Cajero.asset";
        private const string AnotadorRoom = "Assets/Rollgeon/Rooms/Room_Boss_Anotador.asset";
        private const string GeneralaRoom = "Assets/Rollgeon/Rooms/Room_Boss_Generala.asset";
        private const string TahurRoom = "Assets/Rollgeon/Rooms/Room_Boss_Tahur.asset";

        [MenuItem("Tools/Rollgeon/Bosses/Build Floor Pools")]
        public static void Install()
        {
            // Dos jefes activos por piso, 90 / 10 (los pesos son relativos, así que 9/1).
            //
            // El principal se lleva el 90% porque es el que está en pulido: la mayoría de las runs
            // de playtest tienen que caer en el jefe cuya legibilidad estamos arreglando. El
            // secundario es el slot de variedad — con un solo jefe por piso la run se aprende de
            // memoria: sabés qué te toca antes de bajar.
            //
            // Los tres viejos (Sunken Grand, Security Boss, General Director) quedan en el pool
            // DESACTIVADOS, y no por deuda de diseño: 'Rollgeon → Enemies → Audit Rigs' los mostró
            // sin rig — cero skinned meshes, cero animaciones. Son arte estático. Un jefe congelado
            // saliendo 1 de cada 10 peleas se ve peor que un piso con dos jefes, así que vuelven
            // recién cuando tengan animaciones. La entry queda para poder re-activarlos de una.
            //
            // La Bandida es de piso 1 y no del 2, que es donde estuvo por error: su vida, su oro y
            // sus builders siempre dijeron piso 1.
            const float MainWeight = 9f;
            const float VarietyWeight = 1f;

            var bp1 = BuildPool("BP_Floor1", new[]
            {
                Entry(CroupierPath, MainWeight, true, CroupierRoom),
                Entry(BandidaPath, VarietyWeight, true, BandidaRoom),
                Entry(SunkenGrandPath, VarietyWeight, false),
            });
            var bp2 = BuildPool("BP_Floor2", new[]
            {
                Entry(CajeroPath, MainWeight, true, CajeroRoom),
                Entry(AnotadorPath, VarietyWeight, true, AnotadorRoom),
                Entry(SecurityBossPath, VarietyWeight, false),
            });
            var bp3 = BuildPool("BP_Floor3", new[]
            {
                Entry(GeneralaPath, MainWeight, true, GeneralaRoom),
                Entry(TahurPath, VarietyWeight, true, TahurRoom),
                Entry(GeneralDirectorPath, VarietyWeight, false),
            });

            AssignToLayout(Floor1LayoutPath, bp1);
            AssignToLayout(Floor2LayoutPath, bp2);
            AssignToLayout(Floor3LayoutPath, bp3);

            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + "Pools de piso instalados y asignados a los layouts.");
        }

        /// <summary>
        /// Registra los bootstraps nuevos (hazards v2 y stun) en
        /// <c>ServiceBootstrap.ExtraServices</c>. Sin el de stun, la estela del Anotador pinta
        /// pero no saltea el turno. Idempotente: no duplica los que ya están.
        /// </summary>
        [MenuItem("Tools/Rollgeon/Bosses/Register Service Bootstraps")]
        public static void RegisterServiceBootstraps()
        {
            var serviceBootstrap = AssetDatabase.LoadAssetAtPath<ServiceBootstrapSO>(
                "Assets/Rollgeon/ServiceBootstrap.asset");
            if (serviceBootstrap == null)
            {
                Debug.LogError(LogPrefix + "No se encontró Assets/Rollgeon/ServiceBootstrap.asset.");
                return;
            }

            var hazard = AssetDatabase.LoadAssetAtPath<HazardServiceBootstrap>(
                "Assets/Rollgeon/Services/HazardServiceBootstrap.asset");
            var stun = AssetDatabase.LoadAssetAtPath<StunServiceBootstrap>(
                "Assets/Rollgeon/Services/StunServiceBootstrap.asset");

            int added = 0;
            added += AddIfMissing(serviceBootstrap, hazard) ? 1 : 0;
            added += AddIfMissing(serviceBootstrap, stun) ? 1 : 0;

            if (added > 0)
            {
                EditorUtility.SetDirty(serviceBootstrap);
                AssetDatabase.SaveAssets();
            }
            Debug.Log(LogPrefix + $"Bootstraps registrados en ExtraServices: {added} nuevos " +
                      "(0 = ya estaban o faltan los .asset en Assets/Rollgeon/Services/).");
        }

        private static bool AddIfMissing(ServiceBootstrapSO bootstrap, IPreloadableService service)
        {
            if (service == null) return false;
            bootstrap.ExtraServices ??= new List<IPreloadableService>();
            if (bootstrap.ExtraServices.Contains(service)) return false;
            bootstrap.ExtraServices.Add(service);
            return true;
        }

        /// <param name="roomSOPath">
        /// Sala propia del jefe. <c>null</c> o inexistente ⇒ la entry queda sin <c>Room</c> y la sala
        /// se sortea del pool del piso, que es el comportamiento previo al vínculo jefe→sala. Los
        /// jefes viejos (Sunken Grand y compañía) van así a propósito: no tienen sala propia, y
        /// ahora que volvieron al pool activo esa es su forma normal de spawnear — pelean en la
        /// sala compartida del piso, sin terreno autorado.
        /// </param>
        private static WeightedBoss Entry(string path, float weight, bool enabled,
                                          string roomSOPath = null)
        {
            var boss = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
            if (boss == null)
            {
                Debug.LogWarning(LogPrefix + $"Falta '{path}' — entry salteada. " +
                                 "Correr el builder de ese boss y re-correr este instalador.");
                return null;
            }

            RoomSO room = null;
            if (!string.IsNullOrEmpty(roomSOPath))
            {
                room = AssetDatabase.LoadAssetAtPath<RoomSO>(roomSOPath);
                if (room == null)
                {
                    Debug.LogWarning(LogPrefix + $"Falta la sala '{roomSOPath}' para '{boss.EntityId}' " +
                                     "— la entry queda sin Room y el piso le va a sortear una sala " +
                                     "cualquiera. Correr 'Rollgeon → Bosses → Build Boss Rooms'.");
                }
            }

            return new WeightedBoss { Boss = boss, Weight = weight, Enabled = enabled, Room = room };
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
