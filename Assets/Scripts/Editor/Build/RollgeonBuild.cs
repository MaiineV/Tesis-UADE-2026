#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
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
    /// El contenido Addressables se reconstruye por partida doble (belt &amp; suspenders,
    /// Fix#0042): el setting está en BuildWithPlayer, así que File → Build / Build
    /// Profiles regeneran las bundles solos; y este script llama explícitamente a
    /// BuildPlayerContent con fail-fast antes de BuildPlayer, para que un content build
    /// roto corte ACÁ (CI-friendly) en vez de shippear localización stale (BUG-025/026:
    /// las tablas del repo estaban bien pero el build shippeaba bundles de semanas atrás).
    /// Costo: en el path scripteado el contenido se buildea dos veces. Si el tiempo
    /// molesta, dejar el call explícito y poner el setting en DoNotBuildWithPlayer.
    ///
    /// Menú: Rollgeon → Build. Los métodos públicos sirven de entry point para
    /// -executeMethod desde CLI, con -buildPath opcional.
    /// </summary>
    public static class RollgeonBuild
    {
        private const string ExpectedProductName = "Rollgeon";
        private const string ExpectedCompanyName = "3AM Games";
        private const string BootstrapScene = "Assets/Scenes/00_Bootstrap.unity";
        private const string SteamAppIdFile = "steam_appid.txt";
        private const string DefaultOutputDir = "Build/Windows64";
        private const string NoSteamOutputDir = "Build/Windows64NoSteam";
        private const string EventOutputDir = "Build/Windows64Event";
        private const string SteamDllRelativePath = "Rollgeon_Data/Plugins/x86_64/steam_api64.dll";
        private const string BuildPathArg = "-buildPath";

        /// <summary>
        /// Una variante = mismo pipeline, distinto output + defines extra. Los defines van
        /// por <c>extraScriptingDefines</c>: aplican solo a esa build y no tocan
        /// ProjectSettings (ver <c>docs/setup/windows-build.md §Variante sin Steam</c>).
        /// </summary>
        private readonly struct BuildVariant
        {
            public readonly string Label;
            public readonly string DefaultDir;
            public readonly bool Development;
            public readonly string[] ExtraDefines;
            public readonly bool StripSteamFiles;

            public BuildVariant(string label, string defaultDir, bool development, string[] extraDefines, bool stripSteamFiles)
            {
                Label = label;
                DefaultDir = defaultDir;
                Development = development;
                ExtraDefines = extraDefines;
                StripSteamFiles = stripSteamFiles;
            }

            public static BuildVariant Steam(bool development) => new BuildVariant(
                development ? "Development" : "Release", DefaultOutputDir, development, Array.Empty<string>(), false);

            /// <summary>Jurados, itch: Steamworks apagado solo para esta build.</summary>
            public static BuildVariant NoSteam => new BuildVariant(
                "Release sin Steam", NoSteamOutputDir, false, new[] { "DISABLESTEAMWORKS" }, true);

            /// <summary>
            /// Ferias/expos (Feature#0074): sin Steam + cuestionario in-game forzado por
            /// <c>ROLLGEON_EVENT_BUILD</c>, independiente del tick Enabled de SurveyConfig.
            /// </summary>
            public static BuildVariant Event => new BuildVariant(
                "Evento", EventOutputDir, false, new[] { "DISABLESTEAMWORKS", "ROLLGEON_EVENT_BUILD" }, true);
        }

        [MenuItem("Rollgeon/Build/Windows 64 (Development)")]
        public static void BuildWindows64Development() => Build(BuildVariant.Steam(development: true));

        [MenuItem("Rollgeon/Build/Windows 64 (Release)")]
        public static void BuildWindows64Release() => Build(BuildVariant.Steam(development: false));

        [MenuItem("Rollgeon/Build/Windows 64 (Sin Steam)")]
        public static void BuildWindows64NoSteam() => Build(BuildVariant.NoSteam);

        [MenuItem("Rollgeon/Build/Windows 64 (Evento)")]
        public static void BuildWindows64Event() => Build(BuildVariant.Event);

        [MenuItem("Rollgeon/Build/Open Build Folder")]
        public static void OpenBuildFolder()
        {
            var dir = ResolveOutputDir(DefaultOutputDir);
            if (!Directory.Exists(dir))
            {
                Debug.LogWarning($"[RollgeonBuild] Todavía no existe {dir} — corré un build primero.");
                return;
            }

            EditorUtility.RevealInFinder(dir);
        }

        private static void Build(BuildVariant variant)
        {
            if (!Validate(out var scenes))
            {
                Fail("Validación fallida — no se buildeó nada.");
                return;
            }

            var dir = ResolveOutputDir(variant.DefaultDir);
            Directory.CreateDirectory(dir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(dir, $"{ExpectedProductName}.exe"),
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = variant.Development
                    ? BuildOptions.Development | BuildOptions.AllowDebugging
                    : BuildOptions.None,
                extraScriptingDefines = variant.ExtraDefines,
            };

            // Fail-fast del contenido Addressables ANTES del player build (que tarda ~20 min).
            // Sin esto, un content build roto/stale shippea localización vieja en silencio.
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult contentResult);
            if (!string.IsNullOrEmpty(contentResult.Error))
            {
                Fail($"Build de contenido Addressables falló: {contentResult.Error}. " +
                     "No se buildeó el player — se habría shippeado localización stale.");
                return;
            }

            var definesLabel = variant.ExtraDefines.Length > 0 ? $", defines extra: {string.Join(";", variant.ExtraDefines)}" : "";
            Debug.Log($"[RollgeonBuild] Buildeando {variant.Label} " +
                      $"→ {options.locationPathName} ({scenes.Length} escenas{definesLabel}).");

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"Build {summary.result} — {summary.totalErrors} errores. " +
                     "Revisar la consola y Logs/ para el detalle.");
                return;
            }

            // steam_appid.txt lo copia SteamAppIdPostProcessor, que ya corrió como
            // parte de BuildPlayer. Las variantes sin Steam lo sacan de vuelta.
            if (variant.StripSteamFiles) StripSteamFiles(dir);

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
        internal static void CopySteamAppId(string outputDir)
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

        internal static string ProjectRoot() => Directory.GetParent(Application.dataPath)!.FullName;

        /// <summary>Default de la variante en la raíz; -buildPath lo redirige para CI.</summary>
        private static string ResolveOutputDir(string defaultDir)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, BuildPathArg);
            if (i >= 0 && i + 1 < args.Length) return args[i + 1];

            return Path.Combine(ProjectRoot(), defaultDir);
        }

        /// <summary>
        /// Con DISABLESTEAMWORKS nada usa steam_appid.txt ni steam_api64.dll: sacarlos
        /// evita que una build "sin Steam" intente hablar con el cliente igual.
        /// </summary>
        private static void StripSteamFiles(string outputDir)
        {
            var targets = new[]
            {
                Path.Combine(outputDir, SteamAppIdFile),
                Path.Combine(outputDir, SteamDllRelativePath),
            };

            foreach (var path in targets)
            {
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[RollgeonBuild] No se encontró {path} para borrar — ¿cambió el layout del player?");
                    continue;
                }

                File.Delete(path);
                Debug.Log($"[RollgeonBuild] Borrado {Path.GetFileName(path)} (variante sin Steam).");
            }
        }

        private static void Fail(string message)
        {
            Debug.LogError($"[RollgeonBuild] {message}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// Copia <c>steam_appid.txt</c> junto al player en <b>toda</b> build de Windows,
    /// no solo en las que salen de Rollgeon → Build.
    /// <para>
    /// Sin ese archivo en el working directory del proceso,
    /// <c>SteamAPI.RestartAppIfNecessary</c> relanza el juego vía <c>steam://run/</c>
    /// y mata este proceso (ver <c>SteamServiceBootstrap</c>): la build local se cierra
    /// sola y arranca la copia instalada de Steam, con un síntoma que no señala a la
    /// causa. Como callback del pipeline cubre también File → Build Settings, Build
    /// Profiles y cualquier script de CI que llame a BuildPipeline por su cuenta.
    /// </para>
    /// </summary>
    public sealed class SteamAppIdPostProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            var platform = report.summary.platform;
            if (platform != BuildTarget.StandaloneWindows64 &&
                platform != BuildTarget.StandaloneWindows)
            {
                return;
            }

            // outputPath es el .exe; el appid va en su carpeta, que es el cwd cuando
            // se lo lanza con doble click.
            var outputDir = Path.GetDirectoryName(report.summary.outputPath);
            if (string.IsNullOrEmpty(outputDir)) return;

            RollgeonBuild.CopySteamAppId(outputDir);
        }
    }
}
#endif
