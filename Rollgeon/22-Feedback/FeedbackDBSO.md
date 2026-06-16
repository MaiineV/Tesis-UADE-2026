---
title: FeedbackDBSO
type: so
domain: 22-Feedback
status: done
tags: [feedback, so, catalog]
---

# FeedbackDBSO

> Base de datos autoral de [[FeedbackEntry]]. Lookup por `FeedbackId`
> con cache rebuiltable; lo consume el [[FeedbackManager]] al recibir
> un request.

## Overview

`OnEnable` / `OnValidate` rebuild de cache. Helpers Editor (Odin):
detectar duplicados, remover empty entries, sort por id, clear all.
`GetFilteredFeedbackIds(type)` para dropdowns selectivos por
[[FeedbackType]] (ej. SFX-only en un combo de SFX).

## API / Shape

```csharp
[CreateAssetMenu(menuName = "Rollgeon/Feedback/Feedback DB")]
public class FeedbackDBSO : ScriptableObject {
    public IReadOnlyList<FeedbackEntry> Entries { get; }

    public void RebuildCache();
    public bool TryGetFeedback(string feedbackId, out FeedbackEntry entry);
    public FeedbackEntry GetFeedbackOrDefault(string feedbackId);
    public bool HasFeedback(string feedbackId);
    public IEnumerable<string> GetAllFeedbackIds();
    public IEnumerable<string> GetFilteredFeedbackIds(FeedbackType type);
}
```

## Dependencies

**Uses:** [[FeedbackEntry]], [[FeedbackType]], Odin
`[ListDrawerSettings]`.
**Used by:** [[FeedbackManager]], [[FeedbackManagerBootstrap]],
[[EffPlayFeedback]] (04-Effects — id dropdown).

## Code

`Assets/Scripts/Rollgeon/Feedback/FeedbackDBSO.cs`

## External references

- TECHNICAL.md §10.2 — Feedback DB.
