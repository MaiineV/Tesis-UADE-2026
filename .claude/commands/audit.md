# Document Audit Skill

You are an expert document auditor for game development. Perform a comprehensive audit of the specified document(s) against the project's codebase, design docs, and spec docs.

## Input
The user will specify what to audit. If no specific target, audit the main GDD at `Assets/8.Documents/Game Design Document.md`.

Optional argument: `$ARGUMENTS` (e.g., "GDD", "spec-docs", "GDD vs code", "items balance", or a specific file path)

## Audit Process

### Phase 1 — Gather Context
1. Read the target document(s) completely
2. Read related documents:
   - GDD: `Assets/8.Documents/Game Design Document.md`
   - Spec docs: `spec-docs/` (read README.md first for index, then relevant specs)
   - Previous audits: `Assets/8.Documents/GDD_AUDIT.md`
   - Memory: Check project memories for confirmed decisions
3. If auditing against code: Use agents to explore `Assets/Scripts/` for current implementation state

### Phase 2 — Cross-Reference Analysis
Run these checks systematically:

#### A. Contradictions (CRITICAL)
- **Doc vs Doc**: Compare GDD sections against spec docs. Flag ANY place where they describe the same mechanic differently (formulas, values, flow, terminology).
- **Doc vs Code**: Compare documented behavior against implemented behavior. Flag where code does something different from what docs say.
- **Internal contradictions**: Flag where the SAME document says two different things about the same mechanic.

For each contradiction found, produce:
```
| Aspect | Source A says | Source B says | Severity | Recommendation |
```

#### B. Gaps & Missing Content (HIGH)
- Sections marked [TBD], empty, or placeholder
- Mechanics referenced but never defined (e.g., "uses Dexterity" but Dexterity is TBD)
- Systems that depend on undecided choices (blocking dependencies)
- Missing formulas, values, or balance numbers
- Items/enemies/bosses listed without concrete stats

For each gap, classify:
- **Blocking**: Cannot implement without this decision
- **Important**: Should be defined before vertical slice
- **Nice to have**: Can wait for later

#### C. Coherence & Logic (MEDIUM)
- Does the system make logical sense? (e.g., a mechanic that contradicts the game's identity pillars)
- Are there edge cases not covered? (e.g., "what happens when X AND Y at the same time?")
- Do formulas produce reasonable values? Run quick mental math with example scenarios.
- Is terminology consistent? (same thing called different names in different places)
- Do difficulty curves make sense? (e.g., Floor 1 enemy harder than Floor 3)

#### D. Balance Red Flags (MEDIUM)
- Dominant strategies with no counterplay
- Useless options that are strictly worse than alternatives
- Snowball/death spiral mechanics
- Economy broken (items too cheap/expensive relative to income)
- Damage/HP ratios that make fights too fast or too slow

#### E. Completeness for Prototype (HIGH)
- Does the prototype scope section list everything needed?
- Are all systems in the prototype scope actually defined in the doc?
- Are there hidden dependencies (System A needs System B which isn't in scope)?

### Phase 3 — Produce Report
Save the audit to `Assets/8.Documents/GDD_AUDIT.md` with this structure:

```markdown
# Auditoría del GDD — [version/date]
**Fecha:** [today]
**Estado:** [status]
**Scope:** [what was audited against what]

## RESUMEN EJECUTIVO
[2-3 sentences: overall health, biggest issue, recommended next step]

## 1. CONTRADICCIONES
[Table format, sorted by severity]

## 2. BACHES / CONTENIDO FALTANTE
[Grouped by Blocking / Important / Nice to Have]

## 3. COHERENCIA Y LÓGICA
[Issues found with edge cases, terminology, logic]

## 4. BALANCE RED FLAGS
[Any balance concerns with example scenarios]

## 5. DECISIONES PENDIENTES PARA EQUIPO
[Prioritized list with options and recommendations]

## 6. NOTAS DE COHERENCIA
[What's working well — don't just criticize, acknowledge solid design]

## 7. PRÓXIMOS PASOS
[Concrete action items, ordered by priority]
```

## Rules
- Always use Spanish for the audit report
- Be specific: cite section names, line references, exact values
- For every problem, suggest a concrete solution or recommendation
- Run example scenarios mentally to validate formulas and balance
- If comparing against code, use agents to read the actual implementation
- Don't just list problems — prioritize them by impact on development
- Acknowledge what's well-designed, not just what's broken
- Compare against confirmed decisions in project memory before flagging as contradiction
