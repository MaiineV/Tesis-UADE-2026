using UnityEngine;

public class FloatUpDown : MonoBehaviour
{
    [Range(0.1f, 5f)] public float range = 1f;
    [Range(0.1f, 5f)] public float speed = 1f;

    Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        float offset = Mathf.Sin(Time.time * speed) * range;
        transform.position = startPos + Vector3.up * offset;
    }
}
