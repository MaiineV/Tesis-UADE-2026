# Sistema de Combate — Propuesta 3 Action Points

> Documento de análisis. Propuesta nueva que reemplaza la discusión Opción A (2 acciones) vs Opción B (1 acción) del GDD actual.
> Cambios clave vs GDD: 3 AP en orden fijo, fases Player/Enemy, opportunity attacks bilaterales, sin botón de huida, sin "modo combate" separado — todo ocurre en el mapa.

---

## 1. Resumen de la Propuesta

### Principios fundamentales

- **No existe "entrar en combate".** No hay transición, no hay pantalla separada. Todo ocurre en el mapa isométrico. Si hay un enemigo en la sala, el combate pasa por turnos directamente en la grilla.
- **El enemigo no te contraataca cuando vos le pegás.** Los ataques solo ocurren en la fase del actor correspondiente. Si atacás en tu Player Phase, el enemigo recibe daño pero no responde hasta su Enemy Phase.
- **No hay botón de huida.** El combate termina cuando todos los enemigos mueren o el jugador muere.
- **Opportunity attacks bilaterales.** Alejarse de un enemigo (o enemigo alejarse del jugador) que te tiene en rango provoca un ataque de oportunidad automático.

### Estructura del Turno — Player Phase

El jugador ejecuta 3 Action Points **en orden fijo**:

| AP | Nombre | Opciones | Obligatorio |
|----|--------|----------|-------------|
| **AP1** | **Movimiento** | Moverse N losetas O quedarse quieto | No — puede pasar |
| **AP2** | **Acción principal** | **Atacar** (Generala) O **Bolsa** (usar ítem/poción/consumible) | Sí — debe elegir una |
| **AP3** | **Defensa** | **Tirada de escudo** — tira dado(s) de defensa. Resultado reduce daño del próximo ataque recibido. | Sí — igual para todas las clases (por ahora) |

> *Nota: AP3 es igual para todas las clases en esta versión. La diferenciación por clase se deja para una iteración futura cuando el sistema base esté probado.*

### Estructura del Turno — Enemy Phase

Cada enemigo ejecuta en orden (de más poderoso a más débil):
1. **Moverse** hacia el jugador (o patrullar si es estático)
2. **Atacar** si está en rango

### Flujo Completo de un Round

```
┌─────────────────────────────────────────────────┐
│              PLAYER PHASE                        │
│  AP1: Movimiento (o quedarse)                    │
│  AP2: Atacar (Generala) O Bolsa (ítem/poción)   │
│  AP3: Tirada de escudo (defensa)                 │
├─────────────────────────────────────────────────┤
│              ENEMY PHASE                         │
│  Enemigo 1: Mover → Atacar (si está en rango)   │
│  Enemigo 2: Mover → Atacar (si está en rango)   │
│  ...                                             │
├─────────────────────────────────────────────────┤
│  → Vuelve a PLAYER PHASE                        │
└─────────────────────────────────────────────────┘
```

---

## 2. AP2 — Atacar: ¿Elegir enemigo antes o después de tirar?

### 2.0 — Fórmula de daño (costura con `balance_dados.md`)

Antes de entrar en ATK-A vs ATK-B, dejamos explícita la fórmula de daño usada en este sistema. Viene del documento de balance y **debe aplicarse en el cálculo de AP2**:

```
daño_final = (base_combo × multiplicador_dado) + bonus_dados_grandes

donde:
  base_combo       = valor del combo logrado (Par=10, Trío=28, Generala=100, etc.)
  multiplicador    = EV_promedio_build ÷ 3.5
  bonus_fijo       = Σ bonus de dados grandes en la build, con escalado:
                     bonus = bonus_dado_mayor + (otros_dados_grandes × 0.5)
                     valores: d10=+4, d12=+8, d20=+14
```

**Ejemplo:** Build `d12 + 3×d4`, saca Trío en AP2.
- base_combo = 28
- mult = ((6.5 + 2.5×3) / 4) / 3.5 = 3.5/3.5 = 1.00
- bonus = +8 (un solo d12)
- **daño final = (28 × 1.00) + 8 = 36**

**UI de combate:** mostrar desglose durante la animación de daño → `"Combo: 28 + Bonus d12: +8 = 36"`.

### Preguntas abiertas que cruzan con balance

| # | Pregunta | Impacto |
|---|----------|---------|
| **F1** | ¿El bonus fijo aplica también en **OA del jugador** (§4) y en **escudo** (§3) o solo en la Generala de AP2? | Cambia el alcance del sistema de bonus |
| **F2** | Si el d4 resulta ser "táctico" (4-F en balance), ¿el d4 táctico puede usarse también para el escudo de AP3? | Conecta el sistema de control con la defensa |
| **F3** | Si se elimina el d8 (8-D en balance), ¿el starting loadout del Warrior cambia a 6×d6 o a 4×d6 + 2×d12? | Impacta las primeras horas de juego en prototipo |

> Estas preguntas deben resolverse **junto con el equipo de balance**, no aisladas. Ver `balance_dados.md §5` para las decisiones correspondientes.

---

### 2.1 — ATK-A vs ATK-B

Esta es la decisión de diseño más importante de la propuesta. Hay dos opciones:

### Opción ATK-A: Elegir enemigo → Tirar Generala → Aplicar daño

```
Jugador selecciona enemigo objetivo → Tira los dados (3 tiradas Generala) → Combo resultante se aplica al enemigo elegido
```

**Flujo de UI:**
1. Se resaltan los enemigos en rango de ataque
2. Jugador clickea un enemigo
3. Se abre la interfaz de Generala (tirar, reservar, confirmar)
4. Daño se aplica al enemigo seleccionado

### Opción ATK-B: Tirar Generala → Ver resultado → Elegir a quién pegarle (o no)

```
Jugador tira los dados (3 tiradas Generala) → Ve el resultado y el daño → Decide a qué enemigo aplicarlo (o si lo aplica)
```

**Flujo de UI:**
1. Se abre la interfaz de Generala directamente
2. Jugador tira, reserva, confirma combo
3. Se muestra el daño resultante
4. Se resaltan los enemigos en rango
5. Jugador elige a quién aplicar el daño (o pasa si no quiere/no puede)

### Comparativa directa

| Criterio | ATK-A (elegir → tirar) | ATK-B (tirar → elegir) |
|---|---|---|
| **Información disponible al tirar** | Sabés contra quién vas. Podés decidir si vale la pena arriesgar otra tirada según el HP del enemigo. | No sabés contra quién va. Tirás "a ciegas" y después decidís. |
| **Decisión táctica** | La decisión está en **la Generala**: ¿arriesgo otra tirada o confirmo? Ya elegí el objetivo. | La decisión está **después de la Generala**: ¿a quién le pego con este resultado? ¿Vale la pena usar este daño o lo "desperdicio"? |
| **Momento de tensión** | Durante la tirada — ¿logro el combo que necesito para matar a este enemigo? | Después de la tirada — tengo X daño, ¿a quién se lo meto? |
| **Información para el jugador** | Alta: sabés el HP del enemigo → calculás cuánto necesitás → tirás con objetivo claro | Media: tirás sin contexto → después evaluás |
| **Posibilidad de "desperdiciar"** | Si sacás Generala contra un Goblin de 5 HP, desperdiciaste daño | Sacás Generala → elegís el enemigo que más lo necesita. No hay desperdicio. |
| **Complejidad de UI** | Simple: seleccionás → tirás → se aplica | Un paso más: tirás → resultado visible → seleccionás |
| **Paralelo con juegos de mesa** | Más parecido a D&D: "ataco al Goblin" → tiro dados | Más parecido a un juego de cartas: "juego esta carta" → elijo objetivo |
| **Riesgo de overkill** | Alto — si no calculás bien, podés matar con Par a un enemigo al que le quedaba 1 HP y desperdiciar el ataque | Bajo — ves el daño y elegís el objetivo óptimo |

### Análisis profundo: ¿Qué genera más decisiones interesantes?

**ATK-A (elegir → tirar)** pone la tensión DENTRO de la Generala:

> "El Orco tiene 30 HP. Necesito al menos un Trío (28 × mult) para matarlo. Tengo 2-2-5-6-3 en la primera tirada. Reservo los dos 2 y tiro el resto... Segundo tiro: 2-2-2-4-1. ¡Trío de 2! Daño = 28 × mult. Justo lo mata."

El jugador tira con un **objetivo numérico** en mente. Cada tirada se evalúa contra "¿me alcanza para matar a este enemigo?". Esto crea tensión porque hay un umbral claro de éxito/fracaso.

**ATK-B (tirar → elegir)** pone la tensión DESPUÉS de la Generala:

> "Tiré y saqué Full House. Daño = 40 × mult = 40. Ahora: ¿se lo meto al Orco (60 HP, no lo mato pero lo debilito) o al Goblin (35 HP, lo mato de un golpe)? Si mato al Goblin, el turno que viene tengo un enemigo menos atacándome. Pero si debilito al Orco, en el próximo turno quizás un Par lo termina..."

El jugador hace **triage táctico** con el daño que ya tiene. La decisión es de asignación de recursos, no de tirada.

### ¿Y si sacás 0 daño (sin combo)?

| Situación | ATK-A | ATK-B |
|---|---|---|
| Sin combo → 0 daño | Ya elegiste enemigo. 0 daño se aplica. Turno "perdido" contra ese enemigo. Frustrante porque ya te comprometiste. | Sacás 0 daño. No elegís enemigo (no tiene sentido). Pasás. Menos frustrante porque no te "comprometiste" a nada. |

Esto es una ventaja significativa de ATK-B: **el 0 daño se siente menos castigador** porque no le "fallaste" a un enemigo específico — simplemente no tuviste resultado.

### ¿Y si no hay enemigos en rango?

| Situación | ATK-A | ATK-B |
|---|---|---|
| AP2 pero no hay enemigos en rango | No se puede seleccionar enemigo → se fuerza "Bolsa" o se pasa | Se puede tirar igual → se obtiene un resultado → no se puede aplicar → ¿se pierde? ¿Se guarda? |

ATK-A es más limpia en este caso: si no hay enemigo en rango, no podés atacar, punto. Usás Bolsa o pasás.

ATK-B tiene un problema: ¿para qué tirarías si no hay a quién pegarle? Habría que bloquear la Generala si no hay enemigos en rango (igual que ATK-A), lo cual elimina la diferencia en este caso.

### Relación con el rango de ataque

El rango de ataque define cuándo se puede atacar. En ambas opciones, el ataque requiere al menos 1 enemigo en rango. La diferencia:

- **ATK-A**: seleccionás un enemigo específico en rango → tirás contra él.
- **ATK-B**: tirás si hay al menos 1 enemigo en rango → después elegís cuál.

Con ATK-B, si tenés 3 enemigos en rango, tirás UNA vez y elegís a cuál de los 3 le aplicás el daño. Es una sola tirada para múltiples opciones de target. Esto es más eficiente para el jugador.

### Recomendación

| Prioridad | Mejor opción | Por qué |
|---|---|---|
| **Tensión en la Generala** (cada tirada se siente importante) | **ATK-A** | El objetivo numérico le da sentido a cada decisión de reservar/retirar dados |
| **Decisión táctica post-tirada** (triage de daño) | **ATK-B** | El jugador asigna su recurso (daño) de forma óptima cada turno |
| **Menos frustración por 0 daño** | **ATK-B** | No te comprometiste a nada, simplemente no salió combo |
| **Simplicidad de implementación** | **ATK-A** | Un paso menos en la UI |
| **Más coherente con juegos de dados** | **ATK-A** | "Ataco al Goblin, tiro dados" es el flujo natural de mesa |
| **Más coherente con la identidad del juego** (apuestas, riesgo) | **ATK-A** | Comprometerte antes de tirar es una apuesta — coherente con la filosofía de casino |

**Mi lectura**: ATK-A es más coherente con la identidad del juego (apuesta, compromiso, riesgo). ATK-B es más "justo" y táctico. Para presentar al equipo, recomiendo llevar ambas opciones claras y decidir en playtest.

---

## 3. Tirada de Escudo (AP3) — Cómo funciona

Todas las clases usan la misma mecánica defensiva (por ahora):

### Mecánica base

El jugador tira un dado de defensa. El resultado se convierte en **escudo** que absorbe daño del próximo ataque enemigo recibido.

### Preguntas de diseño

| # | Pregunta | Opciones | Impacto |
|---|----------|----------|---------|
| 1 | **¿Qué dado se usa para el escudo?** | a) Dado fijo por clase (ej: todos usan d6) | Simple. Igual para todos. |
| | | b) El dado más grande de tu build | Conecta la bolsa de dados con la defensa. Build con d12 = mejor escudo. |
| | | c) Dado específico de escudo (stat separado) | Desacopla ataque de defensa. Más variables a balancear. |
| 2 | **¿El escudo dura 1 ataque o todo el round?** | a) 1 ataque (se consume con el primer golpe) | Contra múltiples enemigos, solo bloqueás al primero. Más tenso. |
| | | b) Todo el round (absorbe daño acumulado) | Más defensivo. Contra 3 enemigos, el escudo de 5 absorbe 5 de daño total. |
| 3 | **¿Se acumula entre rounds?** | a) No — se resetea cada Player Phase | Simple. Cada turno es independiente. |
| | | b) Sí — escudo sobrante se suma al del próximo round | Incentiva no recibir daño (acumular escudo). Puede ser OP si el jugador se aleja y acumula. |
| 4 | **¿Qué pasa si el jugador saca 1 en el dado de escudo?** | Es lo que es — 1 punto de absorción. | La varianza del dado aplica a la defensa también. Coherente con "dados son TODO". |
| 5 | **¿El escudo protege contra opportunity attacks?** | a) Sí — se consume si recibís OA | Moverse es menos castigador si tenés escudo alto. |
| | | b) No — OA bypasea escudo | OA se siente más peligroso. Posicionamiento importa más. |

### Propuesta simple para prototipo

| Parámetro | Valor |
|-----------|-------|
| Dado de escudo | d6 fijo para todas las clases |
| Duración | 1 solo ataque (el primero que reciba) |
| Acumulación | No — se resetea cada Player Phase |
| Protege contra OA | No — el OA bypasea escudo |

Esto es lo más simple de implementar y testear. Se puede complicar después.

---

## 4. Rango de Ataque y Opportunity Attack

### 4.1 — Definiciones de rango

Antes del detalle del OA, dejamos los rangos explícitos. Estos valores son **bloqueantes para la implementación** del prototipo — sin ellos no se puede saber cuándo disparar un ataque, un OA, o cuándo se puede elegir enemigo.

| Concepto | Definición | Medida |
|---|---|---|
| **Tile adyacente** | Una de las 8 losetas que rodean al actor (incluye diagonales en grilla isométrica) | Distancia = 1 |
| **Rango melee** | Alcance de un arma cuerpo a cuerpo | 1 tile (adyacente) |
| **Rango corto** | Alcance intermedio (ej: lanza, arco corto) | 2 tiles |
| **Rango largo** | Alcance de proyectil (ej: arco, hechizo) | 3-4 tiles |
| **Rango de OA** | Distancia a la que el actor dispara un ataque de oportunidad cuando el otro se aleja | = rango del ataque normal del actor |

### 4.2 — Cómo se asigna el rango a cada actor

**Propuesta para el prototipo (simplificada, iterar después):**

| Actor | Rango de ataque | Justificación |
|---|---|---|
| **Jugador (Guerrero)** | 1 tile (melee) | Clase base, arma cuerpo a cuerpo |
| **Goblin** | 1 tile (melee) | Enemigo básico |
| **Orco** | 1 tile (melee) | Enemigo básico |
| **[Futuro] Arquero** | 3 tiles (largo) | Primer enemigo con rango |
| **[Futuro] Clases ranged (Alchemist?)** | 2-3 tiles (corto/largo) | Identidad de clase |

**En el prototipo inicial, todos los actores son melee (rango 1).** Esto simplifica la lógica y deja el sistema de rangos listo para extender cuando se agreguen enemigos/clases con proyectiles.

### 4.3 — Rango de OA = rango de ataque (regla simple)

Un actor dispara OA si el otro se aleja estando dentro de su rango de ataque. En el prototipo, todos son melee, así que:

- El OA solo dispara si alguien **adyacente** se aleja.
- Si te alejás de un enemigo a 2 tiles, **no hay OA** (estaba fuera de rango melee).
- Esto hace que el OA funcione como "costo de zafar del cuerpo a cuerpo", que es la intuición clásica.

### 4.4 — Múltiples enemigos adyacentes: ¿cuántos OAs?

Si el jugador está rodeado por 3 enemigos adyacentes y se mueve alejándose de los 3, hay dos reglas posibles:

| Regla | Comportamiento | Pros | Contras |
|---|---|---|---|
| **A: 1 OA global por movimiento** | Solo el enemigo más cercano/peligroso dispara | Menos castigador. Posicionarse mal no es suicidio. | "Rodeo efectivo" pierde peso — da igual cuántos enemigos te rodeen |
| **B: 1 OA por enemigo en rango** | Los 3 enemigos atacan | Coherente con D&D y SRPGs clásicos. Rodear importa. | Rodeo = muerte casi segura. El jugador se queda quieto siempre. |
| **C: Máximo 2 OAs por movimiento** | Los 2 enemigos más cercanos | Punto medio | Regla arbitraria |

**Recomendación prototipo:** **Regla A (1 OA global)** — es la más simple de implementar y la menos castigadora. Si en playtest se siente que "el rodeo no importa", se sube a B o C.

> Esto responde explícitamente la pregunta #5 del §8 actual (prioridad Alta): resuelta con Regla A para prototipo.

---

### 4.5 — Opportunity Attack: regla y mecánica

**Regla:**

> Si una entidad se **aleja** de otra que la tiene en rango de ataque, la entidad en rango ejecuta un ataque de oportunidad automático. Aplica para **ambos lados** (jugador y enemigos).

**Cómo funciona:**

| Situación | ¿OA? | Quién lo recibe |
|-----------|------|-----------------|
| Jugador en rango de Goblin. Jugador usa AP1 para alejarse. | **Sí** | Jugador |
| Jugador en rango de Goblin. Jugador se queda quieto. | No | — |
| Goblin en rango del jugador. Enemy Phase: Goblin se aleja. | **Sí** | Goblin |
| Jugador se acerca a un enemigo (entra en rango). | No | — |
| Jugador ataca a un enemigo y lo mata. Siguiente turno, se mueve. | No | Enemigo muerto |

**Daño del Opportunity Attack** — no es una Generala completa, es un golpe automático rápido:

| Quién hace el OA | Mecánica | Justificación |
|---|---|---|
| **Enemigo → Jugador** | Daño fijo basado en el stat del enemigo (ej: Goblin = 5, Orco = 10) | Rápido, predecible. El jugador sabe cuánto le va a doler antes de moverse. |
| **Jugador → Enemigo** | Tira 1 dado (el mayor de su build). Resultado = daño. | Usa la build del jugador sin la complejidad de la Generala. Rápido pero con varianza (coherente con dados). |

**Limitaciones recomendadas:**

| Regla | Justificación |
|-------|---------------|
| **Máximo 1 OA por movimiento** (del enemigo más cercano) | Evita la "trampa de 3 enemigos" donde moverse es suicidio. |
| **El escudo (AP3) NO protege contra OA** | El OA es el castigo por moverse. Si el escudo lo absorbe, moverse no tiene costo real. |
| **Los enemigos SÍ reciben OA del jugador** | Bilateral. Si un enemigo se aleja del jugador, recibe daño. Esto incentiva al jugador a posicionarse bien. |

---

## 5. Sin "Modo Combate" — Todo en el Mapa

### 5.1 — Cómo funciona

No hay transición ni pantalla separada. El jugador entra a una sala, ve enemigos en la grilla, y el juego pasa a turnos automáticamente. La Generala se resuelve en una UI overlay sobre el mapa (no en una pantalla aparte).

```
Sala de exploración                    Sala con enemigos
┌──────────────┐                       ┌──────────────┐
│  . . . . . . │                       │  . . G . . . │
│  . . . . . . │   el jugador          │  . . . . O . │
│  . . P . . . │   entra a la  ──►     │  . . P . . . │  → Turnos empiezan
│  . . . . . . │   sala                │  . . . . . . │    automáticamente
│  . . . . . . │                       │  . . . . . . │
└──────────────┘                       └──────────────┘
                                        P=Player G=Goblin O=Orco
```

### 5.2 — Cuándo se activan los turnos

| Condición | Comportamiento |
|-----------|---------------|
| Sala sin enemigos | Movimiento libre (sin turnos). El jugador se mueve tile por tile. |
| Sala con enemigos | Turnos automáticos. Player Phase → Enemy Phase → repite. |
| Todos los enemigos muertos | Vuelve a movimiento libre. |

### 5.3 — Detección de sala: ¿cómo sabe el juego que "entraste"?

Esta es una definición **bloqueante para el prototipo**. Tres implementaciones posibles:

| Opción | Mecánica | Pros | Contras |
|---|---|---|---|
| **A: Room ID (salas cerradas)** | Cada sala es una entidad `RoomData` con un ID. El jugador "entra" cuando cruza un portal/puerta que cambia el room actual del `GridManager`. | Limpio, determinista. Se integra con el sistema de dungeon procedural (cada sala es una pieza). Ya existe `RoomData.cs` en el proyecto. | Requiere que las salas tengan bordes definidos (puertas/portales). No funciona en mapas abiertos. |
| **B: Trigger volumétrico** | Un BoxCollider invisible rodea el área de cada sala. El jugador dispara `OnTriggerEnter`, el juego chequea enemigos dentro del trigger, activa turnos. | Flexible, fácil de setear en el editor. Funciona con layouts irregulares. | Depende del diseñador. Propenso a errores (trigger mal puesto = combate no empieza). |
| **C: Rango de visión dinámico** | No hay "salas". El combate empieza cuando cualquier enemigo detecta al jugador dentro de un radio (ej: 5 tiles). | No requiere diseñar salas. Orgánico. Funciona en mapas abiertos. | Los enemigos pueden "activarse" a destiempo. Difícil de predecir. Rompe la metáfora de "sala con enemigos". |

**Recomendación para el prototipo: Opción A (Room ID).** El proyecto ya tiene `Assets/Scripts/Grid/RoomData.cs`. Solo falta definir el trigger de entrada (puerta/portal) y que el `CombatManager` escuche el cambio de sala y chequee enemigos.

**Regla de activación:**
```
OnRoomEnter(room):
  if room.enemies.Count > 0:
    CombatManager.StartTurns(player, room.enemies)
  else:
    CombatManager.FreeMovement()
```

### 5.4 — Spawn point: ¿dónde aparece el jugador al entrar?

Tres opciones:

| Opción | Mecánica | Pros | Contras |
|---|---|---|---|
| **A: Spawn fijo por sala** | Cada `RoomData` tiene un tile de spawn predefinido. El jugador aparece ahí siempre. | Determinista, predecible para balance. Diseñador controla el encuentro inicial. | El jugador no puede elegir ángulo de entrada. El posicionamiento previo no importa. |
| **B: Spawn por puerta usada** | Cada sala tiene N puertas. El jugador aparece en el tile adyacente a la puerta que cruzó. | Coherente con exploración — la dirección de entrada importa. | Requiere definir múltiples puntos de spawn por sala. Más complejo de balancear. |
| **C: Spawn libre dentro de un área segura** | Un área "entry zone" en la sala, el jugador aparece en cualquier tile de esa zona (el más cercano a su posición previa). | Máxima flexibilidad. | Incertidumbre en el balance del encuentro. |

**Recomendación para el prototipo: Opción B (spawn por puerta usada).** Es un punto medio: el jugador tiene agencia sobre el ángulo de entrada (eligiendo qué puerta cruzar), pero el diseñador controla los puntos de spawn posibles. En prototipo, cada sala puede tener 1-2 puertas definidas en su `RoomData`.

### 5.5 — Implicaciones

- **No hay "first strike" ni emboscada.** Si hay enemigos, empieza Player Phase. Siempre.
- **La Generala se juega en overlay.** Los dados aparecen sobre el mapa, el jugador reserva/tira, el resultado se aplica. El mapa sigue visible debajo.
- **El posicionamiento previo afecta el spawn (Opción B).** Entrar por la puerta norte o la sur cambia dónde aparecés en la sala — y por lo tanto qué enemigos están en rango en el primer turno.
- **Salir de una sala en combate no es posible** (por ahora). El jugador no puede volver a la puerta para huir — recordemos que no hay huida. Si la puerta es parte del área de la sala en combate, cruzarla implica mover el foco del combate a la otra sala, lo cual es desprolijo. **Decisión simple prototipo:** durante combate, las puertas están "bloqueadas" visualmente (TBD).

---

## 6. Comparativa: 3AP vs Opciones Anteriores del GDD

| Criterio | Opción A (2 acciones) | Opción B (1 acción) | **Propuesta 3 AP** |
|---|---|---|---|
| **Acciones por turno** | 2 combinables | 1 sola | 3 en orden fijo |
| **Mover + Atacar** | Sí | No (una u otra) | **Sí, siempre** |
| **Doble movimiento** | Sí | No | **No** |
| **Uso de ítems** | Reemplaza 1 acción | Consume el turno | Reemplaza AP2 (no ataca) |
| **Defensa** | TBD | TBD | **Integrada en AP3** (escudo) |
| **Huida** | TBD | TBD | **No existe** (OA la reemplaza) |
| **Contraataque al pegar** | No definido | No definido | **No existe** (solo en tu fase) |
| **Modo combate separado** | No definido | No definido | **No existe** (todo en mapa) |
| **Complejidad para jugador** | Media | Baja | Media |
| **Mapas chicos** | Riesgo (doble mov.) | Funciona | **Funciona** |

### ¿Qué resuelve?

1. **Cierra la indecisión** Opción A vs B — ni 2 libres ni 1 restrictiva.
2. **Defensa resuelta** — AP3 escudo para todos. Simple.
3. **Huida resuelta** — no existe. OA es el costo de reposicionarse.
4. **Contraataque resuelto** — no hay. Solo atacás en tu fase.
5. **"Modo combate" resuelto** — no hay transición. Todo en mapa.

---

## 7. Escenarios

### Escenario A — Guerrero a 4 losetas de un Goblin

| Fase | Acción |
|------|--------|
| **AP1** | Se mueve 3 losetas. Queda a 1 del Goblin (en rango melee). |
| **AP2** | **Ataca** — Generala. Saca Trío (28 × mult). Goblin recibe daño. |
| **AP3** | **Escudo** — tira d6. Saca 4. Tiene 4 de absorción. |
| **Enemy Phase** | Goblin ataca. Daño: 12. Escudo absorbe 4. Jugador recibe **8**. |

### Escenario B — Jugador rodeado, quiere alejarse

| Fase | Acción |
|------|--------|
| **AP1** | Se aleja del Orco. El Orco está en rango → **OA del Orco: 10 daño fijo**. El Goblin no está en rango (ya murió o está lejos). |
| **AP2** | Usa **Bolsa** — poción de vida. Se cura 15 HP. |
| **AP3** | **Escudo** — tira d6. Saca 5. |
| **Enemy Phase** | Orco se mueve hacia el jugador. Llega a rango. Ataca. Daño: 15. Escudo absorbe 5. Jugador recibe **10**. |

### Escenario C — OA del jugador contra enemigo

| Fase | Acción |
|------|--------|
| **AP1-AP3** | Jugador ejecuta turno. Queda adyacente al Orco. |
| **Enemy Phase** | Orco decide alejarse. Jugador está en rango → **OA del jugador: tira d8 → saca 6. Orco recibe 6 de daño.** |
| | Orco se mueve. Ataca desde nueva posición (si tiene rango). |

---

## 8. Preguntas Abiertas para el Equipo

| # | Pregunta | Prioridad |
|---|----------|-----------|
| 1 | **¿AP2 Atacar: elegir enemigo antes (ATK-A) o después (ATK-B) de tirar?** | **Crítica** — ver análisis en §2 |
| 2 | ¿Movimiento (AP1) usa dado o stat fijo por clase? | Alta |
| 3 | ¿Escudo (AP3) usa d6 fijo o el dado más grande de la build? | Media |
| 4 | ¿El escudo dura 1 ataque o todo el round? | Media |
| 5 | ¿OA máximo 1 por movimiento o ilimitado? | Alta |
| 6 | ¿OA del escudo protege o no? | Media |
| 7 | ¿AP3 eventualmente se diferencia por clase o queda escudo para todos? | Baja (futuro) |
| 8 | **¿El orden AP1→AP2→AP3 es estricto o puede reordenarse?** (ej: escudo preventivo antes de mover) | **Crítica — ver §8.1 abajo** |
| 9 | ¿Qué pasa si no hay enemigos en rango y no tenés ítems? ¿Se pierden AP2 y AP3? | Baja |
| 10 | ¿Los bosses tienen mecánicas especiales en su Enemy Phase? | Media (futuro) |

### 8.1 — Decisión crítica: orden fijo vs flexible de los AP

El documento asume AP1→AP2→AP3 estricto pero nunca justifica el orden. Esta es una decisión de diseño que **cambia la identidad del sistema**, no solo un detalle de UX.

**Opción A: Orden estricto AP1→AP2→AP3**

- El jugador **siempre** mueve primero, ataca segundo, defiende tercero.
- La defensa es reactiva: decidís escudo sabiendo qué hiciste en AP1 y AP2.
- Pros: simple de implementar, UI lineal, fácil de enseñar.
- Contras: el escudo nunca puede ser preventivo. Si sabés que vas a meterte en peligro, no podés "escudar primero y atacar después".

**Opción B: Orden flexible (cualquier permutación)**

- El jugador elige en qué orden ejecutar los 3 AP. Puede hacer AP3 → AP1 → AP2 (escudo preventivo antes de avanzar), o AP2 → AP3 → AP1 (atacar, escudar, retirarse), etc.
- Pros: mucha más profundidad táctica. El jugador puede planificar turnos complejos.
- Contras: UI más compleja (menú de selección de AP). Más difícil de comunicar. Posibles combos degenerados (escudar → atacar → escudar de nuevo si escudo se resetea mal).

**Opción C: Semi-flexible (AP1 obligatorio primero, AP2/AP3 intercambiables)**

- El movimiento siempre va primero (define el posicionamiento del turno), pero después el jugador elige si ataca o escuda primero.
- Pros: mantiene la claridad del "primero me muevo" pero permite decidir si escuda antes o después de atacar. Si escuda antes, el ataque enemigo posterior ya tiene escudo listo. Si escuda después, puede gastar el AP del escudo si no queda energía/motivo.
- Contras: punto medio — no resuelve el escudo preventivo pre-movimiento.

**Comparativa de decisiones tácticas que habilita cada una:**

| Jugada táctica | Orden estricto (A) | Flexible (B) | Semi-flexible (C) |
|---|---|---|---|
| Escudar antes de acercarse a un enemigo peligroso | ❌ | ✅ | ❌ |
| Atacar, ver si el enemigo murió, escudar solo si sigue vivo | ❌ (escudo siempre después) | ✅ | ✅ |
| Moverse a cubierta, escudar, atacar desde lejos | ❌ | ✅ | ❌ |
| Atacar, escudar, moverse lejos (para tener escudo activo durante el desplazamiento) | ❌ | ✅ | ❌ |

**Recomendación:** **Opción C (semi-flexible)** como punto de partida para el prototipo. Mantiene la simpleza del "mover primero" (que ayuda a orientar al jugador) pero le da la decisión AP2↔AP3 que es la más táctica (atacar primero y escudar con contexto, o escudar primero y atacar con defensa lista).

Si en playtest se siente restrictivo, se abre a Opción B completa. Si se siente caótico, se vuelve a Opción A estricta.

---

## 9. Riesgos y Mitigaciones

| Riesgo | Severidad | Mitigación |
|--------|-----------|------------|
| **Turno largo** — AP1 + Generala completa + escudo = muchos pasos | Alta | UI que guíe paso a paso. Escudo = 1 click automático. Generala es la parte central, lo demás es rápido. |
| **Sin huida = frustración** — sala imposible sin salida | Alta | Sala preview (ver enemigos antes de entrar) + tachar combo + ítems de emergencia |
| **OA frena el movimiento** — jugador se queda quieto siempre | Media | OA es daño fijo bajo. El escudo no lo bloquea pero el daño es manejable. Vale la pena moverse si la posición lo amerita. |
| **Escudo igual para todos = clases se sienten iguales** | Media | Las clases se diferencian por pasiva de combate (Generala). AP3 igual es temporal — se diferencia después del prototipo. |
| **ATK-B con 3 enemigos = siempre óptimo** — nunca desperdicias daño | Baja | Si es demasiado eficiente, usar ATK-A. O ATK-B pero sin ver HP de los enemigos antes de elegir (fog of war de stats). |

---

## 10. Plan de Acción

### Para presentar al equipo

1. Compartir este documento
2. **Decisión crítica**: ATK-A vs ATK-B (§2) — idealmente probar ambas en paper prototype
3. Definir: movimiento fijo o dado, escudo d6 o dado de build
4. Aprobar o modificar el flujo AP1→AP2→AP3

### Para prototipo

1. Implementar Player Phase → Enemy Phase (sin transición de combate)
2. AP1: movimiento fijo (3 losetas para todos)
3. AP2: atacar con Generala (empezar con ATK-A por simplicidad)
4. AP3: escudo d6 (1 ataque, no acumula, no protege vs OA)
5. OA: daño fijo para enemigos, 1 dado para jugador, máx 1 por movimiento
6. Testear en sala 7×7 con 1 Goblin + 1 Orco

---

*Última actualización: 2026-04-05*
*Propuesta para discusión con el equipo*
