---
title: IPositionReader
type: interface
domain: 22-Feedback
status: done
tags: [feedback, position]
---

# IPositionReader

> Delegación pluggable de resolución de posición para
> [[SpawnPosition]]`.FromReader`. Típicamente un `ScriptableObject`
> con lógica custom (ej. cámara + offset, HUD anchor del player X).

## Overview

El [[FeedbackEntry]] guarda el `ScriptableObject` reader en
`PositionReaderSO` (no tipado a la interfaz porque Unity no serializa
referencias a interfaces). El [[FeedbackPositionResolver]] lo castea
a `IPositionReader` antes de invocar `Read`.

## API / Shape

```csharp
public interface IPositionReader {
    Vector3 Read(PositionReadInfo info);
}
```

## Dependencies

**Uses:** [[PositionReadInfo]].
**Used by:** [[FeedbackEntry]] (`PositionReaderSO`),
[[FeedbackPositionResolver]] (`ResolveFromReader`).

## Code

`Assets/Scripts/Rollgeon/Feedback/IPositionReader.cs`

## External references

- TECHNICAL.md §10.6 — Reader-based positioning.
