using System;
using Rollgeon.Input;
using UnityEngine;

namespace Rollgeon.Tutorial.UI
{
    /// <summary>Qué señala el paso: nada (popup centrado), una posición de mundo,
    /// una entidad (por Guid, sigue al pawn) o un elemento del HUD.</summary>
    public enum TutorialAnchorKind
    {
        None,
        WorldPosition,
        WorldEntity,
        RectTransform,
    }

    /// <summary>Política de input del overlay mientras está visible.</summary>
    public enum TutorialInputPolicy
    {
        /// <summary>El overlay nunca intercepta input (default): los clicks de mundo y
        /// los botones del HUD siguen funcionando — completar la acción pedida es
        /// responsabilidad del flujo del juego, no del overlay.</summary>
        PassThrough,

        /// <summary>Paso de texto puro: el dim captura el click y dispara el callback
        /// <c>onContinue</c> de <see cref="ITutorialOverlayService.Show"/>.</summary>
        BlockUntilContinue,
    }

    /// <summary>
    /// Request de display de un paso del tutorial: dim + recorte circular sobre el
    /// anchor, flecha apuntando al recorte y popup de texto en el cuadrante opuesto.
    /// </summary>
    public sealed class TutorialStepDisplayRequest
    {
        public TutorialAnchorKind AnchorKind = TutorialAnchorKind.None;

        /// <summary>Anchor para <see cref="TutorialAnchorKind.WorldPosition"/>.</summary>
        public Vector3 WorldPosition;

        /// <summary>Anchor para <see cref="TutorialAnchorKind.WorldEntity"/> — se
        /// resuelve por frame vía IEntityPositionResolver/IPawnRegistry (sigue al pawn).</summary>
        public Guid EntityGuid;

        /// <summary>Anchor para <see cref="TutorialAnchorKind.RectTransform"/> (botón del HUD).</summary>
        public RectTransform UiTarget;

        /// <summary>Rects adicionales que el recorte también debe abarcar (ej. la zona
        /// de dados + los botones Roll y Confirmar). Centro y radio se calculan sobre
        /// el bounding box de <see cref="UiTarget"/> + estos. Opcional.</summary>
        public RectTransform[] UiTargetsExtra;

        /// <summary>Texto del popup (español literal, como el resto de la UI). Puede
        /// contener <c>{0}</c> — se formatea con la tecla viva de <see cref="HotkeyHint"/>.</summary>
        public string Text;

        /// <summary>Si tiene valor, <see cref="Text"/> se formatea con
        /// <c>IGameplayHotkeyService.GetKeyHint(hotkey)</c> (rebind-safe).</summary>
        public GameplayHotkey? HotkeyHint;

        /// <summary>Radio del recorte en píxeles. <c>&lt;= 0</c> = auto (default de
        /// settings para anchors de mundo; derivado del rect para UI).</summary>
        public float CutoutRadiusPx;

        public bool ShowArrow = true;

        public TutorialInputPolicy InputPolicy = TutorialInputPolicy.PassThrough;
    }

    /// <summary>
    /// Overlay de señalización del tutorial: pantalla opacada con recorte circular,
    /// flecha y popup explicativo. Display "tonto" — la detección de "el jugador hizo
    /// la acción" vive en el <c>TutorialFlowController</c>, que llama <see cref="Hide"/>
    /// y muestra el paso siguiente. Registrado Global (canvas persistente, inerte
    /// cuando está oculto).
    /// <para>
    /// Pese al nombre, no es exclusivo del tutorial: no tiene ningún acople al gameplay
    /// (sus dos referencias a <c>IGameplayHotkeyService</c> son opcionales), así que
    /// cualquier pantalla puede usarlo para coach-marks. Hoy también lo consume
    /// <c>Rollgeon.UI.Help.BuildHelpFlow</c> en el armado de bolsa.
    /// </para>
    /// </summary>
    public interface ITutorialOverlayService
    {
        bool IsVisible { get; }

        /// <summary>
        /// Muestra (o retargetea, si ya está visible) el paso. <paramref name="onContinue"/>
        /// solo dispara con <see cref="TutorialInputPolicy.BlockUntilContinue"/>.
        /// </summary>
        void Show(TutorialStepDisplayRequest request, Action onContinue = null);

        /// <summary>Oculta el overlay (tween de fade). Safe si ya está oculto.</summary>
        void Hide();
    }
}
