using UnityEngine;

/// <summary>
/// Scatters 1024 billboarded grass instances on a flat XZ area,
/// matching Godot's MultiMeshInstance3D with 1024 quads.
/// Uses Graphics.RenderMeshInstanced for zero-overhead GPU instancing.
/// </summary>
[ExecuteAlways]
public class GrassInstancer : MonoBehaviour
{
    [Header("Placement")]
    public int count = 1024;
    public float areaSize = 10f;
    public float seed = 0f;

    [Header("Rendering")]
    public Mesh quadMesh;
    public Material foliageMaterial;

    private Matrix4x4[] _matrices;
    private bool _dirty = true;

    void OnEnable()  => _dirty = true;
    void OnValidate() => _dirty = true;

    void RebuildMatrices()
    {
        _dirty = false;
        _matrices = new Matrix4x4[count];
        var rng = new System.Random((int)(seed * 1000));
        float half = areaSize * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float x = (float)(rng.NextDouble() * areaSize - half) + transform.position.x;
            float z = (float)(rng.NextDouble() * areaSize - half) + transform.position.z;
            float y = transform.position.y;
            float scale = (float)(rng.NextDouble() * 0.4 + 0.8); // 0.8–1.2
            _matrices[i] = Matrix4x4.TRS(
                new Vector3(x, y, z),
                Quaternion.identity,
                Vector3.one * scale);
        }
    }

    void Update()
    {
        if (_dirty) RebuildMatrices();
        if (foliageMaterial == null || quadMesh == null || _matrices == null) return;

        // DrawMeshInstanced supports max 1023 per call; split if needed
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.2f);
        Gizmos.DrawCube(transform.position + Vector3.up * 0.5f,
            new Vector3(areaSize, 1f, areaSize));
    }
}
