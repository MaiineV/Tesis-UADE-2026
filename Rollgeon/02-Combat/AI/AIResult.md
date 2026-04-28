---
title: AIResult
type: enum
domain: 02-Combat
status: done
tags: [combat, ai, enum]
---

# AIResult

> Result of evaluating an `AIDecisionNode` — the same shape as a
> behavior-tree status. TECHNICAL §7.5.

## Overview

`Running` is reserved for future multi-frame ticks (long ability
animations). The FP evaluates synchronously and only uses `Succeeded`
/ `Failed`.

## Shape

```csharp
public enum AIResult {
    Succeeded,
    Failed,
    Running,
}
```

## Dependencies
**Used by:** every `AICond_*` and `AINode_*`, `TreeDrivenEnemyAI`.

## Code
`Assets/Scripts/Rollgeon/Combat/AI/AIResult.cs`
