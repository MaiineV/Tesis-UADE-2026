---
title: Crosscutting-Overview
type: concept
domain: 16-Crosscutting
status: wip
tags: [crosscutting, overview, stub, tbd]
---

# Crosscutting systems — overview

> `TECHNICAL.md §17` specifies a broad set of transversal services
> (audio, movement, camera, input, scene, interaction, cutscenes,
> pooling, analytics, settings, shop, tutorial, quest). Several have
> graduated to their own dedicated section folders since this matrix
> was first drafted; the rest remain TBD here.

## Promoted (now have their own section + MOC)

| System | Section | Notes |
|---|---|---|
| Grid | `17-Grid/` | See [[Grid-MOC]]. |
| Movement service | `18-Movement/` | See [[Movement-MOC]]. |
| Economy | `19-Economy/` | See [[Economy-MOC]]. |
| Shop manager | `20-Shop/` | See [[Shop-MOC]]. |
| Audio service | `21-Audio/` | See [[Audio-MOC]] when published. |
| Feedback (VFX/SFX/anim) | `22-Feedback/` | See [[Feedback-MOC]]. |
| Camera service | `23-Camera/` | See [[Camera-MOC]]. |
| Items | `24-Items/` | See [[Items-MOC]]. |
| Exploration controller | `25-Exploration/` | See [[Exploration-MOC]]. |
| PreConditions | `26-PreConditions/` | See [[PreConditions-MOC]]. |

## Status matrix (still in 16-Crosscutting)

| System | Symbol (spec) | Status | Notes |
|---|---|---|---|
| Screen manager | `IScreenManager` | ✅ done | See [[ScreenManager]]. |
| Phase service | `IPhaseService` | ✅ done | See [[PhaseService]]. |
| Player service | `IPlayerService` | ✅ done | See [[PlayerService]]. |
| Dungeon service | `IDungeonService` | ✅ done | See [[DungeonManager]]. |
| Input service | `IInputService` | 🟡 TBD | §17.IN — abstraction over Unity input. |
| Scene service | `ISceneService` | 🟡 TBD | §17.SCE — async scene management. |
| Interaction service | `IInteractionService` | 🟡 TBD | §17.INT — interactables + dialog. |
| Cutscene service | `ICutsceneService` | 🟡 TBD | §17.CS — non-interactive beats. |
| Object pool service | `IObjectPool<T>`, `IPoolService` | 🟡 TBD | §24 — pools for VFX / damage. |
| Analytics service | `IAnalyticsService` | 🟡 TBD | §25 — telemetry. |
| Settings service | `ISettingsService` | 🟡 TBD | §23 — per-user settings + accessibility. |
| Tutorial service | `ITutorialService` | 🟡 TBD | §22 — tutorial steps. |
| Quest service | `IQuestService` | 🟡 TBD | §21 — quests. |
| Localization service | `ILocalizationService` | 🟡 TBD | §17 — i18n strings. |
| RNG service | `IRngService` | 🟡 TBD | §17.RNG — deterministic RNG abstraction. |

## Why not one note per TBD

Each TBD carries essentially the same info: spec pointer + "not yet
implemented". Splitting them into skeleton notes would crowd the vault
graph with low-signal nodes. The matrix above gives the same
information in a single scannable place. When an item graduates,
promote it to its own dedicated note (or its own section folder + MOC,
as Grid / Movement / Audio / Feedback / Camera did) and cross-link
from here.

## External references

- TECHNICAL.md: §17 Crosscutting systems
