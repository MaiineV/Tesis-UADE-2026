using System;
using Patterns;
using Rollgeon.Balance;
using Rollgeon.Run;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rollgeon.Patterns.Bootstrap
{
    /// <summary>
    /// MonoBehaviour que orquesta el arranque del proyecto en la escena <c>00_Bootstrap</c>.
    /// Plan §4.5 y §5.1, TECHNICAL.md §1.1.2.
    /// <para>
    /// <b>Execution order.</b> <c>-10000</c> garantiza que el Awake corra antes que cualquier
    /// MonoBehaviour de gameplay (que por default es 0). Ningun sistema debe suscribirse a
    /// <see cref="ServiceLocator"/> antes de este Awake.
    /// </para>
    /// <para>
    /// <b>async void Awake.</b> Patron intencional (plan key design points): Unity no espera
    /// el Task de un Awake normal, por lo que necesitamos <c>async void</c> para poder hacer
    /// <c>await PreloadAllCatalogsAsync()</c> antes del <c>SceneManager.LoadScene</c>. El body
    /// entero esta envuelto en try/catch para evitar excepciones silenciosas — criterio del
    /// review.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [AddComponentMenu("Rollgeon/Bootstrap/Bootstrap Runner")]
    public class BootstrapRunner : MonoBehaviour
    {
        [Title("Bootstrap Configuration")]
        [Required("Arrastrar el asset ServiceBootstrap (Assets/Rollgeon/Bootstrap/ServiceBootstrap.asset).")]
        [SerializeField]
        private ServiceBootstrapSO _bootstrap;

        [Title("Scene Chaining")]
        [Tooltip("Override opcional del NextSceneName del SO. Vacio = usar valor del SO.")]
        [SerializeField]
        private string _nextScene = string.Empty;

        [Title("Options")]
        [Tooltip("Si esta activo, el GameObject sobrevive al LoadScene (util para persistir logs).")]
        [SerializeField]
        private bool _dontDestroyOnLoad = false;

        [Tooltip("Si esta activo, se espera a que todos los catalogos terminen su PreloadAsync antes de cargar la escena siguiente.")]
        [SerializeField]
        private bool _preloadCatalogs = true;

        private string EffectiveNextScene
        {
            get
            {
                if (BootstrapRunOverride.HasOverride && !string.IsNullOrEmpty(BootstrapRunOverride.TargetScene))
                    return BootstrapRunOverride.TargetScene;
                if (!string.IsNullOrEmpty(_nextScene)) return _nextScene;
                if (_bootstrap != null && !string.IsNullOrEmpty(_bootstrap.NextSceneName)) return _bootstrap.NextSceneName;
                return "01_MainMenu";
            }
        }

        private async void Awake()
        {
            try
            {
                if (_bootstrap == null)
                {
                    BootstrapLog.ErrorContext(
                        "ServiceBootstrapSO reference is null. Arrastrar el asset en el campo '_bootstrap' del BootstrapRunner (ver docs/setup/Foundation#0005_CatalogsAndBootstrap.md §8.4).",
                        this);
                    return;
                }

                if (_dontDestroyOnLoad)
                {
                    DontDestroyOnLoad(gameObject);
                }

                _bootstrap.RegisterAll();

                if (_preloadCatalogs)
                {
                    await _bootstrap.PreloadAllCatalogsAsync();
                }
                else
                {
                    BootstrapLog.Info("Preload skipped (_preloadCatalogs = false)");
                }

                BootstrapHooks.Install();

                var next = EffectiveNextScene;

                if (BootstrapRunOverride.HasOverride && BootstrapRunOverride.Hero != null)
                {
                    var ruleset = BootstrapRunOverride.Ruleset;
                    if (ruleset != null) ServiceLocator.AddService<RulesetSO>(ruleset);
                    PendingRunRequest.Set(
                        BootstrapRunOverride.Hero,
                        Guid.NewGuid(),
                        ruleset != null ? ruleset.RulesetId : null,
                        BootstrapRunOverride.DiceBag,
                        BootstrapRunOverride.StartingItems);
                    BootstrapLog.Info($"Editor override applied: hero={BootstrapRunOverride.Hero.EntityId}, target={next}");
                    BootstrapRunOverride.Consume();
                }

                BootstrapLog.Info($"Loading scene {next}");
                if (!Application.CanStreamedLevelBeLoaded(next))
                {
                    // [SETUP] Agregar la escena destino a File -> Build Settings (ver §8.7 del instructivo).
                    BootstrapLog.Warn($"Scene '{next}' no esta en Build Settings. Esperado solo hasta que T102 MainMenu mergee.");
                    return;
                }
                SceneManager.LoadScene(next);
            }
            catch (Exception ex)
            {
                BootstrapLog.ErrorContext($"Bootstrap failed in Awake: {ex}", this);
            }
        }

        private void OnDestroy()
        {
            BootstrapHooks.Uninstall();
        }
    }
}
