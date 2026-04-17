using System.Collections;
using UnityEngine;

/// <summary>
/// Isometric camera rig matching Godot's camera_rig.gd.
/// WASD moves the rig in the XZ plane relative to its yaw.
/// Q/E rotate it in 45-degree steps (smooth tween).
///
/// Pixel snap: each LateUpdate the rig position is snapped to the nearest
/// RT texel boundary in camera-local XY space, and the sub-texel remainder
/// is pushed as _PixelPanOffset for the SharpUpscale display shader to
/// compensate — recovering smooth motion while eliminating pixel crawl.
/// </summary>
public class CameraRig : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Rotation")]
    public float rotationDuration = 0.3f;

    [Header("Pixel Snap")]
    [Tooltip("Snap camera to RT texel grid each frame to eliminate pixel crawl.")]
    public bool enablePixelSnap = true;
    [Tooltip("Must match the RenderTexture height set in ArtTestSceneBuilder (RT_H).")]
    public int pixelRenderHeight = 380;

    // ── private state ──────────────────────────────────────────────────────────
    private float     _targetRotationY;
    private Coroutine _rotateCoroutine;
    private Camera    _cam;

    static readonly int s_PixelPanOffset = Shader.PropertyToID("_PixelPanOffset");

    // ── lifecycle ──────────────────────────────────────────────────────────────
    void Start()
    {
        _targetRotationY = transform.eulerAngles.y;
        _cam = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        HandleMovement();
        HandleRotationInput();
    }

    void LateUpdate()
    {
        // Pixel snap runs after all movement so it sees the final position.
        if (enablePixelSnap)
            ApplyPixelSnap();
    }

    // ── movement ───────────────────────────────────────────────────────────────
    void HandleMovement()
    {
        var input = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) input.z += 1;
        if (Input.GetKey(KeyCode.S)) input.z -= 1;
        if (Input.GetKey(KeyCode.A)) input.x -= 1;
        if (Input.GetKey(KeyCode.D)) input.x += 1;

        if (input == Vector3.zero) return;

        // Move relative to current yaw so forward is always "into" the screen
        float yaw = transform.eulerAngles.y;
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 dir = yawRot * new Vector3(input.x, 0f, input.z).normalized;
        transform.position += dir * moveSpeed * Time.deltaTime;
    }

    // ── rotation ───────────────────────────────────────────────────────────────
    void HandleRotationInput()
    {
        if (Input.GetKeyDown(KeyCode.E)) RotateStep( 45f);
        if (Input.GetKeyDown(KeyCode.Q)) RotateStep(-45f);
    }

    void RotateStep(float amount)
    {
        _targetRotationY += amount;
        if (_rotateCoroutine != null) StopCoroutine(_rotateCoroutine);
        _rotateCoroutine = StartCoroutine(SmoothRotate(_targetRotationY));
    }

    IEnumerator SmoothRotate(float targetY)
    {
        float startY  = transform.eulerAngles.y;
        float elapsed = 0f;

        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / rotationDuration);
            // Ease in-out cubic (matches Godot TRANS_CUBIC EASE_IN_OUT)
            float smooth = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
            float y = Mathf.LerpAngle(startY, targetY, smooth);
            Vector3 e = transform.eulerAngles;
            transform.eulerAngles = new Vector3(e.x, y, e.z);
            yield return null;
        }

        Vector3 final = transform.eulerAngles;
        transform.eulerAngles = new Vector3(final.x, targetY, final.z);
        _rotateCoroutine = null;
    }

    // ── pixel snap ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Snaps the rig position to the RT texel grid in camera-local XY (screen) space.
    /// Pushes the sub-texel snap error as _PixelPanOffset so the SharpUpscale shader
    /// can shift the UV sample to recover the exact intended position.
    ///
    /// Result: 3D world moves in discrete pixel steps (no crawl), but the UV
    /// compensation makes it look perfectly smooth at any screen resolution.
    /// </summary>
    void ApplyPixelSnap()
    {
        if (_cam == null || !_cam.orthographic) return;

        // World units per RT pixel (height axis)
        float texelSize = (_cam.orthographicSize * 2f) / pixelRenderHeight;
        if (texelSize <= 1e-5f) return;

        // Project the rig world position onto the camera's screen-plane axes.
        // This avoids using InverseTransformPoint (which is relative to the
        // camera origin and introduces a spurious drift offset every frame).
        Vector3 pos   = transform.position;
        Vector3 right = _cam.transform.right;   // screen X axis in world space
        Vector3 up    = _cam.transform.up;       // screen Y axis in world space

        float projRight = Vector3.Dot(pos, right);
        float projUp    = Vector3.Dot(pos, up);

        // Snap each axis to the nearest texel boundary
        float snappedRight = Mathf.Round(projRight / texelSize) * texelSize;
        float snappedUp    = Mathf.Round(projUp    / texelSize) * texelSize;

        // Error = snapped - true  (how much we shifted the camera)
        float errRight = snappedRight - projRight;
        float errUp    = snappedUp    - projUp;

        // Apply snap: move rig along screen axes by the rounding error only
        transform.position = pos + right * errRight + up * errUp;

        // Normalize to RT-UV space and push globally to the SharpUpscale shader.
        // The shader adds this offset so the sampled UV recovers the true position.
        float uvErrX = errRight / (_cam.orthographicSize * 2f * _cam.aspect);
        float uvErrY = errUp    / (_cam.orthographicSize * 2f);
        Shader.SetGlobalVector(s_PixelPanOffset, new Vector4(uvErrX, uvErrY, 0f, 0f));
    }
}
