using System;
using UnityEngine;

public class EnergyManager : MonoBehaviour
{
    public static EnergyManager Instance;

    // Events
    public static event Action<float> OnPlayerEnergyChanged;    // normalized 0-1
    public static event Action OnPlayerEnergyFull;
    public static event Action<float> OnEnemyEnergyChanged;

    private PlayerState playerState;

    void Awake() { Instance = this; }

    public void Initialize(PlayerState player)
    {
        playerState = player;
        playerState.CurrentEnergy = 0;
        playerState.MaxEnergy = 100;
    }

    public void AddPlayerEnergy(float amount)
    {
        playerState.CurrentEnergy = Mathf.Min(
            playerState.MaxEnergy,
            playerState.CurrentEnergy + amount
        );

        OnPlayerEnergyChanged?.Invoke(playerState.CurrentEnergy / playerState.MaxEnergy);

        if (playerState.CurrentEnergy >= playerState.MaxEnergy)
        {
            playerState.CrapsModeAvailable = true;
            OnPlayerEnergyFull?.Invoke();
        }
    }

    public void ResetPlayerEnergy()
    {
        playerState.CurrentEnergy = 0;
        playerState.CrapsModeAvailable = false;
        OnPlayerEnergyChanged?.Invoke(0);
    }

    /// Call after combat actions to grant appropriate energy
    public void ProcessCombatAction(CombatActionType action, CombinationType combo = CombinationType.HighDie)
    {
        float gain = 0;
        switch (action)
        {
            case CombatActionType.DealtDamage:
                gain = 10;
                if (combo == CombinationType.ThreeOfAKind) gain = 15;
                else if (combo == CombinationType.FullHouse) gain = 20;
                else if (combo == CombinationType.FourOfAKind) gain = 25;
                else if (combo == CombinationType.Generala ||
                         combo == CombinationType.DoubleGenerala) gain = 50;
                break;
            case CombatActionType.TookDamage:
                gain = 5;
                break;
            case CombatActionType.KilledEnemy:
                gain = 10;
                break;
        }
        AddPlayerEnergy(gain);
    }
}
