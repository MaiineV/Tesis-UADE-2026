# Plan — Foundation#0001_ServiceLocatorEventManager

## 1. Objetivo

Implementar la infraestructura de patrones base sobre la que va a montarse todo el resto del sprint:

- **`ServiceLocator`** — registro estático `Type → instance` con scopes `Global` / `Run` que permite limpiar sólo los servicios de una run sin tocar la infraestructura global.
- **`EventManager`** — event bus legacy (string‑keyed vía `EventName`) con schema documentado por enum‑XML‑doc y payload `object[]`.
- **`TypedEvent<T>`** — bus tipado complementario por struct de payload, coexistente con el legacy bajo la **regla de canal único**.
- **`EventName`** — enum con la familia mínima necesaria para que los sistemas downstream compilen, acompañado de structs de payload canónicos (`DamageResolvedPayload`, `HealthChangedPayload`, `ComboMatchedPayload`).

Queda **fuera de alcance** (responsabilidad de `Foundation#0005_CatalogsAndBootstrap`):

- `ServiceBootstrapSO` y cualquier scene / MonoBehaviour loader que registre servicios en orden (§1.1.1, §1.1.2).
- Registro de servicios concretos (catálogos, settings, managers de infraestructura). Acá sólo publicamos la **API pura** que Foundation#0005 va a consumir.

También queda fuera de alcance: cualquier uso de `ServiceLocator` / `EventManager` desde sistemas específicos (Energy, Combos, Turn, HUD, Dice, Craps, etc.). Este plan entrega la infra, no los consumidores.

Referencia de sprint: Phase 0.1 en el plan de orquestación (`bien-vamos-a-empezar-optimized-nest.md`), primera foundation del grafo de dependencias, no depende de ningún otro worktree.

---

## 2. Secciones de TECHNICAL.md consumidas

- **§0 Convenciones y stack** (líneas 55–117) — namespace `Patterns`, directorio `Assets/Scripts/Rollgeon/Patterns/`, Odin instalado. Nota: a pesar de Odin estar disponible, esta tarea es 100% infra estática y **no requiere atributos Odin**; todo el código es `static class` o `struct` puro. Odin se usará en foundations posteriores (SOs, catalogs).
- **§1.1 `ServiceLocator` con precarga** (líneas 122–175) — firma exacta del registro (`AddService`, `GetService`, `TryGetService`, `RemoveService`, `HasService`, `ClearScope`, `Clear`), enum `ServiceScope { Global, Run }`, y semántica del bucket `Dictionary<Type, (object instance, ServiceScope scope)>`. **Explícitamente omitimos §1.1.1 `ServiceBootstrapSO`** — va a Foundation#0005.
- **§1.2 `EventManager`** (líneas 413–426) — firma exacta `Subscribe` / `UnSubscribe` / `Trigger` / `ResetEventDictionary`, delegate `EventReceiver(params object[] parameter)`.
- **§1.2 familia mínima de `EventName`** (líneas 500–524, schema en 529–621) — todos los eventos del enum con sus XML‑docs de schema `args: [...]` tal como el spec los documenta. Incluye la regla transversal de `args[0] == Guid` (línea 525).
- **§1.2.1 `TypedEvent<T>` — bus tipado complementario** (líneas 428–498) — firma exacta `Subscribe` / `Unsubscribe` / `Raise` / `Clear`, los tres payload structs canónicos (`DamageResolvedPayload`, `HealthChangedPayload`, `ComboMatchedPayload`), y la **regla de canal único — no coexistencia** (línea 494): un evento vive en `EventName` O en `TypedEvent<T>`, nunca en ambos.

No se consumen secciones de `§1.3 FSM` (va a Foundation#0002) ni `§2+` (va a Foundation#0003+).

---

## 3. Archivos a crear/modificar (código únicamente)

Todos bajo `Assets/Scripts/Rollgeon/Patterns/` con namespace `Patterns`. Se crean 6 archivos nuevos, ningún archivo preexistente se modifica (el worktree arranca sin `Assets/Scripts/`).

| Ruta absoluta | Propósito |
|---|---|
| `Assets/Scripts/Rollgeon/Patterns/ServiceScope.cs` | Define `public enum ServiceScope { Global, Run }` con XML‑docs copiados de §1.1. Archivo separado porque el enum lo consume `ServiceBootstrapSO` en Foundation#0005 y tenerlo suelto evita acoplamiento innecesario al file del `ServiceLocator`. |
| `Assets/Scripts/Rollgeon/Patterns/ServiceLocator.cs` | Implementa `public static class ServiceLocator` tal como lo define §1.1: diccionario privado `Type → (object, ServiceScope)`, métodos `AddService<T>`, `GetService<T>`, `TryGetService<T>`, `RemoveService<T>`, `HasService<T>`, `ClearScope(ServiceScope)`, `Clear()`. Uso de LINQ permitido (§1.1 lo usa en `ClearScope`). |
| `Assets/Scripts/Rollgeon/Patterns/EventName.cs` | Enum `public enum EventName` con la familia mínima del §1.2 (ver §4 abajo para la lista exacta). Cada entry con su XML‑doc `/// <summary>args: [...]</summary>` tal como el spec lo documenta. |
| `Assets/Scripts/Rollgeon/Patterns/EventManager.cs` | Implementa `public static class EventManager` con el delegate `EventReceiver(params object[])` y los métodos `Subscribe(EventName, EventReceiver)`, `UnSubscribe(EventName, EventReceiver)`, `Trigger(EventName, params object[])`, `ResetEventDictionary()`. Diccionario privado `Dictionary<EventName, EventReceiver>`. |
| `Assets/Scripts/Rollgeon/Patterns/TypedEvent.cs` | Implementa `public static class TypedEvent<T> where T : struct` con `Subscribe(Action<T>)`, `Unsubscribe(Action<T>)`, `Raise(T)`, `Clear()`. Event `Action<T>` interno. Comentario en el header explicando la regla de canal único. |
| `Assets/Scripts/Rollgeon/Patterns/EventPayloads.cs` | Los tres structs canónicos del §1.2.1: `DamageResolvedPayload`, `HealthChangedPayload`, `ComboMatchedPayload`. Un solo archivo porque son declaraciones cortas y están conceptualmente agrupadas (payloads canónicos del bus tipado). `public struct` con campos públicos tal cual el spec. |

**No se crean**:

- `ServiceBootstrapSO` ni scenes ni prefabs ni `.asset` ni `.meta` inexistentes (los `.meta` los genera Unity al abrir el proyecto).
- Tests unitarios (no forman parte del pipeline de este sprint; la Definición de Done valida que la API sea *unit‑testable*, no que los tests existan en este worktree).
- `docs/setup/<worktree>.md` — lo produce el dev, no el planner.

---

## 4. Contratos (interfaces / SOs / eventos)

API pública exacta que este worktree expone. Downstream (Foundation#0005, sistemas de §12, §5, §17, etc.) se acopla a estas firmas — cambiar cualquiera post‑merge rompe consumidores.

### 4.1 `ServiceScope`

```csharp
namespace Patterns
{
    public enum ServiceScope
    {
        Global, // persiste toda la sesión
        Run,    // persiste sólo durante la run
    }
}
```

### 4.2 `ServiceLocator`

```csharp
namespace Patterns
{
    public static class ServiceLocator
    {
        public static void AddService<T>(object instance, ServiceScope scope = ServiceScope.Global);
        public static T    GetService<T>();
        public static bool TryGetService<T>(out T service);
        public static void RemoveService<T>();
        public static bool HasService<T>();
        public static void ClearScope(ServiceScope scope);
        public static void Clear();
    }
}
```

Semántica (palabra por palabra del §1.1):
- `AddService<T>` hace **upsert** (sobrescribe si la key ya existe); la línea del spec es `Services[typeof(T)] = (instance, scope)`.
- `GetService<T>` asume presencia y hace cast directo (lanza `KeyNotFoundException` si falta — consistente con el spec que no valida).
- `TryGetService<T>` devuelve `false` y `default(T)` si no está.
- `ClearScope(scope)` borra sólo las entries con ese scope.
- `Clear()` borra todo — documentado como "sólo shutdown o tests".

### 4.3 `EventManager`

```csharp
namespace Patterns
{
    public static class EventManager
    {
        public delegate void EventReceiver(params object[] parameter);

        public static void Subscribe(EventName eventType, EventReceiver method);
        public static void UnSubscribe(EventName eventType, EventReceiver method);
        public static void Trigger(EventName eventType, params object[] parameters);
        public static void ResetEventDictionary();
    }
}
```

Semántica:
- Storage: `Dictionary<EventName, EventReceiver>` privado. `Subscribe` hace `_dict[e] += method` (crea la entry si falta); `UnSubscribe` hace `_dict[e] -= method` si la key existe.
- `Trigger` hace null‑check (`_dict.TryGetValue(e, out var d); d?.Invoke(parameters);`) — nunca lanza si no hay suscriptores.
- `ResetEventDictionary()` vacía el diccionario completo (uso: shutdown, tests, run transitions si el diseñador lo decidiera — no acá).

### 4.4 `EventName` (familia mínima)

Enum con las entries listadas en §1.2, cada una con su XML‑doc de schema. Lista completa que vamos a publicar:

```
// Run lifecycle
OnRunStart, OnRunEnd

// Combat lifecycle
OnCombatStart, OnCombatEnd

// Damage pipeline (legacy; OnDamageResolved MOVIDO a TypedEvent<DamageResolvedPayload> por decisión de orquestación)
OnDamageOutgoing, OnDamageIncoming,
// Nota: OnDamageResolved NO aparece acá — vive sólo en TypedEvent<DamageResolvedPayload>.

// Turn / initiative
OnTurnStarted, OnTurnFinished, OnEnergyChanged, OnTurnQueueBuilt

// Phase lifecycle (§12.0)
OnPhaseExit, OnPhaseEnter, OnOverlayPushed, OnOverlayPopped

// Roll
OnRollStarted, OnDiceRolled, OnRerollStarted, OnRollResolved

// Combat resolve (OnHealthChanged MOVIDO a TypedEvent<HealthChangedPayload>)
OnShieldChanged, OnEntityDestroyed

// Contract (OnComboMatched MOVIDO a TypedEvent<ComboMatchedPayload>)
OnComboCrossed, OnWeaknessHit

// Modifier / attributes
OnAttributeChanged, OnModifierAdded, OnModifierRemoved

// Dungeon
OnRoomEntered, OnRoomCleared, OnFloorCleared

// HUD bindings
OnPlayerHealthChanged, OnPlayerEnergyChanged, OnGoldChanged, OnFloatingNumberRequested

// Craps
OnCrapsSessionStarted, OnCrapsBetPlaced, OnCrapsResolved

// Save
OnCaptureRequested, OnRestoreCompleted

// Feedback
OnFeedbackStarted, OnFeedbackCompleted

// Interaction (§7.7)
OnInteractionTargetChanged, OnInteractionExecuted

// Shop (§17.F)
OnShopItemTargetChanged, OnShopItemPurchased, OnShopRestocked

// Status (§20)
OnStatusApplied, OnStatusRemoved, OnStatusTicked

// Items (§18)
OnItemObtained, OnItemRemoved, OnActiveItemUsed

// Quest (§21)
OnQuestStateChanged

// Scene (§K)
OnSceneLoaded, OnSceneUnloaded
```

**Explícitamente NO incluidos**:
- `OnScreenPushed`, `OnScreenPopped`, `OnPauseChanged` — el spec (línea 514, 578–581) los saca del bus legacy y los mueve a `IScreenManager` (§17.D). No existen en `EventName`.

XML‑doc de cada entry copia textual del schema del spec (`args: [...]`). Dev puede copiar los bloques de líneas 529–621 de `TECHNICAL.md` como fuente.

### 4.5 `TypedEvent<T>`

```csharp
namespace Patterns
{
    public static class TypedEvent<T> where T : struct
    {
        public static void Subscribe(Action<T> listener);
        public static void Unsubscribe(Action<T> listener);
        public static void Raise(T payload);
        public static void Clear();
    }
}
```

Storage: `private static event Action<T> _listeners;`. `Raise` hace `_listeners?.Invoke(payload)`.

### 4.6 Payloads canónicos

```csharp
namespace Patterns
{
    public struct DamageResolvedPayload
    {
        public Guid SourceGuid;
        public Guid TargetGuid;
        public int FinalDamage;
        public bool WeaknessHit;
    }

    public struct HealthChangedPayload
    {
        public Guid EntityGuid;
        public int Current;
        public int Max;
    }

    public struct ComboMatchedPayload
    {
        public Guid SourceGuid;
        public string ComboId;
        public int BaseDamage;
    }
}
```

Campos públicos (no propiedades) — el spec los declara así en §1.2.1.

### 4.7 Regla de canal único — cómo se materializa

Es una regla de convención, no de compilador, pero la documentamos en el header del `TypedEvent.cs`:

> Un evento del juego publica por exactamente una vía: `EventManager` (legacy) o `TypedEvent<T>` (tipado). Nunca ambos. Migrar un evento = eliminar su entry del `EventName` + reemplazar publishers/subscribers por la versión tipada. Ver TECHNICAL.md §1.2.1 — párrafo "Regla de canal único".

Nota de coexistencia temporal: el spec permite migraciones progresivas. Foundation#0001 entrega ambos buses; los sistemas downstream eligen cuál usar para cada evento. No es rol de esta infra forzar una u otra vía.

---

## 5. Pipeline de ejecución

Esta foundation no tiene *pipeline de runtime* propio — es infraestructura estática. Lo que sí describe el flujo es **cómo la consume el resto del juego**:

### 5.1 Ciclo de vida de un servicio

```
[Foundation#0005 — bootstrap loader]
    │
    ▼ construye instancia (SO, manager, lo que sea)
    │
    ▼ ServiceLocator.AddService<IFoo>(instance, ServiceScope.Global)
    │    └── guarda en Dictionary<Type, (object, scope)>
    │
    ▼ (en runtime de gameplay)
    │
    ▼ var foo = ServiceLocator.GetService<IFoo>()
    │    └── cast directo desde Services[typeof(IFoo)].instance
    │
    ▼ (al iniciar una run, se registran servicios de run:)
    │
    ▼ ServiceLocator.AddService<ITurnManager>(tm, ServiceScope.Run)
    │
    ▼ (al terminar la run:)
    │
    ▼ ServiceLocator.ClearScope(ServiceScope.Run)
    │    └── borra sólo los Run, deja intactos los Global
    │
    ▼ (al apagar el juego o en un test cleanup:)
    │
    ▼ ServiceLocator.Clear()
```

### 5.2 Ciclo de vida de un evento legacy

```
[Subscriber, típicamente un MonoBehaviour en OnEnable]
    │
    ▼ EventManager.Subscribe(EventName.OnTurnFinished, HandleTurnFinished)
    │    └── _dict[OnTurnFinished] += HandleTurnFinished
    │
    ▼ ... tiempo pasa ...
    │
[Publisher, típicamente TurnManager]
    │
    ▼ EventManager.Trigger(EventName.OnTurnFinished, entityGuid)
    │    └── _dict[OnTurnFinished]?.Invoke(new object[]{ entityGuid })
    │         └── HandleTurnFinished(args) { var g = (Guid)args[0]; ... }
    │
    ▼ [Subscriber, en OnDisable]
    │
    ▼ EventManager.UnSubscribe(EventName.OnTurnFinished, HandleTurnFinished)
```

Regla transversal: `args[0]` del payload legacy siempre es `Guid` (InstanceId de la entidad primaria). Esto lo valida el publisher, no la infra — lo mencionamos para que el dev lo documente en el XML‑doc del `Trigger` con un `/// <remarks>` que apunte a §1.2 línea 525.

### 5.3 Ciclo de vida de un evento tipado

```
[Subscriber]
    │
    ▼ TypedEvent<DamageResolvedPayload>.Subscribe(OnDamageResolved)
    │    └── _listeners += OnDamageResolved
    │
[Publisher, DamagePipeline]
    │
    ▼ TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload {
    │      SourceGuid = ..., TargetGuid = ..., FinalDamage = ..., WeaknessHit = ...
    │  })
    │    └── _listeners?.Invoke(payload) → OnDamageResolved(payload)
    │         └── payload.FinalDamage, etc. (tipados, sin cast)
    │
    ▼ [Subscriber, en teardown]
    │
    ▼ TypedEvent<DamageResolvedPayload>.Unsubscribe(OnDamageResolved)
```

`TypedEvent<T>.Clear()` se llama típicamente en `ServiceLocator.ClearScope(Run)` transitions o en teardown de tests. **No** es invocado por esta infra automáticamente — es responsabilidad del consumidor (run lifecycle en Foundation#0005 o el sistema de run).

---

## 6. Variables a exponer (para §101)

**N/A — infraestructura.**

Esta tarea no contiene números de balance, ni curvas, ni tunings. Son 6 archivos de código estático/genérico: un enum de scopes, un enum de nombres de evento, 3 clases estáticas (locator + 2 buses) y 3 structs de payload. Ninguno tiene `[SerializeField]`, `[Range]`, `[MinValue]`, `[Tooltip]`.

El audit de §101 (`Balance#0101_InspectorExposureAudit`) puede saltear este worktree sin consecuencias — no hay superficie inspector‑visible que auditar.

---

## 7. Designer tool asociada

**N/A — infraestructura, no hay tool de designer.**

No hay concepto de "asset que un designer configura" en esta tarea. Las Tools (`Tool#XXXXX_*`) del sprint cubren sistemas que exponen catalogs / SOs / behaviors a diseño (`EnergyActionBalanceEditor`, `ContractEditor`, `EntityWizard`, `BossSetupWizard`, `HUDPreview`). Este worktree no produce ningún asset editable, así que no aplica.

---

## 8. Instructivo de setup Engine

**Setup mínimo inmediato**: ninguno. Tras mergear este worktree a `develop`, los 6 archivos compilan por sí solos en Unity. La API está lista para que Foundation#0005 y el resto del sprint la consuman.

El dev va a dejar un `docs/setup/Foundation#0001_ServiceLocatorEventManager.md` con contenido equivalente a:

```markdown
# Setup — Foundation#0001

## Qué hay que hacer ahora
Nada. Este worktree entrega sólo código estático. Abrir Unity, esperar recompilación, verificar 0 errores. Fin.

## Qué queda pendiente para otros worktrees
- El registro de servicios concretos (catálogos, managers) y la bootstrap scene se implementan en Foundation#0005_CatalogsAndBootstrap. Foundation#0005 va a crear un MonoBehaviour entry point (tipo `GameBootstrap`) que llama `ServiceLocator.AddService<T>(...)` para cada servicio listado en un `ServiceBootstrapSO`.
- Ningún sistema downstream (Energy, Combat, Combos, HUD, etc.) tiene que referenciar manualmente este worktree — basta con que su código use `Patterns.ServiceLocator` / `Patterns.EventManager` / `Patterns.TypedEvent<T>`.

## Cómo validar manualmente que la infra funciona
(Opcional, sólo si se quiere smoke test sin esperar a Foundation#0005)
1. Crear temporalmente un MonoBehaviour en una scene vacía que en `Start()` haga:
   ```csharp
   ServiceLocator.AddService<string>("hello", ServiceScope.Global);
   Debug.Log(ServiceLocator.GetService<string>()); // "hello"
   ServiceLocator.ClearScope(ServiceScope.Run);    // no-op, no había Run
   Debug.Assert(ServiceLocator.HasService<string>()); // true
   ServiceLocator.Clear();
   Debug.Assert(!ServiceLocator.HasService<string>()); // true
   ```
2. Mismo approach para `EventManager` con una `EventName` cualquiera.
3. Borrar el MonoBehaviour (no se commitea).
```

Notas para el dev al escribir `docs/setup/`:
- No pedir al usuario que cree scenes, prefabs, SOs, GameObjects.
- Incluir el warning explícito: bootstrap scene se hace en Foundation#0005, no acá.

---

## 9. Definición de Done

Checklist verificable; el reviewer la cruza con el diff antes de dar PASS.

- [ ] Los 6 archivos listados en §3 existen en `Assets/Scripts/Rollgeon/Patterns/` con namespace `Patterns`.
- [ ] `ServiceLocator.cs` implementa exactamente las 7 firmas del §4.2 (`AddService`, `GetService`, `TryGetService`, `RemoveService`, `HasService`, `ClearScope`, `Clear`), sin miembros públicos adicionales.
- [ ] `ServiceLocator.AddService<T>` hace upsert sobre la key `typeof(T)` (sobrescribe si existe).
- [ ] `ServiceLocator.ClearScope(scope)` borra sólo las entries con ese scope y no afecta al resto — probado mentalmente con un escenario mixto Global + Run.
- [ ] `ServiceLocator.Clear()` vacía el diccionario entero.
- [ ] `EventManager.cs` implementa exactamente las 4 firmas + el delegate del §4.3.
- [ ] `EventManager.Trigger` no lanza cuando no hay suscriptores para la `EventName` dada.
- [ ] `EventManager.UnSubscribe` no lanza cuando la `EventName` nunca fue suscripta.
- [ ] `EventName.cs` contiene todas las entries listadas en §4.4 — ni más, ni menos. **En particular no contiene** `OnScreenPushed` / `OnScreenPopped` / `OnPauseChanged` (movidos a `IScreenManager` por el spec).
- [ ] Cada entry de `EventName` tiene un XML‑doc `/// <summary>args: [...]</summary>` idéntico al del spec §1.2 líneas 529–621.
- [ ] `TypedEvent<T>` implementa exactamente las 4 firmas del §4.5, con constraint `where T : struct`.
- [ ] `TypedEvent<T>.Raise` null‑safe (no lanza sin suscriptores).
- [ ] `EventPayloads.cs` contiene los 3 structs canónicos del §4.6, con campos públicos (no properties) exactamente como el spec.
- [ ] Compila 0 errores 0 warnings en Unity (asumiendo Odin presente — aunque este worktree no usa Odin).
- [ ] Cada clase estática es **unit‑testable desde EditMode tests**: los métodos `Clear()` / `ResetEventDictionary()` / `TypedEvent<T>.Clear()` existen para teardown limpio entre tests. (No escribimos los tests; sólo garantizamos la API para que se puedan escribir.)
- [ ] `docs/setup/Foundation#0001_ServiceLocatorEventManager.md` existe, es accionable y aclara que el setup real vive en Foundation#0005.
- [ ] Ningún archivo fuera de `Assets/Scripts/Rollgeon/Patterns/` ni `docs/setup/`.
- [ ] Ningún `.unity`, `.prefab`, `.asset`, `.inputactions`, `ProjectSettings/*` tocado o creado.

---

## 10. Riesgos / preguntas abiertas

### 10.1 `OnDamageResolved` / `OnHealthChanged` / `OnComboMatched` — RESUELTO: solo `TypedEvent<T>`

**Decisión de orquestación (usuario 2026-04-18):** se elimina la coexistencia. Los 3 eventos viven únicamente como `TypedEvent<T>` con sus structs canónicos (`DamageResolvedPayload`, `HealthChangedPayload`, `ComboMatchedPayload`). Sus entries legacy **NO** existen en el enum `EventName`.

Consecuencias:
- Publishers (DamagePipeline, Heal pipeline, Combo resolver — todos downstream) hacen `TypedEvent<DamageResolvedPayload>.Raise(payload)` — no hay versión legacy de esos 3 nombres.
- Subscribers (HUD, feedback, analytics) se subscriben con `TypedEvent<T>.Subscribe(Action<T>)` tipado.
- El enum `EventName` en este worktree **omite explícitamente** `OnDamageResolved`, `OnHealthChanged`, `OnComboMatched`. Ningún `[CANAL]` comment es necesario.
- Si una foundation posterior necesita publicar uno de estos eventos al legacy bus para compat, el planner de esa foundation abre ticket de cambio y lo discute.

### 10.1b `OnAttributeChanged` agregado al `EventName`

**Decisión de orquestación (usuario 2026-04-18):** se agrega `OnAttributeChanged` al `EventName` legacy para que Foundation#0003 (Attributes + Modifiers) lo consuma sin stubs. XML-doc schema: `args: [Guid entityId, Type attributeType]`. Entry listada en §4.4 "Modifier / attributes".

### 10.2 Thread safety

El spec no pide thread safety y el juego es single‑player / single‑thread (Unity main thread). Implementamos sin `lock`. Si en algún momento se habilita Job System / async accediendo al locator, habría que revisitar — no es problema de este sprint.

### 10.3 Subscripción durante `Trigger` (reentrancia)

Si un subscriber de `EventManager.Trigger(X, ...)` hace `EventManager.Subscribe(X, ...)` o `EventManager.UnSubscribe(X, ...)` en el mismo callback, el `Delegate +=`/`-=` devuelve una nueva referencia pero el `MulticastDelegate` que se está iterando es inmutable, así que el callback no se dispara en este `Trigger` pero sí en los siguientes. Comportamiento aceptable y consistente con `event` estándar de C#. **No** se toma medida especial.

### 10.4 `Clear` vs `ResetEventDictionary` nomenclatura

El spec nombra los métodos de "vaciar todo" de forma distinta: `Clear` en `ServiceLocator` y `TypedEvent<T>`, `ResetEventDictionary` en `EventManager`. Respetamos la nomenclatura del spec aunque sea inconsistente — cambiar `ResetEventDictionary` a `Clear` sería desviarse del contrato documentado.

### 10.5 Visibilidad del diccionario interno

§1.1 declara `private static readonly Dictionary<Type, (object, ServiceScope)> Services`. Lo mantenemos **private** (no internal, no [InternalsVisibleTo]) — si tests necesitan inspeccionar el estado, que lo hagan vía la API pública (`HasService`, `TryGetService`). Esto es intencional para que el locator sea caja negra.

### 10.6 Pregunta abierta: ¿assembly definition (.asmdef)?

El repo arranca sin `.asmdef`. Se podría introducir `Rollgeon.Patterns.asmdef` acá para aislar compilación, pero el plan de orquestación no lo menciona y el resto del sprint asume un solo assembly por defecto. **Decisión**: **no** crear `.asmdef` en este worktree. Si se decide introducir asmdefs, que sea un worktree de refactor dedicado (no afecta a las APIs públicas).

### 10.7 Pregunta abierta: ¿usar `ConcurrentDictionary`?

No. El spec usa `Dictionary<,>` explícito y el juego es single‑thread. Mantenemos `Dictionary<,>`.
