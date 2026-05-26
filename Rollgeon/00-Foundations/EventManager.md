---
title: EventManager
type: service
domain: 00-Foundations
status: done
tags: [foundation, events, pub-sub, patterns]
---

# EventManager

> String-keyed (via [[EventName]]) event bus with `object[]` payloads.
> Decouples producers from consumers throughout the codebase.

## Purpose

Legacy typed-string pub/sub. Subscribers receive a variadic `object[]`
and cast positionally per the `EventName`'s documented schema. Coexists
with [[TypedEvent]] under the **single-channel rule**: any given event
ships through exactly one bus, never both.

## API

```csharp
public delegate void EventReceiver(params object[] parameter);

public static class EventManager {
    public static void Subscribe(EventName eventType, EventReceiver method);
    public static void UnSubscribe(EventName eventType, EventReceiver method);
    public static void Trigger(EventName eventType, params object[] parameters);
    public static void ResetEventDictionary(); // teardown
}
```

**Convention:** when an event refers to an entity, `parameters[0]` must be
the entity's `InstanceId` (a `System.Guid`). The bus does not enforce it —
publishers do.

## Dependencies

- **Uses:** [[EventName]]
- **Used by:** [[AttributesManager]] (`OnAttributeChanged`,
  `OnModifierAdded`, `OnModifierRemoved`), [[Modifier]] (lifetime tick
  events, `OnRunEnd`, `OnCombatEnd`), [[PhaseService]], UI screens,
  combat pipelines.

## Migration path

Moving an event to the typed channel means: remove its entry from
[[EventName]], replace publishers/subscribers with [[TypedEvent]]`<T>`.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Patterns/EventManager.cs`

## External references

- Setup: `docs/setup/Foundation#0001_ServiceLocatorEventManager.md`
- TECHNICAL.md: §1.2 Base patterns — EventManager
