using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Pool Settings")]
    [SerializeField] private int poolSize = 8;

    private AudioSource[] sfxPool;
    private int poolIndex;
    private AudioSource musicSource;

    public float MasterVolume { get; private set; } = 1f;

    void Awake()
    {
        Instance = this;
        MasterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        AudioListener.volume = MasterVolume;
        InitPool();
        InitMusicSource();
    }

    private void InitPool()
    {
        sfxPool = new AudioSource[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            sfxPool[i] = gameObject.AddComponent<AudioSource>();
            sfxPool[i].playOnAwake = false;
            sfxPool[i].spatialBlend = 0f; // 2D
        }
    }

    private void InitMusicSource()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
    }

    // ── Static API ──

    /// <summary>Play a 2D sound effect (no position).</summary>
    public static void Play(AudioClip clip, float volumeScale = 1f)
    {
        if (Instance == null || clip == null) return;
        Instance.PlaySFX(clip, volumeScale);
    }

    /// <summary>Play a 2D sound with random pitch variation (0.85–1.15).</summary>
    public static void PlayWithPitch(AudioClip clip, float volumeScale = 1f)
    {
        if (Instance == null || clip == null) return;
        Instance.PlaySFX(clip, volumeScale, Random.Range(0.85f, 1.15f));
    }

    /// <summary>Play a 2D sound with low pitch (0.65–0.75) for impactful hits.</summary>
    public static void PlayWithLowPitch(AudioClip clip, float volumeScale = 1f)
    {
        if (Instance == null || clip == null) return;
        Instance.PlaySFX(clip, volumeScale, Random.Range(0.65f, 0.75f));
    }

    /// <summary>Play a looping 2D sound. Returns the AudioSource so caller can Stop() it.</summary>
    public static AudioSource PlayLoop(AudioClip clip, float volumeScale = 1f)
    {
        if (Instance == null || clip == null) return null;
        return Instance.PlaySFXLoop(clip, volumeScale, Random.Range(0.85f, 1.15f));
    }

    /// <summary>Play a 3D sound effect at a world position.</summary>
    public static void PlayAtPoint(AudioClip clip, Vector3 position, float volumeScale = 1f)
    {
        if (Instance == null || clip == null) return;
        Instance.PlaySFX3D(clip, position, volumeScale);
    }

    /// <summary>Play background music (loops). Replaces current music.</summary>
    public static void PlayMusic(AudioClip clip, float volumeScale = 1f)
    {
        if (Instance == null || clip == null) return;
        Instance.musicSource.clip = clip;
        Instance.musicSource.volume = volumeScale;
        Instance.musicSource.Play();
    }

    /// <summary>Stop background music.</summary>
    public static void StopMusic()
    {
        if (Instance == null) return;
        Instance.musicSource.Stop();
    }

    /// <summary>Set master volume (0-1). Persists in PlayerPrefs.</summary>
    public static void SetVolume(float volume)
    {
        if (Instance == null) return;
        volume = Mathf.Clamp01(volume);
        Instance.MasterVolume = volume;
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("MasterVolume", volume);
    }

    // ── Internal ──

    private void PlaySFX(AudioClip clip, float volumeScale, float pitch = 1f)
    {
        var source = sfxPool[poolIndex];
        poolIndex = (poolIndex + 1) % sfxPool.Length;
        source.clip = clip;
        source.volume = volumeScale;
        source.pitch = pitch;
        source.loop = false;
        source.spatialBlend = 0f;
        source.Play();
    }

    private AudioSource PlaySFXLoop(AudioClip clip, float volumeScale, float pitch = 1f)
    {
        var source = sfxPool[poolIndex];
        poolIndex = (poolIndex + 1) % sfxPool.Length;
        source.clip = clip;
        source.volume = volumeScale;
        source.pitch = pitch;
        source.loop = true;
        source.spatialBlend = 0f;
        source.Play();
        return source;
    }

    private void PlaySFX3D(AudioClip clip, Vector3 position, float volumeScale)
    {
        var go = new GameObject("SFX3D_Temp");
        go.transform.position = position;
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volumeScale;
        source.spatialBlend = 1f;
        source.Play();
        Destroy(go, clip.length + 0.1f);
    }
}
