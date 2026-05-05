---
title: Audio-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, audio]
---

# 21-Audio — Map of Content

> Single canonical channel for SFX (3D / 2D, with `isImportant` flag),
> music (with crossfade + biome lookup), and per-channel linear volume.
> Nobody outside this domain should call `AudioSource.PlayClipAtPoint`.

## Relationships

```
 AudioSettingsSO ── default volumes per AudioChannel + BiomeMusicEntry
       │
 ServiceLocator (Global scope)
       │
       ▼
 IAudioService ── PlaySfx / PlaySfx2D
       │       ── PlayMusic / PlayMusicForBiome / Stop / Pause / Resume
       │       ── SetVolume / GetVolume(AudioChannel)
       │
 AudioManager (impl, MonoBehaviour)
       ├─ SFX pool (importance-aware eviction)
       └─ music source(s) with linear-volume crossfade

 FeedbackManager.Dispatch(SFX) ─────► IAudioService.PlaySfx
 GamePhase / DungeonManager  ───────► IAudioService.PlayMusicForBiome
```

## Pages

### Core service
- [[IAudioService]] — public interface (global-scoped)
- [[AudioManager]] — default `MonoBehaviour` impl
- [[AudioManagerBootstrap]] — registers the service

### Data / config
- [[AudioSettingsSO]] · [[AudioChannel]] · [[BiomeMusicEntry]]

## Cross-domain edges

- **Incoming** (consumers):
  - 22-Feedback: [[FeedbackManager]] routes every `FeedbackType.SFX`
    request through [[IAudioService]].
  - 07-Dungeon: [[DungeonManager]] calls `PlayMusicForBiome` on floor
    / biome transitions.
  - 06-Run / GamePhase: phase hooks switch tracks (combat ↔ exploration).
- **Outgoing** (dependencies):
  - 00-Foundations: [[ServiceLocator]], [[IPreloadableService]].
