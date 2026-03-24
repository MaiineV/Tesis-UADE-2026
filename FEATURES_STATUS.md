# Estado de Features — Prototipo

Documento generado el 2026-03-24.
Compara lo implementado en el código contra lo especificado en `HighConcept.md` y `Prototype.md`.

---

## Leyenda

- ✅ Implementado y funcional
- ⚠️ Implementado parcialmente / falta pulir
- ❌ No implementado

---

## 1. Personaje

| Feature | Estado | Detalle |
|---------|--------|---------|
| Stat de Vida (HP) | ✅ | `PlayerState.CurrentHP / MaxHP`, HUD con barra de vida |
| Stat de Velocidad | ✅ | `PlayerState.Speed`, usado en huida |
| Stat de Destreza | ✅ | `PlayerState.Dexterity`, usado en arco/poción/forzar puerta |
| Personaje base con stats fijas | ✅ | Warrior: HP=100, Dex=3, Speed=3 |
| Múltiples clases (Warrior/Mage/Rogue) | ❌ | Solo Warrior. High Concept menciona 3 clases — fuera del alcance del prototipo |

---

## 2. Build de Dados (Inventario)

| Feature | Estado | Detalle |
|---------|--------|---------|
| Sistema de poder dádico (costos float) | ✅ | d6=1, d8=1.5, d10=2, d12=2.5 |
| Presupuesto fijo (5 puntos) | ✅ | `StartingPowerBudget = 5f` |
| Pantalla de selección pre-run | ✅ | `InventoryBuilderUI` con cards, costo visible, budget en tiempo real |
| Al menos 3 tipos de dados disponibles | ✅ | 4 tipos: d6, d8, d10, d12 |
| Validación de presupuesto al seleccionar | ✅ | No permite seleccionar si excede budget |
| Máximo 5 dados en combate | ✅ | `CombatDiceSlots = 5` |
| Dados especiales con efectos únicos | ❌ | High Concept lo menciona, fuera del prototipo |
| Pasivas (modificadores de run) | ❌ | High Concept lo menciona, fuera del prototipo |
| Enchanting de caras individuales | ⚠️ | Sistema de upgrade de caras existe (`DiceUpgrader`, `FaceUpgradeType`) pero solo como reward post-combate, no en tienda |

---

## 3. Exploración — Estructura del Piso

| Feature | Estado | Detalle |
|---------|--------|---------|
| Generación procedural de piso | ✅ | `FloorGenerator` con random walk, 8-14 salas |
| Tipos de sala: Combate | ✅ | `RoomType.Combat` |
| Tipos de sala: Tienda | ✅ | `RoomType.Shop` |
| Tipos de sala: Poción | ✅ | `RoomType.Potion` |
| Tipos de sala: Boss | ✅ | `RoomType.Boss`, ubicado a máxima distancia del inicio |
| Movimiento entre salas por puertas | ✅ | `CheckDoorTransition`, `StartRoomTransition` |
| Minimapa en HUD | ✅ | `MinimapUI` con celdas coloreadas |
| Minimapa: labels T/B/P | ✅ | Shop=T, Boss=B, Potion=P |
| Minimapa: salas adyacentes descubiertas | ✅ | `DungeonManager.SetCurrentRoom` descubre adyacentes |
| Piso termina al derrotar boss | ✅ | Victoria al limpiar sala Boss |
| Sala de Craps/Sacrificio | ❌ | High Concept las menciona, no están en Prototype.md |

---

## 4. Turno del Jugador — Exploración

| Feature | Estado | Detalle |
|---------|--------|---------|
| Acción: Moverse (dado de velocidad) | ✅ | `SpeedDie.Roll()`, tiles alcanzables resaltados |
| Acción: Usar Arco (5x5, d20+Dex) | ✅ | `ExplorationActions.AttemptBow`, targeting visual |
| Acción: Tomar Poción (d10+Dex, % curación) | ✅ | `ExplorationActions.AttemptPotion`, un uso, recarga en sala Poción |
| UI de acciones de exploración (izquierda) | ✅ | `ExplorationActionsUI` con botones Mover/Arco/Poción |
| Acciones mutuamente excluyentes por turno | ✅ | Elegir una pasa el turno |
| Arco/Poción no disponibles en combate | ✅ | `SetCombatMode` oculta Arco/Poción, muestra Huir/Forzar |

---

## 5. Combate

| Feature | Estado | Detalle |
|---------|--------|---------|
| Inicio por contacto enemigo-jugador | ✅ | Colisión en movimiento activa `EnterCombat` |
| 3 tiradas estilo Generala | ✅ | `AttackPhase` con 3 rolls, lock/reroll |
| Reservar dados entre tiradas | ✅ | Toggle lock por click en dado |
| Combinaciones: Par | ✅ | `CombinationDetector` |
| Combinaciones: Pierna (Three of a Kind) | ✅ | |
| Combinaciones: Escalera | ✅ | |
| Combinaciones: Full House | ✅ | |
| Combinaciones: Póker (Four of a Kind) | ✅ | |
| Combinaciones: Generala (Yahtzee) | ✅ | |
| Combinaciones: Generala Doble | ✅ | `generalaScoredThisRun` tracking |
| Combinaciones: Dado más alto | ✅ | Fallback `HighDie` |
| Fase de defensa (rolls sobrantes) | ✅ | `DefensePhase`, rolls = 3 - rolls usados en ataque |
| Escudo bloquea daño entrante | ✅ | `PlayerState.ShieldValue` absorbe daño |
| Preview de combo en tiempo real | ✅ | `CombatUI.UpdateComboPreview` al lockear dados |

---

## 6. Barra de Energía y Modo Craps

| Feature | Estado | Detalle |
|---------|--------|---------|
| Barra de energía durante combate | ✅ | `EnergyManager`, `EnergyBarUI` |
| Energía se llena por acciones de combate | ✅ | Daño dealt, kills, damage taken la llenan |
| Modo Craps: apostar combinación | ✅ | `CrapsUI` con 6 opciones de apuesta |
| Acierto: bonus de daño | ✅ | `CrapsMode.Resolve` aplica multiplicador |
| Fallo: penalización | ✅ | Pierde HP si falla |
| Indicador visual de apuesta activa | ✅ | `CrapsBetIndicator` en panel de ataque |
| Toast de resultado Craps | ✅ | `CrapsToastUI` |
| Flash de pantalla en resultado | ✅ | `ScreenFlashUI.FlashCrapsSuccess/Failure` |

---

## 7. Turno del Enemigo

| Feature | Estado | Detalle |
|---------|--------|---------|
| Tirada única de daño | ✅ | `EnemyEntity.RollAttack()` |
| Barra de energía propia | ✅ | `EnemyState.CurrentEnergy`, `GainEnergy()` |
| Enrage: doble daño al llenar energía | ✅ | `IsEnraged` → 60% chance de crit ×2 |
| Panel de info del enemigo (nombre + HP) | ✅ | `EnemyInfoUI` |

---

## 8. Enemigo Arquero

| Feature | Estado | Detalle |
|---------|--------|---------|
| IA de rango: mantiene distancia preferida | ✅ | `EnemyAI.MoveRanged`, `PreferredRange=3` |
| Huye si jugador está adyacente (1x1) | ✅ | `MoveAwayFromPlayer` si distancia ≤ 1 |
| Disparo con porcentaje (dado + Puntería) | ✅ | `EnemyEntity.RollAttack` con `Accuracy` check |
| Datos del Archer configurados | ✅ | HP=30, Accuracy=60, IsRanged=true |

---

## 9. Combate Doble

| Feature | Estado | Detalle |
|---------|--------|---------|
| Enemigos no en combate avanzan 1 tile/turno | ✅ | `AdvanceWaitingEnemies()` |
| Al llegar activan combate doble | ⚠️ | Avanzan y se loguea "joined combat", pero falta implementar la elección de target entre múltiples enemigos |
| Jugador elige a cuál atacar | ❌ | Actualmente ataca solo al `currentCombatEnemy`, no hay selector de target |
| Ambos enemigos atacan al jugador | ❌ | Solo ataca `currentCombatEnemy` |
| Combate termina al derrotar a ambos | ⚠️ | Verifica `enemies.Any(alive)` pero el flujo de combate doble no está completo |

---

## 10. Huida

| Feature | Estado | Detalle |
|---------|--------|---------|
| Botón de huida en HUD de combate | ✅ | `FleeBtn` en `ExplorationActionsUI` |
| Dado de huida + Velocidad → % éxito | ✅ | `ExplorationActions.AttemptFlee(speed)` |
| Éxito: cobra 10% vida máxima | ✅ | `player.State.MaxHP * 0.1f` |
| Éxito: sale del combate | ✅ | Oculta panel de combate, vuelve a movimiento |
| Fallo: turno consumido, sin penalización de vida | ✅ | Enemigo recibe turno de ataque gratis |
| Auto-roll de movimiento al huir | ❌ | No se tira dado de movimiento automático al huir, solo vuelve a fase de exploración |

---

## 11. Forzar Puerta

| Feature | Estado | Detalle |
|---------|--------|---------|
| Opción visible si jugador está en puerta | ✅ | `ForceDoorBtn`, `IsPlayerOnDoor()` |
| Dado + Destreza → % éxito | ✅ | `ExplorationActions.AttemptForceDoor` |
| Éxito: pasa a sala siguiente | ✅ | `StartRoomTransition` |
| Éxito: enemigos pierden 25% HP | ✅ | Aplica daño a todos los enemigos vivos |
| Fallo: turno consumido | ✅ | Enemigo ataca |

---

## 12. Persistencia de Enemigos

| Feature | Estado | Detalle |
|---------|--------|---------|
| Guardar HP de enemigos al salir de sala | ✅ | `DungeonManager.SaveEnemyState` |
| Re-spawn con HP guardado al volver | ✅ | `SpawnEnemiesForRoom` lee `room.Enemies` |
| Posición aleatoria al volver | ✅ | Usa `layout.EnemySpawns` nuevos |
| Enemigos muertos no reaparecen | ✅ | Filtra `saved.IsAlive` |

---

## 13. Sala de Tienda

| Feature | Estado | Detalle |
|---------|--------|---------|
| UI de tienda funcional | ✅ | `ShopUI` con nombre, descripción, precio, botón comprar |
| Generación de ítems (Poción, d10, d12) | ✅ | `GenerateShopItems` crea 3 ítems |
| Compra con Oro | ✅ | `OnShopBuy` descuenta gold |
| Botón comprar deshabilitado si no alcanza el oro | ✅ | `buyButton.interactable = playerGold >= item.GoldCost` |
| Ítems distribuidos en el suelo con texto visible | ❌ | Ítems se muestran en overlay UI, no como objetos en el grid |
| Descripción completa al acercarse | ❌ | Se muestra directo al entrar, no por proximidad |
| Ítems persisten si el jugador sale y vuelve | ✅ | `ShopItemData.Purchased` se guarda en `RoomData` |
| Calibración de precios (3-4 enemigos = ítem estándar) | ⚠️ | Poción=15G, d10=25G, d12=40G. Enemigos dan 5-20G. Alineado aprox. |

---

## 14. Economía de Oro

| Feature | Estado | Detalle |
|---------|--------|---------|
| Enemigos sueltan Oro al morir | ✅ | `HandleEnemyDeath` con fallback 5-15G |
| Oro visible en HUD | ✅ | `goldText` en HUD, actualizado con `UpdateGold` |
| Floating text de oro ganado | ✅ | `FloatingDamageUI.ShowGold` |
| No mejoras de dado como drop directo | ✅ | Solo oro, upgrades via reward post-combate |

---

## 15. Cámara y Visual

| Feature | Estado | Detalle |
|---------|--------|---------|
| Perspectiva isométrica fija | ✅ | Cámara ortográfica, 35° pitch, 45° yaw |
| Sin rotación de cámara | ✅ | Fija |
| Grilla 8x8 con tiles 3D | ✅ | `GridManager` con cubos planos |
| Player: cubo azul | ✅ | Prefab con visual + `CharacterColor #4fc3f7` |
| Goblin: cubo verde | ✅ | `EnemyColor #66bb6a` |
| Orc: cubo rojo | ✅ | `EnemyColor #ef5350` |
| Obstáculos: cubos oscuros | ✅ | Cubos más altos, color oscuro |
| Iluminación direccional | ✅ | `SceneSetup.SetupLighting` |
| Pixel art post-process shader | ⚠️ | `PixelationFeature` existe pero puede no estar activo |
| Dados con física real al tirar | ❌ | Dados son UI 2D, no 3D con física |
| Feedback visual escalado por combo | ❌ | No hay escalado visual (glow, shake, explosión) según tipo de combo |
| Screen shake en combos grandes | ❌ | No implementado |
| Daño visual escalado por valor | ⚠️ | Floating text existe pero mismo tamaño para todo |

---

## 16. Audio

| Feature | Estado | Detalle |
|---------|--------|---------|
| Sistema de audio | ✅ | `AudioManager`, `SoundLibrary` |
| Slider de volumen en HUD | ✅ | `VolumeSliderUI` |
| Sonido de ataque al enemigo | ✅ | `SoundLibrary.AttackToEnemy` |
| Sonido de ataque al jugador | ✅ | `SoundLibrary.AttackToPlayer` |
| Sonidos de dados rodando | ❌ | No implementado |
| Música de fondo | ❌ | No implementado |

---

## 17. UI General

| Feature | Estado | Detalle |
|---------|--------|---------|
| Barra de vida | ✅ | `HealthBarUI` |
| Barra de energía | ✅ | `EnergyBarUI` |
| Escudo display | ✅ | `ShieldDisplay` |
| Combat log | ✅ | `CombatLogUI` con scroll |
| Panel de ataque con dados clickeables | ✅ | `CombatUI` + `DieSlotUI` |
| Panel de defensa | ✅ | Dados de defensa con lock/reroll |
| Panel de ataque enemigo | ✅ | Muestra roll, absorción, daño neto |
| Overlay de rewards post-combate | ✅ | `RewardUI` con 2 opciones |
| Overlay de Game Over | ✅ | `GameOverUI` con stats y restart |
| Overlay de Victoria | ✅ | `VictoryUI` con stats y restart |
| Indicador de nivel/sala | ✅ | `levelTMP` en HUD |
| Phase label (turno actual) | ✅ | Texto grande central |

---

## Resumen de lo que FALTA (priorizado para el prototipo)

### Alta prioridad (requerido por Prototype.md)

1. **Combate doble completo** — Selector de target entre múltiples enemigos, ambos atacan al jugador
2. **Auto-roll de movimiento al huir exitosamente** — Tirar dado de velocidad y mover automáticamente
3. **Ítems de tienda en el suelo del grid** — Actualmente es un overlay; Prototype.md pide ítems visibles en el grid con interacción por proximidad

### Media prioridad (mejora la experiencia del prototipo)

4. **Feedback visual por tipo de combo** — Glow, shake, partículas según combo (Par → sutil, Generala → explosión)
5. **Floating damage escalado** — Números más grandes/llamativos para más daño
6. **Sonidos de dados** — Audio al tirar/lockear dados
7. **Daño del arco** — Actualmente usa fórmula propia; verificar que el daño sea balanceado

### Baja prioridad (mencionado en High Concept, fuera del alcance del prototipo)

8. **Dados con física 3D** — Animación de dados reales rodando en 3D
9. **Múltiples clases** (Mage, Rogue) — Solo Warrior implementado
10. **Dados especiales con efectos únicos** — No hay dados con propiedades especiales
11. **Pasivas** — Sin sistema de pasivas
12. **Sala de Craps** — Tipo de sala dedicada a apostar (separada del modo Craps en combate)
13. **Sala de Sacrificio** — Perder HP máximo a cambio de poder
14. **Enchanting en tienda** — Mejorar caras específicas comprando en shop
15. **Meta-progresión entre runs** — Desbloqueo de contenido por milestones
16. **Música de fondo**
17. **Screen shake**
