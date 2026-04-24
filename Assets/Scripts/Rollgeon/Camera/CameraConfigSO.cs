using System.Collections.Generic;
using PrimeTween;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.GameCamera
{
    /// <summary>
    /// Tuning de la cámara isométrica — TECHNICAL.md §17.E.3.
    /// Registrado por <see cref="CameraServiceBootstrap"/> y consumido por
    /// <see cref="CameraService"/> al despertar. Ningún valor vive hardcoded
    /// en el service; todo es editable por el diseñador en el inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Camera/Camera Config", fileName = "CameraConfig")]
    public class CameraConfigSO : SerializedScriptableObject
    {
        // === Placement inicial (editor-only) =================================
        [Title("Placement — editor only")]
        [InfoBox("Se lee una sola vez al inicializar la cámara. No se modifica en runtime.")]
        public float DistanceFromTarget = 12f;
        [Range(0f, 89f)] public float PitchDegrees = 45f;
        public CameraFacing StartingFacing = CameraFacing.NE;

        // === Rotation ========================================================
        [Title("Rotation")]
        [ToggleGroup(nameof(EnableRotation))] public bool EnableRotation = true;
        [ToggleGroup(nameof(EnableRotation))] public float RotationStepDegrees = 45f;
        [ToggleGroup(nameof(EnableRotation))] public float DragPixelsPerStep = 50f;
        [ToggleGroup(nameof(EnableRotation))] public float RotationTweenSeconds = 0.25f;
        [ToggleGroup(nameof(EnableRotation))] public Ease RotationEase = Ease.OutQuad;

        // === Pan =============================================================
        [Title("Pan")]
        [ToggleGroup(nameof(EnablePan))] public bool EnablePan = true;
        [ToggleGroup(nameof(EnablePan))] public float PanSpeed = 18f;
        [ToggleGroup(nameof(EnablePan))] public bool PanClampToFloorBounds = true;
        [ToggleGroup(nameof(EnablePan))] public float PanLerpSeconds = 0.08f;

        // === Zoom ============================================================
        [Title("Zoom")]
        [ToggleGroup(nameof(EnableZoom))] public bool EnableZoom = true;
        [ToggleGroup(nameof(EnableZoom))] public float ZoomMin = 6f;
        [ToggleGroup(nameof(EnableZoom))] public float ZoomMax = 22f;
        [ToggleGroup(nameof(EnableZoom))] public float ZoomStep = 1.5f;
        [ToggleGroup(nameof(EnableZoom))] public float ZoomTweenSeconds = 0.18f;
        [ToggleGroup(nameof(EnableZoom))] public Ease ZoomEase = Ease.OutQuad;
        [ToggleGroup(nameof(EnableZoom))] public bool IsOrthographic = true;

        // === Recenter ========================================================
        [Title("Recenter")]
        public bool EnableRecenterInput = true;
        public float RecenterTweenSeconds = 0.4f;
        public Ease RecenterEase = Ease.InOutQuad;

        // === Wall occlusion ==================================================
        [Title("Wall Occlusion")]
        [ToggleGroup(nameof(EnableWallOcclusion))] public bool EnableWallOcclusion = true;
        [ToggleGroup(nameof(EnableWallOcclusion))] public float WallFadeSeconds = 0.2f;
        [ToggleGroup(nameof(EnableWallOcclusion))]
        [InfoBox("Por cada yaw discreto, qué direcciones de pared se ocultan. " +
                 "Dejar vacío para no ocultar nada en ese yaw.")]
        [OdinSerialize]
        public Dictionary<CameraFacing, List<WallDirection>> OcclusionMap = DefaultOcclusionMap();

        // === Floor view ======================================================
        [Title("Floor View")]
        [ToggleGroup(nameof(EnableFloorView))] public bool EnableFloorView = true;
        [ToggleGroup(nameof(EnableFloorView))]
        [InfoBox("Si CurrentZoom >= este valor se activa la vista del piso (sala actual oculta, shells visibles).")]
        public float FloorViewZoomThreshold = 18f;
        [ToggleGroup(nameof(EnableFloorView))] public float FloorViewTweenSeconds = 0.3f;
        [ToggleGroup(nameof(EnableFloorView))] public Color ShellColor = new(0.1f, 0.1f, 0.15f, 0.85f);

        [ToggleGroup(nameof(EnableFloorView))]
        [InfoBox("Material para los shells del floor view. Si es null se crea uno con URP/Unlit + ShellColor.")]
        public Material ShellMaterial;

        /// <summary>
        /// Mapa simétrico por default (§17.E.3). Ocluye 1 pared en cardinales
        /// y 2 en diagonales.
        /// </summary>
        public static Dictionary<CameraFacing, List<WallDirection>> DefaultOcclusionMap() => new()
        {
            { CameraFacing.N, new List<WallDirection> { WallDirection.S } },
            { CameraFacing.NE, new List<WallDirection> { WallDirection.S, WallDirection.W } },
            { CameraFacing.E, new List<WallDirection> { WallDirection.W } },
            { CameraFacing.SE, new List<WallDirection> { WallDirection.W, WallDirection.N } },
            { CameraFacing.S, new List<WallDirection> { WallDirection.N } },
            { CameraFacing.SW, new List<WallDirection> { WallDirection.N, WallDirection.E } },
            { CameraFacing.W, new List<WallDirection> { WallDirection.E } },
            { CameraFacing.NW, new List<WallDirection> { WallDirection.E, WallDirection.S } },
        };
    }
}
