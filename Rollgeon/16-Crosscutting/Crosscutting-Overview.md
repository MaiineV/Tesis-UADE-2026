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
> pooling, analytics, settings, shop, tutorial, quest). Sprint 03
> implements only the subset required by the FP. Everything else lives
> here as stubs for future worktrees.

## Status matrix

| System | Symbol (spec) | Status | Notes |
|---|---|---|---|
| Screen manager | `IScreenManager` | ✅ done | See [[ScreenManager]]. |
| Phase service | `IPhaseService` | ✅ done | See [[PhaseService]]. |
| Player service | `IPlayerService` | ✅ done | See [[PlayerService]]. |
| Dungeon service | `IDungeonService` | ✅ done | See [[DungeonManager]]. |
| Exploration controller | — | ✅ done | See [[ExplorationController]]. |
| Audio service | `IAudioService` | 🟡 TBD | §17.A — no audio pipeline in Sprint 03. |
| Movement service | `IMovementService` | 🟡 TBD | §17.M — BFS movement on dungeon graph. |
| Camera service | `ICameraService` | 🟡 TBD | §17.C — cinemachine wiring. |
| Input service | `IInputService` | 🟡 TBD | §17.IN — abstraction over Unity input. |
| Scene service | `ISceneService` | 🟡 TBD | §17.SCE — async scene management. |
| Interaction service | `IInteractionService` | 🟡 TBD | §17.INT — interactables + dialog. |
| Cutscene service | `ICutsceneService` | 🟡 TBD | §17.CS — non-interactive beats. |
| Object pool service | `IObjectPool<T>`, `IPoolService` | 🟡 TBD | §24 — pools for VFX / damage. |
| Analytics service | `IAnalyticsService` | 🟡 TBD | §25 — telemetry. |
| Settings service | `ISettingsService` | 🟡 TBD | §23 — per-user settings + accessibility. |
| Shop manager | `IShopManagerService` | 🟡 TBD | §17.SHP — shop interactions. |
| Tutorial service | `ITutorialService` | 🟡 TBD | §22 — tutorial steps. |
| Quest service | `IQuestService` | 🟡 TBD | §21 — quests. |
| Feedback manager | `FeedbackManager` | 🟡 TBD | §10 — VFX / SFX pipeline. |
| RNG service | `IRngService` | 🟡 TBD | §17.RNG — deterministic RNG abstraction. |

## Why not one note per TBD

Each TBD carries essentially the same info: spec pointer + "not yet
implemented". Splitting them into 18 skeleton notes would crowd the
vault graph with low-signal nodes. The matrix above gives the same
information in a single scannable place. When an item graduates, promote
it to its own dedicated note and cross-link from here.

## External references

- TECHNICAL.md: §17 Crosscutting systems
