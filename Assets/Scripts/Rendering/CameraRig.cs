using System.Collections;
using UnityEngine;

/// <summary>
/// Isometric camera rig matching Godot's camera_rig.gd.
/// WASD moves the rig in the XZ plane relative to its yaw.
/// Q/E rotate it in 45-degree steps (smooth tween).
/// </summary>
public class CameraRig : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Rotation")]
    public float rotationDuration = 0.3f;

    private float _targetRotationY;
    private Coroutine _rotateCoroutine;

    void Start()
    {
        _targetRotationY = transform.eulerAngles.y;
    }

    void Update()
    {
        HandleMovement();
        HandleRotationInput();
    }

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

    void HandleRotationInput()
    {
        if (Input.GetKeyDown(KeyCode.E)) RotateStep(45f);
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
        float startY = transform.eulerAngles.y;
        // Clamp start angle delta so LerpAngle picks the short path
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
}
