using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Rollgeon.Heroes;
using Rollgeon.Patterns.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rollgeon.EditorTools.Playtest
{
    /// <summary>
    /// Lado editor del boss bot: entra a Play Mode, spawnea el <see cref="BossBotDriver"/> y, si
    /// la corrida vino de la línea de comandos, cierra Unity con el exit code correspondiente.
    /// </summary>
    /// <remarks>
    /// <b>El problema que resuelve la SessionState.</b> Entrar a Play Mode dispara un domain
    /// reload (el proyecto tiene <c>m_EnterPlayModeOptions: 0</c>), así que cualquier estado
    /// estático de este runner muere entre "pedí Play" y "estoy en Play". La config viaja por
    /// <see cref="SessionState"/>, que sobrevive el reload y muere con el proceso — el mismo
    /// mecanismo que ya usa <see cref="BootstrapRunOverride.StashForPlayMode"/>.
    ///
    /// <b>El arranque de la run no lo hace este código.</b> Lo hace
    /// <see cref="BootstrapRunOverride"/>: <c>BootstrapRunner.Awake</c> lo consume, llama a
    /// <c>PendingRunRequest.Set</c> y carga <c>02_Gameplay</c> directo. Es el mismo camino del
    /// Scene Switcher (<c>SceneSwitcherWindow.StartPlayWithConfig</c>), así que el bot no
    /// inventa una segunda forma de arrancar una run.
    /// </remarks>
    public static class BossBotRunner
    {
        private const string BootstrapScenePath = "Assets/Scenes/00_Bootstrap.unity";
        private const string TargetScene = "02_Gameplay";
        private const string DefaultHeroId = "Warrior";
        private const string SessionKey = "Rollgeon.BossBot.Pending";
        private const string QuitKey = "Rollgeon.BossBot.Quit";
        private const string LogPrefix = "[BossBotRunner] ";
        private const string RunsFolder = "PlaytestRuns";

        /// <summary>
        /// Techo global. Si el bot se cuelga en un estado que sus propios timeouts no cubren,
        /// esto es lo que evita que el <c>.ps1</c> espere para siempre a un Unity vivo.
        /// </summary>
        private const double WatchdogSeconds = 900d;

        [Serializable]
        private sealed class Pending
        {
            public string BossId;
            public int Turns;
            public int Seed;
            public float TimeScale;
            public bool InfiniteEnergy;
            public bool GodMode;
            public bool Honest;
            public string OutputDir;

            /// <summary>
            /// <c>true</c> ⇒ la corrida vino por <c>-executeMethod</c> y hay que cerrar Unity al
            /// terminar. Desde el MenuItem es <c>false</c>: cerrarle el editor al usuario sería
            /// una sorpresa muy fea.
            /// </summary>
            public bool FromCli;
        }

        // ---- Entradas --------------------------------------------------------

        /// <summary>Entrada de <c>tools/playtest/run-boss-bot.ps1</c> vía <c>-executeMethod</c>.</summary>
        public static void Run()
        {
            var args = BossBotArgs.Parse(Environment.GetCommandLineArgs());
            Debug.Log(LogPrefix + "CLI: " + args);

            if (!Launch(args, fromCli: true))
            {
                // Sin esto un error de setup dejaría Unity abierto y el .ps1 colgado.
                EditorApplication.Exit(3);
            }
        }

        [MenuItem("Rollgeon/Playtest/Run Boss Bot (Cajero)")]
        private static void RunCajero() => LaunchFromMenu("cajero");

        [MenuItem("Rollgeon/Playtest/Run Boss Bot (Generala)")]
        private static void RunGenerala() => LaunchFromMenu("generala");

        /// <summary>
        /// Mismo camino que el CLI pero sin cerrar el editor. Es la herramienta de diagnóstico:
        /// cuando una corrida falla, corriéndola de acá se separa "el bot está mal" de "el
        /// launcher está mal".
        /// </summary>
        private static void LaunchFromMenu(string bossAlias)
        {
            var args = BossBotArgs.Parse(new[]
            {
                BossBotArgs.Prefix + "boss", bossAlias,
                BossBotArgs.Prefix + "turns", BossBotArgs.DefaultTurns.ToString(CultureInfo.InvariantCulture),
            });
            Launch(args, fromCli: false);
        }

        // ---- Arranque --------------------------------------------------------

        private static bool Launch(BossBotArgs args, bool fromCli)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(LogPrefix + "ya está en Play Mode — salí primero.");
                return false;
            }

            var hero = FindHero(DefaultHeroId);
            if (hero == null)
            {
                Debug.LogError(LogPrefix + $"no encontré un ClassHeroSO con EntityId '{DefaultHeroId}'.");
                return false;
            }

            var bootstrapAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);
            if (bootstrapAsset == null)
            {
                Debug.LogError(LogPrefix + $"no encontré la escena de bootstrap en {BootstrapScenePath}.");
                return false;
            }

            string heroGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(hero));
            if (string.IsNullOrEmpty(heroGuid))
            {
                Debug.LogError(LogPrefix + "no pude resolver el GUID del hero.");
                return false;
            }

            string outputDir = ResolveOutputDir(args);
            Directory.CreateDirectory(outputDir);

            SessionState.SetString(SessionKey, JsonUtility.ToJson(new Pending
            {
                BossId = args.BossId,
                Turns = args.Turns,
                Seed = args.Seed,
                TimeScale = args.TimeScale,
                InfiniteEnergy = args.InfiniteEnergy,
                GodMode = args.GodMode,
                Honest = args.Honest,
                OutputDir = outputDir,
                FromCli = fromCli,
            }));

            // Bag/ruleset nulos: el boot registra el ruleset default y CombatHandoffService cae
            // al StartingDiceBagRef del hero. Fijar un bag acá sería inventar una build.
            BootstrapRunOverride.StashForPlayMode(TargetScene, heroGuid, null, null, null);

            Debug.Log(LogPrefix + $"arrancando — {args} → {outputDir}");

            EditorSceneManager.playModeStartScene = bootstrapAsset;
            EditorApplication.isPlaying = true;
            return true;
        }

        private static string ResolveOutputDir(BossBotArgs args)
        {
            if (!string.IsNullOrWhiteSpace(args.OutputDir)) return args.OutputDir;

            // Fuera de Assets/ a propósito: ahí Unity importaría cada PNG, generaría .meta y
            // podría disparar un reimport en medio de la corrida.
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? ".";
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string bossLeaf = args.BossId.Replace("boss.", string.Empty);
            return Path.Combine(projectRoot, RunsFolder, $"{stamp}_{bossLeaf}");
        }

        private static ClassHeroSO FindHero(string entityId)
        {
            return AssetDatabase.FindAssets("t:" + nameof(ClassHeroSO))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ClassHeroSO>)
                .FirstOrDefault(h => h != null
                    && string.Equals(h.EntityId, entityId, StringComparison.OrdinalIgnoreCase));
        }

        // ---- Supervisión (post domain reload) --------------------------------

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            // Salir de Play Mode dispara otro domain reload, que se lleva la suscripción a
            // EditorApplication.update. El quit pendiente viaja por SessionState porque es lo
            // único que cruza el reload — si no, Unity quedaría abierto y el .ps1 esperando
            // para siempre a un proceso que ya terminó su trabajo.
            int pendingQuit = SessionState.GetInt(QuitKey, -1);
            if (pendingQuit >= 0)
            {
                SessionState.EraseInt(QuitKey);
                // delayCall y no ya mismo: Exit en medio de un InitializeOnLoad deja el
                // AssetDatabase a medio abrir y Unity puede escribir un log de crash.
                EditorApplication.delayCall += () => EditorApplication.Exit(pendingQuit);
                return;
            }

            // Se re-engancha en cada reload. Sale gratis cuando no hay corrida pendiente.
            if (string.IsNullOrEmpty(SessionState.GetString(SessionKey, null))) return;

            EditorApplication.update -= Supervise;
            EditorApplication.update += Supervise;
        }

        private static bool _spawned;
        private static double _startedAt;
        private static bool _finishing;

        private static void Supervise()
        {
            string json = SessionState.GetString(SessionKey, null);
            if (string.IsNullOrEmpty(json))
            {
                EditorApplication.update -= Supervise;
                return;
            }

            var pending = JsonUtility.FromJson<Pending>(json);
            if (pending == null)
            {
                Clear();
                return;
            }

            if (!EditorApplication.isPlaying) return;

            if (!_spawned)
            {
                _spawned = true;
                _startedAt = EditorApplication.timeSinceStartup;
                BossBotDriver.Create(ToArgs(pending), pending.OutputDir).Begin();
                return;
            }

            if (_finishing) return;

            var driver = BossBotDriver.Active;
            if (driver == null)
            {
                // Lo único que puede perderlo es un domain reload en medio de la corrida
                // (una recompilación), que se lleva el estático.
                Finish(pending, 1, "se perdió el driver (¿recompiló en medio de la corrida?)", null);
                return;
            }

            if (EditorApplication.timeSinceStartup - _startedAt > WatchdogSeconds)
            {
                Finish(pending, 1, "watchdog", driver);
                return;
            }

            // Un tick del bot por tick del editor. En Play Mode el update del editor corre al
            // menos una vez por frame, así que un tick equivale a un frame de juego.
            bool stillRunning = driver.Pump();

            if (driver.State == BotRunState.Failed) Finish(pending, 1, driver.Failure, driver);
            else if (!stillRunning) Finish(pending, 0, "listo", driver);
        }

        private static BossBotArgs ToArgs(Pending p)
        {
            // Reconstruido por la línea de comandos que el parser ya entiende, para no tener dos
            // formas de armar la config que puedan divergir.
            var argv = new System.Collections.Generic.List<string>
            {
                BossBotArgs.Prefix + "boss", p.BossId,
                BossBotArgs.Prefix + "turns", p.Turns.ToString(CultureInfo.InvariantCulture),
                BossBotArgs.Prefix + "seed", p.Seed.ToString(CultureInfo.InvariantCulture),
                BossBotArgs.Prefix + "timeScale", p.TimeScale.ToString(CultureInfo.InvariantCulture),
                BossBotArgs.Prefix + "out", p.OutputDir,
            };
            // 'honest' primero: apaga los dos cheats, y los flags de abajo (que en una corrida
            // honesta vienen en false) no lo vuelven a prender.
            if (p.Honest) argv.Add(BossBotArgs.Prefix + "honest");
            if (p.InfiniteEnergy) argv.Add(BossBotArgs.Prefix + "infiniteEnergy");
            if (p.GodMode) argv.Add(BossBotArgs.Prefix + "godMode");
            return BossBotArgs.Parse(argv);
        }

        private static void Finish(Pending pending, int exitCode, string reason, BossBotDriver driver)
        {
            _finishing = true;
            WriteResult(pending, exitCode, reason, driver);
            Debug.Log(LogPrefix + $"corrida terminada ({reason}) — exit {exitCode}. Salida: {pending.OutputDir}");

            // Antes de salir de Play Mode: desuscribe los eventos y devuelve timeScale a 1, o el
            // editor del usuario queda acelerado después de una corrida por el MenuItem.
            driver?.Dispose();

            Clear();

            // Salir del Play Mode primero y cerrar después: así corren los OnDestroy (el driver
            // escribe su log ahí) y el hook del Scene Switcher limpia playModeStartScene.
            if (pending.FromCli) SessionState.SetInt(QuitKey, exitCode);
            EditorApplication.isPlaying = false;

            if (!pending.FromCli) return;

            // Doble red: normalmente cierra el Hook tras el reload de salida (ver QuitKey), pero
            // si esa salida no reloadea, esto lo cierra igual. EraseInt lo hace idempotente.
            void QuitWhenStopped()
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                EditorApplication.update -= QuitWhenStopped;
                if (SessionState.GetInt(QuitKey, -1) < 0) return;
                SessionState.EraseInt(QuitKey);
                EditorApplication.Exit(exitCode);
            }
            EditorApplication.update += QuitWhenStopped;
        }

        private static void WriteResult(Pending pending, int exitCode, string reason, BossBotDriver driver)
        {
            try
            {
                Directory.CreateDirectory(pending.OutputDir);
                string json = JsonUtility.ToJson(new Result
                {
                    ExitCode = exitCode,
                    Reason = reason ?? string.Empty,
                    BossId = pending.BossId,
                    TurnsRequested = pending.Turns,
                    TurnsPlayed = driver != null ? driver.TurnsPlayed : 0,
                    Screenshots = driver != null ? driver.ShotsTaken : 0,
                }, prettyPrint: true);
                File.WriteAllText(Path.Combine(pending.OutputDir, "result.json"), json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(LogPrefix + "no pude escribir result.json: " + ex.Message);
            }
        }

        [Serializable]
        private sealed class Result
        {
            public int ExitCode;
            public string Reason;
            public string BossId;
            public int TurnsRequested;
            public int TurnsPlayed;
            public int Screenshots;
        }

        private static void Clear()
        {
            SessionState.EraseString(SessionKey);
            EditorApplication.update -= Supervise;
            _spawned = false;
            _finishing = false;
        }
    }
}
