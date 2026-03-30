---
name: GDD Audit 2026-03-29
description: Full GDD audit findings — critical contradiction between GDD (threshold combat) and spec docs (Generala combat), plus 5 other major issues being resolved point by point
type: project
---

Full audit saved to Assets/8.Documents/GDD_AUDIT.md. The #1 issue is GDD describes threshold-based 1-die combat while spec docs describe a Generala/Yahtzee multi-dice combo system — these are fundamentally different games. Resolving point by point with Gabriel.

**Why:** GDD evolved after specs were written. Team needs alignment before more code is written.
**How to apply:** Before implementing ANY combat or dice system, confirm which design doc is authoritative. Reference the audit doc for all open decisions.
