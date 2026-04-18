using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Patterns;
using Rollgeon.Patterns.Catalogs;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Patterns.Bootstrap
{
    /// <summary>
    /// <b>Hub central de balance.</b> SO unico que lista catalogos, settings y servicios
    /// runtime que el <see cref="BootstrapRunner"/> inyecta al <see cref="ServiceLocator"/>
    /// durante la carga de la escena <c>00_Bootstrap</c>.
    /// <para>
    /// Plan §4.4 y TECHNICAL.md §1.1.1. El diseno pluggable (listas vacias valido) permite
    /// que cada downstream worktree agregue su catalogo/settings sin tocar este archivo.
    /// </para>
    /// <para>
    /// <b>Como popular el asset.</b> Ver <c>docs/setup/Foundation#0005_CatalogsAndBootstrap.md</c>.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Service Bootstrap", fileName = "ServiceBootstrap")]
    [InfoBox("Hub central de balance. Lista catalogos, settings SOs y servicios runtime " +
             "pre-instanciados que deben vivir en ServiceScope.Global durante toda la sesion. " +
             "RegisterAll() los inyecta al ServiceLocator en el orden: Catalogs -> Settings -> ExtraServices.")]
    public class ServiceBootstrapSO : SerializedScriptableObject
    {
        // ---------------------------------------------------------------- Catalogs
        [Title("Catalogs")]
        [InfoBox("Cada slot referencia un asset derivado de BaseCatalogSO<T>. Se agregan a " +
                 "medida que cada downstream worktree mergea su catalogo concreto " +
                 "(Entity, Combo, Action, Ruleset, ...). Lista vacia = valido.")]
        [OdinSerialize]
        [ValidateInput(nameof(ValidateNoNullCatalogs), "Hay entries null en Catalogs.")]
        [ValidateInput(nameof(ValidateNoDuplicateCatalogs), "Hay catalogos duplicados (misma instancia) en Catalogs.")]
        public List<BaseCatalogSO> Catalogs = new List<BaseCatalogSO>();

        // ---------------------------------------------------------------- Settings
        [Title("Settings Assets")]
        [InfoBox("SOs de configuracion (SaveSettings, CameraConfig, AudioSettings, ...). " +
                 "Se registran por su Type runtime en ServiceScope.Global.")]
        [OdinSerialize]
        [ValidateInput(nameof(ValidateNoNullSettings), "Hay entries null en SettingsAssets.")]
        [ValidateInput(nameof(ValidateNoDuplicateSettings), "Hay settings duplicados (misma instancia) en SettingsAssets.")]
        public List<ScriptableObject> SettingsAssets = new List<ScriptableObject>();

        // ---------------------------------------------------------------- Extra services
        [Title("Extra Runtime Services")]
        [InfoBox("Servicios que implementan IPreloadableService. Se invoca Register() en cada " +
                 "uno, ordenados por Priority ascendente. Odin puede requerir que los concretos " +
                 "sean SerializedScriptableObject o [System.Serializable] para serializar la lista " +
                 "polimorfica correctamente (ver plan R4).")]
        [OdinSerialize]
        public List<IPreloadableService> ExtraServices = new List<IPreloadableService>();

        // ---------------------------------------------------------------- Scene chaining
        [Title("Scene Chaining")]
        [Tooltip("Nombre exacto (Build Settings) de la escena a cargar post-bootstrap. Default '01_MainMenu'.")]
        [Required]
        [SerializeField]
        private string _nextSceneName = "01_MainMenu";

        /// <summary>Nombre de la escena a cargar despues de <c>RegisterAll</c>/<c>PreloadAllCatalogsAsync</c>.</summary>
        public string NextSceneName => _nextSceneName;

        // ======================================================================
        // API publica
        // ======================================================================

        /// <summary>
        /// Registra todos los catalogos y settings al <see cref="ServiceLocator"/> en
        /// <see cref="ServiceScope.Global"/>, y luego invoca <see cref="IPreloadableService.Register"/>
        /// en cada extra service ordenado por <c>Priority</c>. Plan §5.1.
        /// </summary>
        public void RegisterAll()
        {
            BootstrapLog.Info("RegisterAll() invoked");

            int catalogsRegistered = 0;
            if (Catalogs != null)
            {
                foreach (var catalog in Catalogs)
                {
                    if (catalog == null)
                    {
                        BootstrapLog.Warn("Null entry in Catalogs — skipping.");
                        continue;
                    }
                    RegisterByRuntimeType(catalog, ServiceScope.Global);
                    catalogsRegistered++;
                }
            }

            int settingsRegistered = 0;
            if (SettingsAssets != null)
            {
                foreach (var so in SettingsAssets)
                {
                    if (so == null)
                    {
                        BootstrapLog.Warn("Null entry in SettingsAssets — skipping.");
                        continue;
                    }
                    RegisterByRuntimeType(so, ServiceScope.Global);
                    settingsRegistered++;
                }
            }

            int extrasRegistered = 0;
            if (ExtraServices != null)
            {
                var ordered = ExtraServices
                    .Where(s => s != null)
                    .OrderBy(s => s.Priority)
                    .ToList();

                foreach (var svc in ordered)
                {
                    try
                    {
                        svc.Register();
                        extrasRegistered++;
                    }
                    catch (Exception ex)
                    {
                        BootstrapLog.Error($"IPreloadableService '{svc.GetType().Name}' threw in Register(): {ex}");
                    }
                }
            }

            BootstrapLog.Info($"Registered {catalogsRegistered} catalogs, {settingsRegistered} settings, {extrasRegistered} extra services");
        }

        /// <summary>
        /// Espera a que todos los <see cref="ICatalog.PreloadAsync"/> completen.
        /// Default: todos resuelven inmediato (no-op). Catalogos que overrideen (p.ej.
        /// <c>EntityCatalogSO</c> con Addressables) honran este await.
        /// </summary>
        public async Task PreloadAllCatalogsAsync()
        {
            if (Catalogs == null || Catalogs.Count == 0)
            {
                BootstrapLog.Info("Preload skipped (0 catalogs)");
                return;
            }

            var tasks = Catalogs
                .Where(c => c != null)
                .Select(c => SafePreload(c))
                .ToArray();

            await Task.WhenAll(tasks);
            BootstrapLog.Info("Preload complete");
        }

        private static async Task SafePreload(ICatalog catalog)
        {
            try
            {
                await catalog.PreloadAsync();
            }
            catch (Exception ex)
            {
                BootstrapLog.Error($"Catalog '{catalog.CatalogName}' threw in PreloadAsync(): {ex}");
            }
        }

        // ======================================================================
        // Helpers privados
        // ======================================================================

        // [FOLLOWUP] Foundation#0001 solo publica AddService<T>(object, ServiceScope). Para
        // registrar una lista polimorfica por su Type runtime necesitamos reflection. Si
        // F#0001 agrega una overload no-generica AddService(Type, object, ServiceScope) en
        // el futuro, reemplazar este helper por una llamada directa (plan R1).
        private static readonly MethodInfo _addServiceGeneric = typeof(ServiceLocator)
            .GetMethod(nameof(ServiceLocator.AddService), BindingFlags.Public | BindingFlags.Static);

        private static void RegisterByRuntimeType(object instance, ServiceScope scope)
        {
            if (instance == null) return;
            if (_addServiceGeneric == null)
            {
                BootstrapLog.Error("ServiceLocator.AddService<T> reflection failed — API mismatch vs Foundation#0001.");
                return;
            }

            var typed = _addServiceGeneric.MakeGenericMethod(instance.GetType());
            typed.Invoke(null, new object[] { instance, scope });
        }

        // ---- Odin validators (privados, solo para el inspector) -----------------

        private bool ValidateNoNullCatalogs(List<BaseCatalogSO> list)
        {
            if (list == null) return true;
            return list.All(c => c != null);
        }

        private bool ValidateNoDuplicateCatalogs(List<BaseCatalogSO> list)
        {
            if (list == null) return true;
            var nonNull = list.Where(c => c != null).ToList();
            return nonNull.Count == nonNull.Distinct().Count();
        }

        private bool ValidateNoNullSettings(List<ScriptableObject> list)
        {
            if (list == null) return true;
            return list.All(c => c != null);
        }

        private bool ValidateNoDuplicateSettings(List<ScriptableObject> list)
        {
            if (list == null) return true;
            var nonNull = list.Where(c => c != null).ToList();
            return nonNull.Count == nonNull.Distinct().Count();
        }
    }
}
