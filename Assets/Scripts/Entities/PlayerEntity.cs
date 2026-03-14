using System;
using System.Collections;
using UnityEngine;

public class PlayerEntity : MonoBehaviour
{
    public PlayerState State { get; private set; }
    public SpriteRenderer Visual;

    [SerializeField] private float moveSpeed = 5f; // tiles per second (0.2s per tile)

    public bool IsMoving { get; private set; }

    public void Initialize(CharacterData data, Vector2Int startPosition)
    {
        State = PlayerState.Create(data);
        State.GridPosition = startPosition;

        if (Visual == null) Visual = GetComponent<SpriteRenderer>();
        if (Visual != null)
        {
            if (Visual.sprite == null)
                Visual.sprite = CreateSquareSprite();
            Visual.color = data.CharacterColor;
        }
        transform.position = GridManager.Instance.GridToWorld(startPosition);
    }

    private static Sprite CreateSquareSprite()
    {
        var tex = new Texture2D(32, 32);
        var pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);
    }

    public void MoveTo(Vector2Int newPosition)
    {
        State.GridPosition = newPosition;
        transform.position = GridManager.Instance.GridToWorld(newPosition);
    }

    /// Animated move along a path. Calls onComplete when done.
    public void MoveAlongPath(Vector2Int[] path, Action onComplete = null)
    {
        StartCoroutine(MoveAlongPathRoutine(path, onComplete));
    }

    private IEnumerator MoveAlongPathRoutine(Vector2Int[] path, Action onComplete)
    {
        IsMoving = true;

        foreach (var tile in path)
        {
            Vector3 target = GridManager.Instance.GridToWorld(tile);
            Vector3 start = transform.position;
            float duration = 1f / moveSpeed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.position = Vector3.Lerp(start, target, t);
                yield return null;
            }

            transform.position = target;
            State.GridPosition = tile;
        }

        IsMoving = false;
        onComplete?.Invoke();
    }
}
