using UnityEngine;

public class ShopItemEntity : MonoBehaviour
{
    public ShopItemData ItemData { get; private set; }
    public Vector2Int GridPosition { get; private set; }

    private static readonly Color ColorGold;
    private static readonly Color ColorGreen;
    private static readonly Color ColorCyan;
    private static readonly Color ColorPurple;

    static ShopItemEntity()
    {
        ColorUtility.TryParseHtmlString("#ffd54f", out Color gold);
        ColorGold = gold;
        ColorUtility.TryParseHtmlString("#66bb6a", out Color green);
        ColorGreen = green;
        ColorUtility.TryParseHtmlString("#4fc3f7", out Color cyan);
        ColorCyan = cyan;
        ColorUtility.TryParseHtmlString("#ab47bc", out Color purple);
        ColorPurple = purple;
    }

    public void Initialize(ShopItemData data, Vector2Int gridPos)
    {
        ItemData = data;
        GridPosition = gridPos;
        data.TilePosition = gridPos;

        transform.position = GridManager.Instance.GridToWorld(gridPos) + new Vector3(0, 0.3f, 0);
        transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

        // Set color based on item type
        Color itemColor;
        switch (data.ItemType)
        {
            case ShopItemType.DiceAdd:
                itemColor = ColorGold;
                break;
            case ShopItemType.PotionRefill:
                itemColor = ColorGreen;
                break;
            case ShopItemType.Buff:
                itemColor = ColorPurple;
                break;
            default: // StatBoostDex, StatBoostSpeed
                itemColor = ColorCyan;
                break;
        }

        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.material.color = itemColor;

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
