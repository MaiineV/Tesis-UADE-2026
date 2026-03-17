using UnityEngine;
using System.Collections;

public class TileVisual : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Color defaultColor;
    private Coroutine pulseCoroutine;

    private static Sprite sharedSprite;

    public void Initialize(Color color)
    {
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (sharedSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            sharedSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        }

        spriteRenderer.sprite = sharedSprite;
        defaultColor = color;
        spriteRenderer.color = color;
    }

    public void SetColor(Color color)
    {
        spriteRenderer.color = color;
    }

    public void ResetColor()
    {
        spriteRenderer.color = defaultColor;
    }

    public void SetAsLadder(Color ladderColor)
    {
        defaultColor = ladderColor;
        spriteRenderer.color = ladderColor;

        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseRoutine(ladderColor));
    }

    private IEnumerator PulseRoutine(Color baseColor)
    {
        Color bright = Color.Lerp(baseColor, Color.white, 0.3f);
        float speed = 2f;

        while (true)
        {
            float t = (Mathf.Sin(Time.time * speed) + 1f) / 2f;
            spriteRenderer.color = Color.Lerp(baseColor, bright, t);
            yield return null;
        }
    }
}
