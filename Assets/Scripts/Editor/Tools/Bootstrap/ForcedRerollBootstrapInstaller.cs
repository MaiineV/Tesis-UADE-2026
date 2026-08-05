using System.Collections.Generic;
using System.Linq;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Upgrades.Dice;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Bootstrap
{
    /// <summary>
    /// BUG-030 (Torpe): crea <c>ForcedRerollCapabilityBootstrap.asset</c> en
    /// <c>Assets/Rollgeon/Services/</c> y lo agrega a <c>ExtraServices</c> del
    /// <c>ServiceBootstrap.asset</c>. Por tool y no a mano porque el
    /// ServiceBootstrap es Odin-serializado — editar su YAML es frágil.
    /// Idempotente: re-correrlo no duplica ni el asset ni la entrada.
    /// </summary>
    public static class ForcedRerollBootstrapInstaller
    {
        private const string AssetPath =
            "Assets/Rollgeon/Services/ForcedRerollCapabilityBootstrap.asset";
        private const string ServiceBootstrapPath = "Assets/Rollgeon/ServiceBootstrap.asset";

        [MenuItem("Rollgeon/Bootstrap/Install Forced Reroll Capability")]
        public static void Install()
        {
            var bootstrap = AssetDatabase.LoadAssetAtPath<ForcedRerollCapabilityBootstrap>(AssetPath);
            if (bootstrap == null)
            {
                bootstrap = ScriptableObject.CreateInstance<ForcedRerollCapabilityBootstrap>();
                AssetDatabase.CreateAsset(bootstrap, AssetPath);
                Debug.Log($"[ForcedRerollBootstrapInstaller] Creado {AssetPath}");
            }

            var serviceBootstrap =
                AssetDatabase.LoadAssetAtPath<ServiceBootstrapSO>(ServiceBootstrapPath);
            if (serviceBootstrap == null)
            {
                Debug.LogError($"[ForcedRerollBootstrapInstaller] No se encontró {ServiceBootstrapPath}");
                return;
            }

            serviceBootstrap.ExtraServices ??= new List<IPreloadableService>();
            if (serviceBootstrap.ExtraServices.Any(e => e is ForcedRerollCapabilityBootstrap))
            {
                Debug.Log("[ForcedRerollBootstrapInstaller] Ya estaba en ExtraServices — no-op.");
            }
            else
            {
                serviceBootstrap.ExtraServices.Add(bootstrap);
                EditorUtility.SetDirty(serviceBootstrap);
                Debug.Log("[ForcedRerollBootstrapInstaller] Agregado a ExtraServices.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[ForcedRerollBootstrapInstaller] Listo.");
        }
    }
}
