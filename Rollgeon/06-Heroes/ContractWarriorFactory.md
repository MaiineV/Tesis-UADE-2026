---
title: ContractWarriorFactory
type: system
domain: 06-Heroes
status: done
tags: [heroes, contract, factory, warrior]
---

# ContractWarriorFactory

> Test-friendly factory that builds the canonical 8-entry Warrior
> [[ContractSheet]]:
> `[Par, DoblePar, SumaX, Trio, Escalera, FullHouse, Poker, Generala]`.

## Why a factory

- Tests need a deterministic sheet; spinning up an entire
  [[ClassHeroSO]] asset for every test is overkill.
- The production Warrior asset is assembled via Odin in the editor;
  duplicating that layout in the factory lets us round-trip through
  `ContractSheet.Validate` in fast EditMode tests.

## Dependencies

- **Uses:** [[ContractSheet]], all 8 concrete combos.
- **Used by:** test fixtures (`ContractWarriorFactoryTests.cs`,
  downstream combat / combo tests).

## Code

- Runtime: `Assets/Scripts/Rollgeon/Heroes/ContractWarriorFactory.cs`
- Tests: `.../Tests/ContractWarriorFactoryTests.cs`

## External references

- Setup: `docs/setup/Content#0097b_WarriorContractAndWeakness.md`
- TECHNICAL.md: §5.3 Warrior contract reference
