# Escalado de los seis jefes nuevos contra el techo de su piso

> Auditoría del 12/08/2026, segunda ronda. Fuente: fichas de los seis jefes del
> documento de diseño, `CH_Warrior.asset`, `Ruleset.asset`,
> `ThreatAreaShape.cs`, `AINode_KeepDistance.cs`, `AnalyticsTrackerService.cs`.
> Ver [`pas-techo-dano-telegraphs.md`](pas-techo-dano-telegraphs.md) y
> [`pas-ataques-sin-resolucion.md`](pas-ataques-sin-resolucion.md).
>
> **Restricción de alcance (Sebastián, 12/08):** este trabajo NO toca al
> jugador. Toda corrección es de jefes y salas; el kit del player es la vara,
> nunca la palanca.

## Problema

**Qué pasa:**
- Los seis jefes nuevos se numeraron **antes** del cap de 25/35/45 y nunca se
  reconciliaron: **10 de sus 14 ataques lo rompen**. Peores casos: jackpot de
  La Bandida 60 en piso 1 (cap 25), Cajero 70 en piso 2 (cap 35), fase 2 de
  La Casa **120 contra 100 de HP**.
- **Cinco de los seis se ganan con 0 de daño recibido** usando una sola acción
  repetida (peor caso: el paso diagonal contra el Anotador — `Row`/`Column` se
  centran en la casilla del jugador y la diagonal cambia ambas a la vez).
- Dos mecánicas centrales no son ejecutables: el jackpot de La Bandida es
  matemáticamente imposible (restar 1 mod 3 preserva la distinción de los
  rodillos) y la intersección de reglas de La Casa queda vacía desde la 2ª.
- El Cajero tiene un loop de realimentación positiva: suelta 10-15 fichas por
  golpe → tu oro decide su daño → el Arqueo convierte 40% de tu oro en su HP.
  Con 150 de oro inicial se cruza el escalón de 250 en el turno 7-8 sin decidir
  nada.

**Impacto:** sin reconciliar, el escalado 1→2→3 no existe: un jefe de piso 1
pega más que el techo del piso 3 y la mitad del roster se trivializa.

---

## Análisis

**Modelo de daño del jugador (decisión de Sebastián, 12/08):** 13-27 medido es
la **base del piso 1**; se espera **20/24/30** por piso porque el build crece.
La run que no llega a ese daño **pierde, y es correcto** — no se tunean jefes
para que gane una run floja. Con ese modelo el TTK queda: Generala 10 turnos
(objetivo ~10, la mejor calibrada), Cajero 10, Bandida 8, Croupier 9, y dos
desvíos reales: **Casa 14 (+33%)** y **Anotador 12 (+41%)**.

**Por qué se desvían esos dos (no es HP):**
- El Anotador prohíbe el combo más jugado → baja el **daño por turno** del
  jugador (~×0,85), no sus turnos. Nadie lo había modelado.
- La Casa no pega, pero sus reglas empujan al jugador fuera del alcance 1
  (melee puro) → uptime 0,60, el peor del roster.

**Los ejes que hacen medible la curva** (valor piso 1 / 2 / 3):
- Amenazas simultáneas: 1 / 1 / 2.
- Reglas activas persistentes: 0 / 0-1 / 1 (el canto del Tahúr: una regla de
  contrato viva por ronda, reescrita cada turno).
- Carga informacional (datos por turno): 1-2 / 2-3 / 2-6.
- Ventana de planificación: 1 / 2 / 4+ turnos.
- Permanencia de efectos: 0 / 1 / hasta 4.
- Regla de simultaneidad: el daño simultáneo **suma contra el cap** salvo
  disyunción geométrica garantizada (la barra de vida no distingue de cuántos
  nodos vino).

**Escalera espacial (BFS 4 vecinos, 77 casillas):** los tres puestos más
exigentes son los tres jefes de piso 3 — el techo del juego es el techo
espacial medido. Una inversión: **La Bandida (piso 1) entra 4ª**, por el eje
exacto de 4 pasos del jackpot 7×7.

---

## Opciones

### A: Renumerado data-only + los dos cambios de idea ya identificados
Los 14 ataques al cap por campo `Damage`; HP alineado a 140/190/250; un golpe
chico nuevo por jefe (≤10/15/20); Bandida con rodillo-contador y Casa con techo
de 2 reglas + fase 2 en 4 detonaciones de 30.
- **Pro:** casi todo es data; los dos cambios de idea ya están especificados y
  resuelven imposibilidades, no gustos. Anotador y Casa cierran su TTK con
  ajuste de mecánica (corrimiento al 2º combo más jugado o 1 de cada 2 turnos;
  1 de las 2 reglas siempre compatible con melee → uptime 0,80, cierra en 11).
- **Contra:** el `RoomSector` del Croupier es forma nueva real (no existe en
  `ThreatShape` y `ScatteredSquares` no llega al borde por construcción).
- **Esfuerzo:** bajo-medio

### B: Rediseño geométrico completo por piso
Partición real de sectores, disyunción garantizada de áreas dobles, formas
nuevas por jefe.
- **Pro:** saca el solapamiento de raíz y permite números más altos.
- **Contra:** 4-5 formas nuevas + validadores; bloquea los seis jefes detrás
  de código.
- **Esfuerzo:** alto

### C: Dejar los números y confiar en el playtest
- **Pro:** cero trabajo ahora.
- **Contra:** dos ataques matan de 1 golpe desde vida llena, dos mecánicas son
  imposibles de ejecutar; el playtest sólo va a redescubrir esta auditoría.
- **Esfuerzo:** nulo, con costo alto seguro

---

## Decisión

**Elegimos: A** — recomendación de la auditoría, pendiente de mesa con Bocco.

**Justificación:** 12 de los 14 ajustes son un campo `Damage`; los dos cambios
de idea (Bandida, Casa) no son opcionales porque arreglan mecánicas imposibles,
no números feos. B se reserva para cuando algún jefe pida números que el
solapamiento no permite.

**Cambios concretos** (detalle completo con el "antes → ahora" por campo en el
documento de jefes, sección Escalado):
- Caps aplicados: Croupier fase 2 → 2×12; Bandida jackpot → 25, brazo → 9;
  Cajero → 14/28/35, fichas 6-9, soborno 35/3 rondas, Arqueo con techo +30 HP;
  Anotador fila → 30, columna fase 2 → 32 **nunca el mismo turno**; Generala
  bust 18 / Full 2×20 / Póker 45 / Generala **65 con `ScatteredSquares 8×3`**;
  Casa 22/regla con **techo de 2 reglas**, fase 2 → 60 (Distanciamiento, 1
  regla, +1 ronda de aviso).
- Golpe chico nuevo por jefe: Croupier 8 (sólo si le pegás en windup),
  Bandida 9, Cajero 14, Anotador 12 (turnos impares), Generala 18 (bust),
  Casa 15 (Advertencia, 1 ronda antes de cada regla).
- Jackpot rediseñado: los rodillos **arrancan alineados** tras reponer;
  cuenta regresiva de 2 rondas; dañar cualquier rodillo la cancela.
- Anti-exploit: el paso diagonal del Anotador se cierra con la columna liviana
  ya en fase 1; su árbol debe envolver `KeepDistance` en
  `Selector(KeepDistance, Wait)` (bug documentado en el propio nodo).
- Telemetría previa al playtest de escalado: agregar **`BossId`** a
  `combat_ended`/`player_death` y un contador de turnos sin atacar. Sin eso,
  2 de las 4 métricas de aceptación no se pueden auditar.
- Verificación pendiente (la única que exige jugar): ¿un build bien armado
  pasa de 27 de daño en piso 3? Si no, **el HP del piso 3 baja de 250 a ~225**
  (recalibrar el jefe a lo medido — el kit del jugador no se toca).

**Status:** [TBD] — números propuestos, pendiente de mesa con Bocco. Orden de
encuentro por piso ya definido: Croupier→Bandida, Cajero→Anotador,
Generala→Tahúr.

---

## Adenda 12/08 — La Casa se descarta; entra El Tahúr

**Decisión de Sebastián:** La Casa no va. El paquete de arreglos la volvía
resoluble pero el jefe no convenció ("me pareció una mierda"). El slot B del
piso 3 lo toma **El Tahúr** (boceto 7 del menú): canta una mano antes de tu
turno — armás exacto = 0 daño; una mejor = tu golpe le entra doble; una peor =
te castiga. Armar EXACTO > armar MÁXIMO: la primera vez que el juego pide
contención.

Consecuencias:
- Las 4 reglas invertidas (R07-R10) quedan como material para el rework del
  General Director, no mueren con el jefe.
- Los máximos de dos ejes de la curva los definía La Casa; recalculados con el
  Tahúr diseñado: reglas activas 0/0-1/1, permanencia piso 3 = el pozo (todo
  el combate), carga informacional 2-5 (máx: Tahúr fase 2, 5 datos).
- El diseño completo del Tahúr (12/08, integrado al documento): HP 250, sin
  debilidad fija; castigo 18/26/34/40/45 según el pozo (5 fichas); techo 45
  sin check propio de 65 (el cupo del piso lo gasta La Generala); poke 10 en
  rondas limpias, nunca junto al castigo (55 rompería el cap); cobro = exacto
  dentro de La Mesa (su 3×3, daño 0), paga 12×fichas; fase 2 al 40%: se
  invierte el canto (cobra d=−1) + rastrillo +1 ficha/ronda. TTK ~10 rondas en
  la línea intencional; la codicia (×2) baja a 7 pagando ~90% de esquiva.
  Escalera de manos por `combo.Priority`, NO por daño base. Costo ~700 líneas
  (2 nodos + `IWagerService`); el canto y el ×2 reusan R03/R01.
- Los kits operativos de los seis quedaron en sus fichas, con las decisiones
  de Sebastián del 12/08: Represalia del Croupier sólo en números impares
  (~4/turno); fase 2 del Croupier con costura 2×12 (24 sólo en la columna
  compartida); fichas del Cajero duran 1 turno y caen dentro de la columna
  marcada; Arqueo devuelve el oro al vencerlo; jackpot de La Bandida como
  cuenta regresiva de 2 rondas (cancelable dañando un rodillo); Anotador sin
  `MatchColumn` (cero código) con fila 30 / columna 32 alternadas.
- Terreno con estado (decisión de Sebastián, 12/08): el sector detonado del
  Croupier queda **en llamas** — 6/turno al quedarse adentro, dura 1 turno en
  fase 1 y 2 en fase 2, y **la detonación consume la llama** (sin esa regla,
  costura 24 + fuego 6 = 30 rompe el cap de 25). El Anotador deja **estela
  helada** al replegarse: las casillas que pisó (1-3), dura 1 turno, pisarla =
  **stun de 1 turno** y se derrite (sin derretirse, dos estelas encadenan
  stuns). Costo: el fuego pide `DurationRounds` + área dinámica en
  `HazardService` (el nodo `AINode_ActivateHazard` ya existe); el hielo pide
  trigger al pisar (hoy los hazards resuelven por ciclo) y un estado stun que
  **no existe en el codebase** — el Anotador deja de ser el jefe de cero
  código. El eje de permanencia del piso 1 pasa de 0 a 1-2; la diferenciación
  de la curva queda en *qué* persiste (daño pintado → negación → estado total).
- Nota de consistencia: la corrección de auditoría "en fase 2 la rueda sigue
  moviéndose" quedó superada por la ficha final del Croupier (rueda trucada,
  pero gratis de pegar — quita palanca, devuelve tempo; el fuego a 2 turnos es
  lo que aprieta la fase). El documento lo marca con nota fechada.

## Adenda 12/08 (2) — Paquete de dificultad v2, calibrado por simulación

Simulación Monte Carlo (3.000 peleas × jefe × nivel de lectura 95/75/55%)
sobre los números finales de las fichas. Criterio de curva de Sebastián:
muertes bajas en P1, medias en P2, altas en P3. Decisión: aplicar el paquete
completo (4 de 4 palancas, todas del lado jefe, caps 25/35/45 intactos).

- **Generala — el cubilete**: castigo por adyacencia (12, SquareAroundSelf,
  turnos impares; espejo del lápiz del Anotador). Resuelve el punto 6 de la
  lista de decisiones y cierra el exploit medido: el óptimo pasaba la pelea
  con 0% de vida perdida; ahora paga 58%. Muertes del medio 23% → 47%.
- **Tahúr — HP 250→290, castigos 18/26/34/40/45 → 26/32/38/42/45, poke
  10→12**. TTK entra en banda (9→10-13); vida del medio 22%→46%. Sus muertes
  (~10-13%) quedan bajo la banda per-jefe A PROPÓSITO: es el jefe de
  lectura/tempo del piso; la sangre la pone la Generala. Las tres líneas
  siguen viables y ordenadas (exacto 13 rondas, codicia 8-9).
- **Bandida y Cajero — sin cambio de ficha**: el rearme inmediato tras el
  jackpot (ResetCountdown del árbol) y la columna Size 3 desde 100 de oro ya
  estaban escritos; la primera pasada del modelo los leía generosos. Sólo se
  agregaron notas explícitas ("la pausa es el premio de cancelar").
- Corrección de modelo (misma fecha): la mano exacta del Tahúr TAMBIÉN pega —
  sólo cobrar el pozo reemplaza el ataque. Los números pre-corrección del
  Tahúr (TTK exacto 24) eran artefacto del modelo.
- Resultado con el paquete, jugador medio: muertes ~0% / ~13% / ~29% por piso
  y vida perdida 24% / 51% / 56% — las tres bandas adentro. Flojo en P3:
  48-80% de muertes (la run floja pierde, y es correcto).
- Herramienta: `_sim_bosses.py` (scratchpad de la sesión del 12/08; `--v2` =
  fichas actuales). Pendiente decidir si se mueve al repo para Bocco.
