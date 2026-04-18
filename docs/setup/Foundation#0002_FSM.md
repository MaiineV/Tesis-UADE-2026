# Foundation#0002_FSM — Setup

Sin acciones de engine requeridas. Merge + recompile de Unity son suficientes.

Esta tarea agrega infraestructura 100% C#: ninguna escena, prefab, ScriptableObject o setting de proyecto fue tocado.

## Archivos agregados

```
Assets/Scripts/Rollgeon/Patterns/FSM/
  IState.cs
  BaseState.cs
  StateMachine.cs
  TransitionGuard.cs
  StateMachineBuilder.cs
  FSMLogger.cs
  Tests/
    FSMTests.cs
    Rollgeon.Patterns.FSM.Tests.asmdef
```

## Tipos públicos nuevos

- `Patterns.FSM.IState`
- `Patterns.FSM.BaseState<TContext, TInput>`
- `Patterns.FSM.StateMachine<TContext, TInput>`
- `Patterns.FSM.TransitionGuard<TContext>` (delegate)
- `Patterns.FSM.StateMachineBuilder<TContext, TInput>`
- `Patterns.FSM.FSMLogger` (static, `[Conditional]`-gated)

## Verificación

1. Abrir Unity. Esperar recompilación. Debe haber **0 errores** y **0 warnings nuevos**.
2. Abrir `Window → General → Test Runner`. Seleccionar la pestaña **EditMode**.
3. Correr los tests del assembly `Rollgeon.Patterns.FSM.Tests`. Deben pasar los 9 tests (7 del DoD + 2 extras de cobertura — guard y ticks).

## Uso rápido

### Modo override-based (estado decide sus transiciones)

```csharp
public sealed class PlayerTurn : BaseState<CombatContext, TurnInput>
{
    public PlayerTurn(CombatContext ctx) : base(ctx) { }

    public override void Enter(TurnInput input)
    {
        Context.ResetActionsUsed();
    }

    public override bool CheckInput(TurnInput input, out BaseState<CombatContext, TurnInput> next)
    {
        if (input == TurnInput.EndTurn) { next = Context.States.EnemyTurn; return true; }
        next = null;
        return false;
    }
}

var sm = new StateMachine<CombatContext, TurnInput>(ctx, initial: ctx.States.PlayerTurn);
sm.OnTransition += FSMLogger.LogTransition;
sm.Start();
sm.SendInput(TurnInput.EndTurn);
```

### Modo declarativo (builder)

```csharp
var sm = new StateMachineBuilder<CombatContext, TurnInput>()
    .From(playerTurn).On(TurnInput.EndTurn).To(enemyTurn)
    .From(enemyTurn).On(TurnInput.EndTurn).If(ctx => ctx.EnemyDone).To(playerTurn)
    .Build(ctx, initial: playerTurn);

sm.Start();
```

## Reglas de uso (contrato)

- **El owner tickea la FSM** — la FSM no se suscribe a `MonoBehaviour.Update`. Llamá `sm.Update()` / `sm.LateUpdate()` / `sm.FixedUpdate()` desde tu `MonoBehaviour` o driver equivalente. Esto permite freezear la FSM (ej.: bajo overlay de `IPhaseService`).
- **`SendInput` es reentrante-safe** — si un `Enter` o `Exit` llama `SendInput`, el input se encola y se drena al terminar el dispatch en curso. No hay stack overflow.
- **`Stop()` llama `Exit(default)` sobre el estado actual** y marca `IsRunning=false`, pero **no** emite `OnStateExited` (es shutdown, no transición).
- **Single-thread, síncrono.** Sin `async/await`, sin tasks, sin callbacks diferidos. Si un estado necesita esperar una animación, expone un `bool IsReady` o un evento y el owner dispara el siguiente input cuando corresponda.
- **Zero acoplamiento con `EventManager`.** Si querés levantar eventos globales (`OnTurnStarted`, etc.), hacelo desde el `Enter`/`Exit` de tu estado concreto. La infra sólo expone C# events.
- **Naming convention para estados concretos (TECHNICAL.md §1.3)**: usar sufijos `State` o `Step`, nunca `Phase`. `Phase` está reservado para `GamePhase`/`IPhaseService`.

## Notas técnicas

- **Divergencia de firma vs. spec §1.3** (aprobada): la spec muestra `State<T>`/`EventFSM<T>` con un único type param. Acá dividimos en `TContext` (owner) y `TInput` (discriminador) para no ensuciar el input con dependencias del owner. Ver plan §10 R1.
- **`FSMLogger` es la única clase del paquete que toca `UnityEngine`.** Está gateada por `[Conditional("UNITY_EDITOR")]` + `[Conditional("DEVELOPMENT_BUILD")]`, así que las llamadas se eliminan del IL en builds de release.
- **No se agregó `.asmdef` de runtime** — la infra vive en `Assembly-CSharp`. Decisión diferida (plan §10.6). El test assembly sí tiene su propio `.asmdef` porque Unity Test Runner lo requiere.
- **No se usan atributos Odin.** La FSM es infraestructura pura y no necesita inspector.
