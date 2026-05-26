---
title: AudioChannel
type: enum
domain: 21-Audio
status: done
tags: [audio, enum]
---

# AudioChannel

> Canales de mezcla expuestos por [[IAudioService]]. Cada uno mapea a
> un parámetro expuesto del `AudioMixer` autoral
> (ver [[AudioSettingsSO]]).

## Shape

```csharp
public enum AudioChannel {
    Master = 0,
    Music  = 1,
    Sfx    = 2,
    Ui     = 3,
}
```

## Dependencies

**Used by:** [[IAudioService]], [[AudioManager]], [[AudioSettingsSO]]
(`GetParamFor` / `GetDefaultFor`).

## Code

`Assets/Scripts/Rollgeon/Audio/AudioChannel.cs`

## External references

- TECHNICAL.md §17.A.1 — Mixing channels.
