# Sprint 03 — FP: Tareas de Agustin Martinez Maiine
**Proyecto:** TF26 - Déjala Correr - DicenDungeon  
**Board:** M1 — First Playable v1 / Sprint 03 — FP  
**Asignado a:** Agustin Martinez Maiine  
**Fecha de extracción:** 17/04/2026  

---

## Resumen General

| # | Título | Estado | Prioridad | Tiempo Estimado | Categoría |
|---|--------|--------|-----------|-----------------|-----------|
| #100 | Implementar sistema de Energía completo | Planned | 🔴 Urgent | 12h | Programming |
| #97 | Implementar Contrato de Generala por clase — Guerrero | Planned | 🔴 Urgent | 8h | Programming |
| #104 | Implementar sistema de Energía × Re-roll | Planned | 🟠 High | 4h | Programming |
| #98 | Pantalla de selección de clase — Guerrero como única opción disponible | Planned | 🟠 High | 5h | Programming |
| #102 | Pantalla principal — Main Menu (Jugar / Salir) | Planned | 🟠 High | 2h | Programming |
| #95 | HUD completo del First Playable — integración desde Figma a Unity | Planned | 🟠 High | 10h | Programming |
| #99 | Implementar arquetipo de enemigo: Support | Planned | 🟠 High | 6h | Programming |
| #103 | Boss piso 1 — implementación jugable para el FP | Planned | 🟠 High | 8h | Programming |
| #101 | Exponer variables de balance en Inspector de Unity | Planned | 🟡 Normal | 3h | Programming |

**Total estimado: 58 horas**

---

## Cadena de Dependencias (Implícitas por descripción)

> ⚠️ **Nota:** No se registraron dependencias explícitas en HacknPlan (la función de dependency graph es Premium). Las siguientes dependencias son **lógicas/implícitas** detectadas desde las descripciones de las tareas.

```
#102 Pantalla principal (Main Menu)
  └── #98 Pantalla de selección de clase
        └── #97 Contrato de Generala — Guerrero
              └── #98 (requiere el contrato para mostrarlo en la pantalla)

#100 Sistema de Energía completo
  ├── #104 Sistema de Energía × Re-roll (extensión directa del sistema de energía)
  ├── #99  Arquetipo enemigo: Support (tiene barra de energía por turno)
  ├── #103 Boss piso 1 (tiene barra de energía y mecánicas especiales)
  └── #95  HUD completo (barra de energía en HUD depende del sistema implementado)

#95 HUD completo → referencia diseño externo #94 (Figma - no asignado a Agustin)
```

---

## Detalle Completo de Tareas

---

### #100 — Implementar sistema de Energía completo
**Categoría:** Programming  
**Board:** M1 — First Playable v1 / Sprint 03 — FP  
**Estado:** Planned  
**Prioridad/Importancia:** 🔴 Urgent  
**Tiempo estimado:** 12h  
**Asignado:** Agustin Martinez  
**Fecha asignación:** 15 de abril de 2026, 12:30 PM  

#### Objetivo
Implementar el sistema de Energía que reemplaza el loop de turno único actual. Es el cambio estructural más importante del FP.

#### Especificaciones — Sistema de Energía
- Energía máxima: 4 (no acumulable más allá del límite)
- Energía al iniciar la run: 2 de 4
- Al terminar turno: regenera 2 energía base + energía no utilizada, con tope de 4

#### Acciones y costos
| Acción | Costo | Tiradas |
|--------|-------|---------|
| Movimiento | 1 energía | Sin tirada |
| Ataque básico | 1 energía | Generala 3 tiradas |
| Ataque especial | 2 energía | Generala 3 tiradas |
| Curarse | 1 energía | 1 tirada única (requiere poción) |
| Forzar puerta | 2 energía | 1 tirada única |
| Terminar turno | 0 energía | Regenera energía |

#### Restricción central
El jugador NO puede repetir la misma acción dos veces en el mismo turno.  
*Ejemplo: puede Mover + Atacar, pero NO Atacar + Atacar.*

#### Cola de turnos
- Determinada por stat VELOCIDAD de cada entidad (oculta al jugador)
- El jugador actúa primero en su turno
- Los enemigos actúan en orden de velocidad cuando el jugador termina o pasa su turno
- La UI muestra quién actúa a continuación

#### Definición de Done
- [ ] La barra de energía refleja el estado en tiempo real en el HUD
- [ ] Las acciones se deshabilitan si no hay energía suficiente
- [ ] La restricción de no repetir acción está aplicada
- [ ] La regeneración al terminar turno funciona correctamente
- [ ] La cola de turnos por Velocidad funciona y se muestra en UI
- [ ] Probado con al menos un combate completo sin crashes

#### Dependencias (en HacknPlan)
Ninguna registrada.  
**Dependencias implícitas:** Esta tarea es prerequisito para #104, #99, #103 y #95.

---

### #97 — Implementar Contrato de Generala por clase — Guerrero
**Categoría:** Programming  
**Board:** M1 — First Playable v1 / Sprint 03 — FP  
**Estado:** Planned  
**Prioridad/Importancia:** 🔴 Urgent  
**Tiempo estimado:** 8h  
**Asignado:** Agustin Martinez  
**Fecha asignación:** 15 de abril de 2026, 12:30 PM  

#### Objetivo
Reemplazar la tabla de combos global por el sistema de Contrato de Generala por clase. Para el FP, implementar únicamente el Contrato del Guerrero.

#### Contrato del Guerrero
| Combo | Daño base | Condición |
|-------|-----------|-----------|
| Par | 10 | 2 dados muestran el mismo número |
| Doble par | 18 | 2 pares distintos en la misma tirada |
| Suma 4 | 25 | Suma de todos los dados que muestran un 4 |
| Trío | 28 | 3 dados muestran el mismo número |
| Escalera | 35 | Los 5 dados son consecutivos |
| Full house | 40 | Un trío y un par en la misma tirada |
| Póker | 60 | 4 dados muestran el mismo número |
| Generala | 100 | Los 5 dados muestran el mismo número |

#### Comportamiento
- Si ningún combo del Contrato se forma → daño mínimo = valor del dado más alto
- El Contrato debe ser visible en la pantalla de selección de personaje al elegir el Guerrero
- Debe ser fácilmente modificable (ScriptableObject o archivo de config) para balance

#### Debilidad de enemigos
- Cada enemigo tiene un combo al que es débil. Si el jugador logra ese combo, hace más daño.
- Implementar el flag de debilidad y el multiplicador de daño correspondiente
- La UI debe indicar visualmente cuál es la debilidad del enemigo actual

#### Definición de Done
- [ ] Los 8 combos del Guerrero detectan correctamente con 5 dados de distintos tipos
- [ ] El daño calculado corresponde al Contrato
- [ ] El Contrato se muestra en la pantalla de selección de clase
- [ ] El sistema de debilidad de enemigos aplica el multiplicador correctamente
- [ ] Los valores son modificables sin recompilar

#### Dependencias (en HacknPlan)
Ninguna registrada.  
**Dependencias implícitas:** Esta tarea es prerequisito para #98 (debe mostrarse en pantalla de clase).

---

### #104 — Implementar sistema de Energía × Re-roll
**Categoría:** Programming  
**Board:** M1 — First Playable v1 / Sprint 03 — FP  
**Estado:** Planned  
**Prioridad/Importancia:** 🟠 High  
**Tiempo estimado:** 4h  
**Asignado:** Agustin Martinez  
**Fecha asignación:** 15 de abril de 2026, 12:26 PM  

#### Objetivo
Permitir al jugador gastar 1 energía adicional para obtener una tirada extra más allá del límite de 3 re-rolls.

#### Especificaciones

**En ataques (básico/especial):**
- El jugador tiene 3 tiradas incluidas en el costo de la acción
- Si quiere más tiradas después de las 3, puede gastar 1 energía adicional por tirada extra
- Cada re-roll extra consume 1 energía
- La UI debe mostrar claramente que está disponible esta opción y cuánta energía tiene

**En acciones secundarias (curarse, forzar puerta):**
- El jugador tiene 1 tirada incluida en el costo de la acción
- Puede gastar 1 energía adicional para obtener 1 tirada extra
- Ejemplo: forzar puerta cuesta 2 energía (acción) + puede gastar 1 energía más por un re-roll extra

#### Definición de Done
- [ ] El botón de re-roll extra aparece tras agotar las tiradas gratuitas
- [ ] El botón se deshabilita si no hay energía disponible
- [ ] La energía se descuenta correctamente al usarlo
- [ ] Funciona tanto en ataques como en acciones secundarias

#### Dependencias (en HacknPlan)
Ninguna registrada.  
**Dependencias implícitas:** Requiere que #100 (Sistema de Energía completo) esté implementado primero.

---

### #98 — Pantalla de selección de clase — Guerrero como única opción disponible
**Categoría:** Programming  
**Board:** M1 — First Playable v1 / Sprint 03 — FP  
**Estado:** Planned  
**Prioridad/Importancia:** 🟠 High  
**Tiempo estimado:** 5h  
**Asignado:** Agustin Martinez  
**Fecha asignación:** 15 de abril de 2026, 12:26 PM  

#### Objetivo
Implementar la pantalla de selección de personaje previa al armado de build. En el FP solo el Guerrero está disponible.

#### Flujo de la pantalla
- El jugador ve las opciones de clase
- **Guerrero** — disponible, seleccionable
- **Mago y Pícaro** — bloqueados (muestran ícono de candado)
- Al seleccionar Guerrero, se despliega a un costado:
  - Sprite/placeholder del personaje
  - Su Contrato de Generala completo (tabla de 8 combos con daño base)
  - Su Pasiva (si está implementada; puede mostrarse como TBD)
  - Botón **Confirmar** → pasa a la pantalla de armado de build de dados

#### Requisitos de UI
- El Contrato debe ser legible (no demasiado pequeño)
- Las clases bloqueadas deben verse claramente distintas de la disponible
- La transición a la pantalla de build debe ser fluida

#### Definición de Done
- [ ] La pantalla se accede desde el menú principal al presionar Jugar
- [ ] El Guerrero es seleccionable y muestra su Contrato
- [ ] Mago y Pícaro están bloqueados y no son seleccionables
- [ ] El botón Confirmar lleva a la pantalla de build de dados
- [ ] El estado de la clase seleccionada persiste hasta comenzar la run

#### Dependencias (en HacknPlan)
Ninguna registrada.  
**Dependencias implícitas:** Requiere #97 (Contrato de Generala — Guerrero) para mostrar la tabla, y #102 (Main Menu) para la navegación.

---

### #102 — Pantalla principal — Main Menu (Jugar / Salir)
**Categoría:** Programming  
**Board:** M1 — First Playable v1 / Sprint 03 — FP  
**Estado:** Planned  
**Prioridad/Importancia:** 🟠 High  
**Tiempo estimado:** 2h  
**Asignado:** Agustin Martinez  
**Fecha asignación:** 15 de abril de 2026, 12:26 PM  

#### Objetivo
Implementar la pantalla principal del juego que sirva como punto de entrada.

#### Contenido
- Título del juego (nombre provisional o placeholder)
- Botón: **Jugar** → lleva a pantalla de selección de clase
- Botón: **Salir** → cierra la aplicación
- Estética acorde a la dirección visual (oscura, tonos casino)

#### Definición de Done
- [ ] La pantalla aparece al iniciar el juego
- [ ] Jugar navega correctamente a selección de clase
- [ ] Salir cierra la aplicación sin crashes
- [ ] La pantalla respeta la paleta de colores aprobada

#### Dependencias (en HacknPlan)
Ninguna registrada.  
**Dependencias implícitas:** Punto de entrada del flujo. Prerequisito para #98 (Pantalla de selección de clase).

---

### #95 — HUD completo del First Playable — integración desde Figma a Unity
**Categoría:** Programming  
**Board:** M1 — First Playable v1 / Sprint 03 — FP  
**Estado:** Planned  
**Prioridad/Importancia:** 🟠 High  
**Tiempo estimado:** 10h  
**Asignado:** Agustin Martinez  
**Fecha asignación:** 15 de abril de 2026, 12:26 PM  
**Nota:** Tarea referencia el diseño externo #94 (Figma) como base.

#### Objetivo
Implementar en Unity el HUD completo del FP tomando como referencia el diseño aprobado en Figma (#94).

#### Elementos a implementar

**HUD de exploración (siempre visible):**
- Barra de HP del jugador
- Barra de Energía actual (número + visual)
- Contador de Oro
- Íconos de ítems activos: Arco y Poción (activo / inactivo / agotado)
- Minimapa (esquina — corregir rotación si persiste el bug)

**HUD de combate (adicional durante combate):**
- Zona de dados: área de tirada + zona de hold
- Indicador de tiradas restantes (ej: 2/3)
- Nombre del combo detectado resaltado
- Panel del enemigo: nombre + barra de HP + indicador de debilidad
- Barra de energía del modo Craps
- Cola de turnos: quién actúa a continuación
- Botones de acción: Atacar / usar Energía extra / Terminar turno

**Feedback visual:**
- Número de daño flotante al impactar
- Indicación visual de combo logrado (mínimo: texto del combo destacado)
- Feedback al recibir daño

#### Definición de Done
- [ ] Todos los elementos del HUD de exploración funcionan y se actualizan en tiempo real
- [ ] Todos los elementos del HUD de combate funcionan durante el combate
- [ ] El feedback de daño y combo es visible
- [ ] El HUD no tapa el área central de gameplay (perspectiva isométrica)
- [ ] El minimapa está correctamente orientado

#### Dependencias (en HacknPlan)
Ninguna registrada.  
**Dependencias implícitas:** Requiere #100 (Sistema de Energía) para la barra de energía, y referencia diseño externo #94 (Figma, tarea de Art/Design).

---

### #99 — Implementar arquetipo de enemigo: Support
**Categoría:** Programming  
**Board:** M1 — First Playable v1 / Sprint 03 — FP  
**Estado:** Planned  
**Prioridad/Importancia:** 🟠 High  
**Tiempo estimado:** 6h  
**Asignado:** Agustin Martinez  
**Fecha asignación:** 15 de abril de 2026, 12:26 PM  

#### Objetivo
Agregar el tercer arquetipo de enemigo requerido por el FP: el Support. No ataca directamente al jugador sino que cura o potencia a otros enemigos en la sala.

#### Comportamiento
- No tiene ataque directo al jugador
- En su turno: cura a un aliado con menor HP o aplica un buff (ej: aumentar daño del siguiente ataque de un aliado)
- Si no hay aliados vivos, permanece en su posición sin acción ofensiva
- Tiene su propia barra de HP y barra de energía

#### Stats sugeridos (exponer en Inspector para balance)
- HP: a definir en balance
- Fuerza de curación: valor base que aplica a aliados
- Stat de Velocidad: oculta al jugador, determina orden de turno
- Energía máxima por turno

#### Temática (The Sunken Grand)
Es el **Auditor de Mesa** — esqueleto en traje, habla estadísticas durante el combate. Comportamiento narrativo: observa y apoya, no confronta directamente.

#### Definición de Done
- [ ] El Support aparece en salas de combate junto a otros enemigos
- [ ] Su lógica de curación a aliados funciona correctamente
- [ ] Si no hay aliados, permanece pasivo
- [ ] Sus stats están expuestas en el Inspector
- [ ] La barra de HP del Support es visible en el HUD de combate

#### Dependencias (en HacknPlan)
Ninguna registrada.  
**Dependencias implícitas:** Requiere #100 (Sistema de Energía) ya que tiene barra de energía por turno.

---

### #103 — Boss piso 1 — implementación jugable para el FP
**Categoría:** Programming  
**Board:** M1 — First Playable v1 / Sprint 03 — FP  
**Estado:** Planned  
**Prioridad/Importancia:** 🟠 High  
**Tiempo estimado:** 8h  
**Asignado:** Agustin Martinez  
**Fecha asignación:** 15 de abril de 2026, 12:27 PM  

#### Objetivo
Implementar el Boss del primer piso con mecánicas que lo diferencien de un enemigo normal. Debe ser una experiencia completa: diferente, desafiante y con condición de victoria clara.

#### Identidad (The Sunken Grand)
- **Nombre:** Gerente de Piso (Floor Manager)
- Esqueleto impecable en traje de gerente, libro de registros en mano
- Al morir: hace una anotación, emite un recibo, se retira ordenadamente. *No muere en el piso de juego — eso está en el reglamento.*

#### Mecánicas requeridas

**Stats base (exponer en Inspector):**
- HP máximo
- Fuerza de ataque base
- Stat de Velocidad

**Mecánica especial del Boss:**
- Cada 3 turnos, bloquea temporalmente un combo del Contrato del jugador (ese combo no se puede usar por 2 turnos)
- El combo bloqueado se muestra claramente en la UI (tachado o en gris)
- Pasado el bloqueo, el combo vuelve a estar disponible

**Barra de energía del Boss:**
- Al llenarse: aumenta probabilidad de ataque de doble daño

**Condición de victoria:**
- El Boss llega a 0 HP
- Al derrotarlo: aparece objeto interactuable para avanzar al siguiente piso

#### Definición de Done
- [ ] El Boss tiene sus propias stats distintas a los enemigos normales
- [ ] La mecánica de bloqueo de combo funciona cada 3 turnos
- [ ] El combo bloqueado se refleja visualmente en el HUD
- [ ] Al derrotarlo aparece la salida al siguiente piso
- [ ] Todos los valores son modificables sin recompilar

#### Dependencias (en HacknPlan)
Ninguna registrada.  
**Dependencias implícitas:** Requiere #100 (Sistema de Energía) y #97 (Contrato de Generala) para el funcionamiento de la mecánica de bloqueo de combo.

---

### #101 — Exponer variables de balance en Inspector de Unity
**Categoría:** Programming  
**Board:** M1 — First Playable v1 / Sprint 03 — FP  
**Estado:** Planned  
**Prioridad/Importancia:** 🟡 Normal  
**Tiempo estimado:** 3h  
**Asignado:** Agustin Martinez  
**Fecha asignación:** 15 de abril de 2026, 12:27 PM  

#### Objetivo
Garantizar que todos los valores de balance del juego sean modificables sin recompilar. Requisito explícito del FP.

#### Variables a exponer

**Guerrero (jugador):**
- Vida máxima
- Energía inicial al comenzar la run
- Energía máxima
- Daño base por combo (cada fila del Contrato por separado)
- Costo de energía por acción (movimiento, ataque básico, especial, curarse, forzar puerta)
- Stat de Velocidad
- Umbral de curación de la poción
- Umbral de forzar puerta

**Enemigos (por arquetipo: Melee / Ranged / Support):**
- Vida máxima
- Fuerza de ataque
- Fuerza de curación (Support)
- Stat de Velocidad
- Rango de ataque en casillas
- Energía máxima por turno

**Boss:**
- Vida máxima
- Fuerza de ataque
- Stat de Velocidad
- Intervalo de turnos de la mecánica especial

#### Definición de Done
- [ ] Todos los valores listados son accesibles y editables desde el Inspector de Unity
- [ ] Los cambios en el Inspector se reflejan en el gameplay sin recompilar
- [ ] Los valores tienen nombres descriptivos (no nombres de variable crípticos)

#### Dependencias (en HacknPlan)
Ninguna registrada.  
**Dependencias implícitas:** Tarea transversal. Idealmente se realiza en paralelo o al final cuando todos los sistemas (#100, #97, #99, #103) estén implementados.

---

## Flujo de implementación sugerido (por dependencias lógicas)

```
FASE 1 — Base del juego (sin dependencias):
  #102  Pantalla principal — Main Menu (2h) [High]
  #97   Contrato de Generala — Guerrero (8h) [Urgent]
  #100  Sistema de Energía completo (12h) [Urgent]

FASE 2 — Flujo de UI y enemigos (depende de Fase 1):
  #98   Pantalla de selección de clase (5h) [High] → necesita #97 y #102
  #104  Energía × Re-roll (4h) [High] → necesita #100
  #99   Arquetipo enemigo Support (6h) [High] → necesita #100
  #103  Boss piso 1 (8h) [High] → necesita #100 y #97

FASE 3 — Integración visual y balance (depende de Fase 1 y 2):
  #95   HUD completo del FP (10h) [High] → necesita #100 + diseño #94
  #101  Exponer variables en Inspector (3h) [Normal] → tarea transversal final
```

---

*Documento generado automáticamente desde HacknPlan el 17/04/2026.*
