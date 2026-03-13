using UnityEngine;

public class PlayerEntity : MonoBehaviour
{
    public PlayerState State { get; private set; }
    public SpriteRenderer Visual;

    public void Initialize(CharacterData data, Vector2Int startPosition)
    {
        State = PlayerState.Create(data);
        State.GridPosition = startPosition;

        // Placeholder visual: colored square
        Visual.color = data.CharacterColor;
        transform.position = GridManager.Instance.GridToWorld(startPosition); // TODO: Depends on US-03
    }

    public void MoveTo(Vector2Int newPosition)
    {
        State.GridPosition = newPosition;
        // For prototype: instant teleport. Later: animate movement.
        transform.position = GridManager.Instance.GridToWorld(newPosition); // TODO: Depends on US-03
    }
}
