using UnityEngine;
using System.Collections.Generic;

public enum ShopItemType { PotionRefill = 0, DiceAdd = 10, StatBoostDex = 20, StatBoostSpeed = 30, Buff = 40 }

[System.Serializable]
public struct RollResult { public string DiceId; public int FaceIndex; public int Value; }

[System.Serializable]
public struct FullRollResult { public RollResult[] Results; public int RollNumber; }

[System.Serializable]
public struct CombinationResult { public CombinationType Type; public int[] MatchingDice; public int[] RemainingDice; public int BaseDamage; }

[System.Serializable]
public struct RunStats { public int RoundsFought; public int DamageDealt; public int DamageTaken; public CombinationType BestCombo; public int BestComboDamage; public int CrapsAttempts; public int CrapsWins; public int EnemiesDefeated; public int LevelsCleared; }

[System.Serializable]
public class FaceUpgrade { public FaceUpgradeType Type; public int TargetFaceIndex; public int Value; public string Description; }

[System.Serializable]
public struct FaceUpgradeOffer { public string TargetDiceId; public string TargetDiceName; public FaceUpgrade Upgrade; }

[System.Serializable]
public struct CrapsResult { public bool Success; public CombinationType BetCombo; public CombinationType ActualCombo; public float DamageMultiplier; public int HPChange; public int FinalDamage; }

[System.Serializable]
public struct DiceLoadout { public DiceData DiceType; public int Quantity; }

[System.Serializable]
public struct BagSummary { public int TotalDice; public float UsedPower; public float MaxPower; public float RemainingPower; public List<DiceSummary> DiceList; }

[System.Serializable]
public struct DiceSummary { public string Id; public string TypeName; public int[] Faces; public float PowerCost; public Color Color; }

[System.Serializable]
public class RunBuffData { public RunBuffType Type; public string Title; public string Description; public float Value; public bool IsFromShop; }

[System.Serializable]
public class BossDebuffData { public BossDebuffType Type; public string Title; public string Description; public float Value; public CombinationType TargetCombo; }
