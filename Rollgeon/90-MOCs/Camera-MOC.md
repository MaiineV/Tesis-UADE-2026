---
title: Camera-MOC
type: moc
domain: 90-MOCs
status: done
tags: [moc, camera]
---

# 23-Camera — Map of Content

> Scripted isometric camera: 45° discrete yaw, planar pan, ortho/perspective
> zoom, recenter on follow target, and a `Shake` hook for feedback.
> Run-scoped `ICameraService` registered against the live `Main Camera`
> rig; input flows through a separate router.

## Relationships

```
 CameraConfigSO ── default zoom, FloorViewZoomThreshold, walls, ...
       │
 ServiceLocator (Run scope)
       │
       ▼
 ICameraService
       ├─ state: CurrentFacing (CameraFacing) / CurrentZoom / FollowTarget /
       │           IsPanning / IsFloorView
       ├─ commands: RotateBy45 / PanBy / ZoomBy / Recenter / SetFollowTarget / Shake
       └─ events: FacingChanged(CameraFacing) / FloorViewToggled(bool)

 CameraService (impl, MonoBehaviour on Main Camera)
       ├─ WallOccluder(WallDirection) ◄── FacingChanged listener
       └─ CameraInputRouter (Camera action map)
              ├─ CameraInputConfig (from CameraServiceBootstrap)
              └─ rotate-drag / pan-drag / zoom / recenter
```

## Pages

### Core service
- [[ICameraService]] — public interface (run-scoped)
- [[CameraService]] — `MonoBehaviour` impl on `Main Camera`
- [[CameraServiceBootstrap]] — registers service + publishes input config

### Data / config
- [[CameraConfigSO]] · [[CameraFacing]]
- [[CameraInputConfig]]
- [[WallDirection]] · [[WallOccluder]]

### Input
- [[CameraInputRouter]]

## Cross-domain edges

- **Incoming** (consumers):
  - 22-Feedback: [[FeedbackManager]] calls `Shake` on hit feedback.
  - 25-Exploration: [[ExplorationController]] sets the follow target on
    room transitions.
  - 02-Combat: target-focus / framing on `EnemyTurnState` and `VictoryScreen`.
  - 14-UI / Input: routes `Camera` action map gestures.
- **Outgoing** (dependencies):
  - 00-Foundations: [[ServiceLocator]], [[IPreloadableService]].
  - 07-Dungeon: room bounds drive recenter / floor-view threshold.
  - `UnityEngine.InputSystem` for the `InputActionAsset`.
