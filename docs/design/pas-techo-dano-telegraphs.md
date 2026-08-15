# Techo de daño de los telegraphs contra 100 de HP

> Auditoría del 12/08/2026. Fuente: assets en `Assets/Rollgeon/Enemies/`,
> `CH_Warrior.asset`, `Ruleset.asset`. Ver también
> [`pas-ataques-sin-resolucion.md`](pas-ataques-sin-resolucion.md) y
> [`pas-legibilidad-amenaza.md`](pas-legibilidad-amenaza.md).

## Problema

**Qué pasa:**
- El jugador tiene **100 de HP máximo** (`CH_Warrior.asset:4294`) y hay **cuatro telegraphs de 100 o más**: franja del Boss 2 bajo 20% (100), media sala del Boss 3 (100), y en las propuestas la mano Generala (90) y la fase 2 de La Casa (120).
- El telegraph **más chico de un jefe pega 40** (área del Boss 1). No existe ningún golpe de jefe que quepa en la ventana de la pasiva del Warrior, que se activa con 30 o menos (`CP_Warrior.asset`, `_hpThreshold: 30`).
- El escudo no compensa: la `ShieldBaseTable` de Odin da **0 a 8 puntos** según el combo. Contra un golpe de 70, un Par aporta **1**.
- El HP de los tres jefes es **200/200/200** (`ED_Boss_Sunken_Grand.asset:656`, `ED_Boss_Security_Boss.asset:590`, `ED_Boss_GeneralDirector.asset:399`) mientras su Attack va 20/30/40. La curva de daño sube y la de vida no.

**Impacto en el jugador:**
- Dos golpes cualquiera lo matan desde vida llena; el jefe necesita 8 a 14 turnos para caer con 13-27 de daño por turno (`damage-analysis.md` §4.1). La pelea exige esquivar casi todo durante ocho turnos seguidos.
- **La decisión "me como el golpe chico para meter un ataque" no existe**, y seis de los nueve rediseños del documento de jefes la asumen como jugada válida.
- La pasiva del Warrior nunca funciona como comeback: cualquier golpe que la active deja al jugador también a un golpe de morir.

---

## Análisis

**Por qué pasa:**
- El daño de los telegraphs se autoró antes de que la vida del jugador quedara en 100. No hay un techo escrito en ningún documento contra el que validarlo.
- `balance-modelo-3-pisos.md` ya reporta el HP plano desde el 09/07/2026, pero su modelo de TTK está calculado con Attack 2/3/4 y los assets hoy dicen 20/30/40: sus números de turnos no sirven para decidir.

**Variables que influyen:**
- `AINode_TelegraphMark.Damage`: 6 a 100 hoy → data por nodo, sin downstream.
- `EnemyDataSO.BaseHP`: 200 en los tres → data por asset, sin downstream.
- `EnergyRegenBase`: 2 (`Ruleset.asset`) y `BlockOnRepeat: true` en Movement y Base Attack → **un movimiento y un ataque por turno**, tope estructural.
- `ShieldBaseTable` (Odin): 0/1/2/3/4/5/6/8 → mitigación del 1% al 8% contra los golpes grandes.

---

## Opciones

### A: Cap por piso sobre el daño de los telegraphs
Techo de 25 / 35 / 45 puntos según piso, más un único check anunciado de ≤65 en el piso 3.
- **Pro:** es un campo `Damage` por nodo, cero código. Crea la decisión de comerse el golpe chico y vuelve la pasiva un comeback.
- **Contra:** hay que re-autorar ~20 nodos y re-testear las tres peleas.
- **Esfuerzo:** bajo

### B: Subir el HP del jugador
Llevar `BaseMaxHp` de 100 a 200 y dejar el daño como está.
- **Pro:** un solo campo.
- **Contra:** rompe la tabla de escudo, la ventana de la pasiva y el balance de los enemigos regulares (40-78 de HP, daño entrante 3-5 por turno). Mueve todo para no tocar 20 números.
- **Esfuerzo:** bajo, con downstream alto

### C: Subir el daño del jugador
Dejar el daño de jefe y acelerar la pelea desde la tabla de combos.
- **Pro:** acorta las peleas de 8-14 turnos a 4-6.
- **Contra:** los enemigos regulares ya caen en 2-3 turnos: trivializa el run entero. Y la tabla de combos es la identidad del juego.
- **Esfuerzo:** medio, con downstream alto

---

## Decisión

**Elegimos: Opción A** — recomendación de la auditoría, pendiente de mesa con Bocco.

**Justificación:** Es la única que se resuelve en data sin downstream, y es la que desbloquea las mecánicas que el resto del diseño ya asume. B y C mueven el juego entero para no tocar veinte campos.

**Cambios concretos:**
- Cap de `Damage` por piso: **25 / 35 / 45**. Check anunciado en piso 3: ≤**65**, con una ronda de aviso.
- Cada jefe suma un telegraph de **≤10% / ≤15% / ≤20%** de la vida según piso, para que comerse el chico sea jugable.
- `BaseHP` de los jefes a **140 / 190 / 250**. Derivación: turnos × daño por turno × uptime de ataque → 6,5 × 20 × 0,90 = 117; 8,5 × 24 × 0,80 = 163; 10,5 × 30 × 0,70 = 220, con margen.
- Resolver la `ShieldBaseTable` duplicada antes de medir: el bloque Odin dice 0-8 y el espejo YAML dice 0-90 en el mismo archivo.

**Status:** [TBD] — pendiente de decisión de Sebastián y Bocco. Bloquea a los otros dos PAS.
