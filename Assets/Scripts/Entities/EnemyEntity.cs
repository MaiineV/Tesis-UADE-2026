using System;
using System.Collections;
using UnityEngine;

public class EnemyEntity : MonoBehaviour
{
    public EnemyState State { get; private set; }
    public MeshRenderer Visual;

    [SerializeField] private float moveSpeed = 3.3f;

    public bool IsMoving { get; private set; }

    private MaterialPropertyBlock propBlock;
    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    public void Initialize(EnemyData data, Vector2Int position)
    {
        State = EnemyState.Create(data, position);

        // If a 3D model prefab is assigned, instantiate it
        if (data.ModelPrefab != null)
        {
            // Disable SpriteRenderer on root so the 2D placeholder doesn't cover the model
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;

            // Remove any existing visual children
            foreach (Transform child in transform)
                Destroy(child.gameObject);

            var modelInstance = Instantiate(data.ModelPrefab, transform);
            modelInstance.name = "Model";
            Visual = modelInstance.GetComponentInChildren<MeshRenderer>(true);
        }
        else
        {
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
            propBlock = new MaterialPropertyBlock();
            Visual.sharedMaterial = MaterialCache.Get("Mat_" + data.EnemyName);
        }

        transform.position = GridManager.Instance.GridToWorld(position) + new Vector3(0, 0.4f, 0);
    }

    private Coroutine _bossPulseCoroutine;

    public void StartBossPulse()
    {
        if (_bossPulseCoroutine != null) StopCoroutine(_bossPulseCoroutine);
        _bossPulseCoroutine = StartCoroutine(BossPulseRoutine());
    }

    private IEnumerator BossPulseRoutine()
    {
        Color baseColor = State.BaseData.EnemyColor;
        Color bright = Color.white;
        float speed = 2f;
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        while (true)
        {
            float t = (Mathf.Sin(Time.time * speed) + 1f) / 2f;
            Color c = Color.Lerp(baseColor, bright, t * 0.4f);
            if (Visual != null)
            {
                Visual.GetPropertyBlock(propBlock);
                propBlock.SetColor(ColorID, c);
                propBlock.SetColor(BaseColorID, c);
                Visual.SetPropertyBlock(propBlock);
            }
            yield return null;
        }
    }

    public int RollAttack()
    {
        int totalDamage = 0;

        // Ranged accuracy check
        if (State.BaseData.IsRanged)
        {
            int hitCheck = UnityEngine.Random.Range(1, 101);
            if (hitCheck > State.BaseData.Accuracy)
                return 0; // miss
        }

        for (int i = 0; i < State.BaseData.AttackDiceCount; i++)
        {
            totalDamage += UnityEngine.Random.Range(1, State.BaseData.AttackDiceFaces + 1);
        }

        if (State.IsEnraged)
        {
            bool crit = UnityEngine.Random.value < 0.6f;
            State.CurrentEnergy = 0;
            if (crit)
            {
                totalDamage *= 2;
            }
        }

        State.GainEnergy();
        return totalDamage;
    }

    public void MoveTo(Vector2Int newPosition)
    {
        State.GridPosition = newPosition;
        transform.position = GridManager.Instance.GridToWorld(newPosition) + new Vector3(0, 0.4f, 0);
    }

    public void AnimateMoveTo(Vector2Int newPosition, Action onComplete = null)
    {
        StartCoroutine(AnimateMoveRoutine(newPosition, onComplete));
    }

    private IEnumerator AnimateMoveRoutine(Vector2Int newPosition, Action onComplete)
    {
        IsMoving = true;
        Vector3 start = transform.position;
        Vector3 target = GridManager.Instance.GridToWorld(newPosition) + new Vector3(0, 0.4f, 0);
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
        State.GridPosition = newPosition;
        IsMoving = false;
        onComplete?.Invoke();
    }

    public void StopBossPulse()
    {
        if (_bossPulseCoroutine != null)
        {
            StopCoroutine(_bossPulseCoroutine);
            _bossPulseCoroutine = null;
        }
    }

    public void PlayDeathAnimation(Action onComplete = null)
    {
        StopBossPulse();
        StartCoroutine(DeathRoutine(onComplete));
    }

    private IEnumerator DeathRoutine(Action onComplete)
    {
        float duration = 0.5f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

            // Scale-to-zero handles visual death (alpha on opaque URP material is a no-op)

            yield return null;
        }

        gameObject.SetActive(false);
        transform.localScale = startScale;

        onComplete?.Invoke();
    }
}
