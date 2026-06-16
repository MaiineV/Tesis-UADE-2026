---
title: IAudioService
type: interface
domain: 21-Audio
status: done
tags: [audio, service]
---

# IAudioService

> Contrato global de audio. Único canal por el que el código de gameplay
> reproduce SFX y música; nadie debe llamar `AudioSource.PlayClipAtPoint`
> fuera de esta capa.

## Overview

Expone tres áreas: SFX (3D y 2D, con flag `isImportant` para que el pool
no descarte clips críticos), música (play / biome / stop / pause / resume
con crossfade lineal) y volumen lineal por [[AudioChannel]] convertido a
dB internamente. Volúmenes en `[0, 1]`. Implementación viva: [[AudioManager]].

## API / Shape

```csharp
public interface IAudioService {
    // SFX
    void PlaySfx(AudioClip clip, Vector3 worldPos,
                 float volume = 1f, float pitch = 1f, bool isImportant = false);
    void PlaySfx2D(AudioClip clip,
                   float volume = 1f, float pitch = 1f, bool isImportant = false);

    // Music
    void PlayMusic(AudioClip clip, float fadeSeconds = 1f);
    void PlayMusicForBiome(string biomeId, float fadeSeconds = 1f);
    void StopMusic(float fadeSeconds = 1f);
    void PauseMusic();
    void ResumeMusic();

    // Volume
    void SetVolume(AudioChannel channel, float value);
    float GetVolume(AudioChannel channel);
}
```

## Dependencies

**Uses:** [[AudioChannel]], [[AudioSettingsSO]] (vía implementación), [[BiomeMusicEntry]].
**Used by:** [[AudioManager]], [[FeedbackManager]] (dispatch SFX), [[GamePhase]] hooks de música, DungeonManager (`PlayMusicForBiome`).

## Code

`Assets/Scripts/Rollgeon/Audio/IAudioService.cs`

## External references

- TECHNICAL.md §17.A.1 — Audio service contract.
