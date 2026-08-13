# Modelo de balance — experiencia escalonada en 3 pisos

> **WS3 del plan de acción (09/07/2026).** Data extraída de los assets reales del
> repo. Insumo para la sesión de balance con Bocco — las conclusiones son
> propuestas para discutir, no números finales.

---

## 1. Data extraída (fuente: assets en `Assets/Rollgeon/`)

### Enemigos regulares

| Enemigo | HP | Attack | Tiers extra |
|---|---|---|---|
| ED_RangedEnemy | 40 | 1 | — (vacío) |
| ED_Healer | 50 | 2 | — (vacío) |
| ED_MeleeCardEnemy | 60 | 2 | T2: HP ×1.3 (=78), Atk ×1.2 (=2.4) |

### Bosses

| Boss | HP | Attack | Piso (presunto por nombre) |
|---|---|---|---|
| ED_Boss | 100 | 2 | ¿legacy/placeholder? |
| ED_Boss_Sunken_Grand | 200 | 2 | ¿1? |
| ED_Boss_Security_Boss | 200 | 3 | ¿2? |
| ED_Boss_GeneralDirector | 200 | 4 | ¿3? |

### Encounter pool (EP_01)

Dos entradas con pesos 1:2, tier weights 80% T1 / 20% T2.

### Jugador (fórmula v2, tabla de ataque global)

Par 8 · SumaX 10+X·hits · DoblePar 15 · Trío 28 · Full 35 · Escalera 40 ·
Poker 55 · Generala 90. Daño = `Attack_PJ + base×multi + bono`.

**Parámetros sin extraer** (Odin-serialized, verificar en editor): HP del
jugador, `Attack_PJ` base del Warrior, energía por turno, costo de reroll.

---

## 2. Modelo TTK (turnos para matar)

Escenarios de daño del jugador por turno (con d6, multi = 1.0, Attack_PJ ≈ 2-4):

- **Turno pobre** (Par/nada): ~10
- **Turno medio** (DoblePar/SumaX/Trío): ~20-30
- **Turno alto** (Escalera/Full/Poker): ~40-58

### TTK jugador → enemigo (turnos, escenario medio ~25 dmg/turno)

| Objetivo | HP | TTK medio | TTK pobre (~10) |
|---|---|---|---|
| Ranged | 40 | 2 | 4 |
| Healer | 50 | 2 | 5 |
| Melee T1 | 60 | 3 | 6 |
| Melee T2 | 78 | 4 | 8 |
| Boss piso 1 (200?) | 200 | 8 | 20 |
| Boss piso 2/3 | 200 | 8 | 20 |

### Presión enemigo → jugador

Encuentro típico de 2-3 enemigos: **3-5 daño/turno entrante** (piso 1).
Con la fórmula NUEVA de escudo (cap 8, típico 2-4 con d6): el escudo absorbe
**~0.5-1 turno de daño entrante** en peleas múltiples — exactamente la tensión
que busca la spec (mitiga, no inmuniza). Con la fórmula vieja (Generala=90)
absorbía 18-30 turnos: el juego estaba resuelto.

---

## 3. Red flags detectadas (para la sesión con Bocco)

1. **HP de bosses plano entre pisos: 200/200/200.** La escalada es solo por
   Attack (2→3→4). Si el jugador mejora dados entre pisos (multi 1.0 → 1.5+),
   su daño crece ~50% pero el HP del boss no — **el boss de piso 3 puede caer
   MÁS rápido que el de piso 1**. Propuesta: escalar HP también (ej. 160/220/300)
   o revisar la curva de upgrades de dados.
2. **Sistema de tiers desaprovechado**: solo Melee tiene T2; Ranged, Healer y
   bosses tienen `ExtraTiers` vacío. Si los pisos 2-3 reusan los mismos EDs sin
   tiers, la dificultad de los encuentros regulares queda plana y toda la
   escalada recae en la composición (cantidad) — que castiga más de lo que
   desafía. Propuesta: T2/T3 para los tres regulares con pesos por piso
   (piso 1: 80/20/0 · piso 2: 30/60/10 · piso 3: 0/50/50).
3. **Attack enemigo 1-2 vs cap de escudo 8**: en piso 1 el escudo medio (2-4)
   ya neutraliza a un Ranged entero. Si los Attack no escalan por piso, en
   piso 3 el escudo vuelve a tender a trivial *relativo*. El tier multiplier de
   Attack (×1.2) es la perilla correcta — hoy solo existe en Melee.
4. **Boss "ED_Boss" (100/2)** parece placeholder — confirmar si se usa en algún
   layout o se borra (candidato a registro de pulido).
5. **Dato faltante**: qué EP usa cada piso (los `Pool:` de los 3 FloorLayouts
   referencian por GUID — mapear en editor). Sin eso no se puede cerrar la
   curva de composición por piso.

## 4. Curva objetivo propuesta (hipótesis de partida)

| Piso | Rol | TTK encuentro regular | Presión entrante/turno | Sensación |
|---|---|---|---|---|
| 1 | Enseñar | 2-3 turnos | 2-4 | Perdona errores; el escudo casi cubre el turno |
| 2 | Exigir | 3-4 turnos | 5-7 | Holds/rerolls dejan de ser opcionales |
| 3 | Examinar | 4-5 turnos | 8-11 | Exige dados mejorados; el cap de escudo se siente |

**Regla de sanidad**: presión entrante por piso ≈ 0.5×, 0.8×, 1.2× del cap de
escudo (8). Así el escudo pasa de "casi te cubre" a "elige qué golpe comés" —
la escalada se siente en la misma mecánica que el jugador domina.

## 5. Próximos pasos

1. Completar parámetros faltantes en editor (HP/Attack jugador, mapa EP↔piso) — 15 min con MCP.
2. Validar las 5 red flags con Bocco; acordar curva de §4.
3. Aplicar en assets (tiers + pesos + HP bosses) — solo data, sin código.
4. Playtest de 3 pisos con seeds fijas; outliers al registro de bugs/pulido.
