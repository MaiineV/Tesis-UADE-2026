# Session State — Rollgeon (Tesis UADE 2026)

- **Proyecto:** Rollgeon — dungeon crawler con dados (Unity 6, namespace `Rollgeon.*`)
- **Etapa:** D) Development — sprint en curso, develop @ 2376bd0
- **División:** Game Dev
- **Fuente de verdad de estado:** `docs/audits/2026-06-10_GameAudit.html` (audit completo doc-vs-código)

## WIP accionable
1. Meta-progresión #164 — código+tests listos, falta wiring de escenas (`docs/setup/0164_MetaProgression.md`) + playtest
2. Floor system #158 — falta playtest de 3 pisos (`docs/setup/0158_FloorSystem.md`)
3. Feedback sequences #0012 — mergeado a develop (2376bd0), falta autoría de entries/secuencias
4. RulesetSO — submódulos RollConfig (T100b) y ScalingConfig (T99) pendientes; ScalingConfig bloquea balance por piso
5. Camera shake — scaffold TODO desde v8 (CameraService.cs:541)

## Faltantes (spec'd sin código)
§19 Rewards/Loot, §20 Status Effects, §15 Save central (parcial), §23 Settings, §25 Analytics, §21 Quests, §22 Tutorial, §24 Pooling central, §17.C/N Craps/Cutscenes

## Decisiones pendientes del usuario
- Scope cut formal de §20–§25 (sugerido: Save + Analytics sí, resto post-tesis)
- §5.6 Strike combos: TBD por diseño
- Pasada v16 de TECHNICAL.md (drift en §1.1.1, §1.3, §13.5, §14, §14.7)

## Flujo recomendado
1. Cerrar junio: wiring Meta #164 + playtest #158
2. ScalingConfig (T99) — desbloquea dificultad progresiva
3. Analytics mínimo (§25) antes de playtests formales de la tesis
