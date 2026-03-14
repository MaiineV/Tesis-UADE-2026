using System;
using System.Collections;
using UnityEngine;

public class EnemyEntity : MonoBehaviour
{
    public EnemyState State { get; private set; }
    public SpriteRenderer Visual;

    [SerializeField] private float moveSpeed = 3.3f; // tiles per second (0.3s per tile)

    public bool IsMoving { get; private set; }

    public void Initialize(EnemyData data, Vector2Int position)
    {
        State = EnemyState.Create(data, position);
        if (Visual == null) Visual = GetComponent<SpriteRenderer>();
        if (Visual != null) Visual.color = data.EnemyColor;
        transform.position = GridManager.Instance.GridToWorld(position);
    }

    /// Roll attack. Returns final damage value.
    public int RollAttack()
    {
        int totalDamage = 0;

        for (int i = 0; i < State.BaseData.AttackDiceCount; i++)
        {
            totalDamage += Random.Range(1, State.BaseData.AttackDiceFaces + 1);
        }

        if (State.IsEnraged)
        {
            bool crit = Random.value < 0.6f;
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
        transform.position = GridManager.Instance.GridToWorld(newPosition);
    }

    /// Animated move to a single tile. Calls onComplete when done.
    public void AnimateMoveTo(Vector2Int newPosition, Action onComplete = null)
    {
        StartCoroutine(AnimateMoveRoutine(newPosition, onComplete));
    }

    private IEnumerator AnimateMoveRoutine(Vector2Int newPosition, Action onComplete)
    {
        IsMoving = true;
        Vector3 start = transform.position;
        Vector3 target = GridManager.Instance.GridToWorld(newPosition);
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

    /// Death animation: fade out and shrink over 0.5s, then deactivate.
    public void PlayDeathAnimation(Action onComplete = null)
    {
        StartCoroutine(DeathRoutine(onComplete));
    }

    private IEnumerator DeathRoutine(Action onComplete)
    {
        float duration = 0.5f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Color startColor = Visual != null ? Visual.color : Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Shrink
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

            // Fade
            if (Visual != null)
                Visual.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);

            yield return null;
        }

        gameObject.SetActive(false);

        // Reset for potential reuse
        transform.localScale = startScale;
        if (Visual != null) Visual.color = startColor;

        onComplete?.Invoke();
    }
}
