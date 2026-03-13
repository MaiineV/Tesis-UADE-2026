# GAME DESIGN DOCUMENT — Roguelite de Mazmorra con Dados

**Versión 1.0 — Marzo 2026**

---

## 1. Visión General del Juego

El juego es un roguelite de mazmorra con vista isométrica o top-down (referencia: Crypt of the NecroDancer / The Binding of Isaac) en el que todos los sistemas giran alrededor de dados: movimiento, combate, inventario y progresión. El jugador explora mazmorras procedurales sala por sala, enfrentándose a enemigos mediante un sistema de combate inspirado en la Generala y configurando su set de dados como si armara un build con chapas de Hollow Knight.

### 1.1 Pilares de Diseño

- **Dados como recurso central:** cada dado es un objeto físico del inventario con costo, tamaño y potencial. El jugador nunca tira dados abstractos; siempre tira los dados que equipó.
- **Risk / Reward constante:** desde la configuración del pouch hasta el modo Craps en combate, cada decisión ofrece una apuesta calculada.
- **Builds emergentes:** la combinación de clase + dados + pasivas + ítems genera sinergias únicas en cada run.
- **Rejugabilidad roguelite:** desbloqueo meta-progresivo de dados, clases e ítems al cumplir hitos y derrotar jefes.

---

## 2. Flujo de Inicio de Partida

Al comenzar una nueva run, el jugador pasa por tres pantallas secuenciales antes de entrar a la mazmorra.

### 2.1 Pantalla 1 — Selección de Clase

El jugador elige entre las clases desbloqueadas. Cada clase muestra de forma clara sus atributos base, dado de velocidad, slots de inventario, límite de poder y habilidad única. Las clases aún no desbloqueadas aparecen en silueta con la condición de desbloqueo visible.

#### 2.1.1 Clases Disponibles

| Clase | HP Base | Dado Velocidad | Slots | Límite Poder | Habilidad Pasiva |
|-------|---------|----------------|-------|--------------|------------------|
| Guerrero | 120 | d8 | 5 | 12 | Golpe Crítico: Full y Generala hacen +50% daño |
| Pícaro | 80 | d10 | 6 | 10 | Dados Ágiles: puede re-tirar 1 dado extra por turno |
| Mago | 70 | d6 | 4 | 16 | Arcano: los combos de Escalera hacen daño AoE |
| Clérigo | 100 | d6 | 5 | 12 | Bendición: al sacar Full, recupera 10% HP |
| Ranger | 90 | d10 | 5 | 11 | Precisión: Poker y Generala ignoran 50% de armadura |
| Bárbaro | 140 | d8 | 4 | 14 | Furia: por cada 20% de HP perdido, +10% daño base |

*Nota de diseño: El Límite de Poder es el presupuesto total que el jugador puede gastar al armar su pouch de dados. Un límite alto permite equipar dados más grandes pero con menos variedad; un límite bajo obliga a combinar dados pequeños con inteligencia.*

### 2.2 Pantalla 2 — Roleado de Estadísticas (Estilo DnD)

Tras elegir clase, el jugador rolea sus stats principales tirando dados en pantalla, replicando la experiencia clásica de DnD.

#### 2.2.1 Stats a Rolear

| Stat | Dados | Efecto en Juego |
|------|-------|-----------------|
| Fuerza (FUE) | 3d6 | Modifica daño base en combate cuerpo a cuerpo |
| Destreza (DES) | 3d6 | Modifica prioridad de turno e iniciativa de combate |
| Constitución (CON) | 3d6 | Modifica HP máximo y resistencia a estados |
| Inteligencia (INT) | 3d6 | Modifica eficacia de habilidades mágicas y combos especiales |
| Sabiduría (SAB) | 3d6 | Modifica probabilidad de eventos favorables en salas especiales |
| Carisma (CAR) | 3d6 | Modifica precios en tienda y calidad de recompensas |

#### 2.2.2 Reglas del Roleado

- Se tiran 3d6 por cada stat (rango 3–18). La animación muestra los dados rodando en pantalla.
- El jugador puede elegir entre 2 opciones: aceptar la tirada o re-rolear TODO el set una única vez (risk/reward: podría salir peor).
- **Alternativa por clase:** algunas clases avanzadas (desbloqueables) podrían usar 4d6 descartando el menor, otorgando stats más altos como recompensa de progresión meta.

### 2.3 Pantalla 3 — Configuración del Pouch (Inventario de Dados)

El jugador arma su set de dados de combate. Esta es la pantalla más estratégica del inicio de la run.

#### 2.3.1 Sistema de Slots y Costos

El pouch funciona como el sistema de chapas de Hollow Knight: cada dado ocupa una cantidad de slots proporcional a su poder, y el jugador tiene un límite total de poder determinado por su clase.

| Dado | Caras | Slots que Ocupa | Costo de Poder | Daño Promedio por Cara |
|------|-------|-----------------|----------------|------------------------|
| d4 | 4 | 1 | 1 | 2.5 |
| d6 | 6 | 1 | 2 | 3.5 |
| d8 | 8 | 1 | 3 | 4.5 |
| d10 | 10 | 2 | 4 | 5.5 |
| d12 | 12 | 2 | 5 | 6.5 |
| d20 | 20 | 3 | 7 | 10.5 |

#### 2.3.2 Ejemplo de Configuración

**Guerrero (5 slots, 12 de poder):**

- Opción A — Equilibrada: 2x d8 (2 slots, 6 poder) + 2x d6 (2 slots, 4 poder) + 1x d4 (1 slot, 1 poder) = 5/5 slots, 11/12 poder
- Opción B — High-roller: 1x d20 (3 slots, 7 poder) + 1x d8 (1 slot, 3 poder) + 1x d6 (1 slot, 2 poder) = 5/5 slots, 12/12 poder
- Opción C — Consistente: 5x d6 (5 slots, 10 poder) = 5/5 slots, 10/12 poder

*Nota: La Opción B tiene un d20 que puede sacar 20 pero también 1; la Opción C es predecible pero nunca hará daño explosivo. El jugador elige qué nivel de varianza quiere en su run.*

#### 2.3.3 Dados Desbloqueables

Además de los dados estándar, existen dados especiales que se desbloquean con la meta-progresión. Estos dados tienen caras con valores o efectos modificados:

- **Dado Flambé (d6 especial):** caras: 0, 0, 4, 4, 8, 8. Alto riesgo, alto reward. Costo: 3 poder.
- **Dado Vampiro (d8 especial):** por cada 6+ sacado, recupera 2 HP. Costo: 4 poder.
- **Dado Doble Filo (d10 especial):** valor x1.5 pero recibe 2 de daño al tirarlo. Costo: 4 poder.
- **Dado Arcano (d12 especial):** si sale 10+, cuenta como 2 dados para combos. Costo: 6 poder.

---

## 3. Sistema de Exploración de la Mazmorra

### 3.1 Estructura de la Mazmorra

La mazmorra se genera proceduralmente sobre una matriz de salas interconectadas. El layout se presenta al jugador mediante un mini-mapa visible en la HUD (similar al de Isaac), donde cada sala se representa como un nodo cuadrado con un icono que indica su tipo.

#### 3.1.1 Generación del Layout

- Cada piso tiene entre 8 y 15 salas distribuidas en una grilla.
- Siempre existe exactamente 1 sala de inicio, 1 sala de jefe, y al menos 1 sala de tienda.
- Las conexiones entre salas se representan como puertas (norte/sur/este/oeste). El jugador elige qué puerta tomar.
- El mini-mapa revela salas adyacentes. Las salas lejanas permanecen ocultas hasta que el jugador las descubre, salvo que posea un ítem de revelación.
- Al avanzar de piso, la densidad de salas de combate aumenta y las tiendas se vuelven más escasas.

#### 3.1.2 Tipos de Sala

| Tipo | Icono Mini-mapa | Descripción | Frecuencia |
|------|-----------------|-------------|------------|
| Sala de Inicio | Estrella | Sin enemigos. El jugador aparece aquí al entrar al piso. | 1 por piso |
| Sala de Combate | Espada | Contiene 1–4 enemigos. El movimiento se rige por dados hasta limpiar la sala. | 50–65% |
| Sala de Jefe | Calavera | Boss del piso. Puerta bloqueada hasta derrotar X salas de combate. | 1 por piso |
| Sala de Tienda | Moneda | NPC mercader. El jugador compra/vende dados, ítems y consumibles. | 1–2 por piso |
| Sala de Evento | Signo ? | Evento narrativo con opciones que otorgan beneficio o penalización. | 10–15% |
| Sala de Tesoro | Cofre | Recompensa garantizada sin combate. Puede contener dado especial o pasiva. | 0–1 por piso |
| Sala de Descanso | Fogata | El jugador elige: recuperar 30% HP o mejorar un dado del pouch. | 0–1 por piso |

### 3.2 Movimiento Dentro de las Salas

Cada sala tiene un grid de casilleros (por ejemplo, 8x8 o 10x10 según el tipo). El sistema de movimiento varía según el estado de la sala.

#### 3.2.1 Movimiento en Sala con Enemigos (Por Turnos con Dados)

Cuando la sala contiene enemigos, el movimiento se convierte en táctico y por turnos:

1. **Fase de Turno del Jugador**
   - El jugador tira su dado de velocidad (determinado por la clase). El resultado indica la cantidad máxima de casillas que puede moverse en línea recta (horizontal, vertical o diagonal).
   - El jugador puede moverse menos casillas de las indicadas si lo desea (movimiento parcial).
   - El movimiento se ejecuta en tiempo real dentro del turno: el jugador selecciona la dirección y el personaje se mueve con animación fluida.

2. **Fase de Turno de los Enemigos**
   - Cada enemigo tira su propio dado de movimiento (variable según tipo de enemigo).
   - Los enemigos se mueven automáticamente hacia el jugador por la ruta más corta, hasta el máximo de casillas obtenido.
   - Si un enemigo alcanza la casilla del jugador (o una adyacente, según el tipo), se inicia el combate.

3. **Resolución de Colisión**
   - Al colisionar, la cámara transiciona al modo combate (ver Sección 4).
   - Tras finalizar el combate, el jugador regresa a la misma posición en el grid.
   - Si quedan enemigos, el ciclo de turnos continúa.

#### 3.2.2 Movimiento Libre (Sala sin Enemigos)

Una vez eliminados todos los enemigos (o en salas sin combate), el jugador se mueve libremente sin restricciones de dados. Esto incluye:

- Movimiento con controles direccionales estándar (stick analógico o WASD).
- Interacción con objetos del entorno: cofres, trampas desactivadas, interruptores.
- Acceso a las puertas para transitar a la siguiente sala.

*Nota de diseño sobre la fluidez:* Para evitar que el movimiento por dados se sienta tedioso, se implementan las siguientes soluciones:

- **Animación rápida:** la tirada del dado es instantánea (0.5s) con feedback visual claro del resultado.
- **Auto-movimiento:** si el jugador mantiene presionada la dirección, el personaje avanza automáticamente las casillas disponibles.
- **Preview de rango:** al iniciar el turno, se iluminan todas las casillas alcanzables para que la decisión sea rápida.
- **Opción de dash:** en salas con pocos enemigos o enemigos lejanos, el jugador puede gastar una carga de dash para duplicar el resultado de la tirada (recurso limitado, recargable).

### 3.3 Enemigos en Exploración

Los enemigos se posicionan en el grid al entrar a la sala. Cada tipo tiene comportamiento y dado de movimiento distintos:

| Enemigo | Dado Movimiento | Comportamiento | Dados Combate |
|---------|-----------------|----------------|---------------|
| Goblin | d8 | Agresivo: siempre va directo al jugador | 3x d4 |
| Esqueleto | d6 | Patrulla: recorre un patrón fijo hasta detectar al jugador | 2x d6 + 1x d4 |
| Slime | d4 | Lento pero se divide al recibir daño | 4x d4 |
| Murciélago | d10 | Errático: dirección parcialmente aleatoria | 2x d6 |
| Caballero Oscuro | d6 | Defensivo: espera a que el jugador se acerque | 2x d8 + 1x d6 |
| Hechicero | d4 | Estacionario: ataca a distancia si el jugador está a 3 casillas | 1x d10 + 2x d4 |

---

## 4. Sistema de Combate

El combate es el corazón del juego. Utiliza un sistema de Generala adaptado donde el jugador tira los dados de su pouch buscando combinaciones que determinan el daño infligido. El sistema de turnos sigue la mecánica de iniciativa estilo Final Fantasy.

### 4.1 Inicio del Combate

- La cámara transiciona del grid de exploración a una vista de combate dedicada.
- Se determina el orden de turno según la Destreza del jugador vs. la velocidad del enemigo (similar a ATB de FF).
- Se muestra una barra de turno visual que indica quién actúa primero y el orden subsiguiente.

### 4.2 Turno del Jugador — Sistema de Generala

Durante su turno, el jugador dispone de 3 tiradas para buscar la mejor combinación posible con los dados de su pouch.

#### 4.2.1 Flujo de Tirada

1. **Tirada 1:** se lanzan todos los dados del pouch simultáneamente. Los resultados aparecen en pantalla.
2. **Selección:** el jugador elige qué dados conservar (bloquear) y cuáles re-tirar.
3. **Tirada 2:** se re-lanzan los dados no bloqueados.
4. **Selección 2:** nueva oportunidad de bloquear dados.
5. **Tirada 3 (final):** se re-lanzan los dados restantes. El resultado final determina el daño.

*Regla crítica: El jugador puede detenerse antes de la tirada 3 si ya obtuvo una buena combinación. Las tiradas no utilizadas se redirigen a defensa (ver 4.4).*

#### 4.2.2 Tabla de Combinaciones y Daño

El daño base depende de la combinación obtenida. Si no se logra ninguna combinación, se toma el dado de mayor valor como daño base.

| Combinación | Condición | Fórmula de Daño | Ejemplo (5x d6) |
|-------------|-----------|-----------------|------------------|
| Generala | 5 dados iguales | Suma x3 + bonificación de clase | 5x6 = 30 → 90 + bonus |
| Poker | 4 dados iguales | Suma x2 | 4x5 + 1x3 = 23 → 46 |
| Full | 3 iguales + 2 iguales | Suma x1.75 | (3x4 + 2x6 = 24) → 42 |
| Escalera Grande | 5 valores consecutivos | Suma x2.5 | (2+3+4+5+6 = 20) → 50 |
| Escalera Chica | 4 valores consecutivos | Suma x1.5 | (1+2+3+4+6 = 16) → 24 |
| Trío | 3 dados iguales | Suma x1.25 | (3x5 + 2+4 = 21) → 26 |
| Doble Par | 2 pares distintos | Suma x1.1 | (2x3 + 2x5 + 1 = 17) → 19 |
| Par | 2 dados iguales | Suma x1 | (2x6 + 3+2+1 = 18) → 18 |
| Nada | Sin combinación | Dado más alto × 1 | Mayor dado: 6 → 6 |

*Nota: La fórmula final de daño se multiplica por el modificador de Fuerza (o Inteligencia según la clase) del personaje. Las combinaciones con dados de mayor rango (d10, d12, d20) producen sumas base más altas, lo que amplifica el multiplicador del combo.*

#### 4.2.3 Consideraciones para Dados Mixtos

Dado que el pouch puede contener dados de diferentes tamaños (ej: d4, d6, d8, d10), las combinaciones se evalúan únicamente por el valor numérico obtenido, no por el tipo de dado. Ejemplo: un d4 que saca 4, un d8 que saca 4 y un d12 que saca 4 cuentan como trío de 4s. Esto incentiva builds donde dados de diferente tamaño comparten rangos de valores solapados (d4-d6-d8 todos pueden sacar 1–4).

### 4.3 Turno del Enemigo

Para mantener el ritmo del combate, los enemigos tienen un sistema simplificado:

- El enemigo realiza una única tirada con sus dados asignados (sin re-tiradas).
- El daño se calcula igual que para el jugador (misma tabla de combos).
- Al tener 1 sola tirada, las probabilidades de combos altos son menores, equilibrando la dificultad.

#### 4.3.1 Barra de Energía del Enemigo

Los enemigos también acumulan energía a lo largo del combate. Cuando su barra se llena:

- **Ataque Potenciado:** su siguiente tirada tiene un 50% de probabilidad de duplicar el daño resultante.
- La barra se carga 25% por cada turno que pasa. Se llena en 4 turnos.
- Indicador visual: la barra brilla y el enemigo tiene un aura de advertencia para que el jugador pueda planificar.

### 4.4 Sistema de Defensa

La defensa está atada directamente a las tiradas no utilizadas en ataque.

#### 4.4.1 Defensa por Tiradas Sobrantes

Si el jugador obtiene su combo en la tirada 1 o 2 y decide no seguir tirando, las tiradas restantes se convierten en tiradas de defensa:

- **1 tirada sobrante:** el jugador tira todos sus dados una vez. La suma total se convierte en puntos de escudo temporal que absorben daño del próximo ataque enemigo.
- **2 tiradas sobrantes:** el jugador tira dos veces y elige el mejor resultado, o suma ambos con un multiplicador reducido (x0.6 cada una).

**Ejemplo:** El jugador saca Poker en la tirada 1. Le quedan 2 tiradas de defensa. Tira sus dados dos veces y obtiene sumas de 18 y 22. Opción A: toma 22 como escudo. Opción B: suma ambas con reducción = (18+22) x 0.6 = 24 de escudo.

#### 4.4.2 Decisión Estratégica

Este sistema genera un dilema constante: ¿gasto mis 3 tiradas buscando Generala (daño máximo pero 0 defensa)? ¿O acepto un Trío en la tirada 1 y uso las 2 tiradas restantes para defenderme? La respuesta depende del estado del combate, la barra de energía del enemigo y los HP del jugador.

### 4.5 Barra de Energía del Jugador y Modo Craps

El jugador tiene su propia barra de energía que se carga al realizar combinaciones exitosas en combate.

#### 4.5.1 Carga de la Barra

| Combinación | Energía Ganada |
|-------------|----------------|
| Generala | 50% |
| Poker | 35% |
| Full / Escalera Grande | 25% |
| Trío / Escalera Chica | 15% |
| Par / Doble Par | 10% |
| Nada | 5% |

#### 4.5.2 Activación del Modo Craps

Cuando la barra llega al 100%, el jugador puede activar el Modo Craps en cualquiera de sus turnos. El flujo es el siguiente:

1. **Apuesta:** el jugador declara qué combinación obtendrá en su próxima tirada. Las opciones se muestran en pantalla con las probabilidades reales calculadas.
2. **Tirada Única:** el jugador tira TODOS sus dados una sola vez (sin re-tiradas). Es todo o nada.
3. **Resolución:**
   - **Si acierta la apuesta:** el daño se calcula con un multiplicador x3 adicional sobre la fórmula base del combo. Devastador.
   - **Si falla:** el jugador recibe una penalización: pierde su turno siguiente y el enemigo obtiene un turno extra.

*Risk/Reward: apostar a Generala es casi suicida pero puede one-shottear un jefe. Apostar a Par es casi seguro pero el bonus es moderado. El jugador debe evaluar el riesgo según la situación.*

---

## 5. Recompensas, Economía y Progresión en Run

### 5.1 Moneda — Fichas de Oro

La moneda del juego son Fichas de Oro, tematizadas como fichas de casino para reforzar la estética de apuesta. Se obtienen de las siguientes fuentes:

- Derrotar enemigos comunes: 3–8 fichas.
- Derrotar jefes: 25–50 fichas + recompensa especial.
- Salas de evento (según elección): 5–15 fichas.
- Vender dados o ítems en la tienda: precio variable.

#### 5.1.1 Usos de las Fichas

- **Tienda:** comprar dados, consumibles, pasivas y ítems de equipo.
- **Sala de Descanso:** pagar extra por una mejora de dado más potente.
- **Eventos:** algunas opciones requieren fichas para activarse.
- **Modo Craps Premium (opcional):** apostar fichas adicionalmente para multiplicar aún más el daño en caso de acierto.

### 5.2 Recompensas por Combate

Al derrotar a un enemigo, se despliega una pantalla de recompensa con opciones:

| Tipo Enemigo | Recompensa Garantizada | Recompensa Aleatoria (% chance) |
|--------------|------------------------|---------------------------------|
| Enemigo Común | 3–8 fichas | Dado básico (15%) · Consumible (20%) |
| Enemigo Élite | 10–15 fichas | Dado especial (25%) · Pasiva (15%) |
| Jefe de Piso | 30–50 fichas + dado/pasiva | Dado raro (100%) + elección de pasiva |
| Jefe Final | Desbloqueo meta + final | Dado legendario |

### 5.3 Sistema de Pasivas

Las pasivas son modificadores permanentes para la run actual. Se obtienen al derrotar jefes, en salas de evento, o comprándolas en la tienda. El jugador puede tener hasta 4 pasivas activas simultáneamente (ampliable con ítems).

| Pasiva | Efecto | Obtención |
|--------|--------|-----------|
| Dados Calientes | +1 al valor de cada dado en la primera tirada | Jefe Piso 1 |
| Bolsillo Profundo | +1 slot de inventario en el pouch | Tienda (caro) |
| Contraataque | Al defender, 30% de reflejar daño al enemigo | Sala de Evento |
| Ojos de Serpiente | Sacar 1 en cualquier dado da energía extra (10%) | Drop enemigo élite |
| Cargado | La barra de energía empieza al 25% en cada combate | Jefe Piso 2 |
| Jugada Maestra | Tras una Generala, la siguiente tirada es automáticamente Poker mínimo | Jefe Final |
| Retirada Táctica | Permite escapar del combate 1 vez por piso sin penalización | Tienda |
| Ambidiestro | En modo Craps, puedes apostar a 2 combos (si sale cualquiera, ganas) | Jefe Piso 3 |

### 5.4 Ítems de Equipo

Los ítems representan equipo físico que modifica las stats o habilita mecánicas nuevas. Se equipan en slots limitados (arma, armadura, accesorio).

#### 5.4.1 Armas

- **Espada de Acero:** +15% daño base en combos de Par o superior.
- **Daga Envenenada:** cada ataque aplica veneno (3 daño por turno, 3 turnos).
- **Báculo Arcano:** las Escaleras hacen daño AoE a todos los enemigos del combate.
- **Martillo de Guerra:** +30% daño pero -1 al dado de velocidad.

#### 5.4.2 Armaduras

- **Cota de Malla:** +10 HP y +5 de escudo base permanente.
- **Túnica del Mago:** +2 a la barra de energía por cada tirada de defensa.
- **Armadura de Cuero:** +1 al dado de velocidad (exploración).

#### 5.4.3 Accesorios

- **Amuleto de la Suerte:** +5% chance de obtener un combo un nivel superior al obtenido.
- **Anillo de Energía:** barra de energía se carga 10% más rápido.
- **Guantes de Tahúr:** en modo Craps, puedes re-tirar 1 dado.

### 5.5 Consumibles

Objetos de uso único que se guardan en un inventario separado (máx. 3 slots):

- **Poción de Vida:** recupera 30% HP.
- **Dado de Emergencia:** añade temporalmente un d6 extra al pouch por 1 combate.
- **Bomba de Humo:** permite escapar de un combate sin penalización.
- **Elixir de Precisión:** la próxima tirada permite bloquear 1 dado adicional.
- **Ficha Dorada:** duplica las fichas ganadas en el próximo combate.

---

## 6. Sinergias entre Clases y Dados

Cada clase tiene una afinidad natural con ciertos tipos de dados y estrategias. Estas sinergias no son restricciones (cualquier clase puede usar cualquier dado) sino bonificaciones que premian la especialización.

| Clase | Dados Recomendados | Estrategia Natural | Sinergia Clave |
|-------|--------------------|--------------------|----------------|
| Guerrero | d8, d10 | Apuntar a Poker/Full con dados medianos-grandes | Golpe Crítico escala con dados de mayor suma |
| Pícaro | d6 mixtos + especiales | Alta consistencia, muchas re-tiradas | Dados Ágiles + Dado Flambé = riesgo controlado |
| Mago | d10, d12 | Pocos dados de alto rango para Escaleras | Arcano + Escaleras AoE = clear de grupos |
| Clérigo | d6, d8 | Full para daño + heal simultáneo | Bendición + Dado Vampiro = sustain infinito |
| Ranger | d8, d10, d12 | Poker con dados grandes para burst | Precisión ignora armadura = mata tanques |
| Bárbaro | d4, d6 (muchos) | Muchos dados para combos + baja HP a propósito | Furia + HP bajo + Generala = damage explosivo |

---

## 7. Condiciones de Victoria y Derrota

### 7.1 Condición de Derrota

La run termina cuando el HP del jugador llega a 0. No hay vidas extra ni continues. Al morir:

- Se muestra un resumen de la run: piso alcanzado, enemigos derrotados, mejor combo logrado, fichas acumuladas.
- Las fichas acumuladas se convierten parcialmente (50%) en Fichas de Legado (moneda meta).
- Se registran los hitos alcanzados para desbloqueos permanentes.

### 7.2 Condición de Victoria

La run se completa al derrotar al Jefe Final del último piso. La estructura base es de 5 pisos con dificultad escalonada:

| Piso | Temática | Jefe | Mecánica Especial |
|------|----------|------|-------------------|
| Piso 1 — Catacumbas | No-muertos, trampas básicas | Rey Esqueleto | Invoca minions que tiran dados extra |
| Piso 2 — Cuevas Fúngicas | Slimes, venenos | El Gran Slime | Se divide en slimes menores al perder HP |
| Piso 3 — Forja Abandonada | Constructos, fuego | El Autómata | Armadura que requiere Escalera para dañar |
| Piso 4 — Torre Arcana | Hechiceros, anomalías | El Archimago | Modifica dados del jugador (cambia caras) |
| Piso 5 — El Abismo | Demonios, caos | Señor del Azar | Modo Craps obligatorio cada 3 turnos |

### 7.3 Post-Victoria y New Game+

Tras completar la run:

- Se desbloquea la dificultad Asc. 1 (incrementa el número de enemigos élite y reduce recompensas). Hasta 10 niveles de ascensión.
- Se desbloquean dados legendarios y clases avanzadas según el rendimiento.
- Cada nivel de ascensión introduce modificadores únicos (ej: Asc. 3 = los enemigos tienen Modo Craps).

---

## 8. Meta-Progresión (Entre Runs)

### 8.1 Fichas de Legado

Moneda permanente obtenida al final de cada run (50% de las fichas de oro + bonus por hitos). Se gastan en el Hub entre runs.

### 8.2 Hub / Menú Principal

Entre runs, el jugador accede a un hub con las siguientes estaciones:

- **Colección de Dados:** desbloquear dados nuevos (estándar y especiales) para que aparezcan en futuras runs.
- **Gremio de Aventureros:** desbloquear nuevas clases pagando con fichas de legado o cumpliendo condiciones específicas.
- **Armero:** desbloquear ítems de equipo para el loot pool de futuras runs.
- **Biblioteca de Pasivas:** desbloquear nuevas pasivas que podrán aparecer como recompensa.
- **Tablero de Hitos:** muestra los logros y condiciones para desbloqueos pendientes.

### 8.3 Condiciones de Desbloqueo (Ejemplos)

| Desbloqueo | Condición |
|------------|-----------|
| Clase: Bárbaro | Derrotar al jefe del Piso 1 con menos de 30% HP |
| Dado Flambé | Obtener 3 Generalas en una misma run |
| Dado Vampiro | Completar el Piso 2 sin usar pociones |
| Pasiva: Jugada Maestra | Derrotar al Jefe Final |
| Dificultad Asc. 1 | Completar la run por primera vez |
| Clase: Nigromante (avanzada) | Completar Asc. 5 |

---

## 9. Mapa de Decisiones del Jugador (Risk / Reward)

Este apartado resume todas las decisiones significativas que el jugador enfrenta y dónde reside el riesgo y la recompensa en cada una.

| Momento | Decisión | Riesgo | Recompensa |
|---------|----------|--------|------------|
| Configuración Pouch | ¿Dados grandes o pequeños? | Dados grandes = alta varianza, poca consistencia | Sumas altas cuando conectan los combos |
| Roleado Stats | ¿Aceptar o re-rolear? | Re-rolear puede empeorar todo el set | Chance de stats superiores |
| Exploración | ¿Avanzo hacia el enemigo o me alejo? | Acercarse expone al combate | Iniciar combate en tus términos |
| Combate: Tiradas | ¿Sigo tirando o acepto este combo? | Tirada extra = menos defensa | Posibilidad de combo superior |
| Defensa vs. Ataque | ¿Gasto las 3 tiradas atacando? | 0 defensa, vulnerable al contraataque | Daño máximo posible |
| Modo Craps | ¿Apuesto a combo alto o bajo? | Fallo = pierdo turno + turno extra enemigo | Acierto = daño x3 devastador |
| Tienda | ¿Comprar dado caro o guardar fichas? | Gastar todo = sin recursos para emergencias | Dado potente para el resto de la run |
| Sala Descanso | ¿Healear o mejorar dado? | No healear = riesgo en próximos combates | Dado mejorado rinde más a largo plazo |
| Sala Evento | ¿Riesgo por recompensa alta? | Penalización si falla | Pasiva o dado especial gratis |
| Ruta en mapa | ¿Atajar al jefe o explorar más? | Más salas = más desgaste | Más loot, fichas y preparación |

---

## 10. Interfaz y HUD

### 10.1 HUD de Exploración

- **Esquina superior izquierda:** HP del jugador, barra de energía, icono de clase.
- **Esquina superior derecha:** mini-mapa de la mazmorra con iconos por tipo de sala.
- **Parte inferior:** inventario rápido (consumibles), indicador de piso actual.
- **Centro (contextual):** al iniciar turno de movimiento, se muestra el dado de velocidad y el resultado con highlight de casillas alcanzables.

### 10.2 HUD de Combate

- **Zona central:** dados del pouch con animación de tirada. Los dados bloqueados se resaltan.
- **Lateral izquierdo:** stats del jugador (HP, escudo, barra de energía, turno).
- **Lateral derecho:** stats del enemigo (HP, barra de energía, dados visibles).
- **Inferior:** indicador de tirada actual (1/3, 2/3, 3/3), botón de ACEPTAR combo, botón de MODO CRAPS (si disponible).
- **Superior:** nombre del combo actual detectado + fórmula de daño preview.

### 10.3 Pantalla de Pouch (Inventario)

Accesible en cualquier momento fuera de combate. Muestra:

- Los slots como casilleros visuales (estética de fieltro de mesa de juego).
- Cada dado ocupa su cantidad de slots de forma visual (un d20 ocupa 3 casilleros contiguos).
- Indicadores de poder restante y slots restantes.
- Tooltip al pasar sobre cada dado mostrando sus stats y probabilidades.

---

## 11. Glosario de Términos

| Término | Definición |
|---------|------------|
| Pouch | Bolsa de dados que el jugador equipa para combate. Funciona como inventario principal. |
| Slots | Casilleros del pouch. Cada dado ocupa una cantidad determinada. |
| Límite de Poder | Presupuesto total para configurar el pouch. Dados más grandes cuestan más poder. |
| Generala | Sistema de tirada de dados con 3 oportunidades (inspirado en el juego de mesa Generala/Yahtzee). |
| Modo Craps | Mecánica de apuesta activada con la barra de energía. Alto riesgo, alta recompensa. |
| Fichas de Oro | Moneda dentro de la run. Se usan en tiendas y eventos. |
| Fichas de Legado | Moneda permanente entre runs. Se obtiene al finalizar cada run. |
| Combo | Combinación de dados que determina el multiplicador de daño (Generala, Poker, Full, etc.). |
| Dado de Velocidad | Dado específico de cada clase que determina casillas de movimiento en exploración. |
| Escudo Temporal | Puntos de defensa obtenidos por tiradas sobrantes. Se consume en el próximo ataque recibido. |
| Ascensión | Niveles de dificultad progresiva desbloqueables tras completar la run. |
