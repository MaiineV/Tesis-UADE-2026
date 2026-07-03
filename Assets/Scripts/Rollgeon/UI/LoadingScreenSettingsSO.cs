using UnityEngine;

namespace Rollgeon.UI
{
    /// <summary>
    /// Config de <see cref="LoadingScreen"/>: materiales de fondo/spinner y
    /// parámetros de tween. Mismo rol que <c>AudioSettingsSO</c> para
    /// <c>AudioManagerBootstrap</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/UI/Loading Screen Settings", fileName = "LoadingScreenSettings")]
    public class LoadingScreenSettingsSO : ScriptableObject
    {
        [Header("Materiales")]
        [Tooltip("Material con shader Unlit/BackGround — fondo full-rect con iris circular (_Progreso) que revela el gameplay.")]
        public Material BackgroundMaterial;

        [Tooltip("Valor de _Progreso (rango real del shader) que corresponde a \"totalmente revelado\".")]
        public float BackgroundProgressMax = 1.5f;

        [Tooltip("Material con shader RotateUI — spinner con relleno de progreso y dissolve.")]
        public Material SpinnerMaterial;

        [Tooltip("Sprite del ícono del spinner (ej. Ruleta_0) — necesario para que preserveAspect " +
                 "respete la proporción real de la imagen en vez de estirarla al cuadrado.")]
        public Sprite SpinnerIcon;

        [Header("Layout")]
        [Tooltip("Tamaño máximo del spinner — con preserveAspect activo, se ajusta a la proporción real del ícono.")]
        public Vector2 SpinnerSize = new Vector2(200f, 200f);

        [Header("Canvas")]
        public Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        public int SortingOrder = 31000;

        [Range(0f, 1f)]
        [Tooltip("Igual que el CanvasScaler de 01_MainMenu/02_Gameplay (0.5) para que el spinner se escale proporcionalmente al ancho y alto de la pantalla.")]
        public float MatchWidthOrHeight = 0.5f;

        [Header("Tween")]
        [Tooltip("Tiempo mínimo que se muestra el loading, aunque el trabajo real termine antes " +
                 "(evita el parpadeo si la carga es demasiado rápida).")]
        public float MinimumDisplayDuration = 3f;

        [Tooltip("Duración del tween que suaviza cada escalón de ReportProgress.")]
        public float StepTweenDuration = 0.15f;

        [Tooltip("Duración del dissolve del spinner (_Vanish) al completar.")]
        public float VanishDuration = 0.4f;

        [Tooltip("Duración de la apertura del fondo (_Progreso de Unlit/BackGround) al completar.")]
        public float RevealDuration = 0.5f;
    }
}
