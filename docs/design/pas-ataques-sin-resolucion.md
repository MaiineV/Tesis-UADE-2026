# Ataques sin resolución posicional

> Auditoría del 12/08/2026. Geometría portada de `ThreatAreaShape.cs` y corrida
> con BFS de 4 vecinos desde las 77 casillas de la sala, para cada posición
> posible del jefe. Ver [`pas-techo-dano-telegraphs.md`](pas-techo-dano-telegraphs.md).

## Problema

**Qué pasa:**
- El presupuesto de esquiva es **4 casillas de camino real, una sola vez por turno** (`CH_Warrior.asset`, acción Movement: `EnergyCost 1`, `BlockOnRepeat: true`, `SelectionSettings.Range 4`, `RangeMode PathReachable`; BFS de 4 vecinos en `MovementService`).
- **`HalfRoom` del Boss 3 es inesquivable desde 21 de las 77 casillas** y pega 100. Se corta en `x = 5` y desde las columnas del borde hacen falta 5 o 6 pasos. Está en `develop`, no es una propuesta.
- El eje vertical es el **default del nodo** (`AINode_TelegraphMark`), así que cualquier `HalfRoom` nuevo nace con el mismo defecto.
- En las propuestas: la **mano Generala** amenaza las 77 casillas (sin salida desde el 100%), y **La Casa** cruza reglas invertidas cuya intersección queda vacía.
- El **área 5×5 del Boss 1** no es inesquivable, pero prohíbe atacar: cubre todas las casillas a distancia 2 y el jugador pega a distancia 1 (`Base Attack`, `Range 1`). En el 0% de los casos existe una casilla de melee no amenazada.

**Impacto en el jugador:**
- Contra el Boss 3, parado en el borde, no hay input posible: el golpe entra y es su vida completa.
- Contra el Boss 1, la mitad de los turnos son turnos donde pegarle cuesta 40. La pelea no cierra: 14 turnos o más.
- Contra La Casa en la ronda 6, cuatro de cada cinco veces no hay dónde pararse.

---

## Análisis

**Por qué pasa:**
- Las formas se diseñaron por su lectura visual, no contra el presupuesto de movimiento. Nadie había medido el presupuesto: son 4 pasos, no "moverse".
- `R08 Audiencia` (anillo pegado al jefe) y `R09 Desalojo` (perímetro) tienen **intersección vacía por identidad geométrica** en cuanto el jefe se despega de una pared. El colapso de La Casa no es mala suerte de la ruleta: arranca en la **segunda** regla.
- Amenazar la sala entera y amenazar mucho no son lo mismo, y el documento los trataba igual.

**Variables que influyen:**
- `HalfAxis`: 0 (vertical) → caminata máxima 6. Con 1 (horizontal) → **4**, porque la sala es 11 × 7.
- `ThreatShape.ScatteredSquares` + `CenterAnchorPool`: ancla en el 50% central **por construcción** → el anillo de borde nunca se amenaza (32 casillas seguras garantizadas).
- Reglas invertidas simultáneas: 1 regla → seguro en 96% de las posiciones; 2 → 64%; 3 → 26%; 4 → 1%.
- `SquareAroundSelf Size ≥ 1` → siempre cubre las casillas de melee. Incompatible con un jugador de alcance 1.

---

## Opciones

### A: Garantía dura en el nodo
`AINode_EnforceRules` y `TelegraphMark` validan que queden ≥3 casillas seguras alcanzables; si no, relajan la última regla o achican la forma.
- **Pro:** cubre todos los casos presentes y futuros, y se testea sin escena.
- **Contra:** un nodo nuevo y una regla implícita que el jugador no ve.
- **Esfuerzo:** medio

### B: Arreglar cada forma con los campos que ya existen
`HalfAxis` a 1; mano Generala de `WholeRoom 90` a `ScatteredSquares Count 8 Size 3`; techo de dos reglas simultáneas en La Casa; fase 2 de La Casa con Distanciamiento como única regla.
- **Pro:** cero código. Cada arreglo mantiene la tensión y le da una lectura propia a la sala: "corré al eje", "corré a la pared", "no te muevas".
- **Contra:** no previene el próximo caso mal dimensionado.
- **Esfuerzo:** bajo

### C: Ampliar el presupuesto de movimiento
Subir `Range` de 4 a 6, o quitar `BlockOnRepeat` de Movement.
- **Pro:** un campo, y todos los casos límite se aflojan a la vez.
- **Contra:** habilita el kiteo infinito, que ya rompe al Boss 2 hoy. Cambia el techo táctico de todo el juego para arreglar cuatro ataques.
- **Esfuerzo:** bajo, con downstream alto

---

## Decisión

**Elegimos: B ahora, A después** — recomendación de la auditoría, pendiente de mesa.

**Justificación:** Los cuatro casos se arreglan con campos que ya existen y sin perder tensión; la garantía en el nodo es la red para el contenido que venga, no el arreglo de este.

**Cambios concretos:**
- `HalfAxis: 0 → 1` en `ED_Boss_GeneralDirector.asset`. Caminata máxima 6 → **4**. Una línea.
- Mano Generala: `WholeRoom 90` → `ScatteredSquares Count 8 Size 3`. Deja **32 casillas de borde seguras** por construcción, peor caminata 3.
- La Casa: techo de **2 reglas simultáneas** — la tercera deroga la primera. Con 2 reglas la intersección es vacía en el 1% y se llega desde el 64%.
- La Casa fase 2: **≤65 de daño** y una sola regla, **Distanciamiento** (la casilla segura es la tuya). La resolución pasa a ser *no moverse*: difícil, con salida, y no obvia.
- Boss 1: el área deja de ser `SquareAroundSelf Size 2`. O pasa a `Size 1` con un anillo de melee libre, o el jefe se mueve después de marcar. Sin esto, atacarlo cuesta 40 sí o sí.
- Ninguna franja de más de 50 de daño con `Size 1`: `HalfBand = (width-1)/2` hace que `Size 1` y `Size 2` sean la misma banda, así que el primer escalón real es `Size 3`.

**Status:** [TBD] — el `HalfAxis` se puede aplicar ya, es una línea y arregla el único ataque inesquivable que está en producción.
