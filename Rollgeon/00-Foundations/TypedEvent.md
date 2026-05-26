---
title: TypedEvent
type: service
domain: 00-Foundations
status: done
tags: [foundation, events, pub-sub, typed]
---

# TypedEvent

> Allocation-free typed pub/sub complement to [[EventManager]]. One
> generic static channel per struct payload type.

## Purpose

Strongly-typed events with no boxing. Subscribers get the exact payload
struct instead of a variadic `object[]`. Used for hot-path events like
`DamageResolvedPayload`, `HealthChangedPayload`, `ComboMatchedPayload`.

## API

```csharp
public static class TypedEvent<T> where T : struct {
    public static void Subscribe(Action<T> listener);
    public static void Unsubscribe(Action<T> listener);
    public static void Raise(T payload);
    public static void Clear(); // teardown / run boundary
}
```

The `struct` constraint is deliberate: prevents allocations per `Raise`
and keeps the payload immutable by value during dispatch.

## Single-channel rule

An event lives **either** on [[EventManager]] **or** on `TypedEvent<T>`.
Never both — publishing through two channels lets subscribers miss
invocations silently.

## Dependencies

- **Used by:** any system that publishes / consumes a typed payload
  struct. See domain MOCs for concrete payloads.

## Code

- Runtime: `Assets/Scripts/Rollgeon/Patterns/TypedEvent.cs`

## External references

- TECHNICAL.md: §1.2.1 TypedEvent — single-channel rule
