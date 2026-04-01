# Game Design Document V2

## Resumen del Juego

**Título de trabajo:** [Sin título] — Roguelite de mazmorras basado en dados

**Concepto en una línea:** Un roguelite por turnos donde los dados son tu inventario, tu arma, tu movimiento y tu defensa: armá tu bolsa de dados, explorá mazmorras procedurales y dominá el sistema de combos estilo Generala para sobrevivir.

| Campo | Detalle |
|---|---|
| Género | Roguelite dungeon crawler, por turnos |
| Perspectiva | Isométrica fija |
| Motor | Unity 6.3 (C#) |
| Plataforma | PC (itch.io / Steam) |
| Controles | Mouse (click en grilla + arrastrar dados) |
| Público objetivo | Jugadores de estrategia, fans de roguelites, comunidad de juegos de mesa |
| Duración de una run | ~1 hora 40 minutos |
| Equipo | 6 personas — Licenciatura en Desarrollo de Videojuegos (UADE) |
| Etapa | Concepto / Pre-Prototipo |

*Fuente: GDD PDF p6*

## Concepto

**Elevator Pitch:** Imaginá la profundidad estratégica de Balatro (donde cada carta importa y cada build cuenta) pero dentro de las mazmorras procedurales de The Binding of Isaac. En vez de cartas, tus herramientas son dados físicos que coleccionás, encantás cara por cara, y tirás para atacar, defenderte y moverte. El combate usa un sistema estilo Generala: tres tiradas, reservás dados, armás combos. Cuanto mejor el combo, más daño hacés. Pero las tiradas usadas para atacar son tiradas que no tenés para defenderte. Cada decisión importa. Cada dado cuenta.

**Oportunidad de mercado:**
1. **Nicho desatendido:** ningún roguelite combina dados físicos como sistema de build permanente con exploración de mazmorras. Dicey Dungeons usa dados como recursos consumibles. Dice A Million logra la sensación de dados pero no tiene exploración ni gameplay espacial.
2. **Momentum del género:** Balatro (2024) demostró que "juego de mesa + roguelite" tiene demanda masiva — vendió millones en su primer mes.
3. **La brecha entre Isaac y Balatro:** Isaac tiene exploración pero exige reflejos. Balatro tiene estrategia pero no tiene exploración. Este juego une las dos mitades.
4. **Accesibilidad de la Generala:** el sistema de combos es universalmente conocido y fácil de aprender. En Argentina la variante local se juega desde la infancia, barrera de entrada extremadamente baja.

*Fuente: GDD PDF p6-7*

## Gameplay

**Público objetivo:**
- **Primario:** Jugadores de PC (18-35) que disfrutan roguelites de estrategia y juegos de mesa digitales. Conocen Balatro, Isaac, Dicey Dungeons.
- **Secundario:** Jugadores argentinos/latinoamericanos que conocen la Generala desde siempre.

*Fuente: GDD PDF p8*

## Dirección de Arte

**Estilo: Pixel-Poly** (3D Low Poly + Pixel Art Shader)

Notas de implementación (reunión 03/24):
- Pixel art shader logrado bajando la resolución de cámara y usando filtrado "point" en texturas.
- Los dados muestran el resultado de la tirada vía UI overlay para el número (evita pixelación durante el giro).
- Textura de paleta única con UV mapping para colorear modelos.

**Paleta de colores:**
- Fondos: tonos oscuros
- Verde mesa de casino
- Rojo dado
- Dorado fichas de casino
- Neones para efectos especiales y UI destacada
- Paleta específica por piso: TBD por el equipo de arte

### Decisiones de Arte

| Criterio | Elección | Justificación |
|---|---|---|
| Estilo | Low-poly 3D + pixel art post-process shader | Rápido de producir, estética única, ideal para equipo de 6 |
| Perspectiva | Isométrica fija, sin rotación de cámara | Simplifica arte y desarrollo, foco en grilla táctica |
| Excepción visual | Los dados rotan con física real al tirarlos | Son el protagonista visual, merecen tratamiento especial |
| Paleta | Tonos oscuros + neones casino (verde mesa, rojo dado, dorado ficha) | Alto contraste = legibilidad. Atmósfera casino sin ser genérica |
| Jerarquía visual | Dados > Jugador > Enemigos > Entorno | El jugador siempre sabe dónde mirar |

### Jerarquía Visual
1. **Dados** — siempre el elemento más prominente en pantalla
2. **Jugador** — segundo en jerarquía
3. **Enemigos** — claramente identificables
4. **Entorno** — siempre subordinado al gameplay

### Animaciones
Mínimas: Idle, Walk, y Die/Disappear.

### Estilos 3D Evaluados

| Estilo | Resultado |
|---|---|
| Low-poly angular/puntiagudo (AI + retopología manual) | Descartado visualmente |
| Ultra-simple geométrico (formas básicas) | Preferido por el equipo |
| Voxel/Blockbench estilo Hytale | Alternativa pero no favorito |

*Fuente: GDD PDF p33-34*

## Game Feel y Feedback Visual

5 principios de game feel:

1. **Cada tirada tiene impacto** — los dados golpean la mesa con física, rebotan, se asientan. La pantalla reacciona. Los números aparecen. Nunca hay momento "muerto".
2. **Los combos escalan visualmente** — Par = brillo sutil, Trío = destello, Póker = sacude la pantalla, Generala = explosión visual completa.
3. **La victoria y la derrota se sienten, no se leen** — combo devastador llena la pantalla de efectos; daño pesado oscurece la pantalla, sacude la cámara.
4. **La tensión del juego de azar nunca para** — cada decisión de reservar o volver a tirar debe sentirse como empujar fichas sobre la mesa. La UI, animaciones y ritmo lo refuerzan.
5. **Los números deben sentirse satisfactorios** — números de daño escalan visualmente con su valor. Golpe de 3 = pequeño. Golpe de 50 = grande y contundente. Multiplicadores y efectos encadenados deben cascadear visualmente.

*Fuente: GDD PDF p7*

---

# Glosario

| Término | Significado | NO usar |
|---|---|---|
| **Bolsa de Dados** (Dice Bag) | El inventario/loadout del jugador. 5 espacios con costo variable. | Mochila, inventario |
| **Espacio** | Unidad de costo en la bolsa de dados. Dados más grandes cuestan más espacios. | Slot, capacidad |
| **Run** (Corrida) | Una partida completa desde el inicio hasta victoria o derrota. | Partida, sesión |
| **Piso** (Floor) | Un nivel procedural de la mazmorra. Termina al derrotar al boss. | Nivel, stage |
| **Sala** (Room) | Un espacio individual en la grilla del piso. | Cámara, área |
| **Loseta** (Tile) | Una celda individual de la grilla isométrica dentro de una sala. | Casilla, cuadrado |
| **Combo** | Combinación de dados estilo Generala (Par, Trío, Full House, Póker, Escalera, Generala). | Jugada, tirada |
| **Generala** | Todos los dados iguales. Combo máximo del sistema (base 100). | Yahtzee (en contexto de diseño interno) |
| **Craps Mode** | Mecánica de apuesta activada al llenar la barra de energía. Resultado ×2 o ×0. | Ataque especial, ultimate |
| **Barra de Energía** | Recurso que se recarga por turno. Al llenarse activa Craps Mode. | Maná, rage |
| **Face Enchanting** (Encantamiento de Dados) | Modificar una cara específica de un dado en salas especiales. | Mejorar, subir de nivel |
| **Tachar Combo** | Sacrificar un combo permanentemente a cambio de un beneficio inmediato. Mecánica de crisis. | Descartar, eliminar |
| **Meta-Progresión** | Desbloqueos permanentes entre runs (clases, dados, pasivas). | Progresión |
| **Build** | La combinación específica de dados en la bolsa. Define daño, combos posibles y estilo. | Configuración, setup |
| **Multiplicador de Dado** | EV del dado ÷ 3.5 (EV del d6). Escala el daño base del combo. | Bonus, modifier |
| **Gold** (Oro) | Moneda única del juego. Se obtiene de enemigos derrotados. | Monedas, dinero |

*Fuente: terminología extraída del propio GDD V2 y CLAUDE.md del proyecto*

---

# Core Loop del Juego

## 3 Capas

| | **EQUIPAR BUILD** | **→** | **EXPLORAR** | **→** | **COMBATIR** | **→** | **RECOMPENSAR** | |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|---|
| | **↑** | | | | | | **↓** | |
| | *← Actualizar build con nuevos dados y volver a empezar →* | | | | | | | |

*Fuente: sistemasDaños.md §3.1*

## Posibilidades de Diseño

| Eje | Descripción | Ejemplo |
|---|---|---|
| Builds | 227 combinaciones posibles de dados base (con d3 incluido). | 5×d4 (generala fácil) vs d20+2×d6 (apostador) |
| Enemigos | Diferentes resistencias a combos. Ej: un enemigo inmune a Full House. | Obliga a repensar la build antes de cada sala. |
| Salas especiales | Sala de encantamiento, sala de intercambio, sala de apuesta. | Variedad de contenido sin salir del sistema de dados. |
| Dados encontrados | Dados raros con propiedades especiales (d3, d%, dados encantados). | Narrativa de progresión orgánica. |
| Craps como tensión | La barra de energía crea momentos de decisión de alto riesgo. | ¿Uso el Craps ahora o espero un combo mejor? |

*Fuente: sistemasDaños.md §3.2*

---

# Sistema de Combate — Generala Isométrica

## 1. Estructura del Round

El combate se desarrolla íntegramente en el mapa isométrico. Los enfrentamientos son por turnos estrictos: el jugador actúa primero, luego los monstruos en orden descendente de poder.

| Fase | Actor | Descripción |
|---|---|---|
| **1** | **Jugador** | Ejecuta su acción de turno (ver Sistema de Acciones) |
| **2** | Monstruo más poderoso | Se mueve y ataca según su comportamiento de IA |
| **3** | Resto de monstruos | Actúan en orden descendente de poder hasta el más débil |
| **4** | **Nuevo round** | Vuelve al paso 1. Se repite hasta que el combate termine |

> *El jugador siempre actúa primero. No hay tirada de iniciativa.*

*Fuente: sistema_combate_v5.md §1*

## 2. Sistema de Acciones del Jugador

⚠ *Pendiente de decisión — Se presentan ambas opciones para evaluación del equipo.*

### Opción A — 2 Acciones por turno (Acción + Acción)

El jugador tiene 2 acciones que puede combinar libremente. La restricción central: el ataque solo puede ocurrir **una vez por turno**. Inspirado en D&D 5e y Wrath of Ashardalon.

| Slot | Opciones | Restricción |
|---|---|---|
| **Acción 1** | Moverse, usar ítem, habilidad especial, interactuar, pasar | Una sola opción por slot |
| **Acción 2** | **Atacar O moverse** una segunda vez | Si no atacás, el slot se convierte en movimiento extra. Nunca un segundo ataque. |

**Combinaciones posibles:**

| Combinación | Qué hace el jugador |
|---|---|
| **Mover → Atacar** | Se acerca y golpea. Flujo estándar de CaC. |
| **Atacar → Mover** | Ataca primero y se reposiciona. Ideal para el Arquero. |
| **Mover → Mover** | No ataca. Usa ambas acciones para moverse el doble de losetas. |
| **Ítem → Atacar** | Cura o usa consumible, luego ataca si hay enemigo en rango. |
| **Habilidad → Atacar** | Activa un buff o trampa, luego ataca. |
| **Mover → Ítem** | Se mueve y usa un consumible. No ataca ese turno. |

**Ventajas y riesgos (Opción A):**

| Ventajas | Riesgos |
|---|---|
| Mayor flexibilidad táctica por turno. | En cuartos chicos, el doble movimiento puede cubrir todo el espacio en un turno. |
| El Arquero puede atacar y reposicionarse sin perder nada. | Si el jugador y los enemigos pueden moverse el doble, el combate puede volverse caótico. |
| Permite más variedad de estrategias por clase. | Más complejo de entender para jugadores nuevos. |
| Consistente con referencias del género (D&D, Wrath of Ashardalon). | Puede requerir mapas más grandes para que el doble movimiento no sea dominante. |

### Opción B — 1 Acción por turno

El jugador elige exactamente una acción por turno. El movimiento usa un **dado de velocidad** propio de cada clase. Más simple, más predecible, pensado para salas pequeñas.

| Acción | Descripción |
|---|---|
| **Moverse** | Tira el dado de velocidad de su clase. Resultado = losetas. |
| **Atacar** | Solo si hay enemigo en rango. Se ejecuta el sistema de 3 tiradas con Generala. |
| **Usar ítem** | Usa un consumible del inventario. Consume el turno completo. |
| **Usar activo** | Activa habilidad especial de clase. Consume el turno completo. |
| **Pasar** | No hace nada. Los monstruos actúan normalmente. |

> *El dado de velocidad introduce varianza táctica: un Warrior que saca 1 cuando necesitaba llegar al enemigo es presión de diseño. A calibrar con playtest.*
>
> *Alternativa: usar el dado de velocidad solo como límite máximo (el jugador elige cuántas losetas moverse hasta el resultado del dado). Reduce la frustración de sacar 1.*

**Ventajas y riesgos (Opción B):**

| Ventajas | Riesgos |
|---|---|
| Muy simple de entender: una cosa por turno. | Menos flexibilidad táctica. El jugador no puede moverse y atacar en el mismo turno. |
| El doble movimiento no existe: el mapa chico no se rompe. | El Rogue/Mage ya en rango pierde un turno si quiere moverse — puede sentirse ineficiente. |
| El dado de velocidad diferencia más las clases. | La varianza del dado puede frustrar si el jugador saca 1 en un momento crítico. |
| Ritmo de combate más lento y más predecible. Más fácil de implementar. | El combate puede sentirse más lento, especialmente clases lentas como Mage. |

### Dado de Velocidad por Clase (Opción B)

Clases según GDD PDF (Warrior, Mage, Rogue). Valores de dado de velocidad y rango TBD.

| Clase | Dado de velocidad | Rango ataque | Pasiva (PDF) |
|---|---|---|---|
| **Warrior** | TBD | TBD | +1 defensa base |
| **Mage** | TBD | TBD | Relanzar 1 dado extra por tirada |
| **Rogue** | TBD | TBD | Mayor % de huida exitosa |

### Comparativa Directa entre Opciones

| Criterio | Opción A — 2 Acciones | Opción B — 1 Acción |
|---|---|---|
| **Simplicidad para el jugador** | Media — hay que entender las combinaciones | Alta — una acción, sin combinar |
| **Riesgo en mapas chicos** | Alto — el doble movimiento puede ser excesivo | Bajo — el mapa nunca se "rompe" |
| **Flexibilidad táctica** | Alta — muchas combinaciones posibles | Baja — una decisión por turno |
| **Diferenciación de clases** | Por losetas fijas + rango | Por dado de velocidad + rango |
| **Ritmo del combate** | Más dinámico | Más pausado y estratégico |
| **Complejidad de implementación** | Mayor | Menor |
| **Problema del doble ataque** | Resuelto — Acción 2 no puede ser 2.° ataque | Resuelto — solo 1 acción por turno |
| **Arquero en rango** | Ataca y se mueve en el mismo turno | Ataca o se mueve, no ambas |

### Preguntas para el Equipo

| # | Pregunta | Impacta en |
|---|---|---|
| 1 | ¿Qué tamaño promedio tienen las salas? ¿5×5, 7×7, más? | Define si el doble movimiento de Opción A rompe el espacio |
| 2 | ¿El dado de velocidad (Opción B) suma varianza buena o frustrante? | Si el juego ya tiene mucha varianza de dados, más puede saturar |
| 3 | ¿Queremos que el Arquero pueda atacar y reposicionarse en el mismo turno? | Opción A lo permite, Opción B no |
| 4 | ¿El dado de velocidad es el resultado exacto o el límite máximo de movimiento? | Límite máximo reduce frustración pero pierde varianza |
| 5 | ¿Los monstruos también usan dado de velocidad o tienen losetas fijas? | Si también tiran dado, el combate puede ser muy impredecible |

*Fuente: sistema_combate_v5.md §2*

## 3. Rango de Ataque por Clase

El rango de ataque es igual en ambos sistemas de acciones. Define cuándo el botón de ataque se habilita.

| Clase | Rango ataque | Notas de diseño |
|---|---|---|
| **Warrior** | TBD | TBD |
| **Mage** | TBD | TBD |
| **Rogue** | TBD | TBD |

> *En la Opción B, el botón de ataque se habilita visualmente en la UI cuando el jugador está dentro del rango necesario.*

*Fuente: sistema_combate_v5.md §3*

## 4. Flujo de un Turno de Ataque

Cada turno de ataque sigue el flujo de Generala:

```
┌─────────────────────────────────────────────────┐
│ 1. TIRAR       Tirar todos los dados de la build│
│ 2. ELEGIR      Guardar / retirar cada dado      │
│                (hasta 3 tiradas totales)         │
│ 3. CONFIRMAR   Aceptar el combo o tacharlo       │
│ 4. RESOLVER    Aplicar daño / efecto al enemigo  │
└─────────────────────────────────────────────────┘
```

### Reglas
* El jugador puede guardar/desbloquear cualquier dado entre tiradas
* El jugador puede confirmar temprano (después de tirada 1 o 2) si está satisfecho
* Menos dados = más difícil armar combos, pero sigue siendo posible (un Par necesita solo 2 dados)

*Fuente: sistemasDaños.md §2.1*

## 5. Tabla de Combos — Fórmulas de Daño

Los combos disponibles dependen de cuántos dados tiene la build:

| Combo | Descripción | 3d | 4d | 5d | Daño base |
|---|---|---|---|---|---|
| **Generala** | Todos los dados iguales | ✓ | ✓ | ✓ | 100 |
| **Póker** | 4 dados iguales | ✗ | ✓ (=gen) | ✓ | 60 |
| **Full House** | 3+2 iguales | ✗ | ✗ | ✓ | 40 |
| **Escalera** | Todos consecutivos | ✓ | ✓ | ✓ | 35 |
| **Trío** | 3 dados iguales | ✗ | ✓ | ✓ | 28 |
| **Doble par** | 2 pares distintos | ✗ | ✓ | ✓ | 18 |
| **Par** | 2 dados iguales | ✓ | ✓ | ✓ | 10 |
| **Sin combo** | — | ✓ | ✓ | ✓ | 0 |

### Fórmula de Daño

```
daño_final = base_combo × multiplicador_dado
multiplicador_dado = EV_promedio_build ÷ 3.5 (EV del d6)
```

**Ejemplos:**

| Build | Combo | Base | Multiplicador | Daño final |
|---|---|---|---|---|
| 5×d3 | Full House | 40 | ×0.57 | 23 |
| 5×d6 | Full House | 40 | ×1.00 | 40 |
| 5×d8 | Full House | 40 | ×1.29 | 51 |
| d20 + 2×d6 | Generala | 100 | ×1.58 | 158 |
| 5×d4 | Generala | 100 | ×0.71 | 71 |

### ❓ Duda abierta: ¿Generala con 3 dados?

| | Argumento | Impacto |
|---|---|---|
| ✓ SÍ permitirla | Es matemáticamente coherente: si todos los dados son iguales, es Generala. | Con 3×d3 la probabilidad de Generala es ~18% por tirada — muy alta. Puede sentirse 'barata'. |
| ✗ NO permitirla | La Generala debería requerir los 5 dados para mantener su peso como combo máximo. | Builds pequeñas (3d) quedan sin techo de daño alto. Puede frustrar al jugador. |
| ⚡ ALTERNATIVA | Renombrarla: con 3 dados se llama 'Trío perfecto' y vale menos (ej: base 60 en vez de 100). | Mantiene la coherencia mecánica y diferencia el peso entre builds grandes y chicas. |

> *Recomendación: usar la alternativa 'Trío perfecto' (base 60). Así las builds de 3 dados tienen su propio techo de daño sin devaluar la Generala de 5 dados.*

*Fuente: sistemasDaños.md §1.3, §1.4, §1.5*

## 6. Inicio del Combate

El combate se activa automáticamente cuando se cumple cualquiera de las dos condiciones. No hay transición ni pantalla separada.

| Condición | Descripción |
|---|---|
| **El jugador ataca a un monstruo** | Si el monstruo está dentro del rango de ataque y el jugador ejecuta la acción de ataque |
| **Un monstruo alcanza su rango de ataque** | Cuando un monstruo se desplaza hasta quedar a la distancia necesaria para golpear al jugador |

### Comportamiento de Enemigos al Entrar a una Sala

| Tipo de enemigo | Comportamiento |
|---|---|
| **Enemigo activo (estándar)** | Se desplaza hacia el jugador cada turno hasta entrar en su rango de ataque. Combate inevitable si el jugador permanece. |
| **Enemigo estático (guardián)** | No se mueve. Defiende un punto fijo: un cofre, una salida o una recompensa. Solo ataca si el jugador entra en su rango. Puede ignorarse. |

> *Los enemigos estáticos son la única forma de evitar el combate en una sala. Funcionan como decisión de riesgo/recompensa: ¿vale la pena pelear para acceder al cofre?*

*Fuente: sistema_combate_v5.md §5*

## 7. Escenarios Comparativos

Los mismos escenarios resueltos con cada sistema de acciones, para visualizar las diferencias.

### Escenario A — Bárbaro a 4 losetas de un Goblin

| Paso | Opción A (2 acciones) | Opción B (1 acción) |
|---|---|---|
| **Turno 1** | Acción 1: mueve 3 losetas. Acción 2: ataca (rango 1 alcanzado). | Tira d4 → saca 3. Se mueve 3 losetas. No puede atacar (a 1 loseta). |
| **Turno 2** | *(ya atacó en turno 1)* | Ahora está en rango 1 → ataca con Generala. |
| **Resultado** | **1 turno** para llegar y atacar. | **2 turnos:** 1 para moverse, 1 para atacar. |

### Escenario B — Arquero en rango, quiere reposicionarse

| Paso | Opción A (2 acciones) | Opción B (1 acción) |
|---|---|---|
| **Turno 1** | Acción 2: ataca. Acción 1: se mueve 3 losetas alejándose. | Elige: atacar O moverse. Si ataca, no se mueve. |
| **Turno 2** | *(ya atacó y se movió en turno 1)* | Si eligió atacar en T1, ahora puede moverse. |
| **Resultado** | Ataca y reposiciona en **1 turno**. Ventaja clara del Arquero. | Debe elegir: eficiencia o posición. Decisión más difícil. |

### Escenario C — Mago en peligro, enemigo encima

| Paso | Opción A (2 acciones) | Opción B (1 acción) |
|---|---|---|
| **Situación** | Orco al rango del Mago. Poca vida. | Ídem. |
| **Turno 1** | Acción 1: mueve 2 losetas. Acción 2: mueve 2 más. Se aleja 4 en total. | Tira d3 → saca 2. Se mueve 2 losetas. Solo 2, no 4. |
| **Resultado** | Se escapa más fácil gracias al doble movimiento. | Escape más limitado. El Orco probablemente lo alcanza. |

> *Este escenario muestra la diferencia más clara entre ambos sistemas. En Opción A el Mago puede escapar cómodamente; en Opción B queda más expuesto y el combate es más tenso.*

*Fuente: sistema_combate_v5.md §4*

---

# Flujo del Juego

[TBD — Falta definir el diagrama de flujo completo: Menú Principal → Selección de Clase → Armado de Bolsa de Dados → Exploración del Piso → Salas (combate/tienda/craps/etc.) → Boss → Siguiente Piso → Victoria/Game Over → Pantalla de Resultados → Menú de Desbloqueos.]

---

# Bolsa de Dados — El Inventario

## Sistema de Espacios

El inventario tiene 5 espacios. Los dados más grandes ocupan más espacio, forzando al jugador a elegir entre cantidad de dados (más combos posibles) y potencia de cada dado (más daño por cara).

| Tipo de Dado | Espacios | Rango de Caras | EV | Mult. daño | Rol de diseño |
|---|---|---|---|---|---|
| d3 | 1 | 1–3 | 2.0 | ×0.57 | Generala muy fácil (P=1/9 por tirada). Daño muy bajo. Especialista. |
| d4 | 1 | 1–4 | 2.5 | ×0.71 | Combos fáciles, daño bajo. Cazador de repeticiones. |
| d6 | 1 | 1–6 | 3.5 | ×1.00 | Referencia base. Equilibrado. |
| d8 | 1 | 1–8 | 4.5 | ×1.29 | Mayor EV por espacio. Debe ser poco común o caro en la progresión. |
| d10 | 2 | 1–10 | 5.5 | ×1.57 | Daño alto, menos dados disponibles. |
| d12 | 2 | 1–12 | 6.5 | ×1.86 | Alta varianza, daño bruto. |
| d20 | 3 | 1–20 | 10.5 | ×3.00 | Extremo. Solo cabe con 2 dados chicos. Desbloqueo final del meta. |

> *El multiplicador de daño se calcula como EV_dado / EV_d6 (3.5). El d3 es el dado con más probabilidad de Generala (P=1/9 por tirada) pero el menor daño (×0.57). El d8 tiene el mejor ratio EV/espacio — debe ser poco común o caro en la progresión.*

## Reglas de la Bolsa
* Todos los dados en la bolsa se tiran cada turno de ataque
* Más dados = más opciones para combos
* Dados más grandes (d10, d12) = valores más altos en combos
* La build se arma durante la run a través de tiendas, loot y recompensas
* La combinación elegida define el rango de daño, los combos posibles y la probabilidad de conseguirlos

## Espacio de Builds

Con 6 tipos de dados y 5 espacios con costos variables, el sistema genera **227 builds posibles** (1-5 espacios). Las builds que usan los 5 espacios completos son 120.

| Build | Espacios | Dados | EV/tirada | Mult. | Arquetipo |
|---|---|---|---|---|---|
| **5×d3** | 5 | 5 | 10.0 | ×0.57 | Generala casi segura — daño muy bajo. Consistencia pura. |
| **5×d4** | 5 | 5 | 12.5 | ×0.71 | Cazador de combos fáciles |
| **d3×3 + d6×2** | 5 | 5 | 13.0 | ×0.74 | Generala fácil en los d3 + daño decente de los d6 |
| **5×d6** | 5 | 5 | 17.5 | ×1.00 | Baseline — referencia |
| **5×d8** | 5 | 5 | 22.5 | ×1.29 | ⚠ Muy fuerte — revisar rareza |
| **d12 + 3×d4** | 5 | 4 | 15.0 | ×1.07 | Caótico — 1 dado potente + combos fáciles |
| **d10 + d12 + d6** | 5 | 3 | 18.5 | ×1.59 | Potencia mixta |
| **2×d10 + d6** | 5 | 3 | 15.0 | ×1.05 | Equilibrado medio |
| **d20 + 2×d6** | 5 | 3 | 17.5 | ×1.58 | Apostador — alta varianza |
| **d20 + d4 + d6** | 5 | 3 | 15.5 | ×1.35 | Sniper + soporte |

*Fuente: sistemasDaños.md §1.1, §1.2*

---

# Dados Especiales
[TBD — pendiente de definir con el equipo]

---

# Face Enchanting (Encantamiento de Dados)

En salas especiales, el jugador puede gastar oro para encantar un dado. El encantamiento es aleatorio (estilo gacha controlado). El dado encantado reemplaza al original en el inventario. Conserva el mismo costo de espacios que el dado base.

## Ejemplos de Encantamientos

| Dado | Encantamiento | Efecto |
|---|---|---|
| d6 | de hielo | Si sale 6 → el enemigo pierde velocidad por 1 turno (recarga la barra de energía más lento) |
| d12 | par | Cuando sale cara par (2,4,6,8,10,12) → +20% daño en ese combo |
| d8 | de fuego | Si sale 8 → aplica quemadura (daño continuo por 2 turnos) |
| d4 | doble | Una vez por combate, el valor del d4 cuenta doble |
| d6 | maldito | Siempre hace +15% daño PERO si sale 1 → te quita HP a vos |
| d10 | de veneno | Si contribuye a un combo, el enemigo queda envenenado |

> *Los dados encantados deberían distinguirse visualmente (color diferente, ícono de elemento).*
>
> *❓ ¿Cuántos encantamientos puede tener un dado? ¿Se puede re-encantar o el primer encantamiento es definitivo?*

*Fuente: sistemasDaños.md §4.1*

---

# Barra de Energía y Craps Mode

## Barra de Energía

El jugador tiene una barra de energía que se recarga cada turno. La **velocidad** del jugador determina cuánta energía se recarga por turno. Velocidad baja → energía lenta → Craps poco frecuente. Al llenarse → **Craps Mode se activa**.

## Craps Mode — La Apuesta

Cuando la barra está llena, el jugador puede apostar el resultado de su próxima tirada de combate:

* **Ganás la apuesta → daño ×2**
* **Perdés la apuesta → daño ×0**

> *❓ Por definir: ¿La barra de energía se resetea al usarla o queda en 0 y vuelve a cargarse? ¿Se puede elegir NO usar el Craps si ya está llena?*

*Fuente: sistemasDaños.md §2.2*

---

# Tachar Combos — Mecánica de Crisis

El jugador puede "tachar" uno de los combos de su lista. Al tacharlo, ese combo deja de otorgar daño para el resto de la partida, pero a cambio recibe un beneficio inmediato:

```
TACHAR UN COMBO                →    BENEFICIO INMEDIATO         →    CONSECUENCIA PERMANENTE
(ej: sacrificar Full House)         Opción A: recuperar X% de HP     El combo tachado no vuelve
Ese combo ya NO otorga daño         Opción B: +ataque por N turnos   más en esa corrida.
el resto de la partida.
```

> *El tachar un combo es una mecánica de presión extrema — se usa cuando estás en crisis. Diseñar con cuidado cuántas veces se puede usar por corrida (sugerido: 1 vez por sala, máx. 3 por corrida).*
>
> *❓ Por definir: ¿cuántos combos se pueden tachar por corrida? ¿Los combos tachados se pueden 'restaurar' pagando algo?*

*Fuente: sistemasDaños.md §2.3*

---

# Sistema de Defensa

⚠ *Pendiente de diseño.*

| Opción | Descripción | Ventaja | Riesgo |
|---|---|---|---|
| **Defensa fija** | Valor de armadura que reduce el daño recibido | Simple de entender | Puede hacer el combate muy pasivo |
| **Defensa por combo** | Si el jugador saca cierto combo (ej: escalera), bloquea el próximo ataque | Integra la mecánica de dados a la defensa | Añade complejidad al turno |
| **Defensa como recurso** | El jugador puede gastar dados de su inventario para absorber daño | Crea tensión entre atacar y sobrevivir | Más complejo de implementar |

*Fuente: sistemasDaños.md §2.4*

---

# Sistema de Huida

⚠ *Pendiente de decisión — Se presentan las opciones para discusión del equipo.*

## Costo de Huida (por rango de enemigos)

| Situación al huir | Consecuencia |
|---|---|
| Ningún enemigo tiene al jugador en su rango | Huida sin costo |
| 1 enemigo tiene al jugador en rango | 1 ataque de oportunidad antes de salir. Sin defensa posible. Daño reducido (pendiente: ¿50%?, ¿fijo?) |
| 2+ enemigos tienen al jugador en rango | Cada enemigo ejecuta 1 ataque de oportunidad. Huir rodeado es muy costoso. |

## El Problema Central — El Caso del Arquero

El Arquero bien posicionado huye sin costo porque ningún enemigo lo tiene en rango. Esto es intencional — el Arquero paga por su posición con la falta de movilidad ofensiva.

## Mecánica de Huida — Opciones

| Opción | Cómo funciona | Implicación de diseño |
|---|---|---|
| **A — Tirada de escape** | Tirar dados para escapar. Puede fallar y perder turno. | Tensión alta, pero fallar puede ser muy frustrante. |
| **B — Escape garantizado con costo** | Siempre podés huir, pero enemigos en rango te atacan de oportunidad. | Huida como herramienta táctica predecible. |
| **C — Consumir turno completo** | Huir gasta toda la acción. Los enemigos actúan normalmente antes de que salgas. | Simple de implementar. Siempre recibís al menos 1 round de daño extra. |

> *Recomendación: Opción B (escape garantizado con costo por rango) + reseteo en sala es la más coherente con el diseño roguelike.*

## Enemigos tras la Huida

| Opción | Comportamiento | Implicación |
|---|---|---|
| **Reseteo en sala** | Vuelven a estado inicial. El jugador puede reintentar. | Favorece exploración roguelike. Huir no es irreversible. |
| **Persecución** | Los enemigos te siguen a la sala anterior. | Alta presión, pero puede frustrar si el jugador quedó debilitado. |
| **Sala bloqueada** | Tras huir, la sala se cierra permanentemente. | Penaliza mucho. Riesgo de contenido inaccesible. |

*Fuente: sistema_combate_v5.md §6*

---

# Personajes — Clases y Builds

Las clases son el motor del sistema de progresión. No dicen qué dados usar — dan pasivas que hacen que ciertos dados o combos sean más atractivos. El jugador sigue eligiendo su build libremente, pero la clase sesga naturalmente hacia un estilo.

Las clases se eligen antes de iniciar la run. Nuevas clases se desbloquean permanentemente cumpliendo condiciones en runs anteriores.

## Clase Inicial

| Clase | Pasiva | Dados disponibles |
|---|---|---|
| **Guerrero** (siempre disponible) | Sin pasivas especiales. Daño calculado por fórmula base. El punto de partida limpio para aprender el sistema. | d4 y d6 |

## Clases Desbloqueables

| Clase | Condición de desbloqueo | Pasiva | Builds que potencia |
|---|---|---|---|
| **Berserker** | Win con 5×d6 (Guerrero) | **Primer golpe ×3.** La primera tirada de cada combate vale ×3 daño. No podés guardar dados en la primera tirada. | d8 y d12 — dados de alto EV que maximizan el golpe inicial |
| **Gambler** | Win con 5×d4 (Guerrero) | **Escalera ×2 + Craps anticipado.** La escalera vale el doble. Barra de energía se activa 1 turno antes. | d4 + d12 — rangos amplios para escaleras más fáciles |
| **Necromancer** | Berserker win sin tachar combos | **Triple repetición ×2.** Doble daño cuando 3+ dados muestran el mismo número (trío, póker, generala). | 3×d6 + 2×d12 — buscás tres 6 o tres 12 |
| **Alchemist** | Win con d10 + d12 en la misma build | **Dados encantados ×1.5.** Los dados encantados cuentan ×1.5 en el multiplicador de daño. Sala de encantamiento es parada obligatoria. | Cualquier build — maximizar dados encantados |
| **Trickster** | Gambler win sin usar el Craps | **Tacha doble.** Podés tachar 2 combos por run (en lugar de 1). HP recuperado al tachar es ×2. La clase de gestión de crisis. | Builds extremas — 5×d3 (seguro) o d20+resto (máximo daño) |

*Fuente: rollgeon_progresion.md §2*

---

# Progresión y Meta-Progresión

Rollgeon es un roguelike donde el jugador mejora permanentemente entre runs completando objetivos específicos con builds determinadas. Cada run tiene un inicio claro (elegir clase + bolsa de dados) y un objetivo secundario visible (las condiciones de desbloqueo).

La inspiración principal es The Binding of Isaac: el juego te guía sutilmente hacia estilos de juego distintos sin obligarte, y cada desbloqueo amplía el pool de posibilidades sin invalidar lo anterior.

## Flujo de Meta-Progresión

```
Elegir clase     →    Jugar la run    →    ¿Condición     →    Desbloqueo        →    Nueva run
+ bolsa de dados      (3 jefes)            cumplida?           permanente              con más opciones
                                           (al ganar)          (dado/clase/pasiva)
```

> *Los desbloqueos son permanentes entre runs. Una vez desbloqueado un dado o clase, está disponible para todas las runs futuras.*

## Árbol de Progresión — Condiciones de Desbloqueo

### Tier 0 → Tier 2: Primera Run

| Condición | Resultado | Tipo | Dificultad |
|---|---|---|---|
| Win con 5×d6 (Guerrero) | Desbloquea d8 + clase Berserker | dado + clase | Fácil — build consistente |
| Win con d4×3 + d6×2 (Guerrero) | Desbloquea d10 | dado | Fácil — build mixta |
| Win con 5×d4 (Guerrero) | Desbloquea d12 + clase Gambler | dado + clase | Media — build de baja varianza |

> *Las condiciones requieren mantener la build exacta durante toda la run. El jugador no puede cambiar los dados entre combates si quiere cumplir la condición.*

### Tier 2 → Tier 4: Runs Avanzadas

| Condición | Resultado | Tipo | Diseño de la condición |
|---|---|---|---|
| Win con Berserker sin tachar ningún combo | Desbloquea Necromancer | clase | Sin tachar = no usás el sistema de emergencia. Alta habilidad. |
| Win usando d10 + d12 en la misma build | Desbloquea Alchemist | clase | Explora builds de potencia con pocos dados (3 dados en 5 espacios). |
| Win con Gambler sin activar el Craps | Desbloquea Trickster | clase | Ironía deliberada: el apostador que no apuesta. Requiere control. |

### El d20 — Desbloqueo Final

El d20 es el dado más extremo del sistema (EV 10.5, multiplicador ×3.00, ocupa 3 espacios). Su desbloqueo requiere demostrar conocimiento del sistema completo:

| Condición para desbloquear el d20 |
|---|
| Completar una run con 3 clases distintas (en cualquier orden, en runs separadas) |

> *El d20 no aparece en el menú hasta que se desbloquea. Antes de eso, el slot está vacío o tiene un candado sin ninguna pista — el jugador tiene que descubrir la condición por sí solo o mediante la pantalla de objetos bloqueados.*

## Principios de Diseño para las Condiciones

### Dos Tipos de Condiciones

| Tipo | Descripción | Ejemplo | Cuándo usar |
|---|---|---|---|
| **Simple** | Legible antes de la run. El jugador puede planificar toda la run. | "Ganá con 5×d6" | Tier 1-2. Jugador aprendiendo. |
| **Compleja** | Requiere trackeo de métricas durante la run. Se verifica en resultados. | "Que escalera sea tu combo más usado" | Tier 3-4+. Jugadores avanzados. |

> *Las condiciones complejas son ideas para futuras iteraciones. En el prototipo priorizar condiciones simples.*

### Checklist de Diseño para cada Condición
- ¿El jugador puede leerla antes de la run y entender exactamente qué tiene que hacer?
- ¿Es verificable por el juego sin ambigüedad? (sí/no, no "más o menos")
- ¿Empuja al jugador hacia una build o estilo que no usaría normalmente?
- ¿Es alcanzable en una run razonablemente bien jugada (no requiere suerte extrema)?
- ¿La recompensa tiene sentido temáticamente con la condición?

*Fuente: rollgeon_progresion.md §3, §5*

---

# Menú de Objetos Desbloqueados

La pantalla de "objetos desbloqueados" es el hub central de meta-progresión. El jugador la accede desde el menú principal. Muestra todos los dados y clases del juego, incluyendo los bloqueados.

## Estados de Items

| Estado | Visual | Hover / tooltip | Lógica |
|---|---|---|---|
| Desbloqueado | Item en color con nombre visible | Descripción completa + pasiva | Disponible para seleccionar |
| Bloqueado (condición simple) | Icono con candado, nombre oculto | Condición exacta de desbloqueo | Ej: "Ganá una run con 5×d6" |
| Bloqueado (condición especial) | Icono con candado, nombre oculto | Pista vaga o "???" | Ej: el d20 solo dice "Se desbloquea demostrando dominio del sistema" |
| Bloqueado (no descubierto) | Silueta genérica sin nombre | Sin tooltip — solo "???" | Para items cuya existencia es un secreto |

> *El misterio del candado es parte del loop. Ver un objeto bloqueado sin saber exactamente qué hay detrás genera curiosidad y motiva runs adicionales.*

## Pantalla de Resultados Post-Run

Al terminar una run (win o game over), se muestra una pantalla de resultados:

| Estadística mostrada | Para qué sirve |
|---|---|
| Combos más usados (ranking) | Permite ver si cumpliste condiciones tipo "escalera fue tu combo más usado" |
| Dados usados en la build final | Confirma si cumpliste condiciones de build específica |
| ¿Usaste el Craps? (sí/no) | Para la condición del Gambler |
| ¿Tachaste algún combo? (sí/no) | Para la condición del Berserker |
| Clase usada en esta run | Para trackear el progreso del desbloqueo del d20 |
| Nuevos items desbloqueados | Resalta visualmente qué se desbloqueó |

*Fuente: rollgeon_progresion.md §4*

---

# Sistemas de Mejora Intra-Run

Se plantean 3 sistemas de mejora dentro de la run. El riesgo de diseño es la sobrecarga de sistemas — se recomienda priorizar 1 o 2 para el prototipo.

## 1. Encantamiento de Dados (Prioridad 1)
Ver sección Face Enchanting arriba.

## 2. Contador de Combos (Inspirado en Balatro) (Prioridad 3 — Futuro)

Cada vez que el jugador ejecuta exitosamente un combo específico, suma 1 al contador. Los contadores desbloquean efectos pasivos o aumentan el daño.

```
CONTADOR POR COMBO                    →    AL ALCANZAR EL UMBRAL
Full House: ████░ (4/5)                    → Full House +5% daño permanente
Generala:   ██░░░ (2/5)                    → O desbloquear pasiva especial
Escalera:   █░░░░ (1/5)                    → O buff temporal por N turnos
```

**Variante posible:** Pasiva para el combo MENOS usado: *"La escalera te da escudo si no la usaste en 3 turnos"*

> *Puede generar mucho texto de estado en pantalla. Considerar mostrarlo solo en la pantalla de build, no durante el combate.*

## 3. Subida de Nivel (Prioridad 2)

El jugador sube de nivel al acumular experiencia. Cada nivel otorga uno de:

| Tipo de bonus | Descripción | ¿Complementa los otros sistemas? |
|---|---|---|
| Slot adicional | Desbloquea un 6.° espacio de inventario | ✓ Abre nuevas builds sin romper las existentes |
| Bonus de HP | Aumenta el HP máximo del jugador | ✓ Simple, no interfiere con el sistema de dados |
| Velocidad | Barra de energía se carga más rápido | ✓ Conecta directamente con el sistema Craps |
| Punto de encantamiento | Da 1 encantamiento gratis al subir de nivel | ✓ Si se implementa Face Enchanting |

> *Riesgo: si los 3 sistemas se implementan juntos, el juego puede volverse demasiado complejo para un prototipo. Sugerencia: arrancar solo con Subida de nivel + Encantamiento de dados.*

## Priorización para el Prototipo

| Sistema | Complejidad | Impacto en gameplay | Prioridad |
|---|---|---|---|
| Encantamiento de dados | Media | Alta — cambia el rol de cada dado | 1.° — Implementar primero |
| Subida de nivel | Baja | Media — progresión simple y legible | 2.° — Fácil de agregar |
| Contador de combos (Balatro) | Alta | Alta — pero necesita muchas cartas/pasivas | 3.° — Para iteración futura |

*Fuente: sistemasDaños.md §4*

---

# Estructura del Dungeon

## Layout del Piso

- Entre 8 y 14 salas conectadas en grilla estilo Isaac.
- Navegación libre entre salas mediante puertas.
- El piso termina cuando se derrota al boss.

## Tipos de Sala

| Icono | Tipo | Descripción | Frecuencia |
|---|---|---|---|
| SWD | Combate | Enemigos en grilla, combate por turnos | Común |
| BOS | Boss | Moveset único, pasivas, debuffs | 1 por piso |
| SHP | Tienda | Comprar dados, encantar caras, adquirir pasivas | 1-2 por piso |
| CRP | Craps | Apostar en combo, bonus o penalidad | Rara |
| SKL | Sacrificio | Perder HP máximo a cambio de poder | Rara |
| POT | Poción | Recarga el ítem de poción activo del jugador | Rara |

## Minimap

- Visible en el HUD en todo momento.
- Muestra salas descubiertas; salas adyacentes como contornos.
- Solo tres tipos de sala muestran etiqueta: **T** (Tienda), **B** (Boss), **P** (Poción).
- El resto de salas aparecen como tiles en blanco. Las puertas se muestran como aperturas entre tiles.

## Persistencia de Enemigos

- Los enemigos vivos reaparecen con el HP que tenían cuando el jugador se fue.
- La posición se aleatoriza al re-entrar.
- Los enemigos muertos NO respawnean.

## Progresión entre Pisos

- El jugador avanza al siguiente piso al derrotar al boss.
- Cada piso tiene una estética de casino distinta. Temas TBD por el equipo de arte.

*Fuente: GDD PDF p27-28*

---

# Economía de Oro

- Los enemigos dropean Gold al morir (no mejoras de dados).
- El Gold se suma automáticamente al inventario cuando termina el combate.
- El total de Gold es visible en el HUD en todo momento.

## Tienda

- Items distribuidos en el suelo con nombre y precio visible.
- Al acercarse: descripción completa + botón de compra.
- Precio estándar: aproximadamente la recompensa de 3-4 enemigos. Precio premium: ~5 enemigos.
- Items no comprados persisten entre visitas.

*Fuente: GDD PDF p28*

---

# Sistema de Vida
[TBD — pendiente de definir con el equipo]

---

# Sistema de XP / Subida de Nivel

Ver sección **Sistemas de Mejora Intra-Run → 3. Subida de Nivel** más arriba (Prioridad 2). Contiene la propuesta de bonus por nivel: slot adicional, bonus de HP, velocidad, punto de encantamiento. Fórmula de XP y curva de nivel TBD.

---

# Sistema de Menús

### Pantalla de Bolsa de Dados
- Pantalla de selección al inicio de la run.
- Muestra dados disponibles, costo de cada uno, y presupuesto restante en tiempo real.
- El jugador no puede confirmar si excede el presupuesto.
- Diseño específico TBD.

*Fuente: GDD PDF p37*

---

# UI y Visualización

## HUD — Elementos Siempre Visibles

- **Dados** — elemento central y más prominente
- **Barra de vida del jugador**
- **Barra de energía** (Craps mode)
- **Total de Gold acumulado**
- **Minimap** (esquina)
- **Botón de huida** (solo durante combate)

## UI de Combate

- Zona de dados (zona de tirada + zona de reserva)
- Indicador de tiradas restantes (máx. 3)
- Botón "Atacar" (habilitado solo cuando al menos un dado está colocado)
- Botón "Huir"
- Barras de energía del jugador y enemigo
- Feedback visual de resultados de combate (hit / miss) — pendiente de implementación en prototipo

## UI de Tienda

- Items distribuidos en el suelo con nombre y precio visible.
- Al acercarse: descripción completa + botón de compra.
- Balance de Gold visible en todo momento.

*Fuente: GDD PDF p36-37*

---

# Enemigos

## Arquetipos de Enemigos

Cada enemigo mezcla fantasía clásica + temática de casino:

| Enemigo | Descripción | HP | Dado Movimiento | Daño | Notas |
|---|---|---|---|---|---|
| **Goblin Croupier** | Goblin + estética de dealer de blackjack | TBD | TBD | TBD | — |
| **Dado Viviente** | Criatura dado con conciencia propia | TBD | TBD | TBD | — |
| **Golem de Fichas** | Golem de piedra hecho de fichas de casino | TBD | TBD | TBD | — |
| **Arquero de Rango** | Mantiene distancia mínima; si el jugador llega a 1×1, huye el turno siguiente | TBD | TBD | TBD | Huye si el jugador está en 1×1 |

> *Cada enemigo tiene su propio dado de movimiento y patrón de ataque. Stats específicos TBD.*

## Filosofía de Bosses

Los bosses NO son enemigos con más HP. Tienen **mecánicas únicas** que cambian las reglas del combate: deshabilitar combos, robar dados, aplicar debuffs, o alterar cómo funciona el sistema de Generala en esa pelea. Cada boss fuerza al jugador a adaptar su estrategia. Lista de bosses TBD.

## Combate Doble

Cuando un segundo enemigo está en la sala durante el combate:
- Avanza 1 tile por turno completo hacia el jugador, independientemente de su tipo.
- Cuando llega: el jugador elige a qué enemigo atacar. Ambos enemigos atacan al jugador.
- Termina cuando ambos son derrotados.

*Fuente: GDD PDF p29*

---

# Items y Pasivas
[TBD — pendiente de definir con el equipo]

---

# Save / Progreso
[TBD — pendiente de definir con el equipo]

---

# Audio y Atmósfera
[TBD — pendiente de definir con el equipo]

---

# Tutorial y Accesibilidad
[TBD — pendiente de definir con el equipo]

---

# Monetización

Compra única. Sin microtransacciones. Sin loot boxes.

*Fuente: GDD PDF p8*

---

# Técnico y Desarrollo

## Art Pipeline: Cel Shade + Pixel Filter

Pipeline de renderizado estilizado combinando:
- Cel shading a nivel de material
- Filtro de pixelación por cámara
- Bordes exteriores negros
- Bordes interiores resaltados
- Configurable desde el menú de Settings en runtime

### Post-processing en Cámara
- Main Camera > UniversalAdditionalCameraData > m_RenderPostProcessing: true
- Global Volume > profile: SampleSceneProfile.asset (Tonemapping, Bloom, Vignette, Motion Blur)

### URP Pipeline
- RP Asset activo: `Assets/Settings/PC_RPAsset.asset` > renderer: `PC_Renderer.asset`
- Renderer features activos:
  - **ScreenSpaceAmbientOcclusion**
  - **PixelationFeature** (`Assets/Scripts/Rendering/PixelationFeature.cs` + `Assets/Shaders/Pixelation.shader`)

### Valores Configurados Actualmente

| Parámetro | Valor |
|---|---|
| pixelSize | 4 |
| normalEdgeStrength | 0.567 |
| depthEdgeStrength | 0.492 |

### Gap Identificado
No existe shader **CelShadeLit** en el proyecto. El filtro actual provee pixelación y detección de bordes pero NO toon banding a nivel de material.

### Arquitectura Objetivo

**Etapa Material — Custom/CelShadeLit:**
Shader URP-compatible con:
- Diffuse cuantizado (NdotL banding, 3-5 bandas configurables)
- Specular escalonado (opcional)
- Rim light (opcional)

| Parámetro | Descripción |
|---|---|
| `_BaseColor` | Color base |
| `_ShadowColor` | Color de sombra |
| `_BandCount` | Número de bandas de luz (int, 3-5) |
| `_ShadowThreshold` | Umbral de corte de sombra |
| `_SpecStep` | Paso especular (opcional) |
| `_RimIntensity` | Intensidad de rim light (opcional) |

**Etapa Cámara — ScriptableRendererFeature extendida:**

| Control | Notas |
|---|---|
| PixelSize | — |
| OuterEdgeStrength | — |
| OuterEdgeThreshold | — |
| InnerEdgeStrength | — |
| InnerEdgeThreshold | — |
| EdgeColor | default: black |

### Orden de Render Pass
1. Renderizar escena opaque/transparent con CelShadeLit
2. Extracción de bordes (depth/normal driven)
3. Composición de bordes sobre color de escena
4. Pixelación del frame final compuesto
5. Stack de post-process built-in (si está habilitado)

### Presets de Configuración

| Preset | BandCount | PixelSize | OuterEdge | InnerEdge |
|---|---|---|---|---|
| Toon Soft | 5 | 2 | 0.25 | 0.20 |
| Toon Strong | 3 | 1 | 0.45 | 0.30 |
| Pixel Toon | 4 | 4 | 0.55 | 0.40 |

### Riesgos y Mitigaciones

| Riesgo | Mitigación |
|---|---|
| Flicker de bordes durante movimiento | Ajustar thresholds y clamp response |
| Bordes interiores con ruido excesivo | Bajar inner strength + agregar threshold mínimo de contraste |
| Costo en Mobile | Deshabilitar feature en Mobile_Renderer o usar preset de menor costo |
| Inconsistencia de arte | Forzar uso de CelShadeLit en todos los meshes de gameplay |

*Fuente: GDD PDF p38-41*

---

# Juegos de Referencia

| Juego | Qué tomamos | Qué NO tomamos |
|---|---|---|
| **Balatro** | Profundidad de build, loop adictivo, estética casino, sistema de combos | Falta de exploración, gameplay estático/lineal |
| **The Binding of Isaac** | Mazmorras procedurales, meta-progresión, variedad extrema | Dependencia de reflejos, bullet hell |
| **Crypt of the Necrodancer** | Movimiento en grilla, combate por tiles, ritmo | Sincronización musical |
| **Dicey Dungeons** | Dados como recurso de combate, accesibilidad | Sus dados son consumibles por turno, los nuestros son builds permanentes |
| **Luck be a Landlord** | Sinergias de combos dados/slots, loop de apuesta | Gameplay pasivo, sin agencia del jugador por turno |
| **Dice A Million** | Game feel de dados, feedback visual/sonoro satisfactorio, dados con efectos únicos, dopamina de "los números suben", profundidad de build-crafting | Sin exploración, sin gameplay espacial, pantalla estática |

*Fuente: GDD PDF p8*

---

# Puntos de Venta Únicos

Extraídos del Elevator Pitch y Oportunidad de Mercado (ya documentados en este GDD):

1. **Dados como build permanente** — Los dados no son recursos consumibles por turno (como Dicey Dungeons). Son el inventario, el arma y la identidad del jugador. Se coleccionan, se encantan cara por cara, y persisten durante toda la run.
2. **Generala como sistema de combate** — Un sistema de combos universalmente conocido (especialmente en Argentina/Latinoamérica) aplicado al combate de un roguelite. Barrera de entrada extremadamente baja.
3. **La brecha entre Isaac y Balatro** — Exploración procedural de mazmorras (Isaac) + profundidad de build-crafting estratégico (Balatro) en un solo juego. Sin reflejos, sin gameplay pasivo.
4. **Tensión de apuesta constante** — Cada tirada es una decisión: ¿reservo o retiro? ¿Uso la Generala para atacar o tacho un combo para sobrevivir? El Craps Mode añade una capa más de riesgo/recompensa.

*Fuente: secciones Concepto y Oportunidad de Mercado de este mismo documento*

---

# Filosofía de Diseño y Pilares de Identidad

*Fuente: CLAUDE.md del proyecto + GDD PDF p7. Algunas reglas tienen contradicciones con el estado actual del GDD — marcadas con ⚠.*

| # | Pilar | Descripción | Estado |
|---|---|---|---|
| 1 | **Los dados son TODO** | No son RNG — son el build, el inventario, la identidad del jugador. | ✓ Consistente con todo el GDD |
| 2 | **Core simple, los items rompen reglas** | Base = tirar dados, armar combos, mover. Los items agregan defensa, curación, alcance, huida. Filosofía Isaac. | ✓ Consistente — Items y Pasivas pendiente de diseño |
| 3 | **2 AP por turno** | Mover + Atacar. El orden importa. AP no usados se pierden. | ⚠ Sin decidir: el GDD presenta Opción A (2 acciones: mover+atacar combinables) y Opción B (1 sola acción por turno: mover O atacar). El prototipo actual usa 2 AP (Opción A). |
| 4 | **Tensión de Generala** | Cada ataque es una apuesta: 3 tiradas para armar el mejor combo posible. Reservar o retirar dados, confirmar temprano o arriesgar otra tirada. Sin combo = 0 daño. Cuanto mejor el combo, más daño. | ✓ Consistente con el sistema de combate del GDD |
| 5 | **Estrategia sobre reflejos** | Puramente por turnos. La dificultad está en pensar. | ✓ Consistente con todo el GDD |
| 6 | **Progresión roguelite** | Cada run es única: dados, items, pasivas, salas. | ✓ Consistente con la sección de Meta-Progresión |

---

# Preguntas Abiertas (Pendientes de Discusión del Equipo)

## Del Sistema de Combate (sistema_combate_v5.md)
1. ¿Opción A (2 acciones) u Opción B (1 acción)?
2. ¿Qué tamaño promedio tienen las salas? ¿5×5, 7×7, más?
3. ¿El dado de velocidad (Opción B) suma varianza buena o frustrante?
4. ¿Queremos que el Arquero pueda atacar y reposicionarse en el mismo turno?
5. ¿El dado de velocidad es el resultado exacto o el límite máximo de movimiento?
6. ¿Los monstruos también usan dado de velocidad o tienen losetas fijas?

## Del Sistema de Daños (sistemasDaños.md)
7. ¿Generala con 3 dados = Trío perfecto (base 60) o no existe?
8. ¿Cuántos combos se pueden tachar por corrida? ¿Son restaurables?
9. ¿La barra de energía se resetea al usar el Craps o queda en 0?
10. ¿Los enemigos también tiran dados o tienen un patrón fijo de ataque?
11. ¿Cuántos encantamientos puede acumular un dado?
12. ¿El contador de combos es por corrida o persiste entre partidas?
13. ¿El 6.° espacio de inventario (subida de nivel) queda permanente?

## De Progresión (rollgeon_progresion.md)
14. ¿Cuántas clases totales al lanzamiento? El árbol actual tiene 6.
15. ¿El d3 tiene condición de desbloqueo o está disponible desde el inicio?
16. ¿Se puede cambiar la build entre combates dentro de una run o es fija desde el inicio?
17. ¿Las condiciones bloqueadas Tier 3+ muestran pista o "???" en el menú?
18. ¿El d20 queda disponible para todas las clases o solo avanzadas?
19. ¿Hay límite de clases desbloqueables por run?
20. ¿El sistema de defensa es fijo, por combo, o como recurso?

## Ideas de Condiciones Futuras

| Condición | Dificultad | Posible recompensa |
|---|---|---|
| Win con Necromancer usando solo dados de 1 espacio | Alta | Clase o dado especial nuevo |
| Win sin usar la sala de encantamiento en toda la run | Media | Dado con encantamiento preinstalado |
| Win con el combo "par" como único combo en el último jefe | Muy alta | Pasiva global de meta |
| Win con escalera como combo más usado en toda la run | Alta | Item relacionado con escaleras |
| Win con el d20 incluido en la build | Media (una vez desbloqueado) | Skin o variante visual |
| Game over en el tercer jefe con HP máximo (bug intencional) | Secreto | Easter egg / clase oculta |

*Fuente: rollgeon_progresion.md §5.3, §6*

---

*— Rollgeon · Game Design Document V2 · v0.1 —*
