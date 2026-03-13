using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct RollResult { public string DiceId; public int FaceIndex; public int Value; }

[System.Serializable]
public struct FullRollResult { public RollResult[] Results; public int RollNumber; }

[System.Serializable]
public struct CombinationResult { public CombinationType Type; public int[] MatchingDice; public int[] RemainingDice; public int BaseDamage; }

[System.Serializable]
public struct RunStats { public int RoundsFought; public int DamageDealt; public int DamageTaken; public CombinationType BestCombo; public int BestComboDamage; public int CrapsAttempts; public int CrapsWins; public int EnemiesDefeated; }

[System.Serializable]
public class FaceUpgrade { public FaceUpgradeType Type; public int TargetFaceIndex; public int Value; public string Description; }

[System.Serializable]
public struct FaceUpgradeOffer { public string TargetDiceId; public string TargetDiceName; public FaceUpgrade Upgrade; }

[System.Serializable]
public struct CrapsResult { public bool Success; public CombinationType BetCombo; public CombinationType ActualCombo; public float DamageMultiplier; public int HPChange; public int FinalDamage; }

[System.Serializable]
public struct DiceLoadout { public DiceData DiceType; public int Quantity; }

[System.Serializable]
public struct BagSummary { public int TotalDice; public int UsedPower; public int MaxPower; public int RemainingPower; public List<DiceSummary> DiceList; }

[System.Serializable]
public struct DiceSummary { public string Id; public string TypeName; public int[] Faces; public int PowerCost; public Color Color; }
