using UnityEngine;

public class ShopItemEntity : MonoBehaviour
{
    public ShopItemData ItemData { get; private set; }
    public Vector2Int GridPosition { get; private set; }

    public void Initialize(ShopItemData data, Vector2Int gridPos)
    {
        ItemData = data;
        GridPosition = gridPos;
        data.TilePosition = gridPos;

        transform.position = GridManager.Instance.GridToWorld(gridPos) + new Vector3(0, 0.3f, 0);
        transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

        Material mat;
        switch (data.ItemType)
        {
            case ShopItemType.DiceAdd:      mat = MaterialCache.ShopGold;   break;
            case ShopItemType.PotionRefill:  mat = MaterialCache.ShopGreen;  break;
            case ShopItemType.Buff:          mat = MaterialCache.ShopPurple; break;
            default:                         mat = MaterialCache.ShopCyan;   break;
        }

        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.sharedMaterial = mat;

        // Remove collider to avoid physics interference
        var col = GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Add floating animation
        var floater = gameObject.AddComponent<FloatUpDown>();
        if (floater != null)
        {
            floater.range = 0.1f;
            floater.speed = 1.5f;
        }
    }

    public void Remove()
    {
        gameObject.SetActive(false);
    }
}
