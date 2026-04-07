using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ExplorationActionsUI : MonoBehaviour
{
    public static ExplorationActionsUI Instance;

    private Button moveButton;
    private Button bowButton;
    private Button potionButton;
    private Button fleeButton;
    private Button forceDoorButton;
    private TMP_Text potionCountText;

    public ExplorationAction CurrentAction { get; private set; } = ExplorationAction.Move;

    public event Action OnMoveSelected;
    public event Action OnBowSelected;
    public event Action OnPotionSelected;
    public event Action OnFleeSelected;
    public event Action OnForceDoorSelected;

    void Awake() { Instance = this; }

    public void Initialize(Button move, Button bow, Button potion, Button flee, Button forceDoor, TMP_Text potionCount)
    {
        moveButton = move;
        bowButton = bow;
        potionButton = potion;
        fleeButton = flee;
        forceDoorButton = forceDoor;
        potionCountText = potionCount;

        moveButton.onClick.AddListener(() => { CurrentAction = ExplorationAction.Move; OnMoveSelected?.Invoke(); });
        bowButton.onClick.AddListener(() => { CurrentAction = ExplorationAction.Bow; OnBowSelected?.Invoke(); });
        potionButton.onClick.AddListener(() => { CurrentAction = ExplorationAction.Potion; OnPotionSelected?.Invoke(); });
        fleeButton.onClick.AddListener(() => OnFleeSelected?.Invoke());
        forceDoorButton.onClick.AddListener(() => OnForceDoorSelected?.Invoke());
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetExplorationMode(bool hasPotion, int potionCount, bool onDoorTile)
    {
        moveButton.gameObject.SetActive(true);
        bowButton.gameObject.SetActive(false);     // 3AP: bow disabled for prototype
        potionButton.gameObject.SetActive(true);
        potionButton.interactable = hasPotion && potionCount > 0;
        fleeButton.gameObject.SetActive(false);    // 3AP: no flee
        forceDoorButton.gameObject.SetActive(onDoorTile);
        forceDoorButton.interactable = onDoorTile;
        if (potionCountText != null)
            potionCountText.text = (hasPotion && potionCount > 0) ? $"x{potionCount}" : "USED";
    }

    public void SetCombatMode(bool onDoorTile, int playerHP = 999, bool isBossRoom = false)
    {
        // 3AP: in combat, exploration UI is hidden. No flee, no bow.
        moveButton.gameObject.SetActive(false);
        bowButton.gameObject.SetActive(false);
        potionButton.gameObject.SetActive(false);
        fleeButton.gameObject.SetActive(false); // 3AP: no flee
        forceDoorButton.gameObject.SetActive(false);
    }

    public void HideAll()
    {
        gameObject.SetActive(false);
    }
}
