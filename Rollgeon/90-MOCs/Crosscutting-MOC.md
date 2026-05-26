---
title: Crosscutting-MOC
type: moc
domain: 90-MOCs
status: wip
tags: [moc, crosscutting]
---

# 16-Crosscutting — Map of Content

> Transversal systems (§17). Several previously-listed concerns have
> been promoted to their own dedicated sections (17-Grid, 18-Movement,
> 19-Economy, 20-Shop, 21-Audio, 22-Feedback, 23-Camera, 24-Items,
> 25-Exploration, 26-PreConditions). What remains here are the still-TBD
> services (Pooling, Analytics, Localization, Tutorial, Quest, Settings,
> Input, Scene, Interaction, Cutscene, RNG).

## Notes

- [[Crosscutting-Overview]] — single matrix enumerating all
  crosscutting systems with their implementation status and which ones
  have been promoted.

## Shipped crosscutting services (referenced elsewhere)

- [[ScreenManager]] (lives under [[UI-MOC]]).
- [[PhaseService]] (lives under [[Phase-MOC]]).
- [[PlayerService]] (lives under [[Player-MOC]]).
- [[DungeonManager]] (lives under [[Dungeon-MOC]]).

## Promoted to their own sections

- [[Grid-MOC]] · [[Movement-MOC]] · [[Economy-MOC]] · [[Shop-MOC]] ·
  [[Feedback-MOC]] · [[Camera-MOC]] · [[Items-MOC]] ·
  [[Exploration-MOC]] · [[PreConditions-MOC]] (Audio MOC pending).

When a remaining TBD item graduates, promote it the same way and link
it from [[Crosscutting-Overview]].
