using UnityEngine;

public class SoundLibrary : MonoBehaviour
{
    public static SoundLibrary Instance;

    public AudioClip DiceRoll;
    public AudioClip Footstep;
    public AudioClip AttackToEnemy;
    public AudioClip AttackToPlayer;

    void Awake()
    {
        Instance = this;
        DiceRoll = Resources.Load<AudioClip>("Sounds/DiceRoll");
        Footstep = Resources.Load<AudioClip>("Sounds/Footstep");
        AttackToEnemy = Resources.Load<AudioClip>("Sounds/AttackToEnemy");
        AttackToPlayer = Resources.Load<AudioClip>("Sounds/AttackToPlayer");
    }
}
