# PAS — ¿Falta una acción de defensa pura?

> **Origen:** planteo del profesor (julio 2026). **Estado:** análisis para decisión
> de equipo. **Autores:** Sebastián + Claude. **Relacionado:** Spec de Escudo v2
> (Bocco, 08/07/2026), `docs/planning/plan-de-accion-2026-07-09.md` §5.

---

## Problema

En Rollgeon el escudo solo se genera como fase encadenada al ataque: **si el
jugador no ataca, no puede tirar para generar escudo**. No existe una acción de
"solo defenderse". El profesor plantea dos preguntas:

1. ¿Es esto una problemática real de diseño?
2. ¿Una habilidad defensiva pura le agregaría al apartado táctico del juego?

### ¿Cuándo se sentiría la falta?

Estados de juego concretos donde un jugador pediría "defender sin atacar":

- **HP crítico, sin kill garantizado**: 2 HP, rodeado, atacar no mata a nadie —
  el jugador quisiera cerrarse y sobrevivir el turno.
- **Mal roll anticipado**: sin energía para rerolls, con dados pobres — atacar
  "desperdicia" el turno; defender sería el plan B digno.
- **Espera posicional**: el enemigo ranged no entró en rango; el jugador querría
  pasar turno sin quedar expuesto.

Son estados reales y frecuentes en pisos 2-3. El planteo **no es inventado** —
la pregunta es si la respuesta correcta es una acción nueva o si la ausencia es
una decisión de diseño defendible.

---

## Análisis

### A favor de agregar defensa pura

| Argumento | Peso |
|---|---|
| Más decisiones significativas por turno (atacar / defender / posicionar) | Alto |
| Salida digna a un mal roll — reduce frustración por varianza de dados | Medio |
| Abre espacio de build defensivo (clases tanque, encantamientos de escudo) | Medio |
| Lo señala un evaluador externo → señal de que el diseño actual no comunica su intención | Alto como *síntoma*, no como solución |

### En contra

| Argumento | Peso |
|---|---|
| **Turtling**: defender en loop hasta el roll perfecto es la estrategia dominante obvia en cualquier juego de dados con defensa barata. Es exactamente la anti-tensión que `ESCUDO_CAP = 8` combate. | Alto |
| **Identidad**: el loop actual es push-your-luck — atacás para *ganarte* el derecho a defenderte. Riesgo y recompensa acoplados es la firma del juego. Una defensa gratuita desacopla eso. | Alto |
| **Dato duro de balance**: enemigos actuales pegan 1-2 de daño. Una defensa pura spammeable con escudo hasta 8 = casi-inmunidad permanente. Cualquier versión aceptable necesita costo real (energía alta, no-stack, cooldown). | Alto |
| **Scope de tesis**: acción nueva = UI + animación + balance + tests en las semanas finales. | Alto |

### El punto clave

El sistema **ya tiene** una respuesta parcial al estado "no quiero atacar":
la economía de energía y los rerolls existen para convertir un mal roll en uno
aceptable. Y el diseño de "defensa post-ataque" es coherente con el género
(en Balatro no jugás una mano defensiva; en Slay the Spire el block compite por
el mismo recurso que el ataque — acá compite por el mismo *roll*).

Lo que el planteo del profesor probablemente detecta no es la falta de la
mecánica sino que **el juego no comunica que la ausencia es intencional**: nada
en la UI ni en el tutorial dice "el escudo se gana atacando".

---

## Solución

### Opciones evaluadas

**A. Descarte argumentado (mantener el diseño)**
Documentar la decisión: identidad push-your-luck + anti-turtling. Acompañar con
un fix de *comunicación*, no de mecánica: tooltip/onboarding que explicite
"el escudo se genera al atacar" como regla del mundo.
*Costo: solo docs y un string de UI.*

**B. Aceptación acotada — acción "Guardia"**
Acción defensiva con tirada propia, costo de energía alto, y regla no-stack
(el escudo nuevo **reemplaza**, no suma). El no-stack mata el turtling: dos
Guardias seguidas no acumulan.
*Costo: acción nueva completa (código + UI + balance + tests) en semanas finales.*

**C. Híbrido barato — defensa pura como build**
Sin acción nueva: un encantamiento/upgrade **raro** otorga la opción de defensa
pura (ej. "Estandarte: podés usar tu tirada solo para escudo"). Convierte la
defensa pura en decisión de build (drafteable, renunciable) en vez de opción
por defecto de cada turno — el turtling deja de ser gratis porque ocupó un slot
de poder.
*Costo: un content asset sobre el sistema de encantamientos ya existente
(`ModifyResourceTrigger` genérico ya soporta operaciones sobre Shield).*

### Recomendación

**Opción A ahora, con C como concesión opcional si el equipo quiere darle una
respuesta constructiva al profesor.**

Justificación:

1. La debilidad detectada es de **comunicación**, no de mecánica — se arregla
   con un tooltip, no con una acción.
2. B introduce el riesgo de estrategia dominante (turtling) justo cuando la
   spec de escudo v2 está gastando esfuerzo en lo contrario (cap duro), y su
   costo cae en el peor momento del cronograma de tesis.
3. C responde "sí, exploramos defensa táctica" sin tocar el core loop ni el
   cronograma: usa infraestructura existente, es opcional para el jugador, y
   es contenido descartable si el playtest lo desmiente.
4. Para la defensa oral de la tesis, "lo analizamos y decidimos X por Y" vale
   más que "lo agregamos porque nos lo señalaron".

### Próximos pasos

1. Validar esta recomendación con Bocco y Maiine (misma conversación del
   feedback del PR).
2. Si A: redactar la respuesta formal al profesor (medio párrafo: identidad
   push-your-luck, anti-turtling, referencia al cap) + agregar el tooltip.
3. Si A+C: diseñar el encantamiento en una línea de la planilla de balance y
   dejarlo para el sprint de contenido, post-escudo v2.
