# Setup — System#0300 FeedbackManager + Pawn Registry + HitImpulse consumer

> **Audiencia:** el usuario del proyecto, tras mergear el código de §10 completo.
> **Tiempo estimado:** 10 min (verificación) — los assets ya quedaron creados
> vía YAML directo en esta iteración.
> **Prerrequisito merge:** Foundation#0005 (ServiceBootstrapSO) ya en `develop`.

Este instructivo describe cómo dejar el pipeline de feedback (§10) **operativo en
runtime**. Excepción puntual al `CLAUDE.md`: los `ScriptableObject` assets, el
prefab `FloatingNumber`, y la edición de `ServiceBootstrap.asset` se commitearon
directamente editando el YAML. Abrir Unity, dejar que reimporte, y confirmar que
nada quedó roto (ver §6 smoke test).

Si Unity reporta `MonoScript missing` o las entries del DB no cargan, revisar
§7 troubleshooting — la ruta YAML-direct es más frágil que hacerlo por inspector.

---

## 0. Qué quedó mergeado

| Archivo | Rol |
|---|---|
| `Assets/Scripts/Rollgeon/Feedback/FeedbackManager.cs` | Orquestador §10 (MonoBehaviour, dueño de coroutines) |
| `Assets/Scripts/Rollgeon/Feedback/FeedbackDBSO.cs` | DB autoral de entries |
| `Assets/Scripts/Rollgeon/Feedback/FeedbackEntry.cs` | Entry por id, con `ShowIf` por type |
| `Assets/Scripts/Rollgeon/Feedback/FeedbackSequenceStep.cs` | Step de secuencia |
| `Assets/Scripts/Rollgeon/Feedback/FeedbackEventBus.cs` + `FeedbackSequenceRuntime.cs` | Bus latched + puntero estático |
| `Assets/Scripts/Rollgeon/Feedback/FeedbackPositionResolver.cs` | Switch sobre `SpawnPosition` |
| `Assets/Scripts/Rollgeon/Feedback/FeedbackCallbackListener.cs` | Escucha particle end / animator state end |
| `Assets/Scripts/Rollgeon/Feedback/FloatingNumberView.cs` | View del prefab `FloatingNumber` |
| `Assets/Scripts/Rollgeon/Feedback/FeedbackManagerBootstrap.cs` | SO que registra el manager |
| `Assets/Scripts/Rollgeon/Feedback/PawnRegistry.cs` + `PawnRegistryBootstrap.cs` + `PawnRegistryBinding.cs` | Registry de pawn transforms |
| `Assets/Scripts/Rollgeon/Feedback/HitImpulseConsumer.cs` | Knockback consumer en el pawn |
| `Assets/Scripts/Rollgeon/Feedback/FeedbackServiceStub.cs` (sigue existiendo) | Stub opcional para tests / scenes sin arte |

Además:
- `TurnManager.WaitForFeedbackCompletion(manager, timeout)` — coroutine helper.
- `EffectData.ExecuteCoroutine(ctx)` / `TryExecuteCoroutine(ctx, preCtx, onComplete)` —
  variantes async del pipeline que respetan el feedback gate entre efectos.

---

## 1. Verificar `FeedbackDB`

Ya existe en **`Assets/Rollgeon/Feedback/FeedbackDB.asset`** (creado por YAML
directo). Abrirlo en el inspector y confirmar que trae estas 4 entries:

| `FeedbackId` | `Type` | Otros campos clave |
|---|---|---|
| `hit.basic` | `FloatingNumber` | `FloatingNumberSourceKey = FloatingDamage`, `Position = AtTarget`, `Duration = 1.2` |
| `heal.basic` | `FloatingNumber` | `FloatingNumberSourceKey = FloatingHeal`, `Position = AtSource`, `Duration = 1.2` |
| `hit.impulse` | `BehaviorValue` | `BehaviorValueKey = HitImpulse`, `ValueTarget = Target`, `Duration = 0.3` |
| `sfx.hit` | `SFX` | `AudioClip = <tu clip>`, `Volume = 0.8`, `Duration = 0.3` |

**Pendiente manual:** `sfx.hit` tiene `AudioClip = null`. Arrastrar un clip
cuando tengas el SFX elegido, o dejarlo así (el feedback se ejecuta igual,
simplemente no suena nada).

Todas las entries de tipo `FloatingNumber` usan el prefab
`Assets/Resources/FloatingNumber.prefab` (ya creado, §2 acá abajo).

---

## 2. Verificar `FloatingNumber.prefab`

Ya existe en **`Assets/Resources/FloatingNumber.prefab`** con:
- `TextMesh` (legacy) con `m_Font: {fileID: 0}` — **asignar un font manualmente**
  (Arial builtin o el que uses en el proyecto). Sin font el texto no se ve.
- `FloatingNumberView` con `_lifeSeconds = 1.2` y `_riseSpeed = 1.5`.

Si preferís `TMP_Text` en vez de `TextMesh`, reemplazar el componente y
arrastrar la referencia al campo `_uguiText` / `_tmpText` del view.

---

## 3. Verificar `FeedbackManagerBootstrap`

Ya existe en **`Assets/Rollgeon/Feedback/FeedbackManagerBootstrap.asset`**.
Confirmar que el campo `DB` apunta a `FeedbackDB.asset`.

---

## 4. Verificar `PawnRegistryBootstrap`

Ya existe en **`Assets/Rollgeon/Feedback/PawnRegistryBootstrap.asset`**. No
tiene campos, solo existe como marcador para el service locator.

---

## 4b. Verificar registro en `ServiceBootstrap.asset`

`Assets/Rollgeon/ServiceBootstrap.asset` ya fue editado por YAML directo para
registrar los dos bootstraps en `ExtraServices` (índices 19 y 20). Abrirlo en
el inspector y confirmar que aparecen al final de la lista:
- `PawnRegistryBootstrap` (Priority 20)
- `FeedbackManagerBootstrap` (Priority 55)

**Nota:** `FeedbackServiceStubBootstrap` no estaba en la lista — no hace falta
removerlo. El stub solo se usa en tests / editor scenes sin arte.

---

## 5. Colocar `PawnRegistryBinding` en los pawns

Cada prefab de héroe / enemigo / prop **tiene que tener**:

- `PawnRegistryBinding` en el root visual. Registra el transform en
  `IPawnRegistry` al `OnEnable`.
- `HitImpulseConsumer` en el root visual (o un hijo) si querés knockback al recibir
  damage. Si no lo ponés, el feedback `hit.impulse` es no-op silencioso.

Tanto el spawner de héroes como el spawner de enemigos tienen que pegarle
`SetGuid(entity.Guid)` al binding justo después de instanciar el prefab. Si
el binding no recibe Guid, queda unregistered y el resolver de posición cae
a fallback.

> Wireup sugerido: extender el spawner de combate para que acepte un `Entity`
> y haga `binding.SetGuid(entity.Guid)` antes del primer frame.

---

## 6. Smoke test — combate con feedback

1. Abrir `02_Gameplay.unity`.
2. Play desde `00_Bootstrap.unity`.
3. En combate, cualquier acción que use un `EffPlayFeedback` con un id válido
   del DB debería:
   - Mostrar un número flotante (damage/heal).
   - Disparar SFX si la entry tiene clip.
   - Disparar knockback visual si el target tiene `HitImpulseConsumer`.
4. Verificar en consola:
   - Sin warnings `IFeedbackService no registrado`.
   - Sin warnings `Feedback id 'xxx' not found in DB`.
   - Si hay warnings del watchdog (`timed out`), la entry tiene un `Duration`
     muy corto o un listener que nunca dispara — revisar el prefab / animación.

---

## 7. Troubleshooting

| Síntoma | Causa probable |
|---|---|
| `IFeedbackService no registrado — no-op.` | `FeedbackManagerBootstrap` no está en `ExtraServices` |
| `No DB configured — short-circuit` | El bootstrap no tiene asignado el `FeedbackDBSO` |
| `Feedback id 'xxx' not found` | Id mal escrito o entry sin `FeedbackId` |
| Los floating numbers no aparecen | Falta `Assets/Resources/FloatingNumber.prefab` con `FloatingNumberView` |
| Animator no dispara | `TargetSourcePawn` apunta al pawn equivocado o el prefab no tiene `Animator` |
| Knockback no se ve | El pawn no tiene `HitImpulseConsumer`, o el vector llega Zero |
| `feedback gate timed out` | Alguna callback no disparó — la entry tiene `CompletionMode = ParticleEnd` pero el particle nunca termina |

---

## 8. Usar el resolver async (opcional)

Para que el combate **espere** a que termine cada feedback antes de aplicar el
próximo efecto, el combat driver debe usar la variante coroutine en vez de
`TurnManager.TryExecute`:

```csharp
IEnumerator ExecuteActionAsync(ActionDefinitionSO action, Guid player, EffectContext ctx, PreConditionContext preCtx) {
    bool ok = false;
    var co = action.Effect.TryExecuteCoroutine(ctx, preCtx, r => ok = r);
    while (co.MoveNext()) yield return co.Current;
    // ok == true si la cadena completó sin cortocircuitar.
}
```

El `TryExecute` sync sigue funcionando — no rompe nada existente. La diferencia
es que el sync encadena efectos sin esperar al arte del feedback.
