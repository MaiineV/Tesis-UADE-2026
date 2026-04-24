# System#0206 — World-Space Health Bars

> Barras de vida world-space sobre cada entity con HP (enemy/boss).
> Reemplaza el antiguo `EnemyPanelView` screen-space.

---

## 1. Prefab: agregar Canvas + barra al enemy/boss pawn

1. Abrir el prefab de enemy (y/o boss).
2. Crear un **hijo** del root:
   - `Canvas` con **Render Mode = World Space**.
   - Scale del Canvas ≈ `(0.01, 0.01, 0.01)` (ajustar segun tamanio visual deseado).
3. Dentro del Canvas crear:
   - **Image** (renombrar a `Fill`):
     - `Image Type` = **Filled**
     - `Fill Method` = **Horizontal**
     - `Fill Origin` = Left
     - Color de barra a gusto (ej. rojo/verde).
   - **TextMeshPro - Text (UI)** (renombrar a `HpText`, opcional):
     - Centrado, font size apropiado.
4. Agregar componente **`WorldSpaceHealthBar`** al GameObject del Canvas.
5. Cablear en el Inspector:
   - `_fillImage` → el Image `Fill`.
   - `_hpText` → el TMP `HpText` (o dejar null si no se quiere texto).
   - `_barRoot` → el GameObject del Canvas (para show/hide).
   - `_offset` → ajustar Y para que quede sobre la cabeza del pawn (default `(0, 2, 0)`).

---

## 2. EntityPawn: cablear la referencia

1. En el `EntityPawn` del mismo prefab, buscar el campo `_healthBar`.
2. Arrastrar el `WorldSpaceHealthBar` recien creado al slot.
3. Para prefabs de **hero**, dejar `_healthBar` en **null** (el hero usa la barra del HUD).

---

## 3. Limpieza de escena: remover EnemyPanelView

1. En la escena de combate, ubicar el hierarchy del `CombatHUDView`.
2. Eliminar el GameObject `EnemyPanelRoot` (y sus hijos) del Canvas del HUD.
3. En el Inspector del `CombatHUDView`, verificar que no haya Missing References.

---

## 4. Verificacion

- [ ] Spawnar enemigos en combate — cada uno muestra barra de HP sobre la cabeza.
- [ ] La barra siempre mira a la camara (billboard).
- [ ] Al recibir dano, `fillAmount` baja y el texto se actualiza.
- [ ] Al recibir heal, `fillAmount` sube.
- [ ] Al morir el enemigo, la barra se oculta.
- [ ] El hero NO muestra barra world-space.
- [ ] El CombatHUD funciona sin errores de Missing Reference.
