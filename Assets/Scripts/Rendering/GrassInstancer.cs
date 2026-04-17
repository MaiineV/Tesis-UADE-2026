using UnityEngine;

/// <summary>
/// Scatters billboarded grass instances across an area.
///
/// Placement modes:
///   Flat   — random XZ on a plane at the component's Y (original behaviour, no physics).
///   Raycast — fires a Physics.Raycast downward per instance to land on real geometry.
///             Only runs once (in OnEnable / OnValidate) so there is zero runtime cost.
///             Requires colliders on the terrain/floor layer.
/// </summary>
[ExecuteAlways]
public class GrassInstancer : MonoBehaviour
{
    // ── placement ──────────────────────────────────────────────────────────────
    public enum PlacementMode { Flat, Raycast }

    [Header("Placement")]
    public PlacementMode placement = PlacementMode.Flat;
    public int   count    = 1024;
    public float areaSize = 10f;
    public float seed     = 0f;

    [Header("Raycast Placement")]
    [Tooltip("Layer mask for terrain / floor colliders.")]
    public LayerMask terrainLayer = ~0;
    [Tooltip("Ray origin Y above the area center. Must clear any geometry above the floor.")]
    public float raycastOriginHeight = 20f;

    [Header("Rendering")]
    public Mesh     quadMesh;
    public Material foliageMaterial;

    // ── private state ──────────────────────────────────────────────────────────
    private Matrix4x4[] _matrices;
    private bool        _dirty = true;

    // ── lifecycle ──────────────────────────────────────────────────────────────
    void OnEnable()   => _dirty = true;
    void OnValidate() => _dirty = true;

    // ── build ──────────────────────────────────────────────────────────────────
    void RebuildMatrices()
    {
        _dirty = false;
        _matrices = new Matrix4x4[count];
        var rng  = new System.Random((int)(seed * 1000));
        float half = areaSize * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float x     = (float)(rng.NextDouble() * areaSize - half) + transform.position.x;
            float z     = (float)(rng.NextDouble() * areaSize - half) + transform.position.z;
            float y     = transform.position.y;
            float scale = (float)(rng.NextDouble() * 0.4 + 0.8); // 0.8 – 1.2

            if (placement == PlacementMode.Raycast)
            {
                // Cast downward from above to land exactly on floor/terrain geometry.
                // Only runs here at build time — zero cost per frame at runtime.
                var origin = new Vector3(x, transform.position.y + raycastOriginHeight, z);
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                                    raycastOriginHeight * 2f, terrainLayer))
                {
                    y = hit.point.y;
                }
            }

            _matrices[i] = Matrix4x4.TRS(
                new Vector3(x, y, z),
                Quaternion.Euler(0f, (float)(rng.NextDouble() * 360f), 0f),
                Vector3.one * scale);
        }
    }

    // ── render loop ────────────────────────────────────────────────────────────
    void Update()
    {
        if (_dirty) RebuildMatrices();
        if (foliageMaterial == null || quadMesh == null || _matrices == null) return;

        // DrawMeshInstanced supports max 1023 per call
        int drawn = 0;
        while (drawn < _matrices.Length)
        {
            int batch = Mathf.Min(1023, _matrices.Length - drawn);
            var slice = new Matrix4x4[batch];
            System.Array.Copy(_matrices, drawn, slice, 0, batch);
            Graphics.DrawMeshInstanced(quadMesh, 0, foliageMaterial, slice,
                batch, null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
            drawn += batch;
        }
    }

    // ── editor gizmo ───────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.2f);
        Gizmos.DrawCube(transform.position + Vector3.up * 0.5f,
            new Vector3(areaSize, 1f, areaSize));
    }
}
