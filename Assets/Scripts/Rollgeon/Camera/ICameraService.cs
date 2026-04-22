using System;
using UnityEngine;

namespace Rollgeon.GameCamera
{
    /// <summary>
    /// Cámara isométrica scripteada. TECHNICAL.md §17.E.
    /// Registrada como <c>IRun</c>-scope en <see cref="Patterns.ServiceLocator"/>
    /// por <see cref="CameraServiceBootstrap"/>. La implementación concreta
    /// (<see cref="CameraService"/>) vive en <c>02_Gameplay.unity</c> como
    /// <c>MonoBehaviour</c> sobre el <c>Main Camera</c>.
    /// </summary>
    public interface ICameraService
    {
        // --- State readonly -------------------------------------------------
        CameraFacing CurrentFacing { get; }
        float CurrentZoom { get; }
        Transform FollowTarget { get; }
        bool IsPanning { get; }
        bool IsFloorView { get; }

        // --- Commands -------------------------------------------------------
        void RotateBy45(bool clockwise);
        void PanBy(Vector2 screenDelta);
        void ZoomBy(float scrollDelta);
        void RecenterOnPlayer(bool instant = false);
        void SetFollowTarget(Transform target);

        /// <summary>
        /// Camera shake hook (§17.E.10 TODO v8). El <see cref="FeedbackManager"/>
        /// dispara <c>OnCameraShakeRequested</c> y el service lo consume acá.
        /// </summary>
        void Shake(float amplitude, float durationSeconds);

        // --- Events (mirror de Patterns.EventName; ver §1.2 + §17.E.10) ----
        event Action<CameraFacing> FacingChanged;
        event Action<bool> FloorViewToggled;
    }

    /// <summary>
    /// Yaw discreto de la cámara. Valor = grados. §17.E.2.
    /// </summary>
    public enum CameraFacing
    {
        N = 0,
        NE = 45,
        E = 90,
        SE = 135,
        S = 180,
        SW = 225,
        W = 270,
        NW = 315,
    }
}
