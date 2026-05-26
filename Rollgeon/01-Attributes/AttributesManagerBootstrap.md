---
title: AttributesManagerBootstrap
type: service
domain: 01-Attributes
status: done
tags: [attributes, bootstrap, service]
---

# AttributesManagerBootstrap

> `IPreloadableService` wrapper que instancia el [[AttributesManager]]
> global y lo registra en el [[ServiceLocator]] bajo
> [[ServiceScope]] `Global`. Cierra el TODO heredado entre
> `Foundation#0003` (que delega el registro) y `Foundation#0005` (que no
> lo tenía listado).

## Shape

```csharp
[Serializable]
public sealed class AttributesManagerBootstrap : IPreloadableService, IDisposable {
    public int Priority => 5;      // antes que EnergyService (50)

    public void Register();        // instancia + AddService<AttributesManager>(Global)
    public void Dispose();         // disposes the underlying manager
}
```

## Por qué Priority = 5

Varios services resuelven `AttributesManager` en su propio `Register()`
y hacen early-return si no lo encuentran. Orden correcto:

| Priority | Service | Necesita AttributesManager? |
|---:|---|---|
| **5**  | **AttributesManagerBootstrap** | — |
| 10 | PhaseServiceBootstrap | no |
| 30 | PlayerServiceBootstrap | no |
| 50 | EnergyService | **sí** |
| 60 | TurnManagerBootstrap | vía IEnergyService |
| 70 | RerollBudgetServiceBootstrap | vía IEnergyService |
| 100 | TurnOrderServiceBootstrap | no (usa AttributesManager via Speed en runtime) |

Si no se agrega este bootstrap, la consola de Unity escupe una cascada
de `[EnergyService] AttributesManager no esta registrado`,
`[TurnManager] IEnergyService no esta registrado`, etc.

## Registro

Se agrega a `ServiceBootstrap.asset.ExtraServices` en el Inspector de
Unity. Al correr `ServiceBootstrapSO.RegisterAll()`, el SO ordena los
extras por `Priority` ascendente e invoca `Register()` en cada uno.

## Dependencies

- **Uses:** [[AttributesManager]], [[ServiceLocator]],
  [[IPreloadableService]].
- **Used by:** [[ServiceBootstrapSO]] (listado en `ExtraServices`).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Attributes/AttributesManagerBootstrap.cs`

## External references

- Setup: `docs/setup/_SETUP_ROUND2_STATUS.md §C.1 item 4`
- Setup: `docs/setup/Foundation#0003_AttributesAndModifiers.md`
  (delegación al bootstrap)
- TECHNICAL.md: §2.3 AttributesManager
