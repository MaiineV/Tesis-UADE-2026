---
title: AudioManager
type: service
domain: 21-Audio
status: done
tags: [audio, service, monobehaviour, saveable]
---

# AudioManager

> `MonoBehaviour` singleton que implementa [[IAudioService]] sobre un
> pool de `AudioSource` para SFX, dos sources con crossfade PrimeTween
> para música, y volúmenes lineales convertidos a dB sobre el `AudioMixer`
> autoral.

## Overview

Vive en `DontDestroyOnLoad`, instanciado por [[AudioManagerBootstrap]].
Implementa `ISaveable` (key `audio.volumes`) — los sliders del jugador
persisten entre sesiones. Pool con política FIFO + flag `isImportant`:
si el pool está saturado se interrumpe el source más viejo no-importante;
si todos son importantes, se corta el más viejo igual (último recurso).

## API / Shape

```csharp
public sealed class AudioManager : MonoBehaviour, IAudioService, ISaveable {
    public void Configure(AudioSettingsSO settings);

    // IAudioService — ver [[IAudioService]]
    public void PlaySfx(AudioClip clip, Vector3 worldPos, ...);
    public void PlaySfx2D(...);
    public void PlayMusic(AudioClip clip, float fadeSeconds = 1f);
    public void PlayMusicForBiome(string biomeId, float fadeSeconds = 1f);
    public void StopMusic(float fadeSeconds = 1f);
    public void PauseMusic();
    public void ResumeMusic();
    public void SetVolume(AudioChannel channel, float value);
    public float GetVolume(AudioChannel channel);

    // ISaveable
    public string SaveKey => "audio.volumes";
    public object CaptureState();
    public void RestoreState(object state);
}
```

## Notas de implementación

- Crossfade música: dos `AudioSource` (`_musicA`/`_musicB`) que swappean
  su rol active/idle; cancela tweens viejos antes de arrancar uno nuevo.
- `PlayMusicForBiome` busca en [[AudioSettingsSO]]`.BiomeMusic` por id;
  warning si no matchea.
- Conversión lineal→dB en [[AudioSettingsSO]]`.LinearToDecibels`; clamp en
  `-80 dB` cuando el valor es `0`.

## Dependencies

**Uses:** [[IAudioService]], [[AudioSettingsSO]], [[AudioChannel]],
[[BiomeMusicEntry]], `PrimeTween`, `Patterns.Save.ISaveable`.
**Used by:** [[AudioManagerBootstrap]] (dueño), [[FeedbackManager]]
(routing SFX vía `IAudioService`), volume sliders de UI.

## Code

`Assets/Scripts/Rollgeon/Audio/AudioManager.cs`

## External references

- TECHNICAL.md §17.A.2 — Audio manager.
- TECHNICAL.md §17.A.5 — Persistencia de volúmenes.
