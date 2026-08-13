using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI
{
    /// <summary>
    /// Canvas ScreenSpace-Overlay persistente (<c>DontDestroyOnLoad</c>), creado
    /// on-demand por <see cref="LoadingScreenBootstrap"/>. Sobrevive a
    /// <c>SceneManager.LoadScene</c>/<c>LoadSceneAsync</c> para poder tapar la
    /// carga y despues revelar la escena nueva ya activa detras suyo.
    /// </summary>
    /// <remarks>
    /// Mismo patron que <see cref="Rollgeon.UI.HUD.PersistentUiOverlay"/>
    /// (Canvas persistente armado por codigo) y
    /// <see cref="Rollgeon.Audio.AudioManagerBootstrap"/> (ScriptableObject
    /// bootstrap con un SettingsSO). No es una <see cref="BaseScreen"/>: no vive
    /// en el stack de ningun <see cref="ScreenHost"/> puntual, por eso expone su
    /// propia API (<see cref="Show"/>/<see cref="ReportProgress"/>) en vez de los
    /// hooks de <see cref="IBaseScreen"/>.
    /// </remarks>
    [DefaultExecutionOrder(-1000)]
    public sealed class LoadingScreen : MonoBehaviour, ILoadingScreenService
    {
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int VanishId = Shader.PropertyToID("_Vanish");

        // Unlit/BackGround usa su propio nombre de propiedad ("Progreso", en
        // español) y un rango real 0-1.5 en vez de 0-1.
        private static readonly int BackgroundProgressId = Shader.PropertyToID("_Progreso");

        private static LoadingScreen _instance;

        private LoadingScreenSettingsSO _settings;
        private Image _backgroundMask;
        private Image _spinner;

        // Instancias propias, no los assets del proyecto — ver BuildHierarchy.
        private Material _spinnerMaterial;
        private Material _backgroundMaterial;

        private Tween _progressTween;
        private float _lastProgress;
        private Action _onRevealComplete;
        private float _shownAtUnscaledTime;

        /// <summary>Crea el GameObject persistente la primera vez que se llama.</summary>
        public static LoadingScreen Create(LoadingScreenSettingsSO settings)
        {
            if (_instance != null) return _instance;

            var go = new GameObject("[LoadingScreen]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<LoadingScreen>();
            _instance._settings = settings;
            _instance.BuildHierarchy();
            go.SetActive(false);

            return _instance;
        }

        private void BuildHierarchy()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _settings.SortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _settings.ReferenceResolution;
            scaler.matchWidthOrHeight = _settings.MatchWidthOrHeight;

            // Graphic.material devuelve el asset compartido — NO instancia una copia
            // como hace Renderer.material. Animar "el material del Image" mutaría el
            // .mat del proyecto: en el editor eso lo ensucia en disco y deja los
            // valores del último loading commiteados por accidente.
            _backgroundMaterial = new Material(_settings.BackgroundMaterial);
            _spinnerMaterial = new Material(_settings.SpinnerMaterial);

            _backgroundMask = CreateImage("Background", transform);
            var bgRect = _backgroundMask.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            _backgroundMask.material = _backgroundMaterial;

            _spinner = CreateImage("Spinner", transform);
            var spinnerRect = _spinner.rectTransform;
            spinnerRect.anchorMin = spinnerRect.anchorMax = new Vector2(0.5f, 0.5f);
            spinnerRect.anchoredPosition = Vector2.zero;
            spinnerRect.sizeDelta = _settings.SpinnerSize;
            _spinner.material = _spinnerMaterial;
            _spinner.sprite = _settings.SpinnerIcon;
            _spinner.preserveAspect = true;
        }

        private void OnDestroy()
        {
            if (_spinnerMaterial != null) Destroy(_spinnerMaterial);
            if (_backgroundMaterial != null) Destroy(_backgroundMaterial);
        }

        private static Image CreateImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        public void Show(Action onRevealComplete = null)
        {
            _onRevealComplete = onRevealComplete;
            _shownAtUnscaledTime = Time.unscaledTime;

            _progressTween.Stop();
            _lastProgress = 0f;

            _backgroundMaterial.SetFloat(BackgroundProgressId, 0f);
            _spinnerMaterial.SetFloat(ProgressId, 0f);
            _spinnerMaterial.SetFloat(VanishId, -1f);

            gameObject.SetActive(true);
        }

        public void ReportProgress(float progress01)
        {
            // Los callers reportan una vez por frame dentro de un
            // while (op.progress < 0.9f) { ...; yield return null; }. Sin este corte
            // se crea un tween de 0.15s por frame sobre la misma propiedad: se apilan
            // decenas corriendo a la vez y, mientras op.progress sigue en 0, el
            // destino es igual al valor actual y PrimeTween avisa en cada uno.
            // También evita que un 1f repetido dispare el reveal más de una vez.
            if (Mathf.Approximately(progress01, _lastProgress)) return;
            _lastProgress = progress01;

            _progressTween.Stop();
            _progressTween = Tween.MaterialProperty(_spinnerMaterial, ProgressId, progress01,
                _settings.StepTweenDuration, Ease.OutQuad);

            if (progress01 < 1f) return;

            // No arrancar el reveal antes de MinimumDisplayDuration — evita el
            // parpadeo cuando el trabajo real (pasos de piso, carga de escena)
            // termina más rápido de lo que el jugador llega a leer el spinner.
            float elapsed = Time.unscaledTime - _shownAtUnscaledTime;
            float remaining = Mathf.Max(0f, _settings.MinimumDisplayDuration - elapsed);
            if (remaining > 0f)
                Tween.Delay(remaining, PlayReveal, useUnscaledTime: true);
            else
                PlayReveal();
        }

        private void PlayReveal()
        {
            Tween.MaterialProperty(_spinnerMaterial, VanishId, 1f, _settings.VanishDuration, Ease.Linear);
            Tween.MaterialProperty(_backgroundMaterial, BackgroundProgressId, _settings.BackgroundProgressMax,
                    _settings.RevealDuration, Ease.InOutSine)
                .OnComplete(() =>
                {
                    gameObject.SetActive(false);
                    _onRevealComplete?.Invoke();
                });
        }
    }
}
