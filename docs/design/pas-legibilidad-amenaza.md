# Legibilidad de la amenaza: el overlay no distingue estados

> Auditoría del 12/08/2026. Fuente: `ThreatTelegraphOverlay.cs`,
> `TileHighlightService.cs`, `AINode_TelegraphMark.cs`, `AINode_ExecuteTelegraph.cs`.
> Ver [`pas-ataques-sin-resolucion.md`](pas-ataques-sin-resolucion.md).

## Problema

**Qué pasa:**
- `ThreatTelegraphOverlay.Show(sourceGuid, tiles)` **no tiene parámetro de estado, severidad ni estilo**. Marcar y detonar entran por la misma puerta.
- Hay **un único `Material` compartido** por todos los quads, con el color hardcodeado, y el pulso escribe sobre ese material: todas las amenazas del juego laten juntas y del mismo naranja.
- El turno en que la marca detona se ve **idéntico** al turno en que se marcó. La única diferencia es la animación del jefe.
- Con 3 reglas simultáneas (La Casa) se apilan 3 sets de quads del mismo naranja al 55% de alpha: el piso desaparece.
- El alcance de movimiento **sólo se pinta al hacer hover sobre el botón**, y no existe estado para "segura pero fuera de alcance".

**Impacto en el jugador:**
- El jugador tiene **un** movimiento por turno, irreversible, de 4 casillas de camino real, y tiene que decidirlo sin saber qué casillas están seguras *y* a tiro. La decisión es a ciegas.
- No puede distinguir "no vi la salida" de "no llegaba", que son dos errores distintos y una de las dos es culpa del juego.
- El corrimiento de hoja del Anotador vive en un panel cerrado que ni escucha el evento de cambio de modificadores: su mecánica central es invisible por defecto.

---

## Análisis

**Por qué pasa:**
- El overlay se escribió para un solo jefe con una sola amenaza por turno. Nunca tuvo que expresar simultaneidad ni urgencia.
- `AINode_ExecuteTelegraph` llama `ClearOverlay()` **antes** de resolver el daño, a propósito, para que los tiles queden prendidos durante el windup. Eso hace que el estado "detona" no tenga representación posible.

**Variables que influyen:**
- Amenazas simultáneas por turno: 1 hoy → hasta **3** con los jefes nuevos.
- Porcentaje de sala amenazada: 21% (sector del Croupier) a **100%** (mano Generala). Arriba del 60% pintar la amenaza deja de informar.
- `TileHighlightService._styleTextures` → `_BaseMap`: el canal de patrón **ya está implementado** y no se usa para amenazas.

---

## Opciones

### A: Estado en `Show` + gramática de 3 canales
`Show` acepta un estado; el relleno usa 2 tonos (naranja = duele, cian = seguro declarado), el patrón lleva el estado temporal, el pulso lleva la urgencia, y un numeral desambigua el apilamiento.
- **Pro:** cinco estados distinguibles **en escala de grises**, así que resuelve daltonismo sin depender del color. Usa el canal de patrón que ya existe: cero arte nuevo.
- **Contra:** hay que romper el material compartido y hacer el pulso por estado.
- **Esfuerzo:** medio

### B: Sólo separar "marcado" de "detona"
Dos materiales y dos alphas, sin patrón ni numeral.
- **Pro:** arregla el problema de los tres jefes actuales con muy poco.
- **Contra:** no soporta amenazas simultáneas ni el modo invertido. La Casa y la Generala siguen sin poder mostrarse.
- **Esfuerzo:** bajo

### C: No tocar el overlay y limitar el diseño
Prohibir más de una amenaza simultánea y descartar los jefes que apilan reglas.
- **Pro:** cero trabajo de UI.
- **Contra:** saca de la mesa a La Casa, La Generala y el jackpot de La Bandida. El costo se paga en contenido.
- **Esfuerzo:** nulo, con costo de diseño alto

---

## Decisión

**Elegimos: Opción A** — recomendación de la auditoría, pendiente de mesa.

**Justificación:** Es el cambio con más apalancamiento del documento entero: sin estado en el overlay no se puede hacer ninguno de los seis jefes nuevos, y los tres actuales ya se leen mal. B no habilita nada nuevo y C se paga en contenido.

**Cambios concretos:**
- `Show(sourceGuid, tiles, state)` con cinco estados: **marcado** (rayado, pulso lento), **detona ahora** (sólido, sin pulso, flash de 1 frame), **cae en 2 turnos** (punteado), **seguro declarado** (cian, damero, pulso en contra-fase), **seguro parcial** (damero flojo, con fracción `n/N`).
- Material por estado, no compartido. Dedupear casillas y pintar en un solo pass por estado.
- **Política de inversión:** si lo amenazado supera el **60%** de las casillas caminables, se pinta el complemento seguro. Con ese umbral entran La Casa (91-99%), la mano Generala (100%) y el jackpot 7×7 (64%), y queda afuera `HalfRoom` (50%), que se lee bien como área.
- Apilamiento: el patrón pasa a rayado cruzado (una sola densidad extra) y el **numeral suma** — 30 / 60 / 90. A partir de la tercera capa el numeral es lo único que informa, y alcanza.
- Balde nuevo **"segura pero fuera de alcance"**, persistente y sin hover. Regla: ningún dato crítico vive sólo en el hover — el HUD actual es mouse-driven y no hay equivalente para gamepad.
- `ThreatLegendView` persistente que muestre el patrón real, no un cuadrado de color.

**Status:** [TBD] — pendiente. Es prerrequisito de los seis jefes nuevos, no una mejora cosmética.
