# Simulación de run completa con los datos reales del juego

> Corrida 13/08 — Monte Carlo 3.000 runs × nivel de lectura (95/75/55%).
> Script: `_run_full.py` (scratchpad de sesión). A diferencia de la calibración
> de bosses de las fichas (12/08), acá el player NO es un modelo inventado:
> el daño sale del motor de dados real (5×d6, 3 tiradas, fórmula v3
> `N = comboBase + Attack + Σcaras + bonos`), el HP persiste entre salas y
> pisos, el oro se gana y se gasta con los números de los assets, y la
> curación es la del juego (acción Healing, pociones, items).

## TL;DR

1. **El daño real del player es el doble del que usamos para calibrar los
   bosses.** Mediana **42 por golpe** (p10 25, p90 80) contra el 13–27 que
   asumía la simulación de las fichas. Los bosses de 140–290 HP mueren en
   **3–5 turnos**: la Bandida muere antes de que su jackpot dispare una sola
   vez, el pozo del Tahúr casi nunca llega a 5.
2. **La acción Healing rompe la economía de vida entera.** Cura **100 HP
   fijos** (`_healScaleFactor: 0` en `CH_Warrior.asset`) por 2 de energía en
   combate, sin cooldown. Con regen +2/turno es full-heal sostenible cada 2
   turnos: con ella, hasta el jugador flojo gana el **91% de las runs** y
   TODOS los bosses tienen win rate ~100% en TODOS los niveles de lectura.
3. **Sin esa acción, el juego se cae en el lugar equivocado:** el trash del
   piso 2 (150/30 y 70/20 × 2 por sala, 4–6 salas) mata al 64% de los
   jugadores medios **antes** de llegar al boss… y los bosses igual caen
   ~100% cuando se llega. El peligro está invertido: salas comunes letales,
   jefes triviales.
4. **La mecánica de oro del Cajero no se activa nunca.** El jugador llega al
   boss del P2 con ~65–70 de oro (mediana): la columna queda en el escalón
   pobre (14, umbral <80) prácticamente siempre. Los umbrales 80/220 están
   calibrados para una economía que el juego no genera.

## Escenario A — el juego tal cual está (con Healing 100)

| Lectura | Gana la run | Muertes P1/P2/P3 | Bosses |
|---|---|---|---|
| Óptimo 95% | **96,0%** | 0 / 0,7 / 3,3% | todos 100% |
| Medio 75% | **93,4%** | 0 / 1,3 / 5,3% | todos ~100% |
| Flojo 55% | **90,7%** | 0 / 1,6 / 7,7% | todos ~100% |

Las pocas muertes son ráfagas del trash de P3 (53 dmg/ronda de promedio)
entre ventanas de heal. Ningún boss mata. El oro mediano al llegar a cada
boss: **28 / 67 / 145**.

## Escenario B — sin la acción Healing (solo pociones e items)

| Lectura | Gana la run | Muertes P1/P2/P3 | Dónde muere |
|---|---|---|---|
| Óptimo | 23,8% | 1 / 44 / 31% | trash P2 (66% de las muertes) |
| Medio | 8,8% | 4 / 65 / 23% | trash P2 |
| Flojo | 2,4% | 10 / 74 / 14% | trash P2 |

El pool de vida por piso (100 HP + ~2 pociones de 55 promedio) no banca
~300 de daño entrante acumulado del piso 2 sin curación entre salas. **Y aun
así los bosses alcanzados caen 96–100%.** El balance de jefes no se arregla
tocando la curación: el daño del player los derrite igual.

## Escenario C — cada boss aislado (100 HP frescos, sin curación)

Apples-to-apples contra las bandas de las fichas (12/08):

| Boss | Piso | Win óptimo/medio/flojo | TTK med | Vida perdida (medio) | Banda objetivo |
|---|---|---|---|---|---|
| Sunken Grand | 1 | 100 / 100 / 99,9% | 4–5 | 19% | 10–30% ✔ vida, ✘ TTK |
| Croupier | 1 | 100 / 100 / 99,9% | **3** | 11% | TTK pedía 7–10 |
| Bandida | 1 | 100 / 100 / 100% | **4** | 6% | jackpot nunca dispara |
| Cajero | 2 | 100 / 100 / 100% | **4** | 0% | vida pedía 30–55% |
| Anotador | 2 | 99,9 / 97,8 / 82,8% | 5 | 33% | el único de P2 que muerde |
| Generala | 3 | 97,4 / 80,3 / 58,9% | 7–9 | 46% | **la única cerca de banda** |
| Tahúr | 3 | 100 / 97,6 / 77,9% | 6 | 29% | vida pedía 55–80% |

Con TTK 3–5, las mecánicas firmadas (cuenta regresiva, fases, pozo,
represalia) **no llegan a jugarse**. La calibración de las fichas era
correcta para un player de 13–27 de daño; el player real pega 42.

## Por qué el daño es 42 y no 20

Mix de combos con hold greedy (3 tiradas): trio 35%, **poker 24%**, par 13%,
doble par 13%, full 10%, **generala 5%**, escalera ~0%. Un trio de 4s ya son
39 de daño (22 + 5 attack + 12 caras); un poker de 5s son 80. Los items de
tienda suman +20/+50 al combo por 15–35 de oro y empujan la mediana a ~50.

## Red flags de datos (del barrido de assets, 13/08)

1. `CH_Warrior.asset` — acción Healing: `_baseAmount: 100`,
   `_healScaleFactor: 0`, `_healThreshold: 25`. El threshold sugiere que
   DEBÍA escalar con el score del dado y quedó en 100 planos.
2. `Item_Egoista` — `+Attack = oro actual`, sin cap (`ReadCurrentGold`).
   Con el starting gold actual del asset (500, valor de testing) es +500 Attack.
3. `EconomyBootstrap.asset` — `_startingGold: 500` (dev). Diseño: 10–15.
4. `ED_RangedEnemy` — `WeaknessMultiplierOverride: 0.14` (¿debía ser 1.4?).
5. Descripciones vs valores ×10: Corona dice "5 HP" y cura 50; Bendición
   dice "15" y cura 150; la poción cura 1d10×10 (10–100).
6. `char_rew.hp_plus_5` otorga **+50** MaxHealth (Value: 50).
7. Ranged no escala su Attack de P2 a P3 (20 en ambos); su HP sí (70→110).
8. Altar de encantamientos: EV débil — ~24% del pool de 33 empeora el dado,
   +1–2 de daño esperado por 15 de oro vs +20–50 por 15–35 de los items.

## Palancas (a decidir en equipo — nada implementado)

- **Healing action**: pasar a escalar con el score (ej. `heal = score` ≈ 17
  promedio) o cooldown por combate. Es LA palanca: define si el juego tiene
  economía de vida.
- **Daño player vs HP de bosses**: o la tabla de combos baja, o los bosses
  necesitan ~×2 HP para volver a las bandas (Croupier 140→~300, etc.). Tocar
  HP de bosses no toca el kit del player (regla vigente).
- **Trash P2**: si Healing se nerfea, el salto 60/20→150/30 del melee T2 es
  el nuevo muro; revisar tier o dar curación entre salas.
- **Cajero**: umbrales de oro 80/220 → algo tipo 40/120 para que muerda con
  las curvas reales (65–70 de oro al llegar).

## Paquete de fixes decidido (13/08) y calibración

Decisión del equipo sobre las palancas: **Healing escala con la mano**
(target ~20 promedio por uso, rango ~10–35), **los bosses suben HP** (el kit
del player no se toca), y entran los 4 fixes de data (Cajero 40/120, cap del
Egoista, Ranged 1.4 + Attack P3, descripciones ×10).

HP recalibrado por simulación (ancla: TTK en banda — con el healing escalado
el sustain supera el daño de los bosses de golpe chico, así que el win rate
per-boss queda alto por diseño y las muertes viven en Anotador/Generala/Tahúr
y en el trash):

| Boss | HP | TTK medio resultante | Banda TTK |
|---|---|---|---|
| Sunken Grand | 200 → **400** | 8 | 7–10 ✔ |
| Croupier | 140 → **350** | 7 | 7–10 ✔ |
| Bandida | 140 → **280** | 8 | 7–10 ✔ |
| Cajero | 190 → **450** | 7–8 | 9–13 ~ |
| Anotador | 190 → **430** | 9 | 9–13 ✔ |
| Generala | 250 → **560** | 11–15 | 11–16 ✔ |
| Tahúr | 290 → **650** | 9–12 | 11–16 ~ |

Run completa con el paquete aplicado (`_validate_fixes.py`):

| Lectura | Gana la run | Muertes P1/P2/P3 |
|---|---|---|
| Óptimo | **88,0%** | 0 / 1,8 / 10,1% |
| Medio | **66,4%** | 0 / 6,4 / 27,1% |
| Flojo | **39,2%** | 0 / 18,1 / 42,7% |

Curva sana: el flojo pierde 6 de 10 runs, el óptimo casi siempre gana, y las
muertes se concentran en el piso 3. Nota: con el sustain nuevo, los bosses de
golpe chico (Croupier, Bandida, Cajero) casi no matan — su amenaza es de
tempo/desgaste. Si tras el playtest se quiere más letalidad de jefes, la
palanca siguiente es el costo/frecuencia del Healing en combate, no más HP.

## Límites del modelo

Primer orden, igual que la sim de las fichas: geometría colapsada a
probabilidades de lectura; probabilidad de hit del trash es supuesto
documentado en el script (melee `0.85−0.55p`, ranged `0.90−0.40p`);
estrategia de dados greedy (un jugador real puede rendir menos, no más);
energía/movimiento simplificados; `egoista` excluido a propósito. Sirve
para bandas y comparaciones, no para números finos.
