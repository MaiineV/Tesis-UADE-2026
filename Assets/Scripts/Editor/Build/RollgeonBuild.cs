#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Build de Windows 64 para Steam (Feature#0036). Valida los invariantes que
    /// regresan en silencio — identidad del producto, define STEAMWORKS_NET, target
    /// activo y orden de escenas — y aborta antes de gastar 20 minutos produciendo
    /// un player mal configurado.
    ///
    /// Valida pero NO muta PlayerSettings: mutar en cada build ensuciaría
    /// ProjectSettings.asset y generaría ruido de git en cada corrida.
    ///
    /// El contenido Addressables lo construye el package junto con el player
    /// (BuildAddressablesWithPlayerBuild = BuildWithPlayer, ver Feature#0036). Si
    /// algún día CI necesita que el content build sea una etapa que falle por
    /// separado, poner ese setting en DoNotBuildWithPlayer y llamar acá a
    /// BuildPlayerContent chequeando result.Error antes de BuildPlayer.
    ///
    /// Menú: Rollgeon → Build. Los métodos públicos sirven de entry point para
    /// -executeMethod desde CLI, con -buildPath opcional.
    /// </summary>
    public static class RollgeonBuild
    {
        private const string ExpectedProductName = "Rollgeon";
        private const string ExpectedCompanyName = "LetItRide";
        private const string BootstrapScene = "Assets/Scenes/00_Bootstrap.unity";
        private const string SteamAppIdFile = "steam_appid.txt";
        private const string DefaultOutputDir = "Build/Windows64";
        private const string BuildPathArg = "-buildPath";

        [MenuItem("Rollgeon/Build/Windows 64 (Development)")]
        public static void BuildWindows64Development() => Build(true);

        [MenuItem("Rollgeon/Build/Windows 64 (Release)")]
        public static void BuildWindows64Release() => Build(false);

        [MenuItem("Rollgeon/Build/Open Build Folder")]
        public static void OpenBuildFolder()
        {
            var dir = ResolveOutputDir();
            if (!Directory.Exists(dir))
            {
                Debug.LogWarning($"[RollgeonBuild] Todavía no existe {dir} — corré un build primero.");
                return;
            }

            EditorUtility.RevealInFinder(dir);
        }

        private static void Build(bool development)
        {
            if (!Validate(out var scenes))
            {
                Fail("Validación fallida — no se buildeó nada.");
                return;
            }

            var dir = ResolveOutputDir();
            Directory.CreateDirectory(dir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(dir, $"{ExpectedProductName}.exe"),
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = development
                    ? BuildOptions.Development | BuildOptions.AllowDebugging
                    : BuildOptions.None,
            };

            Debug.Log($"[RollgeonBuild] Buildeando {(development ? "Development" : "Release")} " +
                      $"→ {options.locationPathName} ({scenes.Length} escenas).");

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"Build {summary.result} — {summary.totalErrors} errores. " +
                     "Revisar la consola y Logs/ para el detalle.");
                return;
            }

            CopySteamAppId(dir);

            var mb = summary.totalSize / (1024f * 1024f);
            Debug.Log($"[RollgeonBuild] Listo — {options.locationPathName} " +
                      $"({mb:F1} MB, {summary.totalTime.TotalMinutes:F1} min).");

            if (!Application.isBatchMode) EditorUtility.RevealInFinder(dir);
        }

        /// <summary>
        /// Chequea los invariantes cuya regresión no produce error de compilación
        /// pero sí un player roto o con Steam apagado.
        /// </summary>
        private static bool Validate(out string[] scenes)
        {
            scenes = Array.Empty<string>();
            var ok = true;

            if (PlayerSettings.productName != ExpectedProductName)
            {
                Debug.LogError($"[RollgeonBuild] productName es '{PlayerSettings.productName}', " +
                               $"se esperaba '{ExpectedProductName}'. Afecta el nombre del .exe y " +
                               "la carpeta de saves (persistentDataPath).");
                ok = false;
            }

            if (PlayerSettings.companyName != ExpectedCompanyName)
            {
                Debug.LogError($"[RollgeonBuild] companyName es '{PlayerSettings.companyName}', " +
                               $"se esperaba '{ExpectedCompanyName}'.");
                ok = false;
            }

            var appId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone);
            if (appId.Contains("Unity-Technologies"))
            {
                Debug.LogError($"[RollgeonBuild] applicationIdentifier sigue siendo el del template " +
                               $"URP ('{appId}').");
                ok = false;
            }

            // STEAMWORKS_NET lo agrega el instalador del package. Si alguien reordena
            // defines y lo pierde, la build compila igual y shippea con Steam apagado.
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            if (!defines.Split(';').Contains("STEAMWORKS_NET"))
            {
                Debug.LogError("[RollgeonBuild] Falta el define STEAMWORKS_NET en Standalone — " +
                               "el player saldría sin integración de Steam.");
                ok = false;
            }

            if (defines.Split(';').Contains("DISABLESTEAMWORKS"))
            {
                Debug.LogError("[RollgeonBuild] DISABLESTEAMWORKS está activo — " +
                               "SteamServiceBootstrap se compila vacío.");
                ok = false;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                // Cambiar el target acá dispara un domain reload a mitad del método y
                // el build no llega a ocurrir. Se avisa y se corta.
                Debug.LogError($"[RollgeonBuild] El build target activo es " +
                               $"{EditorUserBuildSettings.activeBuildTarget}. Cambiarlo a Windows 64 " +
                               "en Build Profiles (o pasar -buildTarget Win64) y reintentar.");
                ok = false;
            }

            scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[RollgeonBuild] No hay escenas habilitadas en Build Settings.");
                ok = false;
            }
            else if (scenes[0] != BootstrapScene)
            {
                // Los servicios (Steam incluido) se registran en la cadena de bootstrap.
                // Si otra escena queda primera, el juego arranca sin servicios y sin error.
                Debug.LogError($"[RollgeonBuild] La escena 0 es '{scenes[0]}', se esperaba " +
                               $"'{BootstrapScene}'. Arrancar por otra escena deja el juego " +
                               "sin servicios registrados.");
                ok = false;
            }

            return ok;
        }

        /// <summary>
        /// El .exe necesita steam_appid.txt al lado para que SteamAPI.Init funcione
        /// fuera del cliente de Steam. El depot lo excluye por vdf (ver SteamPipe/) —
        /// una sola salida de build, la exclusión vive en el borde que le importa.
        /// </summary>
        private static void CopySteamAppId(string outputDir)
        {
            var source = Path.Combine(ProjectRoot(), SteamAppIdFile);
            if (!File.Exists(source))
            {
                Debug.LogWarning($"[RollgeonBuild] No se encontró {SteamAppIdFile} en la raíz — " +
                                 "el .exe no va a inicializar Steam si se lanza fuera del cliente.");
                return;
            }

            File.Copy(source, Path.Combine(outputDir, SteamAppIdFile), overwrite: true);
        }

        private static string ProjectRoot() => Directory.GetParent(Application.dataPath)!.FullName;

        /// <summary>Default Build/Windows64 en la raíz; -buildPath lo redirige para CI.</summary>
        private static string ResolveOutputDir()
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, BuildPathArg);
            if (i >= 0 && i + 1 < args.Length) return args[i + 1];

            return Path.Combine(ProjectRoot(), DefaultOutputDir);
        }

        private static void Fail(string message)
        {
            Debug.LogError($"[RollgeonBuild] {message}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
#endif
