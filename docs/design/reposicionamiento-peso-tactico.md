# Análisis: Falta de peso en el reposicionamiento durante el combate

> **Origen:** análisis inicial de Sebastián + Claude a partir de capturas del jefe
> de piso 1, ampliado con Gabi (intuición sobre forzar movimiento) y Maiine
> (diseño). **Estado:** diagnóstico + opciones para discutir en mesa de diseño —
> nada de esto está implementado. **Esta revisión** contrasta cada afirmación
> contra el código real (`Assets/Scripts/Rollgeon/...`) para separar "creemos
> que pasa esto" de "esto es lo que pasa", y ajusta el costo/riesgo de cada
> opción en consecuencia.
> **Contexto del proyecto:** tactics isométrico por turnos, 1 solo personaje
> jugable (Warrior — Berserker/Gambler son unlocks todavía no implementados),
> grilla, acciones fijas (Move / Attack / Special Attack / Heal / Force Door).
> Ambientación de casino/mansión.

---

## 1. Diagnóstico — qué tenemos hoy

A partir de las capturas del jefe del piso 1:

- El combate ocurre en salas con **props temáticos fuertes** (ruleta, mesas de
  pool, jackpot, barriles) que son **100% decorativos** — no bloquean, no dan
  cobertura, no interactúan con nada.
- El tablero (los tiles) **no tiene propiedades**: no hay terreno, no hay
  estados, todos los tiles son intercambiables entre sí.
- El HP de ambos lados es alto (50-63 aprox. en la captura), lo que **extiende
  la duración de la pelea** sin que esa duración esté rellenada de decisiones
  nuevas — son más turnos de lo mismo, no más variedad.
- El set de acciones del jugador no premia ni castiga la posición: moverte no
  cura, no evita nada, no genera ninguna ventaja por sí sola más allá de
  acercarte o alejarte. Es la acción "de trámite" para llegar al rival, no una
  decisión en sí misma.
- Los enemigos comunes, salvo uno, atacan desde su posición fija sin ningún
  componente de área, empuje o desplazamiento (ver 1.1 — esto **no es
  parejo entre los tres tipos**, hay un matiz importante).

### 1.1 Verificación contra el código real

Antes de escribir las opciones, chequeé cada supuesto contra
`Assets/Scripts/Rollgeon` para no diseñar sobre una foto vieja del proyecto.
Tres correcciones importantes:

**(a) El movimiento NO es gratis hoy — ya cuesta lo mismo que un ataque.**
`AD_Move.asset` (`ActionDefinitionSO`, `ActionId: player.move`) tiene
`EnergyCost: 1`. Comparado con el resto del catálogo de acciones
(`Assets/Rollgeon/Actions/*.asset`):

| Acción | EnergyCost |
|---|---|
| `player.move` | **1** |
| `attack.basic` | 1 |
| `attack.special` | 2 |
| `player.heal` | 2 |
| `door.force` | 1 |
| `turn.end` | 0 |

Con un pool de energía por turno chico (`EnemyDataSO.MaxEnergy` default = 3;
el jugador vive en el mismo orden de magnitud vía su propio `Energy`
attribute), moverse **ya compite directamente** con atacar, curar o forzar la
puerta — no es una acción "de trámite" sin costo, es una acción que *cuesta*
pero hoy **no paga nada a cambio** más que achicar distancia. Esto cambia el
diagnóstico: el problema no es "moverse es gratis", es **"moverse cuesta pero
no rinde"**. Es una distinción importante para el punto 2 y para la Opción F
más abajo: la mitad de la infraestructura de "recurso único compartido" **ya
existe** en el sistema de energía — no hay que inventar un pool nuevo, hay que
decidir qué le pasa al jugador cuando ese costo no compra ninguna ventaja
posicional.

**(b) Los enemigos comunes no son todos iguales — el Ranged y el Healer ya
reposicionan.** `ED_RangedEnemy.asset` y `ED_Healer.asset` usan
`AINode_KeepDistance` (kiting): cada turno calculan las tiles alcanzables y
eligen la que maximiza la distancia Manhattan hasta una `IdealDistance`
configurable, huyendo si el jugador se acerca demasiado. El único que **sí**
encaja 100% en el diagnóstico "se planta y pega" es `ED_MeleeCardEnemy`, que
usa `AINode_Move` genérico (acercarse y atacar, sin ningún otro comportamiento
posicional). Conclusión: el hábito de "plantarse y pegar" que el jugador
aprende no viene de *todos* los enemigos comunes — viene específicamente del
Melee, que además es probablemente el enemigo con el que más pelea (early
game). Esto no invalida el diagnóstico, lo hace más preciso: el enemigo a
intervenir primero (ver Opción D) ya está identificado por el propio código.

**(c) Los jefes YA fuerzan reposicionamiento — pero solo los jefes.** Existe un
sistema completo de telegraphing espacial: `AINode_TelegraphMark` marca un
área (`ThreatShape.SquareAroundPlayer` para Boss 1, `Row`/`Column` para Boss 2,
`HalfRoom` para Boss 3) vía `IThreatenedAreaService`, la pinta con
`ThreatTelegraphOverlay`, y `AINode_ExecuteTelegraph` la detona el turno
siguiente si el jugador sigue adentro — exactamente el patrón "telegraph antes
del impacto" de Evertried que cito en la sección 3. Es decir: **la Opción D/H
de este documento ya está construida y en uso, pero acotada a los tres
jefes.** Los tres bosses además corren un sistema de "Contract Modifier"
(`AINode_RotateBlock`, `AINode_PromulgateRule`) que multiplica/prohíbe/rota
combos del jugador con temática de mesa de juego ("Decreto de Valor",
"Auditoría Fiscal", "Clausura Temporal") — es la prueba de que un mecanismo de
riesgo/reglas con sabor a casino **ya encaja** en la identidad del juego, solo
que aplicado al combo en vez de al movimiento (dato relevante para la Opción I
más abajo: el riesgo de que "no encaje temáticamente" es más bajo de lo que
parecía, porque ya hay un precedente que sí encajó).

Lo que el código confirma que **no existe en ningún lado** (ni jefes ni
comunes): push/pull/knockback (no hay una sola clase de ese tipo en todo
`Assets/Scripts/Rollgeon`), terreno con estado/propiedades por tile, ni props
interactivos — los únicos archivos "interactuables" de `Dungeon/` son
`DoorController`, `FloorExitInteractable`, `RoomLayout` y
`FloorShellVisibilityController`, ninguno genérico para mobiliario de sala.
Las Opciones A y B del punto 4 son gaps reales, no supuestos.

Un matiz sobre el HP: `CH_Warrior.asset` tiene `BaseMaxHp: 10` — muy lejos del
50-63 observado en la captura del jefe de piso 1. No es una contradicción: la
captura mide el HP *efectivo* en floor 1 después de rewards de personaje,
unlocks de meta-progresión y el propio escudo del chain, no el stat base. Vale
la pena, aparte de este documento, confirmar con Bocco/Maiine en qué punto de
esa curva de escalado el combate empieza a sentirse largo — puede ser tanto un
problema de ritmo posicional (este doc) como uno de curva de daño (ver
`docs/qa/registro-bugs-pulido.md`, que ya tiene abierto el tema de balance de
escudo).

## 2. Por qué el problema no es "el jefe" — es sistémico

Esto es la idea central del documento: **si arreglás solo al jefe del piso 1,
el problema va a seguir intacto en el resto del juego**, y peor — el jugador
va a llegar al jefe habiendo aprendido, durante horas de combates con el
Melee común (ver 1.1.b), que "plantarse y pegar" es la estrategia correcta. Le
vas a estar pidiendo que rompa un hábito que vos mismo le enseñaste.

El motivo de fondo, ahora más preciso tras 1.1: **el movimiento hoy cuesta
energía pero no devuelve nada.** No es que no tenga costo — lo tiene, y es
igual al de un ataque básico — es que ese costo no compra ninguna ventaja
posicional (esquiva, cobertura, flanqueo, escape de una zona marcada). Un
recurso que se gasta sin generar ningún retorno tiende a dejar de gastarse en
cuanto el jugador nota que "quedarse quieto y atacar" rinde más por punto de
energía. Para que reposicionarse importe, tiene que:

1. **Costar algo** (ya cuesta — ver 1.1.a), y
2. **Dar algo a cambio** (evitar daño, generar ventaja, habilitar otra
   jugada) — o
3. **Ser forzado** por el entorno o el enemigo (el sistema de telegraph de
   los jefes ya hace esto — ver 1.1.c — pero solo ahí).

Sin (2) o (3) extendido al resto del juego, no importa cuántos ataques nuevos
le pongas al jefe: el jugador va a preferir quedarse quieto salvo que lo
obliguen, porque hoy solo un tercio de los enemigos (jefes) y un cuarto de los
enemigos comunes (Ranged/Healer) le dan una razón para no hacerlo.

## 3. Cómo lo resuelven otros juegos del género

Investigué patrones concretos en juegos de 1 personaje / pocos personajes en
grilla o carril, con foco en cómo generan peso al movimiento:

| Juego | Mecánica clave | Qué resuelve |
|---|---|---|
| **Evertried** | Área de efecto siempre visible antes del impacto; enemigos no se mueven hasta que vos te movés | El jugador piensa en términos de "a qué zona entro", no solo "a quién le pego" |
| **Integrity** | Movimiento, ataque y retirada salen del mismo recurso (stamina) | Moverte un tile cuesta lo mismo que atacar — deja de ser gratis |
| **Shogun Showdown** | Mover y atacar son la misma acción (la carta define hacia dónde te movés al pegar) | Elimina la posibilidad de "pegar sin moverse" — está fusionado en el sistema |
| **Into the Breach** | El terreno es un arma: podés empujar enemigos a lava/agua/edificios | El daño indirecto vía posición compite con el daño directo |
| **Wildfrost** | Tablero de 2 carriles, timers de acción visibles en cada unidad, reposicionar aliados es una acción central | El posicionamiento importa tanto como las cartas que jugás; "jugar en piloto automático" te castiga |
| **Path of Achra** | Sin sistema posicional fuerte pese a ser 1 solo PJ | Contraejemplo útil: prueba que "1 PJ" no genera peso posicional solo — hay que diseñarlo a propósito |

Patrón común entre los que sí lo logran: **cada punto de movimiento es una
decisión con trade-off explícito**, no una formalidad para llegar al rival.
Rollgeon ya tiene la pieza de Integrity a medias (1.1.a) y la pieza de
Evertried a medias, solo en jefes (1.1.c) — el trabajo real es extender y
completar, no inventar de cero.

## 4. Opciones de solución

Las agrupo de "quirúrgicas" (tocan poco) a "drásticas" (rediseñan el core
loop). Costo/riesgo ajustado tras 1.1 — varias bajan de precio porque parte de
la infraestructura ya existe.

### A. El tablero tiene reglas propias (tiles con estado)
Fuego, hielo, zonas oscuras, tiles que se marcan y explotan (temática
ruleta).

- **Pros:** el impacto se aplica automáticamente a TODAS las peleas del juego
  sin rediseñar cada enemigo uno por uno; refuerza la identidad temática
  (casino peligroso); escalable a futuros pisos con sets de tiles distintos.
- **Contras:** confirmado en 1.1 que **no hay nada de esto hoy** — es la más
  cara técnicamente de las quirúrgicas (necesitás sistema de tiles con
  propiedades, telegraphing visual claro, balance fino); riesgo de
  sobrecargar de información una pantalla que ya tiene bastante UI.
- **Entry point técnico:** `IGridManager`/`GridCoord` ya existen como base de
  grilla; el telegraphing visual puede reusar `ThreatTelegraphOverlay` (ver
  1.1.c) en vez de construir uno nuevo — ese es el ahorro real de esta
  opción.

### B. Los props existentes se vuelven interactivos
Mesas de pool/ruleta bloquean movimiento o dan cobertura; se pueden volcar o
romper para generar un camino o refugio de emergencia.

- **Pros:** costo bajo-medio — el arte ya existe, solo le sumás lógica;
  coherencia total con lo que ya construiste visualmente; el jugador "lee" la
  sala de forma intuitiva porque los objetos ya se ven como objetos.
- **Contras:** confirmado en 1.1 que no hay ningún prop interactivo genérico
  hoy (`Dungeon/` solo tiene puertas y layout, nada de mobiliario); por sí
  sola no fuerza nada, solo habilita — si el jugador no tiene incentivo para
  usarlos, los va a ignorar igual (conviene combinarla con C o D).

### C. Rediseño del kit de acciones del jugador
Que Move dé algo (esquiva pasiva, mini-cura), que Attack tenga bonus por
flanqueo/distancia.

- **Pros:** no requiere contenido nuevo, es puramente sistémico — un cambio,
  impacto en todo el juego; le da identidad mecánica a "Move" como acción con
  valor propio en vez de placeholder que además **ya cuesta energía** (1.1.a)
  — hoy el jugador paga por algo que no le devuelve nada, esta opción cierra
  ese círculo sin tocar el costo.
- **Contras:** toca el corazón del balance — necesita testeo extenso; riesgo
  de volver el juego más "sistema de recursos" y menos visceral si no se
  cuida el feeling.

### D. Enemigos (todos, no solo jefes) empujan/tironean/tienen área
Ataques en línea, en cono, empujones de 1 tile, incluso en enemigos comunes.

- **Pros:** más barato de lo que parecía: `AINode_TelegraphMark` +
  `IThreatenedAreaService` + `SelectionSettings`/`AoeShape` (radio o patrón
  custom bool-grid) **ya existen y ya corren en los 3 jefes** (1.1.c) — extender
  telegraph de área al `ED_MeleeCardEnemy` es enchufar nodos existentes en un
  árbol nuevo, no escribir el sistema de cero; aplicado desde el principio del
  juego, **enseña el lenguaje del reposicionamiento antes de llegar al jefe**
  — resuelve exactamente el problema de raíz identificado en 1.1.b (el Melee
  común es el que enseña el mal hábito); mejor relación costo/aprendizaje del
  jugador de toda la lista.
- **Contras:** el único ingrediente que SÍ falta y hay que construir es el
  empuje/knockback en sí (confirmado ausente en 1.1) — el telegraph de área
  existe, el desplazamiento forzado del jugador no; si se aplica mal (mucho
  empuje, poco control) puede sentirse injusto o frustrante en vez de
  táctico; necesita telegraphing claro de qué tiles van a ser afectados (ya
  resuelto por la pieza que sí existe).

### E. Objetivos secundarios en la sala
Palancas, la propia máquina tragamonedas, algo que conviene activar en otro
punto del mapa.

- **Pros:** genera movimiento sin necesitar ningún ataque enemigo nuevo;
  encaja perfecto con la temática casino (interactuar con el mobiliario del
  casino ES el fantasy del juego); barato de implementar por sala.
- **Contras:** es más "puzzle de sala" que "tensión de combate" — no
  reemplaza el problema de fondo si el resto de la pelea sigue siendo
  estática.

---

### Opciones drásticas (rediseño del core loop)

Estas son más arriesgadas, pero las incluyo porque preguntaste explícitamente
por ideas fuertes, siempre que estén justificadas.

### F. Recurso único compartido (estilo Integrity)
Un solo "pool" de acción por turno que se gasta tanto en moverte como en
atacar/usar habilidades. Ya no es "Move Y Attack cada turno", es "Move O
Attack O una combinación parcial".

- **Pros:** tras 1.1.a, esta opción deja de ser un rediseño desde cero: **el
  pool ya es compartido** (Energy cobra por Move, Attack, Special Attack, Heal
  y Force Door desde `ActionDefinitionSO.EnergyCost`, vía el mismo
  `IEnergyService`). Lo que falta no es infraestructura nueva, es **rebalancear
  los números existentes** (subir el costo relativo de quedarse atacando en el
  mismo lugar turno tras turno, o bajar el de Move) y sumarle el retorno de la
  Opción C. Esto baja el riesgo/esfuerzo de F de "alto" a "medio" — es más
  parecido a una pasada de balance que a un refactor de sistemas.
- **Contras:** sigue siendo el cambio con más superficie de impacto en
  balance ya calibrado (combos, escudo, curva de daño) — pedirle este
  rebalanceo a Bocco mientras cierra el sistema de daño nuevo (deadline
  22/07) es mal timing; dejarlo para después de esa entrega.

### G. Movimiento y ataque fusionados en una sola acción (estilo Shogun Showdown)
Cada "carta" o habilidad que uses ya trae implícito hacia dónde te mueve al
usarla. Elegís la habilidad, no el tile de destino por separado.

- **Pros:** elimina por completo la posibilidad de "pegar sin moverse",
  porque deja de existir esa opción; simplifica la UI (menos botones,
  decisiones más densas); combina muy bien con la temática de
  cartas/casino si en algún momento migran a un sistema de cartas para las
  acciones (el propio `ActionDefinitionSO` ya modela cada acción como una
  entidad de datos autoreable — la pieza de datos para esto ya está, falta el
  diseño de UI/UX).
- **Contras:** cambio de paradigma total en el control del juego; puede
  sentirse menos "libre" para quien ya se acostumbró al control actual;
  altísimo costo de rediseño de todas las habilidades existentes.

### H. El tablero cambia solo, sin que el jugador ataque (arena viva)
La ruleta gira cada X turnos y remarca tiles al azar que se vuelven
peligrosos; la sala "es la casa" y juega en tu contra activamente.

- **Pros:** es la idea más fiel a la temática — la casa siempre gana, el
  escenario mismo es un enemigo pasivo; genera presión de tiempo sin
  necesitar más ataques del jefe; muy memorable/diferenciador si lo venden
  bien en marketing ("hasta la sala está en tu contra"); técnicamente es un
  primo cercano de A + del telegraph que ya existe (1.1.c) — el "timer" es
  solo un contador nuevo sobre `IThreatenedAreaService`.
- **Contras:** puede generar frustración si se siente aleatorio e injusto
  (necesita telegraph fuerte, tipo "este tile se va a marcar en 2 turnos");
  es contenido nuevo por sala, no es gratis escalar a todo el juego.

### I. Apuesta/riesgo como mecánica de movimiento (muy en tema, muy drástico)
Cada vez que te movés "de más" (fuera de lo básico) tirás una especie de
dado/ficha: podés ganar una ventaja grande o quedar expuesto — literalmente
jugás con la casa al reposicionarte.

- **Pros:** conecta mecánica y temática de forma única — y **ya hay
  precedente validado** de que este tipo de mecánica encaja: el sistema de
  Contract Modifier de los 3 jefes (`AINode_PromulgateRule`,
  `AINode_RotateBlock` — "Decreto de Valor", "Auditoría Fiscal", "Clausura
  Temporal") es exactamente esta lógica de riesgo/regla-de-la-casa, aplicada
  al combo en vez de al movimiento (1.1.c). Portar esa identidad al
  movimiento es una extensión temática, no una apuesta sin antecedentes; el
  riesgo/recompensa constante mantiene tensión sin necesitar rediseñar todos
  los enemigos.
- **Contras:** el azar en un tactics puede generar rechazo si no está bien
  calibrado (frustra a jugadores que buscan control total); es un sistema
  nuevo de cero *para movimiento* — aunque el patrón de diseño ya esté
  probado en el combo, la implementación en el grid es distinta.

---

## 5. Recomendación de secuencia (no de una sola opción)

No son excluyentes. Sugiero este orden, de menor a mayor riesgo, ajustado tras
la verificación técnica — el orden barato-primero cambia un poco porque D
resultó más barato de lo estimado originalmente:

1. **Prototipo rápido: D, empezando SOLO por `ED_MeleeCardEnemy`.** Es el
   enemigo que 1.1.b identificó como el único "plántate y pegá" puro, y es
   el que más peleas acumula en early game. Reusar `AINode_TelegraphMark` +
   `IThreatenedAreaService` (ya wireados en los 3 jefes) sobre un árbol nuevo
   para el Melee, sumando el knockback como la única pieza nueva de código.
   Ataca la causa raíz identificada en la sección 2 (dónde se aprende el mal
   hábito) con la menor cantidad de sistemas nuevos.
2. Combinar con **B** en las mismas 2-3 salas de prueba (props que bloquean o
   dan cobertura) — barato porque el arte ya existe, y da un motivo visual
   inmediato para usar las tiles que el telegraph del paso 1 marca como
   peligrosas.
3. Si se siente mejor pero falta punch → evaluar **C** (que Move devuelva
   algo) en conjunto con el rebalanceo de costos de **F** — a esta altura ya
   no son dos iniciativas separadas: F es "tocar los números que ya existen",
   C es "agregar qué compra ese número". Programar esto **después** del cierre
   de damage/balance con Bocco (deadline 22/07) para no pisar esa entrega.
4. Recién si después de eso sigue faltando tensión sistémica → evaluar **A**
   (tiles con estado) y **H** (arena viva) para escalar el impacto a todo el
   juego de forma automática — ambas comparten motor visual con el telegraph
   ya existente, así que el costo real es diseño de contenido, no de sistema.
5. Las opciones **G** e **I** las dejaría en un documento aparte de "ideas
   para una expansión/secuela o pivote grande" — son las que más identidad le
   darían al juego (I en particular ya tiene validación de que el tono
   encaja, ver 1.1.c), pero también las que más arriesgan el trabajo ya
   invertido. Vale la pena que estén escritas y discutidas en algún momento
   con la cabeza fría, no bajo la presión de "arreglemos el jefe del piso 1
   esta semana".

## 6. Preguntas abiertas para la mesa de diseño (Maiine / Gabi / Bocco)

- ¿El knockback del Melee (única pieza de código realmente nueva en el paso 1
  de la secuencia) empuja al jugador o al enemigo? Empujar al jugador refuerza
  la lectura "algo me fuerza a moverme"; empujar al enemigo refuerza "yo
  controlo el espacio". Son identidades distintas, conviene decidirlo antes
  de prototipar.
- ¿Vale la pena, en paralelo a este trabajo, confirmar con Bocco en qué punto
  de la curva de HP (BaseMaxHp 10 → ~50-63 observado en piso 1) el combate
  empieza a sentirse largo? Puede ser el mismo problema visto desde el balance
  en vez de desde el reposicionamiento — las dos lecturas conviven.
- Berserker/Gambler (unlocks futuros, no implementados) — ¿tiene sentido que
  alguna de estas dos clases nazca con una identidad de movimiento distinta a
  la del Warrior (p. ej. Gambler más volátil / cerca de la Opción I), en vez
  de aplicar la misma solución sistémica a las tres por igual?

---

## 7. Propuesta concreta — rediseño de enemigos y jefes

Sebastián pidió replantear el sistema pensando específicamente en
gameplay/enemigos/jefes. Antes de proponer nada relevé el árbol de IA **real**
de cada uno (conteo de nodos por `.asset`) para no proponer sobre una idea de
lo que "debería" tener cada enemigo:

| Enemigo | Nodos posicionales/telegraph hoy | Identidad actual |
|---|---|---|
| `ED_MeleeCardEnemy` | Ninguno — solo `Move` + `Behavior` | Se acerca y pega. El más simple de los 6. |
| `ED_RangedEnemy` | `KeepDistance` + `Move` | Kitea, mantiene distancia ideal. |
| `ED_Healer` | `KeepDistance` + `Move` | Kitea igual que el Ranged (recién reworkeado con heal capado + fallback a distancia). |
| `ED_Boss_Sunken_Grand` (B1) | `KeepDistance` + `TelegraphMark(Square)` + `RotateBlock(Dice)` | Kitea, bloquea dados random, amenaza un 3×3 alrededor tuyo. |
| `ED_Boss_Security_Boss` (B2) | `Move` + `TelegraphMark(Row/Column)` + `RotateBlock(Combo)` | Se acerca, bloquea tus últimos combos, amenaza una franja. |
| `ED_Boss_GeneralDirector` (B3) | `Move` ×2 + `TelegraphMark(HalfRoom)` + `PromulgateRule` | Se acerca (dos veces por turno), reescribe reglas del Contrato, amenaza media sala. |

Cada jefe ya tiene una identidad de movimiento distinta (kitear / acercarse /
acercarse el doble) y una identidad de "regla de la casa" distinta (dados /
combos / reglas). La propuesta de abajo **no inventa una cuarta capa** — 
profundiza esas dos que ya existen y recién ahí conecta con el movimiento.

### 7.1 Melee (`ED_MeleeCardEnemy`) — la carga en línea

Es el enemigo con el árbol más pobre y el que más peleas acumula en early
game (1.1.b). Propuesta que **no requiere ningún nodo nuevo**, solo un árbol
nuevo con nodos que ya existen:

1. Turno N: `AINode_TelegraphMark` con `Shape=Row` o `Column` (el que esté
   más alineado con la posición actual del jugador) — "se prepara para
   embestir en esta franja".
2. Turno N+1: `AINode_ExecuteTelegraph` resuelve el daño si el jugador se
   quedó en la franja — **y**, haya golpeado o no, un `AINode_Move` mueve al
   Melee varias tiles a lo largo de ese eje (la embestida lo desplaza igual,
   conecte o no).

Efecto de diseño: si el jugador se queda quieto, lo pega. Si se mueve fuera
del eje, esquiva **y además el Melee queda sobre-extendido** lejos de su
posición original — potencialmente aislado de otros enemigos, o más cerca de
una esquina/prop (si Opción B llega a existir). Es la versión más barata
posible de "forzar reposicionamiento": cero C# nuevo, un árbol de
comportamiento nuevo. El push/knockback real (empujar al jugador) queda como
mejora posterior, no como bloqueante de esta primera iteración.

### 7.2 Ranged (`ED_RangedEnemy`) — la distancia ideal deja de ser 100% segura

Hoy kitear es una estrategia sin contrapartida: el Ranged huye, el jugador lo
persigue o lo ignora, pero la distancia "ideal" del Ranged es un lugar
perfectamente seguro para él. Propuesta: que atacar desde esa distancia ideal
tenga su propio telegraph de línea (reusando `TelegraphMark` con
`AoeShape.Custom`/patrón bool-grid de `SelectionSettings`, ya genérico, para
marcar el carril recto entre el Ranged y el jugador en vez de fila/columna
entera). Esto le da al jugador una tercera opción además de "perseguir" o
"ignorar": **cortar la línea de tiro** metiéndose en un ángulo, algo que hoy
no tiene ningún motivo mecánico para hacer. *(Nota: no verifiqué en código
cómo dispara hoy el Ranged su ataque base — esto es una propuesta a validar
contra `EnemyActionBehavior`/`TreeDrivenEnemyAI` antes de estimar esfuerzo
real, no un hallazgo confirmado como 7.1).*

### 7.3 Healer (`ED_Healer`) — la sanación como zona, no como click

Recién reworkeada (heal capado + fallback a distancia + `PcAllyBelowMaxExists`
— ver `docs/design/...`/commits recientes), pero su heal sigue siendo
"instantáneo e indiscutible" una vez que decide curar. Propuesta: que la
curación se resuelva con el mismo patrón telegraph-then-execute que los
jefes — turno N marca con un área suave (visualmente distinta al telegraph de
amenaza: es una promesa de heal, no de daño) al aliado que va a curar; si el
jugador logra entrar en esa zona/matar al objetivo antes de N+1, interrumpe la
cura. Esto convierte al Healer en el primer enemigo común que **premia**
acercarse (no solo enemigos que castigan quedarse quieto) — variedad de
lectura espacial en vez de "todo empuja para el mismo lado".

### 7.4 Boss 1 — Sunken Grand: la marea también amenaza de cerca

Hoy el `TelegraphMark(Square)` siempre se centra en el jugador, sea cual sea
la distancia. Como este jefe **ya kitea** (`KeepDistance`), tiene sentido que
el peligro real esté ligado a *quedarse lejos* vs *cerrar distancia* de forma
más marcada de lo que un simple kite comunica hoy: cuando el jugador está
adyacente, cambiar el ancla del `Square` de "alrededor del jugador" a
"alrededor del propio Boss" (un backwash/resaca al ser tocado de cerca). El
jugador aprende: perseguirlo de cerca tiene su propio riesgo distinto al de
quedarse en su rango de tiro — hoy ambas cosas se sienten igual de neutras.
Requiere solo un `AINode_If(distancia<=1)` nuevo antes del `TelegraphMark`
existente, no un sistema nuevo.

### 7.5 Boss 2 — Security Boss: la franja se vuelve vigilancia activa

Hoy `Row`/`Column` se calcula sobre la posición del jugador en el momento del
mark, estático hasta N+1. Como este jefe **se acerca** (`Move`, no kitea),
tiene más sentido de vigilancia/seguridad que la franja "seguidora" gire
según hacia dónde se mueve el Boss, no según dónde está el jugador — leer la
franja como un foco de vigilancia que hay que esquivar activamente, no un
área fija que se marca y se olvida. Combinado con el `RotateBlock(Combo)`
existente (bloquea tus últimos combos), la lectura completa sería: "estoy
siendo vigilado Y me están limitando las jugadas" — dos presiones de la misma
identidad de seguridad/vigilancia reforzándose. Cambio acotado a la lógica de
`ThreatAreaShape.Compute` para Row/Column (anclar en `SelfCoord` en vez de
`PlayerCoord` para este Boss específico), sin tocar el resto de usos del
mismo nodo.

### 7.6 Boss 3 — General Director: una regla espacial en el Contrato

`AINode_PromulgateRule` ya tiene 6 reglas (`R01`-`R06`) que reescriben el
Contrato de combos — ninguna toca el movimiento. Encaja temáticamente sumar
una séptima:

> **R07 — Reubicación Obligatoria**: mientras esta regla esté activa, la
> próxima acción del jugador en su turno debe ser `player.move` (no puede
> abrir con Attack/Special/Heal) — la casa te obliga a "cambiar de mesa".

Es la forma más barata de portar la identidad de Opción I (apuesta/riesgo con
sabor a casino, ver 1.1.c) al movimiento: no hay que inventar un sistema de
apuesta nuevo, es un caso más en el mismo `switch` que ya reescribe reglas
del Contrato — mismo patrón, mismo lugar en el código
(`AINode_PromulgateRule.ApplyRule`), un flavor distinto (espacial en vez de
numérico). Coordinar con Maiine si "obligar" se resuelve como gate duro
(bloquear los otros botones) o como penalidad blanda (los otros botones
siguen disponibles pero cuestan el doble de energía ese turno) — la segunda
opción es más barata de implementar porque reusa el mismo campo `EnergyCost`
de 1.1.a en vez de un gate nuevo en el HUD de acciones.

### 7.7 Qué sigue siendo trabajo nuevo (no maquillaje sobre lo existente)

Para que quede explícito qué de todo 7.1-7.6 es "reusar árboles/nodos" vs
"escribir código nuevo":

- **Reuso puro, cero C# nuevo:** 7.1 (Melee), 7.4 (Boss 1 — un `If` que ya
  existe como tipo de nodo).
- **Nodo/lógica nueva chica, mismo patrón que uno existente:** 7.2 (Ranged,
  si se confirma que hace falta un `TelegraphMark` propio), 7.5 (Boss 2 —
  variante de `ThreatAreaShape.Compute`), 7.6 (Boss 3 — un caso más en
  `PromulgateRule`, más un hook de "próxima acción forzada" si se elige el
  gate duro).
- **Sistema nuevo real:** 7.3 (Healer — telegraph de heal es un flujo
  distinto al de amenaza, aunque comparta la forma de "marcar y resolver un
  turno después"); el knockback/push mencionado en 7.1 y en la sección 6
  como mejora futura.

Nada de esta sección está aprobado ni priorizado — es para llevar a la mesa
de diseño junto con la sección 5/6. El orden sugerido, si hay que elegir por
dónde arrancar un prototipo: **7.1 primero** (cero riesgo técnico, ataca la
causa raíz), después **7.6 con penalidad blanda** (reusa 1.1.a directamente),
dejando 7.2/7.3/7.4/7.5 para una segunda pasada una vez que 7.1 se valide en
playtest.

---

## Referencias consultadas
- Evertried
- Integrity
- Shogun Showdown
- Into the Breach
- Wildfrost
- Path of Achra (como contraejemplo)
- Book of Demons, Roguebook (referencias visuales/temáticas isométrico + cartas)

## Referencias de código (verificación 1.1)
- `Assets/Rollgeon/Actions/AD_Move.asset`, `AD_AttackBasic.asset`,
  `AD_AttackSpecial.asset`, `AD_Health.asset`, `AD_ForceDoor.asset` —
  `EnergyCost` por acción.
- `Assets/Scripts/Rollgeon/Entities/EnemyDataSO.cs` — `MaxEnergy` default.
- `Assets/Scripts/Rollgeon/Combat/AI/Decisions/AINode_KeepDistance.cs` — kiting
  de Ranged/Healer.
- `Assets/Scripts/Rollgeon/Combat/AI/Decisions/AINode_TelegraphMark.cs`,
  `AINode_ExecuteTelegraph.cs`, `Combat/Threat/IThreatenedAreaService.cs`,
  `Combat/Threat/ThreatTelegraphOverlay.cs` — telegraph de área en jefes.
- `Assets/Scripts/Rollgeon/Combat/AI/Decisions/AINode_RotateBlock.cs`,
  `AINode_PromulgateRule.cs` — Contract Modifier de los 3 jefes.
- `Assets/Scripts/Rollgeon/Effects/Selection/SelectionSettings.cs`,
  `AoeShape.cs` — sistema genérico de AoE (radio / patrón custom) reusado por
  telegraph y por ataques de jugador/enemigo.
- `Assets/Rollgeon/Enemies/ED_MeleeCardEnemy.asset` — único común con
  `AINode_Move` puro, sin componente posicional.
- `Assets/Rollgeon/Classes/CH_Warrior.asset` — `BaseMaxHp: 10`.

## Referencias de código (verificación 7 — árboles de IA reales)
- Conteo de nodos por `.asset` de `ED_RangedEnemy`, `ED_Healer`,
  `ED_Boss_Sunken_Grand`, `ED_Boss_Security_Boss`, `ED_Boss_GeneralDirector`,
  `ED_MeleeCardEnemy` (grep de clases `AINode_*` en cada asset — tabla §7).
- `Assets/Scripts/Rollgeon/Combat/AI/Decisions/AINode_ExecuteTelegraph.cs` —
  resolución del daño guardado un turno después (o miss si el jugador se
  movió).
- `Assets/Scripts/Rollgeon/Combat/AI/Decisions/AINode_ApplyStatModifier.cs` —
  patrón de Fase 2 (buff permanente de Attack/Speed) ya usado por los 3 jefes,
  mismo lugar donde podría vivir cualquier ajuste de movimiento por fase.
