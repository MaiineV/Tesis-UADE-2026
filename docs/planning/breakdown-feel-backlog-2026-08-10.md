# Feel del breakdown de daño — backlog completo (2026-08-10)

> Sobre la base funcional de `sprint03/feature/0105-damage-breakdown-v3`.
> Referencias: **Balatro** (identidad chips/mult, escalado del drama con el
> score, pitch ascendente, flaming numbers) y **Dicero** (resolución por dado,
> pixel-impact seco). Herramientas confirmadas en el proyecto: **PrimeTween**,
> **Feel** (`Assets/Feel`, con patrones propios en `MmfJuice`), **UIParticle**
> (`com.coffee.ui-particle` — partículas dentro del canvas), `Rollgeon.Audio`
> (`IAudioService.PlaySfx2D` con pitch, `DuckMusic`) y `CameraService.Shake`
> (ya puentea `MMF_CameraShake`).

Leyenda de herramienta: `[PT]` PrimeTween · `[MMF]` Feel/MMF_Player ·
`[UIP]` UIParticle · `[SFX]` AudioManager · `[CAM]` CameraService ·
`[C]` solo código/color.

---

## 0. Identidad de color (el cambio de mayor impacto)

Auditoría actual: los ~10 textos nuevos del breakdown (CounterN, CounterM,
MultSign, ClashLabel, ContributionLabel, ValueLabel, cascade) quedaron en
**blanco puro sin diferenciación semántica ni backplate**. Balatro debe su
legibilidad a que chips y mult tienen color propio SIEMPRE (azul / rojo).

1. **[C] N azul, M rojo** — estilo Balatro literal. Propuesta: N `#4FA8FF`,
   M `#FF5F55`. Todo lo que vuele hacia N va tinteado azul, hacia M rojo
   (el `FlyingValueView` y su trail heredan el color del target). Conflictos
   a cuidar: Shield floating es `#6FD3FF` (elegir un azul más saturado/oscuro
   para N) y DamageTaken es `#FF4B4B` (el rojo M vive en otra zona de pantalla
   y otro momento; aceptable, pero separar el hue: M más naranja `#FF6B47`
   también funciona).
2. **[C] M cambia de color con su valor** — 1.0 gris apagado → >1 naranja →
   ≥2 rojo fuego → ≥3 rojo+fuego animado (ver §F). Refuerza que el mult es
   "lo peligroso".
3. **[C] Backplate oscuro** detrás de N×M y del cascade — pill `#14141E @ 0.88`
   (patrón ya usado por `InteractionPromptView`). Hoy el blanco flota sobre el
   arte del board: el contraste depende del fondo.
4. **[C] Outline negro** en ContributionLabel y ClashLabel (patrón
   `ActiveItemSlotView`: texto blanco + `outlineColor` negro) — los "+N" caen
   sobre los sprites de los dados.
5. **[C] Contraste a revisar en editor**: hay 2 textos en el prefab con
   `#5F7380` (gris azulado, ~3:1 sobre fondo oscuro — borderline WCAG);
   identificar cuáles son y subirlos a `#C2C6D0` como mínimo.
6. **[C] Tres dorados casi iguales** conviven: ItemBonus `#FFC93C`, Gold
   `#FFC533`, Weakness `#FFD75A`. Consolidar significado: dorado = weakness
   /bonus especial; que el precio/oro use otro tratamiento, o al menos
   documentar la distinción en `FloatingNumberPalette`.
7. **[C] Aporte de dado vs aporte de item** en los "+N": cara plana en blanco
   hueso `#F5EFE0`, porción de encantamiento en el color del item/encanto
   (o el dorado de bonus) — hoy es un solo número plano.

## A. Preview (pre-confirm, N×M estático)

8. **[PT] Pop-in con OutBack** del N×M al detectar combo (scale 0.6→1) en vez
   de aparición seca; fade-out del label viejo ya existe.
9. **[PT] Roll-up numérico** al cambiar el preview por toggle de hold: el
   contador interpola (Tween.Custom int) en ~0.15s en vez de snapear, con
   `sfx_dice_preview_tick` reutilizado. `[SFX]`
10. **[PT] Idle wobble del M cuando > 1.0** — rotación senoidal ±3° continua
    (Balatro: el mult "late"). Amplitud crece con el valor.
11. **[PT] Stagger de los "+N"** — aparecen en cascada (50ms entre slots) con
    mini punch, no todos juntos.
12. **[MMF] Pulso sutil de los dados contribuyentes** mientras el combo está
    armado (reusar el patrón `DiceSlotJuice`/hover) — se lee al instante cuáles
    entran y cuáles no.
13. **[SFX] Chime al armar combo** — `sfx_combo_chime` ya existe; pitch según
    tier del combo (par < trío < full).

## B. Vuelo del player base (espada → N)

14. **[PT] Anticipación de la espada**: squash + wind-up 0.1s antes del
    despegue (hoy despega sin telegraph).
15. **[C] Trail tinteado** — el ghost trail existe; tintearlo azul N (§0.1).
16. **[UIP] Chispas al despegue y al aterrizaje** en el contador (burst de
    4-6 partículas, prefab UIParticle pooled).
17. **[SFX] Whoosh corto** al despegar (`sfx_dice_throw_whoosh` recortado o
    clip nuevo) + **thunk** al aterrizar.
18. **[PT] Punch del contador proporcional al valor** recibido (Balatro: el
    score tiembla más cuanto más grande el aporte) — escala del PunchScale =
    f(aporte/N total), con leve rotación random ±4°.

## C. Dados (el corazón Dicero)

19. **[MMF] Flash blanco + punch del dado** en el momento en que su valor
    despega (patrón de flash ya existente en `DiceBoardSkinJuice`).
20. **[PT] El "+N" despega desde su label**: scale-up 1→1.3 y vuelo; el dado
    queda "gastado" (dim al 70% o desaturado) hasta el fin de la secuencia —
    se lee el progreso de la suma.
21. **[SFX] Pitch ascendente por dado** — cada dado consecutivo sube ~1
    semitono (pitch 1.0 → 1.06 → 1.12…), el truco #1 de Balatro para que
    una cadena larga se sienta creciente. `PlaySfx2D` ya recibe pitch.
22. **[C] Ramp de velocidad**: primer dado a tiempo completo, cada siguiente
    un 10-15% más rápido (gap y flight decrecientes, floor en ~50%). Cadenas
    largas aceleran solas sin necesidad de skip.
23. **[UIP] Burst en el contador por impacto**, intensidad acumulativa (más
    partículas a medida que N crece).

## D. Procs de items / encantamientos

24. **[MMF] Popup del proc con presencia**: sprite del item pop-in OutBack +
    **glow ring** detrás (Image radial escalando con fade, o UIParticle).
25. **[SFX] Sonido por familia**: encantamiento (arcano/brillante) ≠ item
    (mecánico/click) ≠ pasiva de combo (chime grave). 3 clips nuevos.
26. **[PT] Si el proc va a M**: flash rojo del CounterM + jiggle rotacional
    más agresivo que el punch de N — los mults se sienten "peligrosos".
27. **[UIP] Trail de partículas** en vuelos de proc (además del ghost trail),
    color según target.

## E. Cascade de globales (derecha)

28. **[PT] Slide-in desde el borde derecho** al poblarse (hoy solo fade).
29. **[PT] Highlight + punch de la entrada inferior** justo antes de disparar
    su número (telegraph de "ahora me toca").
30. **[PT] Caída con OutBounce suave** de las entradas restantes (hoy caída
    lineal/ease estándar).
31. **[SFX] "Card slide"** al caer las entradas (clip nuevo, cortito).

## F. Choque N×M (el clímax)

32. **[PT] Wind-up**: N y M se separan 10-15px hacia afuera 0.08s antes de
    lanzarse al centro (anticipación clásica).
33. **[C+PT] Flash full-screen** (Image blanca alpha 0→0.15→0, 0.12s) en el
    frame del impacto + **hitstop visual** (todos los tweens de la secuencia
    pausados 0.06s — NUNCA `Time.timeScale`, regla existente del proyecto).
34. **[CAM] `CameraService.Shake`** con amplitud escalada por el total
    (pequeño <30, medio <80, grande 80+). Ya existe y puentea Feel.
35. **[UIP] Explosión de partículas en el punto de choque** (estrellas +
    chispas radiales, 12-20 partículas).
36. **[PT] Roll-up del total** 0→total en ~0.25s con tick sound acelerando,
    o slam directo con mega-punch cuando viene de skip.
37. **[C] Escalado del drama por magnitud** (Balatro): 3 tiers por umbral de
    total (configurable en el SO) que multiplican shake, partículas, tamaño
    del ClashLabel y capa extra de SFX. Un golpe de 120 tiene que sentirse
    distinto a uno de 15.
38. **[UIP] Flaming number**: si M ≥ 2, loop de partículas de fuego sobre el
    total mientras se muestra (Balatro flaming score). Se apaga al liberar.
39. **[SFX] Impacto en capas**: thump grave + crack agudo; **`DuckMusic`**
    (ya existe en `IAudioService`) 0.3s alrededor del choque para dar foco.

## G. Mitigación / weakness (post-choque)

40. **[MMF] El escudo entra con clank metálico** + punch; el "-X" golpea el
    total; el total baja con tick-down y tinte azul-gris `#A3B3B1` (color de
    escudo ya canónico en `ChipStackSettingsSO`) durante 0.3s.
41. **[C] Weakness ×W**: flash dorado `#FFD75A` + SFX brillante — ya hay
    paleta (`DamageWeakness`); hoy el paso existe sin distinción de color.
42. **[UIP] Fragmentos al romper mitigación** — si el golpe supera el escudo
    del enemigo, partículas de "vidrio roto" azul-gris.

## H. Transversales

43. **[C] Todo perillable en `BreakdownAnimSettingsSO`**: colores N/M, umbrales
    de tier, amplitudes de shake, semitonos del pitch ramp, toggles por bloque
    (poder apagar partículas/shake para debug y accesibilidad).
44. **[MMF] Momentos compuestos como MMF_Player** autorados en el prefab
    (choque, proc popup) disparados vía `MmfJuice.Replay` — ⚠ respetar la
    regla de springs <11 Hz documentada en `MmfJuice` (bug PUL-015).
45. **[SFX] Clips nuevos necesarios** (hoy solo hay 11 SFX de dados):
    whoosh corto, thunk aterrizaje, tick de roll-up, proc ×3 familias,
    card-slide, clash en capas (thump+crack), clank escudo, chispa weakness,
    loop de fuego. ~11 clips. Reutilizables ya: `sfx_combo_chime`,
    `sfx_dice_preview_tick`, `sfx_ui_click`, `sfx_dice_throw_whoosh`.
46. **[C] Reduce-motion futuro**: el skip ya existe; un toggle de settings
    que fuerce `SkipStage.Fast` permanente cubre accesibilidad vestibular
    casi gratis.
47. **[C] Los tests del `BreakdownSequencePlayer` no cambian** — todo el feel
    vive en el director/vistas (stage), el orquestador POCO queda intacto.

---

## Orden de impacto sugerido (si hay que priorizar)

1. §0 colores + backplate (1-4) — sin esto el resto no luce.
2. §F choque completo (32-39) — el clímax es lo que se recuerda.
3. §C pitch ramp + ramp de velocidad (21-22) — el truco Balatro más barato.
4. §B/§D punches proporcionales y procs con presencia.
5. §E cascade, §G mitigación, §A preview idle.
6. Partículas UIP en general (pueden entrar al final, son independientes).
