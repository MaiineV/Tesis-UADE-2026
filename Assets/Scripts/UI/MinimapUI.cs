using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MinimapUI : MonoBehaviour
{
    public static MinimapUI Instance;

    private Transform cellContainer;
    private Dictionary<Vector2Int, GameObject> cells = new Dictionary<Vector2Int, GameObject>();
    private Vector2Int currentRoomPos;
    private List<RoomData> rooms;

    private static readonly Color UndiscoveredColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
    private static readonly Color DiscoveredColor = new Color(0.086f, 0.129f, 0.243f, 0.8f);
    private static readonly Color CurrentColor = new Color(1f, 0.835f, 0.31f, 1f);
    private static readonly Color BossColor = new Color(0.937f, 0.325f, 0.314f, 0.8f);
    private static readonly Color ShopColor = new Color(0.26f, 0.765f, 0.416f, 0.8f);
    private static readonly Color PotionColor = new Color(0.447f, 0.624f, 0.812f, 0.8f);
    private static readonly Color ForcedDoorColor = new Color(1f, 0.65f, 0.15f, 0.9f);

    void Awake() { Instance = this; }

    public void Initialize(Transform container)
    {
        cellContainer = container;
    }

    public void BuildMinimap(List<RoomData> roomList)
    {
        rooms = roomList;
        ClearCells();

        // Find bounds
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;
        foreach (var room in rooms)
        {
            minX = Mathf.Min(minX, room.FloorPosition.x);
            maxX = Mathf.Max(maxX, room.FloorPosition.x);
            minY = Mathf.Min(minY, room.FloorPosition.y);
            maxY = Mathf.Max(maxY, room.FloorPosition.y);
        }

        float cellSize = 20f;
        float spacing = 3f;

        foreach (var room in rooms)
        {
            var cellGO = new GameObject($"Cell_{room.FloorPosition}");
            cellGO.transform.SetParent(cellContainer, false);

            var rt = cellGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(cellSize, cellSize);
            float x = (room.FloorPosition.x - minX) * (cellSize + spacing);
            float y = (room.FloorPosition.y - minY) * (cellSize + spacing);
            rt.anchoredPosition = new Vector2(x, y);

            var img = cellGO.AddComponent<Image>();
            img.color = UndiscoveredColor;

            // Label for special rooms
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(cellGO.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            var labelText = labelGO.AddComponent<TextMeshProUGUI>();
            labelText.text = "";
            labelText.fontSize = 10;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white;

            cells[room.FloorPosition] = cellGO;
        }

        RefreshAll();
    }

    public void UpdateCurrentRoom(Vector2Int pos)
    {
        currentRoomPos = pos;
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (rooms == null) return;

        foreach (var room in rooms)
        {
            if (!cells.TryGetValue(room.FloorPosition, out var cellGO)) continue;

            var img = cellGO.GetComponent<Image>();
            var label = cellGO.GetComponentInChildren<TextMeshProUGUI>();

            if (room.FloorPosition == currentRoomPos)
            {
                img.color = CurrentColor;
                SetLabel(label, room);
            }
            else if (room.Discovered)
            {
                img.color = GetRoomColorWithForcedDoors(room);
                SetLabel(label, room);
            }
            else
            {
                img.color = UndiscoveredColor;
                if (label != null) label.text = "";
            }
        }
    }

    private Color GetRoomColor(RoomData room)
    {
        switch (room.Type)
        {
            case RoomType.Boss: return BossColor;
            case RoomType.Shop: return ShopColor;
            case RoomType.Potion: return PotionColor;
            default: return room.Cleared ? new Color(0.3f, 0.3f, 0.3f, 0.6f) : DiscoveredColor;
        }
    }

    private void SetLabel(TMP_Text label, RoomData room)
    {
        if (label == null) return;
        switch (room.Type)
        {
            case RoomType.Boss: label.text = "B"; break;
            case RoomType.Shop: label.text = "T"; break;
            case RoomType.Potion: label.text = "P"; break;
            default:
                label.text = (room.ForcedDoors != null && room.ForcedDoors.Count > 0) ? "F" : "";
                break;
        }
    }

    private Color GetRoomColorWithForcedDoors(RoomData room)
    {
        if (room.ForcedDoors != null && room.ForcedDoors.Count > 0)
            return ForcedDoorColor;
        return GetRoomColor(room);
    }

    private void ClearCells()
    {
        foreach (var kvp in cells)
            if (kvp.Value != null) Destroy(kvp.Value);
        cells.Clear();
    }
}
