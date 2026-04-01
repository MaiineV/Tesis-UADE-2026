using System;
using System.Collections;
using UnityEngine;

public class PlayerEntity : MonoBehaviour
{
    public PlayerState State { get; private set; }
    public MeshRenderer Visual;

    [SerializeField] private float moveSpeed = 5f;

    public bool IsMoving { get; private set; }

    public void Initialize(CharacterData data, Vector2Int startPosition)
    {
        State = PlayerState.Create(data);
        State.GridPosition = startPosition;

        if (Visual == null) Visual = GetComponentInChildren<MeshRenderer>(true);
        if (Visual == null)
        {
            // Fallback: create visual at runtime
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Visual";
            cube.transform.SetParent(transform, false);
            cube.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            var col = cube.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            Visual = cube.GetComponent<MeshRenderer>();
        }
        Visual.sharedMaterial = MaterialCache.Player;
        transform.position = GridManager.Instance.GridToWorld(startPosition) + new Vector3(0, 0.4f, 0);
    }

    public void MoveTo(Vector2Int newPosition)
    {
        State.GridPosition = newPosition;
        transform.position = GridManager.Instance.GridToWorld(newPosition) + new Vector3(0, 0.4f, 0);
    }

    public void MoveAlongPath(Vector2Int[] path, Action onComplete = null)
    {
        StartCoroutine(MoveAlongPathRoutine(path, onComplete));
    }

    private IEnumerator MoveAlongPathRoutine(Vector2Int[] path, Action onComplete)
    {
        IsMoving = true;

        foreach (var tile in path)
        {
            Vector3 target = GridManager.Instance.GridToWorld(tile) + new Vector3(0, 0.4f, 0);
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
