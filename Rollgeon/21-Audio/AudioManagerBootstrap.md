---
title: AudioManagerBootstrap
type: bootstrap
domain: 21-Audio
status: done
tags: [audio, bootstrap, so]
---

# AudioManagerBootstrap

> `IPreloadableService` que crea el GameObject persistente del
> [[AudioManager]] y lo registra como [[IAudioService]] global.

## Overview

Asset creado desde `Assets / Create / Rollgeon / Audio / Audio Manager
Bootstrap`, con la [[AudioSettingsSO]] asignada y agregado a
`ServiceBootstrapSO.ExtraServices`. Priority 50 — antes de
[[FeedbackManagerBootstrap]] (55) para que el dispatch de SFX del
feedback ya encuentre el servicio al registrarse.

## API / Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Audio/Audio Manager Bootstrap")]
public sealed class AudioManagerBootstrap : ScriptableObject, IPreloadableService {
    public int Priority => 50;
    public void Register();
}
```

## Dependencies

**Uses:** [[AudioManager]], [[AudioSettingsSO]], [[IAudioService]],
`Patterns.ServiceLocator`, `Rollgeon.Patterns.Bootstrap.IPreloadableService`.
**Used by:** `ServiceBootstrapSO.ExtraServices` (autoral).

## Code

`Assets/Scripts/Rollgeon/Audio/AudioManagerBootstrap.cs`

## External references

- TECHNICAL.md §17.A — Audio bootstrap.
