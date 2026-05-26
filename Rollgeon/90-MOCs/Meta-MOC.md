---
title: Meta-MOC
type: moc
domain: 90-MOCs
status: wip
tags: [moc, meta]
---

# 15-Meta — Map of Content

> Meta-progression and save layer. **Mostly TBD** in Sprint 03.

## Status

| Piece | Status | Notes |
|---|---|---|
| [[ISaveable]]      | ✅ done (stub)  | Foundation-level contract |
| [[SaveSystem]]     | 🟡 TBD          | §15 not implemented |
| [[UnlockSystem]]   | 🟡 TBD          | §14 not implemented |
| [[RunRecord]]      | 🟡 TBD          | §14 data shape, not live |

## Cross-domain edges

- `OnRunStart` / `OnRunEnd` from [[Run-MOC]] are the hooks that will
  drive save triggers once [[SaveSystem]] lands.
- [[RunComboCounterState]] already implements [[ISaveable]] — first
  system to exercise the stub.
