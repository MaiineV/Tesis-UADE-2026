using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MinimapUI : MonoBehaviour
{
    public static MinimapUI Instance;

    private Transform cellContainer;
    private RectTransform containerRT;
    private Dictionary<Vector2Int, GameObject> cells = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, Vector2> cellLocalPositions = new Dictionary<Vector2Int, Vector2>();
    private Vector2Int currentRoomPos;
    private List<RoomData> rooms;

    // Cell background colors
    private static readonly Color UndiscoveredColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);   // light gray
    private static readonly Color DiscoveredColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);      // same as undiscovered until cleared
    private static readonly Color ClearedColor = new Color(0.45f, 0.55f, 0.45f, 0.7f);      // grayish-green
    private static readonly Color CurrentColor = new Color(1f, 0.835f, 0.31f, 1f);           // yellow/gold

    // Label text colors for special rooms
    private static readonly Color BossLabelColor = new Color(0.937f, 0.325f, 0.314f, 1f);   // red
    private static readonly Color ShopLabelColor = new Color(0.447f, 0.624f, 0.812f, 1f);   // blue
    private static readonly Color PotionLabelColor = new Color(0.26f, 0.765f, 0.416f, 1f);  // green

    void Awake() { Instance = this; }

    public void Initialize(Transform container)
    {
        cellContainer = container;
        containerRT = container.GetComponent<RectTransform>();
        containerRT.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    public void BuildMinimap(List<RoomData> roomList)
    {
        rooms = roomList;
        ClearCells();

        float cellSize = 20f;
        float spacing = 3f;

        foreach (var room in rooms)
        {
            var cellGO = new GameObject($"Cell_{room.FloorPosition}");
            cellGO.transform.SetParent(cellContainer, false);

            var rt = cellGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(cellSize, cellSize);
            float x = room.FloorPosition.x * (cellSize + spacing);
            float y = room.FloorPosition.y * (cellSize + spacing);
            rt.anchoredPosition = new Vector2(x, y);

            cellLocalPositions[room.FloorPosition] = new Vector2(x, y);

            var img = cellGO.AddComponent<Image>();
            img.color = UndiscoveredColor;

            // Label for special rooms — counter-rotate so text stays upright
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(cellGO.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            labelRT.localRotation = Quaternion.Euler(0f, 0f, -45f);
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

        if (containerRT != null && cellLocalPositions.TryGetValue(pos, out var cellPos))
        {
            // Rotate offset by container's 45° rotation so centering works in parent space
            float angle = 45f * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            float rx = cellPos.x * cos - cellPos.y * sin;
            float ry = cellPos.x * sin + cellPos.y * cos;
            containerRT.anchoredPosition = new Vector2(-rx, -ry);
        }

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
                img.color = room.Cleared ? ClearedColor : DiscoveredColor;
                SetLabel(label, room);
            }
            else
            {
                img.color = UndiscoveredColor;
                if (label != null)
                {
                    label.text = "";
                    label.color = Color.white;
                }
            }
        }
    }

    private void SetLabel(TMP_Text label, RoomData room)
    {
        if (label == null) return;
        switch (room.Type)
        {
            case RoomType.Boss:
                label.text = "B";
                label.color = BossLabelColor;
                break;
            case RoomType.Shop:
                label.text = "T";
                label.color = ShopLabelColor;
                break;
            case RoomType.Potion:
                label.text = "P";
                label.color = PotionLabelColor;
                break;
            default:
                label.text = (room.ForcedDoors != null && room.ForcedDoors.Count > 0) ? "F" : "";
                label.color = Color.white;
                break;
        }
    }

    private void ClearCells()
    {
        foreach (var kvp in cells)
            if (kvp.Value != null) Destroy(kvp.Value);
        cells.Clear();
        cellLocalPositions.Clear();
    }
}
