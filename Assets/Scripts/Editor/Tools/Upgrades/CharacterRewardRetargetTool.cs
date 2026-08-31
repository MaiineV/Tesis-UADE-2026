using Rollgeon.Effects.Readers;
using Rollgeon.Upgrades.Character;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Upgrades
{
    /// <summary>
    /// Re-autorado de los boss rewards (BUG-85), vía código porque son SOs Odin
    /// (regla del repo: no editar el YAML a mano):
    /// <list type="bullet">
    /// <item>"Speed+" → "Movimiento+": el stat Speed es orden de turno (oculto,
    /// efecto invisible); pasa a MoveRange (+1 celda al dado de Movimiento).</item>
    /// <item>"Energy+" → "Rolls+": mismo target (RollRegen), display actualizado a
    /// la semántica nueva (sube máximo y arranque del pool).</item>
    /// </list>
    /// Idempotente. El pool (CharacterRewardPool.asset) referencia por guid de
    /// asset, así que el rename no lo rompe.
    /// </summary>
    public static class CharacterRewardRetargetTool
    {
        private const string RewardsFolder = "Assets/Rollgeon/Upgrades/Character/Rewards";
        private const string SpeedAssetPath = RewardsFolder + "/CharacterReward_Speed_Plus2.asset";
        private const string MoveAssetPath = RewardsFolder + "/CharacterReward_Move_Plus1.asset";
        private const string EnergyAssetPath = RewardsFolder + "/CharacterReward_Energy_Plus1.asset";

        [MenuItem("Rollgeon/Upgrades/Retarget Boss Rewards (BUG-85)")]
        public static void Retarget()
        {
            RetargetSpeedToMove();
            RefreshEnergyDisplay();
            AssetDatabase.SaveAssets();
        }

        private static void RetargetSpeedToMove()
        {
            // Post-rename el asset vive en MoveAssetPath — idempotencia.
            var reward = AssetDatabase.LoadAssetAtPath<CharacterRewardSO>(MoveAssetPath)
                         ?? AssetDatabase.LoadAssetAtPath<CharacterRewardSO>(SpeedAssetPath);
            if (reward == null)
            {
                Debug.LogWarning($"[CharacterRewardRetarget] No encontré el asset en '{SpeedAssetPath}' " +
                                 $"ni '{MoveAssetPath}' — nada que re-autorar.");
                return;
            }

            reward.EditorAuthor(
                CharacterRewardTargetStat.MoveRange,
                amount: new ReadConstantInt { Value = 1 },
                upgradeId: "char_rew.move_plus_1",
                displayName: "Movimiento+",
                description: "+1 celda de rango al dado de Movimiento en combate.");
            EditorUtility.SetDirty(reward);

            var currentPath = AssetDatabase.GetAssetPath(reward);
            if (currentPath == SpeedAssetPath)
            {
                string error = AssetDatabase.RenameAsset(SpeedAssetPath, "CharacterReward_Move_Plus1");
                if (!string.IsNullOrEmpty(error))
                    Debug.LogError($"[CharacterRewardRetarget] Rename falló: {error}");
            }

            Debug.Log("[CharacterRewardRetarget] 'Speed+' → 'Movimiento+' (MoveRange +1). " +
                      "Nota: el UpgradeId cambió — un save mid-run con ese pedestal reservado " +
                      "lo omite con warning (path defensivo de InitializeOrHydrate).");
        }

        private static void RefreshEnergyDisplay()
        {
            var reward = AssetDatabase.LoadAssetAtPath<CharacterRewardSO>(EnergyAssetPath);
            if (reward == null)
            {
                Debug.LogWarning($"[CharacterRewardRetarget] No encontré '{EnergyAssetPath}'.");
                return;
            }

            // Mismo target (RollRegen, ordinal 1) — solo el display refleja la
            // semántica nueva: sube el máximo del pool y los rolls al iniciar combate.
            reward.EditorAuthor(
                CharacterRewardTargetStat.RollRegen,
                displayName: "Rolls+",
                description: "+1 al pool de rolls: sube el máximo y los rolls con los que arrancás cada combate.");
            EditorUtility.SetDirty(reward);
            Debug.Log("[CharacterRewardRetarget] 'Energy+' → 'Rolls+' (display de la semántica nueva).");
        }
    }
}
