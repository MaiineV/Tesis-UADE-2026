# Setup — System#0100a_EnergyAttributeAndRegen

> Worktree: `sprint03/system/0100a-energy-attribute-and-regen`
> Scope: stat `Energy` + `EnergyRegenPolicy` + `EnergyService` + esqueleto `RulesetSO`.
> TECHNICAL.md: §2, §3, §12.6.

Este instructivo describe los pasos manuales que el usuario debe ejecutar en el Editor
de Unity tras mergear esta worktree para dejar la pieza T100a funcionando de punta a punta.

## 0. Prerequisitos

- Foundation#0001 (`ServiceLocator`, `EventManager`, `EventName`) mergeada a `develop`.
- Foundation#0003 (`AttributesManager`, `BaseAttribute<T>`, `Modifier<T>` con Carrier/Source) mergeada.
- Foundation#0005 (`IPreloadableService`, `ServiceBootstrapSO`, `BootstrapRunner`) mergeada.
- Escena `00_Bootstrap.unity` existente con un GameObject que corra `BootstrapRunner` apuntando a `Assets/Rollgeon/Catalogs/ServiceBootstrap.asset`.

## 1. Crear el folder de Rulesets (si no existe)

1. En Project window, click derecho sobre `Assets/Rollgeon/`.
2. `Create → Folder` → nombre: `Rulesets`.

Path final: `Assets/Rollgeon/Rulesets/`.

## 2. Crear el asset `Ruleset`

1. Click derecho sobre `Assets/Rollgeon/Rulesets/`.
2. `Create → Rollgeon → Balance → Ruleset`.
3. Nombre del asset: `Ruleset`.
4. Verificar defaults en el Inspector (Odin):
   - `Energy → EnergyMax = 4`
   - `Energy → EnergyAtRunStart = 2`
   - `Energy → EnergyRegenBase = 2`

`OnValidate` clampea automaticamente `EnergyAtRunStart` a `EnergyMax` si se supera — si
eventualmente subis el cap, acordate de actualizar el start.

## 3. Registrar el `Ruleset` como Settings Asset del bootstrap

1. Abrir `Assets/Rollgeon/Catalogs/ServiceBootstrap.asset`.
2. En la seccion **Settings Assets**, agregar un nuevo slot.
3. Arrastrar el asset `Ruleset.asset` al slot.
4. Guardar.

El `RegisterByRuntimeType` de Foundation#0005 lo inyecta al `ServiceLocator` bajo su
Type runtime (`RulesetSO`) durante el bootstrap.

## 4. Registrar `EnergyService` como Extra Runtime Service

1. Mismo asset `ServiceBootstrap.asset`.
2. En la seccion **Extra Runtime Services**, agregar un nuevo slot.
3. Desde el dropdown polimorfico (Odin), elegir `EnergyService`.
4. Guardar.

El `EnergyService.Register()` (invocado por `ServiceBootstrapSO.RegisterAll` tras
catalogos + settings) se autorregistra como `IEnergyService` en `ServiceLocator.Global`
y se suscribe a `EventName.OnTurnFinished` + `EventName.OnRunStart`.

> **Priority = 50.** Corre despues de cualquier `IPreloadableService` con priority
> menor. Si agregas un servicio que deba resolver `IEnergyService` en su propio
> `Register()`, usa `Priority >= 50`.

## 5. Verificar el bootstrap

1. Abrir escena `00_Bootstrap.unity` y presionar Play.
2. En Console deberia aparecer algo como:
   `[BootstrapLog] Registered X catalogs, Y settings, Z extra services`
   con `settings >= 1` y `extra services >= 1`.
3. Si aparece `[EnergyService] RulesetSO no esta registrado...`, volver al paso 3.

## 6. Smoke test (sin HUD)

El HUD de energia (T95a/T95b) NO existe aun. Para validar que la pieza funciona:

1. Abrir `Window → General → Test Runner`.
2. Pestana **EditMode**.
3. Ejecutar los asmdefs:
   - `Rollgeon.Attributes.Stats.Tests` (tests de `Energy`).
   - `Rollgeon.Combat.Energy.Tests` (tests de `EnergyRegenPolicy` + `EnergyService`).
4. Todos en verde = init, spend, regen y gating funcionan.

## 7. Contrato para downstream

- **T100b (TurnManager).** Debe cobrar energia via `IEnergyService.SpendEnergy(playerGuid, cost)`.
  NO usar `AttributesManager.Modify<Energy,int>` directo — ese path no dispara
  `OnEnergyChanged` con payload `(current, max)` completo.
- **T98 / PlayerSpawner.** Tras registrar al player en `AttributesManager`, invocar
  `IEnergyService.InitializeForEntity(playerGuid)` explicitamente. El evento
  `OnRunStart` NO trae player Guid (schema: `[Guid runId, string rulesetId]`).
- **T95a/T95b (HUD).** Suscribirse a `EventName.OnEnergyChanged` (payload
  `[Guid entityGuid, int current, int max]`) o a `EventName.OnPlayerEnergyChanged`
  (mismo payload, solo dispara cuando la entidad == player cacheado).

## 8. Troubleshooting

| Sintoma | Causa | Fix |
|---|---|---|
| `[AttributesManager] Entity '...' is not registered` al inicializar energia | `InitializeForEntity` llamado antes de que el spawner registre al player | Invocar `InitializeForEntity` DESPUES de `AttributesManager.Register(playerGuid, attrs)`. |
| `EnergyMax == 0` en runtime | Asset creado fuera del Editor (no corrio `OnValidate`) | Abrir `Ruleset.asset`, editar cualquier campo, guardar. |
| `OnEnergyChanged` no llega al HUD | HUD escucha `OnPlayerEnergyChanged` y nadie llamo `InitializeForEntity` | Asegurarse de que el spawner llama `InitializeForEntity(playerGuid)` despues del register. |
| Regen no ocurre al terminar turno | Caller dispara `OnTurnFinished` con el Guid de otra entidad (enemigo) en lugar del player | En el FP, solo el player regenera. Enemigos manejan su regen propia cuando llegue T99. |

## 9. Referencias cruzadas

- Plan: `plan.md` en la raiz de esta worktree (excluido del commit).
- Issue HacknPlan: #100 "Implementar sistema de Energia completo" — bullet
  "La regeneracion al terminar turno funciona correctamente".
- TECHNICAL.md: §2.1 (IAttribute), §2.2 (invariantes), §3.1 (Modifier<T>),
  §3.4 (lifecycle OnTurnFinished), §12.6 (action economy), §14.7 (RulesetSO
  completo — cubierto por Balance#0101).
- Tool: `Tool#0100T_EnergyActionBalanceEditor` (planificada, no implementada aqui).
