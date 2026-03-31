# Game Design Document
### [Sin título] — Roguelite de Mazmorra con Dados
**Versión 2.0 — Marzo 2026 | Equipo UADE 2026**

---

## Resumen Rápido

Un roguelite por turnos donde **los dados son tu inventario, tu movimiento y tu arma**. El jugador construye un set de dados, explora mazmorras isométricas procedurales, y usa un sistema de combos estilo Generala/Yahtzee para atacar. La tensión central de cada turno es: **¿uso este dado para moverme, o lo guardo para un combo mejor?**

| | |
|--|--|
| **Género** | Roguelite / Dungeon Crawler por turnos |
| **Vista** | Isométrica fija |
| **Plataforma** | PC (itch.io / Steam) |
| **Motor** | Unity 6.3 (C#) |
| **Duración de run** | ~1h 40min (3-4 pisos, ~25 min c/u) |
| **Equipo** | 6 personas — Game Development UADE 2026 |

---

## Referencias de Diseño

| Juego | Qué tomamos |
|-------|------------|
| **The Binding of Isaac** | Sistema de ítems que rompen reglas. La base es simple; los ítems la transforman. |
| **Balatro** | Los combos base hacen daño moderado; el build los hace devastadores. |
| **Generala / Yahtzee** | Sistema de lock, retirada y combos para el combate. |
| **Hollow Knight** | Presupuesto de Poder como límite de carga (sistema de chapas). |

---

## Concepto Central

> **Los dados son todo.** No son azar — son el build, el inventario, la identidad.

La mecánica base es deliberadamente simple: tirás todos tus dados, elegís cuáles te mueven y usás el resto para Generala. Los ítems, pasivas y dados especiales rompen esas reglas de maneras interesantes — ahí está la profundidad.

**El núcleo funciona sin ítems.** Los ítems lo hacen devastador.

---

# Glosario de Términos

Estos términos tienen significado específico en el juego. Usarlos de forma consistente evita confusión entre el equipo.

| Término | Definición | NO usar |
|---------|-----------|---------|
| **Bolsa de Dados** | El loadout completo del jugador. Todos los dados que lleva. | Inventario, mochila |
| **Presupuesto de Poder** | Límite de costo total de la Bolsa de Dados (sistema de chapas). | Slots, capacidad |
| **Pick & Roll** | El sistema de turno: elegir dados de movimiento → moverse → Generala. | Puntos de acción |
| **Dados de Movimiento** | Los dados que el jugador elige de su tirada para moverse. Valor de cara = tiles. | Dados de velocidad |
| **Fase Generala** | La fase de lock/retirada/commit para el ataque. Reglas de Yahtzee. | Fase de ataque |
| **Combo** | La mano Generala que se forma al commitear. Determina el daño. | Mano, combinación |
| **Piso** | Un nivel completo de la mazmorra (8-14 salas). | Nivel, stage |
| **Sala** | Un espacio individual del piso (grilla 8×8). | Cámara, cuarto |
| **Run** | Una partida completa desde el inicio hasta victoria o derrota. | Match, sesión |
| **Modo Craps** | Mecánica de apuesta: predecir el combo antes de tirar cuando la barra de energía está llena. | Ultimate, especial |
| **Barra de Energía** | Se llena durante el combate. Al llegar a 100 habilita el Modo Craps. | Maná, carga |
| **Encantamiento de Cara** | Modificar una cara específica de un dado (en tienda o por ítem). | Upgrade, mejorar |
| **Enrabiado** | Estado del enemigo al llegar a energía máxima: 60% chance de hacer ×2 daño, luego resetea. | Potenciado, berserk |
| **Ataque de Oportunidad** | Daño de 1d6 que hace el enemigo al jugador cuando éste huye de la adyacencia. | Penalización de huida |

---

# Loop de Juego — 3 Capas

```
┌─────────────────────────────────────────────────────────────────┐
│  MICRO (cada turno)                                             │
│  Tirar dados → Elegir movimiento → Moverse → Generala → Daño   │
├─────────────────────────────────────────────────────────────────┤
│  MACRO (cada piso)                                              │
│  Explorar salas → Pelear → Farmear oro → Tienda → Jefe         │
├─────────────────────────────────────────────────────────────────┤
│  META (entre runs)                                              │
│  Desbloquear clases, dados, ítems cumpliendo hitos             │
└─────────────────────────────────────────────────────────────────┘
```

**Micro:** La tensión de cada turno. Cada dado tiene uso dual — moverte O atacar. Nunca los dos.

**Macro:** La exploración del piso. Salas de combate, tienda, jefe. El build crece con cada sala.

**Meta:** La progresión entre runs. Ganar desbloquea contenido. Perder igual tiene valor.

---

# Sistema de Combate — Pick & Roll

> Este es el núcleo del juego. Todo lo demás se construye sobre esto.

## La Grilla

- El mundo entero es una grilla permanente de tiles. No hay movimiento libre.
- La grilla está activa **tanto fuera como dentro del combate** — no hay transición de escena.
- El combate ocurre en la misma grilla de exploración.
- Adyacencia = 4 tiles cardinales (arriba/abajo/izquierda/derecha). Sin diagonales.
- Las salas tienen 4-6 obstáculos fijos (pilares, mesas, etc.) que bloquean el paso.

## Estructura del Turno

Cada turno del jugador sigue el flujo **Pick & Roll**:

```
┌────────────────────────────────────────────────────────────────┐
│  PASO 0 │ ¿APOSTAR?   Si energía=100, podés apostar tu combo   │
│         │             antes de tirar (opcional, decidís vos)   │
├────────────────────────────────────────────────────────────────┤
│  PASO 1 │ TIRAR       Tirás todos los dados de tu Bolsa        │
├────────────────────────────────────────────────────────────────┤
│  PASO 2 │ ELEGIR      Elegís 0 o más dados para moverte        │
│         │             (valor de cara = tiles, se suman)        │
├────────────────────────────────────────────────────────────────┤
│  PASO 3 │ MOVER       Te movés en la grilla (pathfinding BFS)  │
├────────────────────────────────────────────────────────────────┤
│  PASO 4 │ GENERALA    Con los dados restantes: lock, retirar   │
│         │             hasta 3 tiradas totales, commitear combo │
├────────────────────────────────────────────────────────────────┤
│  PASO 5 │ DAÑO        El mejor combo → fórmula → daño al       │
│         │             enemigo adyacente elegido                │
├────────────────────────────────────────────────────────────────┤
│  PASO 6 │ RESOLUCIÓN  Daño aplicado al enemigo elegido         │
├────────────────────────────────────────────────────────────────┤
│  PASO 7 │ TURNO ENE.  Cada enemigo se mueve y ataca si puede  │
└────────────────────────────────────────────────────────────────┘
```

**No hay sistema de puntos de acción (AP).** El turno entero es: elegir movimiento, moverse, Generala.

---

## Paso 0 — Modo Craps (Apuesta)

### Barra de Energía

La barra de energía empieza en 0 al inicio de cada combate y se llena con acciones:

| Acción | Energía ganada |
|--------|---------------|
| Hacer daño (cualquier combo) | +10 |
| Three of a Kind o mejor | +15 |
| Full House | +20 |
| Four of a Kind | +25 |
| Generala o Generala Doble | +50 |
| Recibir daño | +5 |
| Matar un enemigo | +10 |

Energía máxima: **100**. Al llegar a 100, el **Modo Craps se habilita**.

### La Apuesta

Cuando la energía está llena, **antes de tirar los dados**, el jugador puede:

- **Ignorar** — jugar normalmente. La energía se queda en 100 para el próximo turno.
- **Apostar** — elegir un tipo de combo que cree que va a sacar este turno.

### Resultado de la Apuesta

| Apuesta | Si la acertás | Si la fallás |
|---------|--------------|-------------|
| Par | +25% daño | -10% daño |
| Three of a Kind | +50% daño | -15% daño |
| Escalera | +50% daño + curar 10 HP | -15% daño |
| Full House | +75% daño | -20% daño |
| Four of a Kind | +100% daño (×2) | -25% daño + pierdo 5 HP |
| Generala | +200% daño (×3) + curar 20 HP | -50% daño + pierdo 10 HP |

- **"O mejor" cuenta como acierto.** Apostás Par pero sacás Four of a Kind → acierto.
- Luego de resolverse (acierto o fallo), la energía vuelve a 0.
- **Tensión clave:** los dados que usás para moverte salen del pool de Generala → apostar combos altos cuando necesitás moverte es una apuesta grande.

> **Nota de diseño:** El Modo Craps es opt-in — jamás estás obligado a usarlo. Refuerza la identidad casino: cada combate tiene un momento de "todo o nada" potencial.

---

## Paso 1 — Tirar Todos los Dados

- Al inicio del turno tirás **todos** los dados de tu Bolsa simultáneamente.
- Ves TODOS los resultados antes de tomar cualquier decisión.
- Esta es la única tirada para movimiento — **no hay dado de velocidad separado**.

**Ejemplo** (4×d6 + 2×d8):
```
Resultado: [4] [2] [5] [3] [6] [3]
```
El jugador ahora decide cómo dividir estos 6 dados.

---

## Paso 2 — Elegir Dados de Movimiento

- El jugador elige **0 o más dados** de la tirada para usarlos en movimiento.
- **Valor de cara = tiles.** Un dado que muestra 5 = 5 tiles de movimiento.
- **Múltiples dados se SUMAN.** Elegir [3] y [2] = 5 tiles en total.
- Los dados elegidos **se sacan del pool de Generala** — no pueden usarse para atacar.
- **El jugador puede elegir 0 dados** — saltear el movimiento y guardar todos para Generala. Útil cuando ya estás adyacente a un enemigo.
- El movimiento es siempre un **camino único continuo** (no se divide). Movimiento dividido = territorio de ítems.
- Los tiles de movimiento que no usás se pierden — no se acumulan.

**Decisión estratégica:** identificar qué dados no aportan a un buen combo y sacrificarlos para moverse. Un [2] que rompe tu Escalera es mejor usado como 2 tiles.

---

## Paso 3 — Moverse

- El jugador se mueve en la grilla. El camino se calcula automáticamente sorteando obstáculos.
- Total de tiles = suma de los dados de movimiento elegidos.
- El jugador debe terminar en un tile válido (no ocupado, no obstáculo).
- **Primero mover, después atacar — siempre.** Atacar y luego moverse = territorio de ítems (Hit & Run).

---

## Paso 4 — Fase Generala

Después de moverse, los dados restantes entran en la **Fase Generala**. Reglas de Yahtzee:

### Secuencia de Tiradas

1. **Tirada 1:** Los valores del Paso 1 (ya tirados). Estos cuentan como la primera tirada.
2. El jugador **lockea** los dados que quiere conservar.
3. **Retirada:** Los dados sin lockear se vuelven a tirar.
4. Se repite hasta **3 tiradas totales** (la inicial + 2 retiradas máximo).
5. **Commit:** El jugador confirma su mano final.

### Reglas

- El jugador puede lockear/desbloquear cualquier dado entre tiradas.
- Puede commitear antes de las 3 tiradas si está satisfecho.
- Si el jugador **no está adyacente a ningún enemigo** al terminar de moverse, la Fase Generala se saltea — los dados se pierden. Posicionarse mal tiene costo.
- Con menos dados es más difícil formar combos, pero sigue siendo posible (un Par solo necesita 2 dados).

---

## Paso 5 — Tabla de Combos y Daño

El sistema detecta automáticamente el mejor combo de los dados commitados.

| Combo | Requisito | Fórmula de Daño | Ejemplo con d6 |
|-------|-----------|-----------------|----------------|
| **Dado Mayor** | Sin combo | Cara más alta × 1 | [6] = **6 dmg** |
| **Par** | 2 iguales | Suma del par × 1.5 | [4,4] = **12 dmg** |
| **Doble Par** | 2+2 iguales | Suma de ambos × 1.2 | [3,3,5,5] = **19 dmg** |
| **Three of a Kind** | 3 iguales | Suma del trío × 2 | [5,5,5] = **30 dmg** |
| **Escalera** | 4+ consecutivos | 30 + el dado más alto | [3,4,5,6] = **36 dmg** |
| **Full House** | 3+2 iguales | 35 + suma de todos | [4,4,4,6,6] = **59 dmg** |
| **Four of a Kind** | 4 iguales | Suma de los cuatro × 3 | [5,5,5,5] = **60 dmg** |
| **Generala** | 5 iguales | Suma × 5 | [5,5,5,5,5] = **125 dmg** |
| **Generala Doble** | 6 iguales | Suma × 8 | [5,5,5,5,5,5] = **240 dmg** |

> **Filosofía Balatro:** El daño base es moderado. Los ítems, el encantamiento de caras y las pasivas hacen los combos devastadores. Un Par base hace 12 de daño; un Par encantado con la pasiva correcta puede hacer 40+.

### Bonus de Afinidad

Cada clase tiene un **combo de afinidad**. Cuando el jugador saca ese combo, el daño recibe un bonus porcentual. Ejemplo: el Guerrero tiene afinidad por Full House (+20% daño).

---

## Paso 6 — Resolución de Daño

- El mejor combo se detecta automáticamente.
- El daño se aplica al **enemigo adyacente elegido** por el jugador.
- Si hay múltiples enemigos adyacentes, el jugador elige a cuál atacar.
- El daño sobrante al matar a un enemigo **no se transfiere** a otros (cleave = ítem).

---

## Paso 7 — Turno del Enemigo

Después del turno del jugador, cada enemigo vivo actúa:

1. **Movimiento:** El enemigo tira su movimiento (rango variable según tipo) y se mueve hacia el jugador por el camino más corto posible.
2. **Ataque:** Si está adyacente al jugador, tira sus dados de ataque. El resultado **es el daño directo** (los enemigos siempre golpean — sin chance de fallo).
3. **Enrage:** Si su energía está al máximo → 60% de chance de hacer ×2 daño, luego la energía resetea a 0.

**Los enemigos solo se activan cuando el jugador está en la misma sala.** Los enemigos de otras salas están congelados.

---

## Combate en Adyacencia y Ataque de Oportunidad

### Estar en Combate

- El jugador está "en combate" cuando hay un enemigo adyacente.
- Puede seguir moviéndose usando dados de movimiento, pero si se aleja de la adyacencia, **se activa el Ataque de Oportunidad**.

### Ataque de Oportunidad

Cuando el jugador **huye de la adyacencia** (ya sea moviendo o usando la acción de Huir):

- **El enemigo tira 1d6** → ese resultado es daño directo al jugador.
- No importa si la huida tiene éxito — el Ataque de Oportunidad siempre ocurre.
- **Ítem Smoke Bomb:** niega el Ataque de Oportunidad del enemigo (el jugador se escapa sin recibir el 1d6).

> El costo de huir siempre existe. Esto crea una decisión real: ¿me quedo y combo, o me voy y como el 1d6?

---

## Qué NO está en el Núcleo (se añade con Ítems)

La siguiente lista son mecánicas que **no existen en la base del juego**. Solo aparecen al encontrar o comprar el ítem correspondiente:

| Mecánica | Cómo entra al juego |
|----------|---------------------|
| Defensa / reducción de daño | Ítems: Escudo, Armadura, etc. |
| Curación | Ítems: Poción, dados vampíricos, etc. |
| Evasión | Ítems: Capa de Esquiva, etc. |
| Ataque a distancia | Ítems: Arco, Varita, etc. |
| Hit & Run (moverse después de atacar) | Ítems: Botas de Escape, etc. |
| Negar Ataque de Oportunidad | Ítem: Smoke Bomb |
| Cleave (daño a todos los adyacentes) | Ítems: Espada Torbellino, etc. |
| Movimiento dividido | Ítems: Paso Sombra, etc. |
| Dados extra | Ítems que agregan dados a la Bolsa |
| Modificadores de combo | Ítems que cambian multiplicadores |
| Encantamiento de Caras | Servicio de tienda / ítem |

---

# Bolsa de Dados — El Inventario

## Qué es

La Bolsa de Dados es el loadout del jugador — su build, su identidad, su toolkit. Todos los dados de la Bolsa se tiran cada turno y se dividen entre movimiento y Generala.

## Sistema de Presupuesto de Poder

Cada dado tiene un **costo de poder**. El total de dados del jugador no puede superar su **Presupuesto de Poder** (igual al sistema de chapas de Hollow Knight).

| Tipo de Dado | Costo de Poder | Rango de Caras |
|-------------|---------------|----------------|
| d6 | 1 | 1-6 |
| d8 | 2 | 1-8 |
| d10 | 3 | 1-10 |
| d12 | 4 | 1-12 |

**Presupuesto inicial (Guerrero): 8 puntos.** Configuración por defecto: 4×d6 + 2×d8 = (4×1) + (2×2) = 8 puntos.

## Reglas de la Bolsa

- Todos los dados se tiran cada turno — no se "equipan" individualmente por turno.
- Más dados = más opciones de movimiento Y más chances de formar combos.
- Dados más grandes (d10, d12) = valores de cara más altos = más movimiento Y combos con números más grandes.
- Los ítems pueden agregar dados a la Bolsa o aumentar el Presupuesto.
- La Bolsa crece durante la run a través de tiendas, botines y recompensas.

## Ejemplos de Build

| Build | Dados | Presupuesto | Estrategia |
|-------|-------|-------------|------------|
| Starter (Guerrero) | 4×d6 + 2×d8 | 8 | Balanceado. Bueno para Pares y Three of a Kind. |
| Velocidad | 6×d6 | 6 | Muchos dados bajos = mucho movimiento + combos consistentes. |
| Potencia | 2×d6 + 2×d10 | 8 | Menos dados pero valores altos = combos devastadores, movimiento difícil. |
| Expansión | 4×d6 + 2×d8 + 1×d10 | 11 | Requiere ítem de presupuesto extra. 7 dados = muy flexible. |

---

# Encantamiento de Caras (Ítem / Tienda)

El encantamiento de caras es un **servicio de ítem o tienda**, no una mecánica base.

Permite modificar una cara específica de un dado:

| Tipo de Encantamiento | Efecto |
|-----------------------|--------|
| Aumento de valor | +1 a +3 en una cara específica |
| Fijar valor | Fijar una cara a un número específico |
| Eliminar cara | Quitar una cara (menos caras = promedio más alto) |
| Multiplicador | Esa cara hace más daño en combos |
| Efecto especial | Efecto único en esa cara (veneno, curación, escudo, etc.) |

> **Paralelo Balatro:** El encantamiento es cómo los combos base se vuelven devastadores. Un trío de 7s encantados hace muchísimo más que un trío de 5s base.

*Costos y disponibilidad: a definir en balance.*

---

# Sistema de Ítems

## Filosofía (Modelo Isaac)

Los ítems son la capa de complejidad. La base es simple — los ítems **rompen las reglas** de maneras interesantes. Cada ítem debería hacer sentir al jugador que está "haciendo trampa" al sistema.

**Nota importante:** Como Pick & Roll no tiene defensa base, los ítems de defensa, curación y reducción de daño son **fundamentales para sobrevivir** pisos enteros. Esto crea una demanda fuerte de la tienda y hace que cada drop sea significativo.

## Fuentes de Ítems

- **Salas de Tienda** — compra con Oro.
- **Recompensas de combate** — drop aleatorio al matar un enemigo (no garantizado).
- **Salas de Sacrificio** — perdés HP máximo a cambio de un ítem aleatorio (**ciego** — no ves qué obtenés).
- **Drop del Jefe** — 1 ítem aleatorio del pool del jefe (garantizado).

## Categorías de Ítems

### Dados
Modifican los dados o agregan nuevos.
- *Dado Extra d6* — agrega un d6 a la Bolsa (+1 costo de poder)
- *Dado Cargado* — un dado siempre muestra su cara máxima (cuesta 2× poder)
- *Dado Camaleón* — este dado copia el valor de cualquier otro dado adyacente en el combo

### Defensa
Agregan reducción de daño o bloqueo (no existe en la base).
- *Escudo de Madera* — reduce el daño entrante en 2
- *Armadura de Hierro* — el primer golpe de cada sala hace 0 daño
- *Capa de Esquiva* — 25% de chance de evadir cualquier ataque
- *Escudo de Par* — al sacar Par, ganás escudo igual al valor del par

### Curación
Restauran HP — la única forma de curarse.
- *Poción* — cura 15 HP (consumible, usos limitados por piso)
- *Dados Vampíricos* — los combos Generala curan el 10% del daño hecho
- *Trébol de la Suerte* — cura 5 HP al sacar Escalera o mejor

### Movimiento
Rompen las restricciones de movimiento.
- *Botas de Escape* — después de atacar, te movés 2 tiles gratis
- *Paso Sombra* — dividís el movimiento en 2 caminos separados
- *Botas Magnéticas* — +1 tile al movimiento total cada turno

### Combate
Modifican las reglas de ataque.
- *Arco* — atacás enemigos hasta 3 tiles de distancia (a distancia)
- *Espada Torbellino* — el daño del combo Generala golpea a TODOS los adyacentes
- *Daga de Sombra* — al matar, teleportás a un tile adyacente del enemigo muerto
- *Hacha Berserker* — +50% daño de combo cuando HP < 30%

### Modificadores de Combo
Cambian cómo funcionan los combos Generala.
- *Anillo de Full House* — Full House sube de 35+suma a 50+suma
- *Especialista en Pares* — Pares hacen ×2.5 en vez de ×1.5
- *Flush de Escalera* — si tu Escalera usa todos dados del mismo tipo, daño doble
- *Lucky Seven* — cualquier dado que muestre 7 es comodín (vale para cualquier valor)

### Pasivas
Modificadores siempre activos.
- *Imán de Oro* — los enemigos dropean 50% más oro
- *Explorador* — ves el HP y la acción planeada de cada enemigo antes de tu turno
- *Piel Gruesa* — +10 HP máximo

### Ítems Especiales / Rompe-Reglas
Alteran fundamentalmente las reglas del juego.
- *Loop Temporal* — si tu Fase Generala resulta en Dado Mayor solamente, retirar gratis
- *Frenesí de Batalla* — tirás tus dados dos veces este turno, elegís el mejor set
- *Combo Chain* — si sacás Three of a Kind o mejor, ganás un dado de daño bonus (cara = daño extra)

## Ítems del Prototipo (5 Confirmados)

Estos 5 ítems son el mínimo para el prototipo. Todos viven en el espacio de dados/combos:

| Ítem | Categoría | Efecto | Precio | Por qué existe |
|------|-----------|--------|--------|----------------|
| **Especialista en Pares** | Combo | Multiplicador de Par sube de ×1.5 a ×2.5 | 30g | Hace viable el combo más común. Build-around con muchos dados del mismo tipo. |
| **Dado Cargado** | Dado | Un dado siempre muestra su cara máxima. Cuesta 2× presupuesto. | 35g | Garantiza un valor alto para movimiento O combo. Trade-off: caro en presupuesto. |
| **Token de Retirada** | Dado | +1 retirada extra en Generala (4 totales en vez de 3) | 25g | Control puro. Más chances de construir combos. Simple, siempre útil. |
| **Combo Chain** | Combo | Si sacás Three of a Kind o mejor → tirás 1 dado extra de daño (cara = daño bonus) | 40g | Premia los combos fuertes con daño adicional. Snowball moderado — es 1 dado. |
| **Smoke Bomb** | Escape | Al salir de adyacencia, niega el Ataque de Oportunidad del enemigo (vos seguís haciendo el tuyo) | 20g | Hace de la huida una táctica viable. Sin él, huir siempre cuesta vida. |

**Sinergia de ejemplo:** Especialista en Pares + Dado Cargado en una Bolsa de 6×d6 → el Dado Cargado siempre muestra 6, los otros 5 tienen alta chance de Par → daño de Par consistente con ×2.5.

*Lista completa de ítems: a definir — se necesitan ~20 ítems para el vertical slice.*

---

# Sistema de Pasivas

Las pasivas son modificadores siempre activos que el jugador adquiere durante la run (tiendas, recompensas, salas especiales). Se acumulan e interactúan entre sí y con dados especiales, creando cadenas de sinergia.

Ejemplos de categorías:
- Aumentar multiplicadores de combo
- Ganar oro extra por combate
- Efectos al cumplir condiciones (en Par, en Generala, en kill)
- Modificar la Bolsa de Dados o el Presupuesto

*Pasivas específicas: a definir.*

---

# Personajes — Clases

## Filosofía

Las clases definen un playstyle sin forzar un build único. La clase indica qué tipo de combo es "natural" para ese personaje, pero el jugador puede construir libremente.

## Clases

| Clase | Dados Iniciales | Presupuesto | HP | Combo de Afinidad | Pasiva de Clase |
|-------|----------------|-------------|-----|-------------------|-----------------|
| **Guerrero** | 4×d6 + 2×d8 | 8 | 100 | Full House (+20% dmg) | A definir |
| **Mago** | 2×d6 + 3×d8 | 8 | 80 | Escalera (+20% dmg) | A definir (posible: retirada extra por turno) |
| **Pícaro** | 6×d6 | 6 | 90 | Par (+30% dmg) | A definir (posible: +2 tiles de movimiento) |

Las clases Mago y Pícaro se desbloquean mediante meta-progresión.

**Prototipo:** Solo Guerrero con stats fijas para testear todas las mecánicas.

---

# Dados Especiales

Además de los dados numerados estándar, algunos dados encontrados durante la run tienen **efectos integrados** que se activan en resultados específicos.

Ejemplos:
- Un dado que hace daño de fuego al caer en su cara máxima.
- Un dado que cura al jugador cuando cae en 1.
- Un dado que potencia los dados adyacentes en el combo.

Los dados especiales siguen las reglas de Presupuesto de Poder y se tiran junto con todos los demás dados. Pueden usarse para movimiento O Generala.

*Tipos específicos de dados especiales: a definir.*

---

# Enemigos y Jefes

## Diseño de Enemigos

Cada enemigo combina temática de fantasía clásica con estética casino.

### Tabla de Enemigos

| Enemigo | Tier | Dados de Ataque | Movimiento | HP | Comportamiento | Aparece en |
|---------|------|----------------|------------|-----|----------------|------------|
| **Croupier Goblin** | Débil | 2×d6 (2-12 dmg) | 1-3 tiles | 40 | Agresivo: se mueve hacia el jugador | Piso 1 |
| **Chip Golem (Orc)** | Fuerte | 2×d8 (2-16 dmg) | 1-2 tiles | 60 | Agresivo: lento pero pega fuerte | Piso 1 |
| **Card Archer** | Normal | 1×d8 ranged (con % de precisión) | 1-2 tiles | 30 | Rangeado: huye si el jugador está adyacente, ataca desde lejos | Piso 2+ |
| **Dado Viviente** | Normal | A definir | A definir | A definir | A definir | Piso 1 |

### Cómo Atacan los Enemigos

- Los enemigos **siempre golpean** — sin chance de fallo, sin umbral.
- Tiran sus dados de ataque y el resultado **es el daño directo**.
- **Sin reducción de daño en la base** — el daño completo llega al jugador (los ítems agregan defensa).

### Movimiento de Enemigos

- Cada enemigo tira para moverse en su turno (1-N tiles según tipo).
- Siempre se mueven hacia el jugador por el camino más corto posible, rodeando obstáculos.
- Solo se activan cuando el jugador está en la misma sala.

### Energía y Enrage de Enemigos

Cada enemigo tiene su propia barra de energía que se llena pasando rondas:

| Enemigo | Energía Máxima | Energía por Ronda |
|---------|----------------|------------------|
| Croupier Goblin | 50 | +15 |
| Chip Golem (Orc) | 40 | +12 |
| Card Archer | 40 | +10 |

Cuando la energía llega al máximo → **Enrabiado**: 60% de chance de hacer ×2 daño ese turno, luego la energía vuelve a 0. Esto crea urgencia — dejar a los enemigos vivos los hace más peligrosos con el tiempo.

### Cantidad de Enemigos por Sala

- **Piso 1:** 1-2 enemigos por sala de combate.
- **Piso 2:** 1-3 enemigos por sala de combate.
- **Piso 3+:** 1-3 enemigos (tiers más difíciles, posibles élites).

## Diseño de Jefes

Los jefes **no son simplemente enemigos con más vida**. Tienen mecánicas únicas que cambian las reglas del combate:
- Modificar los dados del jugador (maldecir caras, reducir valores temporalmente).
- Empezar con minions ya en la sala.
- Cambiar sus propios stats entre fases.
- Aplicar debuffs (Presupuesto de Poder reducido, caras malditas).

Cada jefe obliga al jugador a adaptar su estrategia.

### El Dealer (Jefe del Piso 1)

**Stats:** 100 HP | 2×d8 ataque | Velocidad 1-2

**Setup de sala:** El Dealer + 2 Croupier Goblins (40 HP, 2×d6, velocidad 1-3) ya en la sala al inicio.

| Fase | HP | Mecánica |
|------|----|----------|
| **Fase 1** | 100→40 | Comportamiento normal. Los Goblins hacen el trabajo sucio mientras El Dealer pega fuerte. |
| **Fase 2** | 40→0 | **Maldice 2 dados del jugador:** -2 de valor en Generala (pero valor completo para movimiento). La maldición dura 2 turnos. Velocidad aumenta a 2-3. |

**Decisión táctica:** ¿Mato primero a los Goblins (más seguro pero más rondas = más acumulación de Enrage del Dealer) o me voy directo al Dealer (arriesgado pero termina el combate antes)?

**Interacción de la maldición con Pick & Roll:** Los dados malditos valen -2 en Generala pero completo para movimiento → el jugador es incentivado a usar los dados malditos para moverse y guardar los limpios para combos. Es una mecánica del espacio de dados, consistente con la identidad del juego.

*Jefes adicionales: a definir — se necesita al menos 1 más para el Piso 2.*

---

# Estructura de la Mazmorra

## Generación de Pisos

La mazmorra se genera proceduralmente al estilo The Binding of Isaac:

- **8-14 salas** conectadas en una grilla.
- El jugador ve el minimapa desde el inicio (con salas adyacentes no descubiertas como contorno).
- Cada sala = grilla 8×8 con 4-6 obstáculos fijos.
- El piso termina al derrotar al jefe.

## Tipos de Sala

| Tipo | Ícono en Minimapa | Descripción | Frecuencia |
|------|-------------------|-------------|------------|
| **Combate** | *(sin ícono)* | Enemigos en la grilla. Combate por turnos. Recompensa: oro + chance de ítem. | Mayoría |
| **Jefe** | **B** | Mecánicas únicas. Drop garantizado de ítem. Desbloquea el siguiente piso. | 1 por piso |
| **Tienda** | **T** | Comprás ítems, dados, encantamientos y pasivas con Oro. | 1-2 por piso |
| **Sacrificio** | *(sin ícono confirmado)* | Perdés HP máximo a cambio de un ítem aleatorio (ciego — no sabés qué obtenés). | Raro |

## Persistencia de Enemigos

- Al salir de una sala sin limpiarla: los enemigos conservan su HP al volver.
- Las posiciones de los enemigos se randomizan al re-entrar.
- Los enemigos muertos no reaparecen.

## Minimapa

Siempre visible en el HUD:
- Salas descubiertas = tile lleno.
- Salas adyacentes no descubiertas = contorno vacío.
- Conexiones de puerta visibles entre tiles.
- Solo Tienda (T) y Jefe (B) tienen ícono. El resto son tiles en blanco.

---

# Flujo Completo de una Run

```
Inicio
 └── Selección de clase y armado de Bolsa de Dados
      └── Piso 1 (8-14 salas)
      │    ├── Salas de Combate (farmear oro, chance de ítem)
      │    ├── Sala de Tienda (gastar oro)
      │    ├── Sala de Sacrificio (riesgo/recompensa)
      │    └── Sala de Jefe → derrotar → reward screen → Piso 2
      └── Piso 2 (ídem, enemigos más fuertes, archers aparecen)
           └── ...
                └── Piso 3/4 → Jefe Final → Victoria
```

**Derrota:** HP llega a 0 en cualquier momento → fin de la run.
**Victoria:** Derrotar al jefe del piso final.

---

# Sistema de Tienda

## Cómo Funciona

- Los ítems de la tienda están distribuidos en el suelo de la sala (como objetos del grid).
- Al acercarse a un ítem, aparece su descripción completa y el botón de compra.
- Se compra con Oro acumulado.
- Los ítems que no se compran **persisten** si el jugador sale y vuelve.
- La tienda puede ofrecer: ítems, dados extra, servicio de encantamiento de caras, pasivas.

## Calibración de Precios

- Ítem estándar ≈ recompensa de 3-4 enemigos normales.
- Ítem premium ≈ recompensa de 5 enemigos.
- Los ítems de defensa son ligeramente más caros (alta demanda por no haber defensa base).

---

# Economía de Oro

- Los enemigos dropean Oro al morir — la cantidad varía por tier.
- El Oro se recolecta automáticamente al terminar el combate.
- El total de Oro siempre está visible en el HUD.
- Se gasta en Salas de Tienda.

| Tier de Enemigo | Drop de Oro |
|----------------|-------------|
| Débil | 3-7 oro |
| Normal | 7-13 oro |
| Fuerte | 12-18 oro |
| Jefe | 40-60 oro |

---

# Sistema de Vida

## Daño

- **Ataques enemigos** — el resultado del dado del enemigo es daño directo. Sin reducción en la base.
- **Salas de Sacrificio** — perdés HP máximo al entrar.
- **Ataque de Oportunidad al huir** — el enemigo tira 1d6 como daño directo.
- **Fallo en apuesta de Modo Craps (combos altos)** — algunas apuestas fallidas cuestan HP directamente.

## Curación

- **Solo a través de ítems.** Poción, efectos de lifesteal, pasivas.
- No se puede superar el HP máximo.
- **No hay curación base** — el jugador debe encontrar ítems para curarse.

> **Nota de balance:** Sin ítems de defensa, el jugador recibe ~50 de daño por sala de 3 enemigos. Con 100 HP puede sobrevivir ~2 salas antes de necesitar curación o defensa. Esto crea presión fuerte para visitar tiendas y tomar riesgos calculados en salas de sacrificio.

---

# Dirección de Arte

- **Estilo:** Pixel-Poly (3D Low Poly + Pixel Art Shader).
- **Vista:** Isométrica fija, sin rotación de cámara.
- **Paleta:** Tonos oscuros + neones de casino (verde de mesa, rojo de dado, dorado de fichas).
- **Excepción visual:** Los dados ruedan con física real al tirarse — son el protagonista visual.
- **Jerarquía visual:** Dados > Jugador > Enemigos > Entorno.
- Cada piso tiene estética temática de casino distinta *(a definir por el equipo de arte)*.

---

# Feedback Visual y Feel

- Cada tirada tiene impacto — los dados golpean la mesa con física, rebotan, se asientan. La pantalla reacciona.
- Los combos explotan visualmente — Generala llena la pantalla de efectos, Poker la sacude.
- La decisión de split (mover vs atacar) crea tensión visible — los dados se arrastran desde el pool hacia los slots de movimiento.
- Los números de daño escalan visualmente con su valor.
- La tensión de apuesta nunca para — cada Fase Generala es una apuesta sobre si conservar o retirar.

---

# UI — Elementos del HUD

| Elemento | Función |
|----------|---------|
| **Contador de Oro** | Siempre visible |
| **Barra de HP** | Vida del jugador |
| **Barra de Energía** | Llena → Modo Craps disponible |
| **Pool de Dados** | Muestra todos los dados tirados con sus valores actuales |
| **Preview de Movimiento** | Tiles resaltados al elegir dados de movimiento |
| **Panel Generala** | Panel lateral con dados lockeados/libres, contador de tiradas, preview de combo y daño |
| **Barras de HP enemigos** | Visibles cuando están en rango |
| **Minimapa** | Layout del piso con estado de descubrimiento |

## Layout del Combate

```
┌──────────────────────────────────────────┐
│  ┌──────────────┐  ┌────────────────────┐│
│  │              │  │  PANEL GENERALA    ││
│  │   GRILLA     │  │                    ││
│  │  (siempre    │  │  [3][5][6]         ││
│  │   visible)   │  │  Lock: ☑ ☐ ☑       ││
│  │              │  │  Tirada 2/3        ││
│  │  J──→G  O    │  │  Combo: Par        ││
│  │       A      │  │  Daño: 12          ││
│  └──────────────┘  └────────────────────┘│
│  ┌────────────────────────────────────┐  │
│  │  POOL: [4][2][5][3][6][3]         │  │
│  │  MOVER: [arrastrá dados acá]=tiles │  │
│  └────────────────────────────────────┘  │
└──────────────────────────────────────────┘
```

---

# Audio y Atmósfera

## Efectos de Sonido (SFX)
- Dados rodando sobre la mesa (físicamente)
- Dados aterrizando / asentándose
- Confirmación de combo (impacto satisfactorio, escala con el tier del combo)
- Fanfarria de Generala
- Daño dado / recibido
- Muerte de enemigo
- Recolección de oro
- Interacciones de UI (arrastrar dado, click de lock)

## Música y Ambience
- Música de fondo temática de casino por piso
- Capas de intensidad en combate
- Temas para encuentros con jefes
- Música ambiental de tienda

## Efectos Visuales (VFX)
- Dados con física de rebote y asentamiento
- Efectos por tier de combo (Par = destello pequeño, Generala = explosión de pantalla)
- Números de daño que escalan con el valor
- Oscurecimiento de pantalla en daño pesado
- Camera shake en combos grandes
- Efectos de muerte de enemigos (ráfaga de monedas)
- Trail de movimiento en la grilla
- Dado brillando cuando está lockeado

---

# Tutorial

## Cómo se Enseña el Juego

- Primera sala: enseña a tirar dados, elegir movimiento, moverse en la grilla.
- Segundo combate: enseña la Fase Generala, lockear, retirar, combos.
- La tabla de combos y daño siempre accesible desde el menú de pausa.
- Tooltips sobre dados, ítems y combos.
- Las descripciones de ítems explican qué regla rompen.

*Diseño de sala tutorial e introducción progresiva de mecánicas: a definir.*

---

# Progresión Meta (Entre Runs)

**Modelo Isaac.** Cada run desbloquea contenido nuevo a través de hitos:
- Derrotar jefes.
- Completar desafíos (ej. "Ganar con solo d6", "Nunca usar Modo Craps").
- Alcanzar logros.

Todo el contenido desbloqueado (clases, dados, ítems) entra al pool de generación de las runs futuras.

*Hitos específicos y condiciones de desbloqueo: a definir.*

---

# Fórmulas Técnicas

### Flujo Pick & Roll (pseudocódigo)
```
1. todos_dados = BolsaDeDados.TirarTodos()
2. dados_movimiento = JugadorElige(todos_dados)          // 0 o más
3. tiles_movimiento = Suma(dados_movimiento.valor_cara)
4. Jugador.Mover(tiles_movimiento)                       // BFS
5. dados_generala = todos_dados - dados_movimiento
6. si Jugador.EstáAdyacenteAEnemigo():
7.     combo = Generala(dados_generala, max_tiradas=3)   // lock/retirar
8.     daño = FórmulaDaño(combo) + BonusAfinidad
9.     enemigo_objetivo.RecibirDaño(daño)
10. sino:
11.     // Fase Generala salteada — dados perdidos
```

### Combo de Afinidad
```
si combo.Tipo == personaje.ComboAfinidad:
    daño = daño × personaje.MultiplAfinidad
```

### Enrage de Enemigo
```
si enemigo.Energía >= enemigo.EnergíaMax:
    si Random(0,1) < 0.6:
        daño_ataque = daño_ataque × 2
    enemigo.Energía = 0
```

### Ataque de Oportunidad (al huir)
```
daño_oportunidad = Tirar(1d6)
jugador.RecibirDaño(daño_oportunidad)
// Si el jugador tiene Smoke Bomb: saltear esta línea
```

---

# Items Pendientes de Definición

Las siguientes secciones están marcadas como "a definir" y requieren decisión del equipo antes del vertical slice:

| Sección | Estado |
|---------|--------|
| Pasivas específicas | A definir |
| Dados especiales (tipos y efectos) | A definir |
| Lista completa de ítems (~20 para vertical slice) | A definir |
| Jefe del Piso 2+ | A definir |
| Pasivas de clase (Guerrero, Mago, Pícaro) | A definir |
| Sistema de meta-progresión (hitos específicos) | A definir |
| Tutorial interactivo | A definir |
| Pantallas de menú, pausa, victoria, derrota | A definir |
| Sistema de guardado (meta-progresión entre runs) | A definir |
| XP / subida de nivel intra-run | Evaluar tras playtest del core loop |

---

*Documento vivo — se actualiza con cada iteración del diseño.*
