public enum CombinationType { HighDie, Pair, TwoPair, ThreeOfAKind, SmallStraight, MediumStraight, Straight, FullHouse, FourOfAKind, Generala, DoubleGenerala }
public enum TurnPhase { PlayerMovement, PlayerCombatRoll, PlayerDefenseRoll, EnemyCombatRoll, EnemyMovement, RoundEnd,
    // 3AP system phases
    AP1_Movement = 100, AP2_Action = 110, AP3_Shield = 120, EnemyPhase = 130 }
public enum RoomState { Exploration, Combat, Cleared, Shop, Boss, Sacrifice, Craps, Potion }
public enum FaceUpgradeType { ValueIncrease, ValueSet, FaceRemoval }
public enum EnemyBehavior { Aggressive, Cautious, Stationary, Ranged }
public enum CombatActionType { DealtDamage, Defended, TookDamage, KilledEnemy }
public enum ExplorationAction { Move, Bow, Potion }
public enum RunBuffType { DamageBoost = 100, ExtraRoll = 110, HealPerRoom = 120, CritBonus = 130, ShieldOnCombatStart = 140, EnergyGainBoost = 150, ComboDamageBoost = 160 }
public enum BossDebuffType { ComboDamageReduction = 200, ReducedRolls = 210, ReducedShield = 220, DamageReduction = 230, MaxHPReduction = 240 }
