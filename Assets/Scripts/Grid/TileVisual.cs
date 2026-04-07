using UnityEngine;
using System.Collections;

public class TileVisual : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propBlock;
    private Color defaultColor;
    private Coroutine pulseCoroutine;

    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    public void Initialize(Color color)
    {
        // Create a flat cube as the tile
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(transform, false);
        cube.transform.localPosition = Vector3.zero;
        cube.transform.localScale = new Vector3(0.92f, 0.08f, 0.92f);

        meshRenderer = cube.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = MaterialCache.GridTile;
        propBlock = new MaterialPropertyBlock();

        // Remove collider from visual cube (we handle clicks via raycast on ground plane)
        var col = cube.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        defaultColor = color;
        SetColor(color);
    }

    public void SetColor(Color color)
    {
        if (meshRenderer == null || propBlock == null) return;
        meshRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(ColorID, color);
        propBlock.SetColor(BaseColorID, color);
        meshRenderer.SetPropertyBlock(propBlock);
    }

    public void ResetColor()
    {
        StopPulse();
        SetColor(defaultColor);
    }

    public void StopPulse()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
    }

    private void OnDisable()
    {
        pulseCoroutine = null;
    }

    public void SetAsLadder(Color ladderColor)
    {
        defaultColor = ladderColor;
        SetColor(ladderColor);

        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseRoutine(ladderColor));
    }

    public void SetAsDoor(Color doorColor)
    {
        defaultColor = doorColor;
        SetColor(doorColor);

        // Make door tiles slightly taller
        var cube = meshRenderer.transform;
        cube.localScale = new Vector3(0.92f, 0.15f, 0.92f);

        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseRoutine(doorColor));
    }

    private IEnumerator PulseRoutine(Color baseColor)
    {
        Color bright = Color.Lerp(baseColor, Color.white, 0.3f);
        float speed = 2f;

        while (true)
        {
            float t = (Mathf.Sin(Time.time * speed) + 1f) / 2f;
            SetColor(Color.Lerp(baseColor, bright, t));
            yield return null;
        }
    }
}
