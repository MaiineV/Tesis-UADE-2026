---
title: BiomeMusicEntry
type: struct
domain: 21-Audio
status: done
tags: [audio, struct]
---

# BiomeMusicEntry

> Pareja `<biomeId, AudioClip>` para el track de música asignado a un
> biome/piso. Lista en [[AudioSettingsSO]]`.BiomeMusic`.

## Shape

```csharp
[Serializable]
public struct BiomeMusicEntry {
    public string BiomeId;
    public AudioClip Music;
}
```

## Uso

[[IAudioService]]`.PlayMusicForBiome(biomeId)` busca un entry con
`BiomeId == biomeId` y delega a `PlayMusic(entry.Music, fade)`. Si no
matchea ninguno, [[AudioManager]] loguea warning y no cambia la música.

## Dependencies

**Used by:** [[AudioSettingsSO]], [[IAudioService]], [[AudioManager]],
DungeonManager (al entrar a un piso).

## Code

`Assets/Scripts/Rollgeon/Audio/AudioSettingsSO.cs`

## External references

- TECHNICAL.md §17.A.3 — Biome music.
