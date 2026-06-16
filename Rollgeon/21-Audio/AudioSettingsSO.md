---
title: AudioSettingsSO
type: so
domain: 21-Audio
status: done
tags: [audio, so, config]
---

# AudioSettingsSO

> Configuración autoral del sistema de audio: mixer, grupos por canal,
> tamaño del pool de SFX, defaults de volumen y tracks por biome.

## Overview

Registrado como settings catalog en `ServiceBootstrapSO.SettingsAssets`;
lo consume el [[AudioManager]] al despertar para construir el pool
(`SfxPoolSize`), enrutar al `AudioMixer` correcto y aplicar volúmenes
default. Expone `LinearToDecibels` (clamp `-80 dB` en silencio) y
helpers `GetParamFor` / `GetDefaultFor` por [[AudioChannel]].

## API / Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Audio/Audio Settings")]
public sealed class AudioSettingsSO : ScriptableObject {
    // Mixer
    public AudioMixer Mixer;
    public string MasterParam = "MasterVol";
    public string MusicParam  = "MusicVol";
    public string SfxParam    = "SfxVol";
    public string UiParam     = "UiVol";
    public AudioMixerGroup SfxGroup;
    public AudioMixerGroup MusicGroup;
    public AudioMixerGroup UiGroup;

    // Pool
    public int SfxPoolSize = 16;

    // Default volumes (linear [0,1])
    public float DefaultMaster, DefaultMusic, DefaultSfx, DefaultUi;

    // Biome music
    public List<BiomeMusicEntry> BiomeMusic;

    public static float LinearToDecibels(float linear);
    public string GetParamFor(AudioChannel channel);
    public float  GetDefaultFor(AudioChannel channel);
}
```

## Dependencies

**Uses:** [[AudioChannel]], [[BiomeMusicEntry]], `UnityEngine.Audio`.
**Used by:** [[AudioManager]], [[AudioManagerBootstrap]].

## Code

`Assets/Scripts/Rollgeon/Audio/AudioSettingsSO.cs`

## External references

- TECHNICAL.md §17.A.3 — Audio settings.
