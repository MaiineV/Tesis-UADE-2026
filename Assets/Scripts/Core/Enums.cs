public enum CombinationType { HighDie, Pair, TwoPair, ThreeOfAKind, Straight, FullHouse, FourOfAKind, Generala, DoubleGenerala }
public enum TurnPhase { PlayerMovement, PlayerCombatRoll, PlayerDefenseRoll, EnemyCombatRoll, EnemyMovement, RoundEnd }
public enum RoomState { Exploration, Combat, Cleared, Shop, Boss, Sacrifice, Craps, Potion }
public enum FaceUpgradeType { ValueIncrease, ValueSet, FaceRemoval }
public enum EnemyBehavior { Aggressive, Cautious, Stationary, Ranged }
public enum CombatActionType { DealtDamage, Defended, TookDamage, KilledEnemy }
public enum ExplorationAction { Move, Bow, Potion }
public enum RunBuffType { DamageBoost = 100, ExtraRoll = 110, HealPerRoom = 120, CritBonus = 130, ShieldOnCombatStart = 140, EnergyGainBoost = 150 }
public enum BossDebuffType { ComboDamageReduction = 200, ReducedRolls = 210, ReducedShield = 220, DamageReduction = 230, MaxHPReduction = 240 }
