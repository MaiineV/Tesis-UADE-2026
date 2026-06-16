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

        [Range(-44f, 44f)]
        [Tooltip("Offset de yaw aplicado SOLO a las diagonales (NE/SE/SW/NW). " +
                 "Los cardinales (N/E/S/W) siempre quedan en múltiplos exactos de 90°. " +
                 "Ejemplo: 9.7 → NW=324.7°, NE=54.7°, pero N=0°, E=90°.")]
        public float DiagonalYawOffset = 0f;

        [Tooltip("Si true, la cámara sigue al player frame a frame. Si false, queda " +
                 "estática: se ancla al encuadre default (DefaultFocusOffset) y se " +
                 "reposiciona al entrar a cada sala, pero NO trackea al player mientras " +
                 "se mueve dentro de la sala.")]
        public bool FollowPlayer = true;

        [Tooltip("Ajuste fino del encuadre default: offset world-space del foco. En modo " +
                 "follow es respecto del player; en modo estático (FollowPlayer = false) es " +
                 "respecto del CENTRO de la sala. (0,0,0) = sin ajuste (estático: centrado " +
                 "exacto en la sala). X/Z desplazan sobre el plano del piso; Y normalmente 0.")]
        public Vector3 DefaultFocusOffset = Vector3.zero;

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
        // Tamaño orthographic con el que arranca la cámara al cargar la sesión.
        // Antes era (ZoomMin+ZoomMax)/2 = 14 (midpoint), ahora explícito en 9 para
        // que el usuario vea una vista más cerrada al spawnear.
        [ToggleGroup(nameof(EnableZoom))] public float DefaultZoom = 9f;
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

        // === Pixel Snap ======================================================
        [Title("Pixel Snap")]
        [InfoBox("Snappea la cámara a la grilla de texels del RenderTexture para eliminar " +
                 "pixel crawl. Requiere cámara ortográfica + RT de baja resolución.")]
        [ToggleGroup(nameof(EnablePixelSnap))] public bool EnablePixelSnap = true;
        [ToggleGroup(nameof(EnablePixelSnap))]
        [InfoBox("Debe coincidir con la altura del RenderTexture (ej. 180 para 320×180).")]
        public int PixelRenderHeight = 180;

        // === Floor view ======================================================
        [Title("Floor View")]
        [ToggleGroup(nameof(EnableFloorView))] public bool EnableFloorView = true;
        [ToggleGroup(nameof(EnableFloorView))]
        [InfoBox("Si CurrentZoom >= este valor se activa la vista del piso (sala actual oculta, shells visibles).")]
        public float FloorViewZoomThreshold = 18f;
        [ToggleGroup(nameof(EnableFloorView))] public float FloorViewTweenSeconds = 0.3f;

        // Ícono de sala (sprite encima del shell): tamaño world uniforme y cuánto flota
        // sobre la cara superior. Antes vivían hardcoded en FloorShellVisibilityController.
        [ToggleGroup(nameof(EnableFloorView))]
        [MinValue(0.1f)]
        [InfoBox("Tamaño world del sprite de ícono (boss/shop) que flota sobre el shell.")]
        public float ShellIconWorldSize = 3f;
        [ToggleGroup(nameof(EnableFloorView))]
        [Tooltip("Altura a la que flota el ícono sobre la cara superior del shell.")]
        public float ShellIconHeightOffset = 0.75f;

        // Dos estados de shell en el floor view: salas ya visitadas (más claras) vs vecinas
        // descubiertas pero no visitadas (más oscuras). Si el Material slot es null se genera
        // un URP/Unlit con el Color de fallback correspondiente.
        [ToggleGroup(nameof(EnableFloorView))]
        [InfoBox("Shell de salas ya visitadas (más claro). Si es null se usa ShellVisitedColor.")]
        public Material ShellVisitedMaterial;
        [ToggleGroup(nameof(EnableFloorView))] public Color ShellVisitedColor = new(0.38f, 0.38f, 0.44f, 0.9f);

        [ToggleGroup(nameof(EnableFloorView))]
        [InfoBox("Shell de salas adyacentes descubiertas pero no visitadas (más oscuro). Si es null se usa ShellAdjacentColor.")]
        public Material ShellAdjacentMaterial;
        [ToggleGroup(nameof(EnableFloorView))] public Color ShellAdjacentColor = new(0.1f, 0.1f, 0.15f, 0.85f);

        [ToggleGroup(nameof(EnableWallOcclusion))]
        [Button("Reset OcclusionMap to Default", ButtonSizes.Medium)]
        private void ResetOcclusionMapToDefault()
        {
            OcclusionMap = DefaultOcclusionMap();
#if UNITY_EDITOR
            // SaveAssetIfDirty fuerza el flush a disco: si el usuario clickea el
            // botón y entra a Play sin Ctrl+S, Unity recargaría el asset desde
            // disco con el OcclusionMap viejo (Odin lo serializa en SerializationData).
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
#endif
        }

        /// <summary>
        /// Mapa simétrico por default (§17.E.3). Por facing oculta 3 paredes:
        /// la dirección OPUESTA al facing + sus dos vecinas en el compás
        /// (= las paredes más cercanas a la cámara, las que tapan al jugador).
        /// Con iso 45° y CameraFacing = dirección de mirada, la cámara queda
        /// en el lado opuesto al facing → las paredes a ocultar son las del
        /// lado de la cámara.
        /// </summary>
        public static Dictionary<CameraFacing, List<WallDirection>> DefaultOcclusionMap() => new()
        {
            { CameraFacing.N,  new List<WallDirection> { WallDirection.SW, WallDirection.S,  WallDirection.SE } },
            { CameraFacing.NE, new List<WallDirection> { WallDirection.W,  WallDirection.SW, WallDirection.S  } },
            { CameraFacing.E,  new List<WallDirection> { WallDirection.SW, WallDirection.W,  WallDirection.NW } },
            { CameraFacing.SE, new List<WallDirection> { WallDirection.W,  WallDirection.NW, WallDirection.N  } },
            { CameraFacing.S,  new List<WallDirection> { WallDirection.NW, WallDirection.N,  WallDirection.NE } },
            { CameraFacing.SW, new List<WallDirection> { WallDirection.N,  WallDirection.NE, WallDirection.E  } },
            { CameraFacing.W,  new List<WallDirection> { WallDirection.NE, WallDirection.E,  WallDirection.SE } },
            { CameraFacing.NW, new List<WallDirection> { WallDirection.E,  WallDirection.SE, WallDirection.S  } },
        };
    }
}
