# Balance de Dados — Análisis y Plan de Acción

> Documento de trabajo. Basado en el análisis de Bocco (chat1 + chat2) y continuado con propuestas de solución.
> Objetivo: **que todos los dados sean útiles y tengan identidad propia.**

---

## 1. Estado Actual del Sistema

### Tabla Base de Dados

| Dado | Espacios | Caras | EV | Mult. (EV/3.5) | EV/Espacio | Rol actual |
|------|----------|-------|----|-----------------|------------|------------|
| d3 | 1 | 1–3 | 2.0 | ×0.57 | 2.0 | Consistencia máxima, daño mínimo |
| d4 | 1 | 1–4 | 2.5 | ×0.71 | 2.5 | ❓ Sin identidad clara |
| d6 | 1 | 1–6 | 3.5 | ×1.00 | 3.5 | **Referencia base** |
| d8 | 1 | 1–8 | 4.5 | ×1.29 | **4.5** | ⚠ Estrictamente superior al d6 |
| d10 | 2 | 1–10 | 5.5 | ×1.57 | 2.75 | Doble penalidad (espacio + combos) |
| d12 | 2 | 1–12 | 6.5 | ×1.86 | 3.25 | Doble penalidad (espacio + combos) |
| d20 | 3 | 1–20 | 10.5 | ×3.00 | 3.5 | Apuesta extrema — bien diseñado |

### Fórmula de Daño Actual

```
daño_final = base_combo × multiplicador_dado
multiplicador_dado = EV_promedio_build ÷ 3.5
```

| Combo | Daño base |
|-------|-----------|
| Par | 10 |
| Doble Par | 18 |
| Trío | 28 |
| Escalera | 35 |
| Full House | 40 |
| Póker | 60 |
| Generala | 100 |

### Baseline: 5×d6

- EV por ronda: **35.7**
- Probabilidades clave: Póker 22%, Full House 20%, Generala 1.8%
- Tasa de "nada": muy baja (5 dados = siempre algún combo)

---

## 2. Diagnóstico — Problemas Identificados

### 🔴 Problema 1: El d8 domina al d6 sin trade-off

El d8 cuesta 1 espacio (igual que el d6) pero tiene EV superior (4.5 vs 3.5). No hay ningún motivo para elegir d6 sobre d8.

| Métrica | 5×d6 | 5×d8 | Diferencia |
|---------|-------|-------|------------|
| EV/espacio | 3.5 | **4.5** | +28.6% |
| Multiplicador | ×1.00 | ×1.29 | +29% |
| Combos disponibles | Todos | Todos (misma cantidad de dados) | Igual |
| EV por ronda | 35.7 | 36.6 | **+2.6%** |

**Veredicto:** El d8 hace obsoleto al d6. Es el problema más urgente del sistema.

### 🟡 Problema 2: El d4 no tiene identidad

El d4 queda en tierra de nadie:
- Peor daño que el d6 (×0.71 vs ×1.00) al mismo costo de espacio
- Mejor daño que el d3 (×0.71 vs ×0.57) pero peor en probabilidad de combos
- No es el mejor en nada

| Métrica | d3 | d4 | d6 |
|---------|----|----|-----|
| P(Generala) 5 dados, 3 tiros | ~40-50% | ~15-20% | ~1.8% |
| Mult. daño | ×0.57 | ×0.71 | ×1.00 |
| Nicho | Rey de consistencia | ??? | Referencia |

### 🟡 Problema 3: Dados grandes sufren doble penalidad

Los d10 y d12 pagan dos costos simultáneos:

1. **Costo espacial:** 2 espacios → menos dados en la build → menos combos posibles (sin Full House ni Póker con <5 dados)
2. **Costo probabilístico:** más caras → más difícil hacer match (par entre dos d12 = 8.3% vs par entre dos d6 = 16.7%)

El multiplicador de daño no compensa ambos costos.

| Build | Dados | EV ronda | vs Baseline |
|-------|-------|----------|-------------|
| 5×d6 | 5 | 35.7 | ref |
| d12 + 3×d4 | 4 | 27.6 | **-22.6%** |
| d12 + 3×d6 | 4 | ~29.0 | **-18.8%** |
| 2×d12 + d6 | 3 | ~24.0 | **-32.8%** |
| d20 + 2×d6 | 3 | ~23.0 | **-35.6%** |

**Excepción:** El d20 está bien diseñado como "apuesta extrema" — su problema no es balance sino identidad de nicho (que ya tiene).

### 🟢 No-problema: El d3

El d3 funciona correctamente. Alta consistencia (Full House ~64% con estrategia) pero daño bajo (×0.57). El trade-off es real y se siente en el gameplay.

---

## 3. Soluciones Evaluadas (de los chats de Bocco)

### Ronda 1 — Propuestas iniciales

| Opción | Descripción | Resultado | Veredicto |
|--------|-------------|-----------|-----------|
| **A** | Dados grandes tienen piso mínimo (d10 tira 4-10) | Cambia la identidad del dado, difícil de comunicar | ❌ Descartada |
| **B** | Combos exclusivos para dados grandes ("Power Combo") | Añade complejidad al sistema de combos | ❌ Descartada |
| **C** | Bonus ×1.4 al daño cuando dado grande logra combo fuerte | d12+3×d4 sube a +1.4% vs baseline. Funciona. | ✅ Prometedora |
| **D** | Arreglar el d8 primero (subir a 2 espacios o bajar mult.) | Correcto pero no resuelve d10/d12 | ⚠ Parcial |
| **E** | Multiplicador viene del combo, no del dado | Cambio demasiado radical al sistema | ❌ Descartada |

### Ronda 2 — Refinamiento

| Opción | Descripción | Resultado | Veredicto |
|--------|-------------|-----------|-----------|
| **2A** | Dado grande reemplaza resultado "nada" | Solo beneficia builds extremas (d20, 2×d12) que ya son de alto riesgo. No ayuda a d12+3×d4 que solo tiene 3% de "nada". | ❌ Descartada |
| **2B** | Dado grande suma su valor × factor | No escala: factor que equilibra d12+3×d4 rompe 2×d12+d6 | ❌ Descartada |
| **2C** | **Bonus fijo de daño por dado grande** | Todas las builds dentro de ±5% del baseline. Simple. Tunable. | ✅ **ELEGIDA por Bocco** |

### Solución 2C — Bonus Fijo por Dado Grande

| Dado | Bonus inicial | Bonus ajustado |
|------|---------------|----------------|
| d10 | +4 | +4 |
| d12 | +7 | +8 |
| d20 | +12 | +14 |

**Resultado con bonus ajustado:**

| Build | EV | vs Baseline |
|-------|-----|-------------|
| 5×d6 (baseline) | 35.7 | ref |
| d12 + 3×d4 | 34.6 | -3.1% |
| d12 + 3×d6 | 32.8 | -8.1% |
| 2×d12 + d6 | 34.9 | -2.4% |
| d20 + 2×d6 | 34.1 | -4.5% |

**Fórmula actualizada:**
```
daño_final = (base_combo × multiplicador_dado) + bonus_dados_grandes
```

**UI sugerida:** Mostrar desglose → "Combo: 28 + Bonus d12: +8 = 36"

---

## 4. Lo que Falta Resolver

### 🔴 4.1 — El d8 (sin solución definida)

Bocco identificó el problema pero no llegó a una decisión. Cuatro opciones viables:

| Opción | Cambio | Efecto | Pros | Contras |
|--------|--------|--------|------|---------|
| **8-A** | Subir costo a 2 espacios | 5×d8 se vuelve imposible. Máx: 2×d8 + d6 (3 dados) | Solución limpia, sin excepciones | d8 pasa a competir con d10/d12 en el slot de 2 espacios. ¿Se diferencia? |
| **8-B** | Bajar multiplicador a ×1.10 (EV ficticio de 3.85) | 5×d8 baja de +2.6% a ~+0.5% vs baseline | Mínimo cambio, mantiene d8 a 1 espacio | Parche numérico, no da identidad |
| **8-C** | d8 viene siempre con encantamiento preinstalado | El d8 no es "mejor d6", es "d6 con efecto especial y desventaja asociada" | Da identidad única al d8 | Más complejo. Hay que diseñar encantamientos que no sean pure upgrades |
| **8-D** | **Eliminar el d8 del juego** | El sistema queda con 6 dados: d3, d4, d6, d10, d12, d20 | Cada dado restante tiene identidad clara. Menos variables = más fácil de balancear. | Reduce el espacio de builds. Pierde un escalón intermedio entre d6 y d10. |

#### Análisis profundo de cada opción

**8-A (subir a 2 espacios)** — La solución más intuitiva, pero crea un problema nuevo:

Si el d8 cuesta 2 espacios, compite directamente con d10 y d12:

| | d8 (2 esp) | d10 (2 esp) | d12 (2 esp) |
|---|---|---|---|
| EV | 4.5 | 5.5 | 6.5 |
| Mult. | ×1.29 | ×1.57 | ×1.86 |
| P(par entre dos iguales) | 1/8 = 12.5% | 1/10 = 10% | 1/12 = 8.3% |
| Bonus fijo propuesto | +2 | +4 | +8 |

El d8 a 2 espacios es **estrictamente peor que el d10** en daño (EV menor, mult menor, bonus menor). Su única ventaja es mejor probabilidad de combo (12.5% vs 10% para pares). ¿Es suficiente para justificarlo? Depende de cuánto pese esa diferencia de combo en la práctica.

Para que funcione, el d8 necesitaría ser el "dado grande más fácil para combos" — su identidad sería: "pago 2 espacios pero mantengo buena probabilidad de armar combinaciones". El escalonamiento queda limpio: 1 esp (d3, d4, d6) → 2 esp (d8, d10, d12) → 3 esp (d20).

**8-B (bajar mult. a ×1.10)** — Parche numérico:

5×d8 bajaría de +2.6% a ~+0.5% vs baseline. Matemáticamente funciona, pero el d8 sigue siendo "un d6 ligeramente mejor al mismo costo". No genera decisiones interesantes — ¿por qué un jugador elegiría d6 sobre d8 si el d8 siempre es igual o mejor? La diferencia es mínima pero la dirección siempre favorece al d8.

**8-C (encantamiento preinstalado)** — La más creativa:

El d8 siempre viene con un encantamiento aleatorio, pero ese encantamiento tiene un costo/desventaja asociada. Ejemplo: "d8 de fuego: si sale 8 aplica quemadura, pero si sale 1 te quemás vos". Ahora el d8 no es "mejor d6" — es una apuesta. El jugador lo lleva por el efecto especial, no por las stats puras.

Problema: depende 100% de que el sistema de encantamientos esté diseñado y balanceado. Si los encantamientos no están listos, esta opción no se puede implementar.

**8-D (eliminar el d8)** — La más radical:

¿Necesitamos 7 tipos de dados? Con 6 dados (d3, d4, d6, d10, d12, d20), cada uno tiene identidad clara y sin solapamiento:

| Dado | Identidad sin d8 |
|------|-------------------|
| d3 | Consistencia máxima |
| d4 | Early game / cazador de repeticiones |
| d6 | Referencia base |
| d10 | Primer dado "grande" — potencia con trade-off moderado |
| d12 | Build-around piece — alto riesgo, alta recompensa |
| d20 | Apuesta extrema |

El salto de d6 (1 esp) a d10 (2 esp) es más dramático, lo cual hace que la decisión de usar un dado grande se sienta más pesada. Las builds bajan de 227 a menos, pero el espacio de diseño sigue siendo amplio.

Riesgo: el d8 ya aparece en el GDD, en la meta-progresión (se desbloquea con Berserker), y posiblemente en el prototipo. Eliminarlo tiene costo de retrabajo.

#### Inventario de referencias al d8 en el proyecto (costo real de 8-D)

Si el equipo se inclina por eliminar el d8, este es el trabajo concreto que implica. Relevado directamente del codebase (2026-04-05):

| Tipo | Ubicación | Qué hay | Esfuerzo de remover |
|---|---|---|---|
| **ScriptableObject instance** | `Assets/Data/Dice/d8.asset` (+ .meta) | Asset único del d8 | Borrar archivo + meta |
| **Editor script** | `Assets/Scripts/Editor/DataCreator.cs:39,73,77` | Crea d8 SO, lo agrega al loadout del Warrior (2×d8 en starting bag) | Borrar 3 líneas + recalcular starting dice |
| **Scene setup** | `Assets/Scripts/Managers/SceneSetup.cs:124,126,161,168,174` | Crea d8 runtime, lo incluye en `AvailablePoolDice` del Warrior, 2×d8 en loadout | Borrar ~5 líneas |
| **Grid/Room** | `Assets/Scripts/Grid/RoomData.cs:45` | Comentario `"d6", "d8", "d10", "d12"` en campo `DiceType` de items | Actualizar comentario |
| **Escena** | `Assets/Scenes/SampleScene.unity` | Referencia serializada al d8.asset | Reabrir escena y limpiar refs rotas |
| **GDD y docs** | `Assets/Documents/Game Design Document.md`, `chat1.md`, `chat2.md` | Tabla de dados, balance table, ejemplos | Actualizar GDD |
| **Meta-progresión (GDD)** | GDD sección de unlocks (Berserker) | Desbloqueo mencionado | Reasignar el unlock a otro dado |

**⚠ Hallazgo durante el inventario:** el código actual en `SceneSetup.cs:159` usa `d8 = 1.5 puntos` (no 1 espacio como dice `balance_dados.md §1`). **Ya hay un desbalance entre doc y código** — el equipo debe alinearlos independientemente de la decisión 8-A/B/C/D.

**Estimación total de 8-D (eliminar):** ~1-2 horas de trabajo técnico (edits chicos en 4-5 archivos + limpieza de escena). El costo real está en **redefinir el starting loadout del Warrior** (hoy: 4×d6 + 2×d8) y **validar que las builds del GDD sigan siendo viables** sin d8.

#### Recomendación para el d8

No hay una opción claramente superior. Depende de qué priorice el equipo:

| Prioridad del equipo | Mejor opción |
|---|---|
| Simplicidad y claridad | **8-D** (eliminar) |
| Mínimo cambio al sistema actual | **8-B** (bajar mult.) |
| Escalonamiento limpio de costos | **8-A** (2 espacios) |
| Máxima identidad por dado | **8-C** (enchantamiento) |

### 🟡 4.2 — Identidad del d4

El d4 necesita una razón para existir que no sea "d6 pero peor". Seis propuestas:

| Opción | Mecánica | Por qué funciona | Riesgo |
|--------|----------|-------------------|--------|
| **4-A** | d4 es el "dado de soporte" — combos con d4 dan efectos secundarios (veneno, slow, etc.) | Diferencia rol: d6 = daño, d4 = utilidad | Complica el sistema de combos. ¿Cómo funciona en builds mixtas d4+d6? ¿El efecto se activa solo si el d4 "participó"? Difícil de definir. |
| **4-B** | d4 tiene mayor probabilidad de encantamiento raro en salas de enchanting | Incentiva llevar d4 para builds de Alchemist | Depende de que el sistema de enchanting esté diseñado. Si el enchanting no está listo, el d4 sigue sin identidad. |
| **4-C** | Pasiva implícita: "si todos tus dados son d4 o menores, +1 tirada extra" | Recompensa builds de consistencia sin tocar la fórmula | 5×d4 con 4 tiradas en vez de 3 podría ser demasiado fuerte para combos. Es una regla de excepción que aplica a un subconjunto de builds — difícil de comunicar. |
| **4-D** | El d4 es simplemente el dado de early game (barato en tienda, común en loot) y el d6 es mid-game | No necesita mecánica especial — su valor es accesibilidad y precio | El jugador lo descarta en cuanto consigue d6. Se siente "basura de inventario" en late game. |
| **4-E** | d4 tiene "rebote": si sacás el máximo (4), tirás otra vez y sumás | Identidad "explosiva" a baja escala. EV sube de 2.5 a ~3.33, más cercano al d6. Da emoción al tirar d4. | Un d4 que puede dar 8 (4+4) rompe la tabla de combos. ¿Cuenta como 8 para escaleras? ¿Para pares? Complejidad alta en resolución de combos. |
| **4-F** | **El d4 es el dado táctico: el jugador elige qué cara poner (1-4) en vez de tirar** | Sacrificás daño (máx 4) pero ganás control total. Lo usás para completar combos. Identidad clarísima: "menos daño, más control". | Nunca falla, lo cual puede sentirse demasiado seguro. Reduce la tensión de tirada para ese dado. Podría ser dominante en builds que buscan escalera (Gambler). |

#### Análisis profundo de 4-F (dado táctico — elegir cara)

Ejemplo de uso: tenés 4×d6 + 1×d4. Tirás los d6 y sacás 3-3-5-2. Te falta un 3 para trío o un 4 para escalera. Con el d4 táctico, elegís poner 3 → trío asegurado.

Sinergias:
- **Gambler** (escalera ×2): el d4 táctico garantiza completar escaleras parciales. Build d4+4×d6 con Gambler se vuelve consistente.
- **Alchemist**: si el d4 está encantado, elegir la cara óptima maximiza el efecto del encantamiento.
- **Diferencia con d3**: el d3 es consistencia por probabilidad (pocas caras = fácil match). El d4 táctico es consistencia por control (elegís el resultado). Roles distintos.

Trade-off real vs d6: el d4 táctico nunca da más de 4 en el combo. En una Generala, 5×d4-táctico = todos en 4 = combo base 100 × mult 0.71 = 71 daño. Vs 5×d6 Generala = 100 × 1.00 = 100. El control tiene precio.

Riesgo principal: si el d4 táctico es demasiado bueno para completar combos, se vuelve auto-include en todas las builds. Posible mitigación: limitar a 1 d4 táctico por build, o que solo pueda elegir cara 1 vez por combate (las otras tiradas se tira normal).

> **⚠ Pendiente de simular (bloqueante para cerrar la decisión del d4):** todas las otras secciones de este doc tienen EVs del Monte Carlo de Bocco, pero el 4-F solo tiene análisis cualitativo. Antes de votar, hay que correr la app de Bocco con las siguientes builds y medir:
>
> | Build a simular | Qué buscamos | Por qué |
> |---|---|---|
> | `1×d4-táctico + 4×d6` con Guerrero | EV promedio y tasa de combos (Trío, Escalera) | Build base — ¿sube demasiado sobre baseline? |
> | `1×d4-táctico + 4×d6` con Gambler (escalera ×2) | Tasa de Escalera | El riesgo principal: ¿el d4 táctico garantiza escalera siempre? |
> | `2×d4-táctico + 3×d6` | EV y tasa de Full House | ¿Dos d4 tácticos rompen el sistema? Determina si hay que limitar a 1 por build. |
> | `1×d4-táctico + 1×d4-táctico-1-uso-por-combate + 3×d6` | Validar la mitigación "1 uso por combate" | Confirmar que la mitigación funciona |
>
> Sin estos números la recomendación de 4-F es intuición. **Acción:** pedirle a Bocco correr estas 4 simulaciones antes de la próxima reunión de diseño.

#### Recomendación para el d4

| Prioridad del equipo | Mejor opción |
|---|---|
| Mínima complejidad nueva | **4-D** (early game por diseño) |
| Identidad mecánica fuerte sin tocar fórmulas | **4-F** (dado táctico) |
| Sinergia con sistemas existentes (enchanting) | **4-B + 4-D** (early game + mejor enchanting) |
| Máxima profundidad estratégica | **4-F** con límite de 1 uso por combate |

### 🟡 4.3 — Bonus Fijo: ¿Cómo se acumula con múltiples dados grandes?

La Opción 2C de Bocco funciona bien con 1 dado grande por build. Pero con 2+ dados grandes, el bonus se acumula y puede desbalancear:

| Build | EV combo | Bonus | EV total | vs Baseline | Tasa de "nada" |
|-------|----------|-------|----------|-------------|----------------|
| 5×d6 | 35.7 | 0 | 35.7 | ref | <5% |
| d12+3×d4 | 27.6 | +8 | 35.6 | -0.3% | 3% |
| 2×d12+d6 | 24.0 | **+16** | 40.0 | **+12%** | **42%** |
| d20+2×d6 | 23.0 | +14 | 37.0 | +3.6% | **41%** |

2×d12+d6 con +16 de bonus está **+12% sobre baseline**. Pero tiene 42% de turnos donde no saca combo y el daño es solo 16 (el bonus fijo). ¿Está bien?

**Argumento a favor:** el 42% de "nada" ya es un castigo enorme. En esos turnos hacés 16 daño en vez de 0, que es el punto de la mecánica — que los dados grandes no se sientan inútiles. El EV promedio de 40 compensa que casi la mitad de los turnos son flojos.

**Argumento en contra:** +12% sobre baseline es mucho. Un jugador que arme esta build tiene ventaja estadística clara sin necesitar sinergia de clase.

Tres formas de manejarlo:

| Opción | Regla | Efecto en 2×d12+d6 | Efecto en d20+2×d6 | Complejidad |
|--------|-------|---------------------|---------------------|-------------|
| **Sin cap** | Se suma todo | EV 40.0 (+12%) | EV 37.0 (+3.6%) | Ninguna — regla simple |
| **Cap a 1 bonus** | Solo cuenta el dado grande de mayor bonus | EV 32.0 (-10%) — demasiado castigo | EV 37.0 (+3.6%) — sin cambio (1 solo dado) | Baja — pero penaliza builds multi-dado-grande |
| **Bonus escalado** | Segundo dado grande da 50% de su bonus | EV 36.0 (+0.8%) | EV 37.0 (+3.6%) — sin cambio | Media — regla adicional pero equilibrada |

**Recomendación: Bonus escalado (50% en segundo dado).** Es el sweet spot: 2×d12+d6 baja de +12% a +0.8% sin castigar builds de 1 dado grande. La regla es simple de comunicar: "el primer dado grande da bonus completo, cada dado grande adicional da la mitad".

Fórmula expandida:
```
bonus_total = bonus_dado_grande_mayor + (bonus_otros_dados_grandes × 0.5)

Ejemplo 2×d12+d6:
  bonus = 8 (primer d12) + 8×0.5 (segundo d12) = 8 + 4 = 12
```

### 🟢 4.4 — Sinergia con Clases (validación)

Cada solución debe verificarse contra las 6 clases para asegurar que no rompa sinergias:

| Clase | Pasiva | Build óptima esperada | ¿El balance funciona? |
|-------|--------|----------------------|----------------------|
| **Guerrero** | Ninguna | 5×d6 | ✅ Baseline por definición |
| **Berserker** | Primer golpe ×3, no guardar dados T1 | d8/d12 (alto EV para maximizar el golpe único) | ✅ El bonus fijo ayuda a que builds de dados grandes no pierdan tanto en turnos normales. El primer golpe ×3 compensa la varianza. |
| **Gambler** | Escalera ×2, Craps anticipado | Mezcla de rangos para escalera | ⚠ Si d8 sube a 2 espacios (8-A): build máxima para escalera es 3×d-chico + d8 (4 dados). Verificar que 4 dados alcancen para escalera. Si d8 se elimina (8-D): Gambler usa d4+d6+d10 para rango 1-10. Si d4 es táctico (4-F): Gambler con d4 puede completar escaleras consistentemente — posible combo OP. |
| **Necromancer** | Trío+ ×2 | d12 + 3×d4 | ⚠ **Requiere decisión del equipo — ver §4.4.1 abajo.** EV con pasiva: 49.2 (+37% sobre baseline). Es el número más alto de toda la tabla y pasó sin discusión en los chats. |
| **Alchemist** | Dados encantados ×1.5 mult. | Cualquiera — maximizar enchanting | ✅ Si d4 tiene mejor enchanting (4-B): d4 se vuelve el dado preferido del Alchemist. Si d4 es táctico (4-F): Alchemist con d4 encantado + control de cara = sinergia fuerte. |
| **Trickster** | Tachar 2 combos, HP ×2 al tachar | Builds extremas (5×d3 o d20+resto) | ✅ d20 con bonus +14 hace que los turnos sin combo no sean 0 absoluto. La mecánica de tachar combos permite sacrificar combos que casi nunca logra (Full House con 3 dados) a cambio de HP. |

#### 4.4.1 — Auditoría: Necromancer +37% sobre baseline, ¿feature o bug?

El número apareció en los chats de Bocco sin crítica, pero merece una revisión explícita.

**Qué dice el número:** la build óptima del Necromancer (`d12 + 3×d4`) con su pasiva (Trío+ ×2) tiene EV por ronda de **49.2**, contra 35.7 del baseline. Es **+37% de daño promedio sostenido**.

**Comparación con otras clases (estimado, verificar con simulación):**

| Clase | Build óptima estimada | EV estimado | vs Baseline |
|---|---|---|---|
| Guerrero | 5×d6 | 35.7 | 0% (baseline por definición) |
| Necromancer | d12 + 3×d4 (Trío+ ×2) | **49.2** | **+37%** |
| Berserker | 5×d8 con primer golpe ×3 | ~36.6 sostenido, ~110 turno 1 | +2.5% sostenido / +208% turno 1 |
| Gambler | Mezcla rango 1-12 con Escalera ×2 | ~38-42 (pendiente sim) | +6 a +18% |
| Alchemist | Cualquiera + enchant ×1.5 | Depende del enchant | TBD |
| Trickster | Extremas + tachar combos | ~33 EV con utility HP | -7% + utility |

El Necromancer no solo es la clase más fuerte en EV sostenido — es **notablemente más fuerte que cualquier otra** en esa métrica.

**Dos lecturas posibles:**

| Lectura A: Es **feature** | Lectura B: Es **bug** |
|---|---|
| Cada clase tiene su nicho. Necromancer = rey del daño sostenido. Berserker = rey del burst turno 1. Trickster = rey de la utilidad. Es el diseño asimétrico de Isaac/Hades: no todas las clases están en el mismo eje. | +37% es demasiado. El jugador promedio va a converger al Necromancer porque es objetivamente más eficiente. Las otras clases necesitarán más ajuste para quedar a ±10-15%. |
| El +37% requiere **una build específica** (d12 + 3×d4). Si el jugador no consigue d12 en la run, el Necromancer queda flojo. Hay riesgo de no tener la build. | La build es accesible: d4 y d12 son comunes. En cualquier run decente la armás. El "riesgo de no tenerla" es menor de lo que parece. |
| Como compensación, el Necromancer podría tener menos HP o peor defensa (AP3 escudo igual para todos = no compensa en el modelo actual). | En el modelo actual (AP3 igual para todos), el Necromancer no paga ningún costo por su +37% — no tiene menos HP, no tiene peor defensa, no tiene menos movilidad. |
| El +37% es **daño promedio**, no daño real. Con 45% de turnos en "Trío o más" y 55% en combos menores, la varianza alta compensa el pico. | El daño promedio sigue siendo la métrica correcta para balance. La varianza se siente en gameplay pero no cambia los números. |

**Lo que hay que decidir:**

1. ¿Queremos clases **asimétricas** (algunas hacen 40% más daño a cambio de debilidad en otro eje) o clases **similares** (todas dentro de ±10% en cada métrica con diferenciación por mecánica no-numérica)?
2. Si es **asimétrico** → el Necromancer +37% se queda, pero hay que diseñar su debilidad compensatoria (ej: -30% HP, no puede usar escudo, tirada extra de "backfire" cuando falla el trío).
3. Si es **similar** → hay que ajustar la pasiva del Necromancer (ej: Trío+ ×1.5 en vez de ×2) o limitar su acceso a d12 (ej: el Necromancer parte con 3×d4 pero no puede comprar d12 hasta el piso 2).

**Recomendación:** decidir primero la filosofía (asimétrico vs similar). Sin esa decisión, todos los números de clase siguen en el aire, no solo el Necromancer.

> **Nota:** este mismo análisis debería aplicarse al Berserker (+208% turno 1 es otra cifra extrema) y al Gambler cuando se simule. El Necromancer es el primer caso porque es el más claro.

---

## 5. Plan de Acción

### Fase 1 — Decisiones del equipo (bloqueantes)

6 decisiones que el equipo debe tomar antes de seguir. Cada una tiene opciones claras y una recomendación, pero **ninguna está cerrada**.

> **⚠ Decisión #0 (meta-regla):** antes de discutir las 6 decisiones, el equipo debe acordar el **criterio de balance objetivo** (ver Decisión 6). Sin ese criterio, todas las demás decisiones se vuelven opinión: "¿+3% es aceptable?" no tiene respuesta hasta que se define qué rango se considera balanceado.

| # | Decisión | Opciones | Recomendación | Por qué | Quién decide |
|---|----------|----------|---------------|---------|-------------|
| 1 | **¿Qué hacemos con el d8?** | 8-A (2 espacios), 8-B (bajar mult.), 8-C (enchantamiento preinstalado), 8-D (eliminarlo) | Depende de la prioridad (ver tabla en §4.1) + del criterio de balance (decisión 6) | No hay opción claramente superior — cada una resuelve el problema con trade-offs distintos. El inventario de §4.1 muestra que 8-D cuesta ~1-2 horas de trabajo técnico. | Equipo completo |
| 2 | **¿Adoptamos el bonus fijo por dado grande?** | Sí (Opción 2C de Bocco) / No / Modificada | **Sí, con bonus escalado** (50% en segundo dado grande) | Es la solución más simple y tunable. El escalado previene que 2×d12+d6 se pase (+12% baja a +0.8%) | Equipo completo |
| 3 | **¿El d4 necesita mecánica especial?** | 4-A (soporte/efectos), 4-B (mejor enchanting), 4-C (tirada extra), 4-D (early game), 4-E (rebote), 4-F (elegir cara) | Depende de la prioridad (ver tabla en §4.2) — **bloqueada hasta que Bocco simule 4-F** (ver §4.2) | 4-F (táctico) es la más interesante pero la más arriesgada. Sin Monte Carlo no se puede confirmar si es OP o justa. | Equipo completo |
| 4 | **¿Generala con 3 dados sí o no?** | Sí / No / "Trío Perfecto" (base 60) | **Trío Perfecto** | Mantiene peso de la Generala real (5 dados) sin cerrar el techo de daño a builds chicas | Equipo completo |
| 5 | **¿Cómo se acumula el bonus con múltiples dados grandes?** | Sin cap / Cap a 1 / Bonus escalado (50%) | **Bonus escalado** | Equilibra 2×d12+d6 sin castigar builds de 1 dado grande | Equipo completo |
| **6** | **¿Cuál es el criterio de balance objetivo?** | (a) ±5% estricto, (b) ±10% flexible, (c) ±15% con clases asimétricas permitidas, (d) sin criterio numérico | **(c) ±15% con clases asimétricas permitidas** | Un rango estricto (±5%) obliga a homogeneizar las clases. Un rango flexible (±15%) permite diseño asimétrico tipo Isaac/Hades (Necromancer +37% es aceptable si paga con debilidad en otro eje). Sin criterio, cada decisión es opinión. | Equipo completo — **esta decisión es previa a todas las demás** |
| **7** | **Filosofía de clases: asimétrica o similar** | Asimétrica (cada clase tiene su nicho y puede salirse del rango numérico si paga en otro eje) / Similar (todas dentro de ±10% en cada métrica, diferenciación por mecánica no-numérica) | **Asimétrica** (si se eligió 6-c) | Define cómo resolver el Necromancer +37% y casos similares (Berserker +208% turno 1, etc.) | Equipo completo — depende de decisión 6 |

### Fase 2 — Simulación y validación

Una vez tomadas las decisiones:

1. **Actualizar la app de Bocco** con los nuevos valores (bonus fijo, d8 a 2 espacios si se elige 8-A)
2. **Correr Monte Carlo** para todas las builds representativas (las 10-15 builds de la tabla del GDD)
3. **Verificar que todas las builds caigan dentro de ±10% del baseline** con las correcciones aplicadas
4. **Testear sinergia con clases** — sobre todo Gambler (escalera con nuevo d8) y Necromancer (d12+3×d4 + bonus)

### Fase 3 — Documentación

1. Actualizar la tabla de dados en el GDD con valores finales
2. Agregar la fórmula de bonus fijo al GDD
3. Documentar la identidad de cada dado (una línea por dado)
4. Cerrar las preguntas abiertas en el GDD que dependían de este análisis

### Fase 4 — Implementación

1. Actualizar `DiceData` ScriptableObject con campo `bonusDamage`
2. Modificar `DamageResolver` para sumar bonus después del cálculo de combo
3. Actualizar UI de combate para mostrar desglose (combo + bonus)
4. Si d8 sube a 2 espacios: actualizar `DiceData` del d8

---

## 6. Resumen Visual — Identidad de Cada Dado

### Identidad actual (con problemas)

```
d3  ✅  Consistencia pura. Generala casi segura. Daño mínimo. → SIN CAMBIOS NECESARIOS
d4  ❓  Tierra de nadie entre d3 y d6. Sin razón para elegirlo. → NECESITA IDENTIDAD
d6  ✅  Referencia base. El dado que nunca está mal. → SIN CAMBIOS NECESARIOS
d8  ⚠️  Estrictamente mejor que d6 al mismo costo. → NECESITA NERF O ELIMINACIÓN
d10 ⚠️  Doble penalidad. Multiplicador no compensa. → NECESITA BONUS FIJO
d12 ⚠️  Doble penalidad. Solo viable con Necromancer. → NECESITA BONUS FIJO
d20 ✅  Apuesta extrema con identidad clara. → SIN CAMBIOS NECESARIOS (solo bonus fijo)
```

### Identidad objetivo (post-balance)

```
d3  ████░░░░░░  Consistencia pura. Generala casi segura. Daño mínimo.
d4  █████░░░░░  [Depende de decisión: táctico / early game / enchanting]
d6  ██████░░░░  Referencia. El dado que nunca está mal.
d8  ███████░░░  [Depende de decisión: 2 esp / nerf / enchant / eliminado]
d10 ████████░░  Potencia media. Bonus fijo compensa la penalidad.
d12 █████████░  Build-around piece. Brillante con Necromancer + bonus fijo.
d20 ██████████  Apuesta máxima. Bonus fijo asegura daño mínimo incluso sin combo.
```

**Principio de diseño:** Cada dado debería sentirse como una **elección**, no como un upgrade lineal. El jugador debería dudar antes de reemplazar cualquier dado por otro.

---

## Apéndice — Datos de Referencia

### Probabilidades clave (Monte Carlo, 8000 sims, 3 tiradas con estrategia óptima)

| Build | Trío | Full House | Póker | Generala | Nada |
|-------|------|------------|-------|----------|------|
| 5×d6 | — | 20% | 22% | 1.8% | <5% |
| 5×d3 | — | ~64% | — | ~40-50% | <1% |
| d12+3×d4 | **42%** | — | — | 9.9% | 3% |
| d20+2×d6 | — | — | — | — | **41%** |
| 2×d12+d6 | — | — | — | — | **42%** |

### EV por ronda con Opción 2C — Comparativa de reglas de acumulación

**Sin cap (bonus completo para todos los dados grandes):**

| Build | EV sin bonus | Bonus | EV total | vs Baseline |
|-------|-------------|-------|----------|-------------|
| 5×d6 | 35.7 | 0 | 35.7 | ref |
| d12+3×d4 | 27.6 | +8 | 35.6 | -0.3% |
| d12+3×d6 | 29.0 | +8 | 37.0 | +3.6% |
| 2×d12+d6 | 24.0 | +16 | 40.0 | **+12.0%** ⚠ |
| d20+2×d6 | 23.0 | +14 | 37.0 | +3.6% |

**Con bonus escalado (50% en segundo dado grande — RECOMENDADO):**

| Build | EV sin bonus | Bonus | EV total | vs Baseline |
|-------|-------------|-------|----------|-------------|
| 5×d6 | 35.7 | 0 | 35.7 | ref |
| d12+3×d4 | 27.6 | +8 | 35.6 | -0.3% |
| d12+3×d6 | 29.0 | +8 | 37.0 | +3.6% |
| 2×d12+d6 | 24.0 | +8+4=12 | 36.0 | **+0.8%** ✅ |
| d20+2×d6 | 23.0 | +14 | 37.0 | +3.6% |

**Con cap a 1 bonus (solo el mayor):**

| Build | EV sin bonus | Bonus | EV total | vs Baseline |
|-------|-------------|-------|----------|-------------|
| 5×d6 | 35.7 | 0 | 35.7 | ref |
| d12+3×d4 | 27.6 | +8 | 35.6 | -0.3% |
| d12+3×d6 | 29.0 | +8 | 37.0 | +3.6% |
| 2×d12+d6 | 24.0 | +8 | 32.0 | **-10.4%** ⚠ demasiado bajo |
| d20+2×d6 | 23.0 | +14 | 37.0 | +3.6% |

---

*Última actualización: 2026-04-05*
*Autores: Bocco (análisis inicial), Gabriel (continuación y plan de acción)*
