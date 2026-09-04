using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Entities;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Enemy
{
    /// <summary>
    /// Autora los nombres de ataque del sheet "traduccion enemigos.txt" en los árboles
    /// de IA: setea <c>IntentLabelKey</c>/<c>IntentLabelFallback</c> en el nodo que
    /// publica el intent de cada enemigo. Las entries de tabla las sube el seeder
    /// (<c>Seed Content + UI</c>); esto cablea la key en el dato.
    /// </summary>
    /// <remarks>
    /// Por MenuItem y NO editando YAML: los EnemyDataSO son Odin. El árbol se recorre
    /// por reflexión (los composites anidan hijos en campos y listas con nombres
    /// varios) y el campo se setea en TODOS los nodos del tipo objetivo — estos kits
    /// tienen uno solo. Idempotente.
    /// </remarks>
    public static class AttackNameAuthoringInstaller
    {
        private readonly struct Entry
        {
            public readonly string AssetPath;
            public readonly System.Type NodeType;
            public readonly string Key;
            public readonly string Fallback;

            public Entry(string assetPath, System.Type nodeType, string key, string fallback)
            {
                AssetPath = assetPath; NodeType = nodeType; Key = key; Fallback = fallback;
            }
        }

        private static readonly Entry[] Sheet =
        {
            new Entry("Assets/Rollgeon/Enemies/ED_Artillery.asset",
                typeof(AINode_ExecuteTelegraph), "intent.artillery.coin_drop", "Lluvia de Monedas"),
            new Entry("Assets/Rollgeon/Enemies/ED_MeleeCardEnemySweeper.asset",
                typeof(AINode_ExecuteTelegraph), "intent.card_spades.thrust", "Estocada Anunciada"),
            // Corrección 04/09: el Bingo es el Bolillero Francotirador, no Stackpot.
            new Entry("Assets/Rollgeon/Enemies/ED_Sniper.asset",
                typeof(AINode_ExecuteTelegraph), "intent.sniper.dead_on_ball", "Bolilla Certera"),
            new Entry("Assets/Rollgeon/Enemies/ED_Mago.asset",
                typeof(AINode_IgniteArea), "intent.mago.burning_roll", "Tirada Ardiente"),
            // El shove hereda de RangedShot y sin key propia anunciaba "Disparo" (playtest
            // 04/09). Tipo EXACTO: no toca el RangedShot real del mismo árbol.
            new Entry("Assets/Rollgeon/Enemies/ED_Boss_Cajero.asset",
                typeof(AINode_CajeroShove), "intent.cashier.shove", "Empujón"),
            new Entry("Assets/Rollgeon/Enemies/ED_Charger.asset",
                typeof(AINode_ExecuteCharge), "intent.charger.charge_roll", "Embiste y Rueda"),
            new Entry("Assets/Rollgeon/Enemies/ED_Skirmisher.asset",
                typeof(AINode_Behavior), "intent.skirmisher.x_slash", "Corte en X"),
            new Entry("Assets/Rollgeon/Enemies/ED_MeleeCardEnemy.asset",
                typeof(AINode_Behavior), "intent.card_hearts.slash", "Corte Carmesí"),
        };

        [MenuItem("Rollgeon/Enemies/Author Attack Names From Sheet")]
        public static void Author()
        {
            foreach (var entry in Sheet)
            {
                var so = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(entry.AssetPath);
                if (so == null)
                {
                    Debug.LogWarning($"[AttackNames] No se pudo cargar {entry.AssetPath} — salteado.");
                    continue;
                }

                int touched = 0;
                var visited = new HashSet<object>();
                Walk(so.AIRoot, visited, node =>
                {
                    if (node.GetType() != entry.NodeType) return;
                    var keyField = entry.NodeType.GetField("IntentLabelKey");
                    var fbField = entry.NodeType.GetField("IntentLabelFallback");
                    keyField.SetValue(node, entry.Key);
                    fbField.SetValue(node, entry.Fallback);
                    touched++;
                });

                if (touched > 0)
                {
                    EditorUtility.SetDirty(so);
                    Debug.Log($"[AttackNames] {System.IO.Path.GetFileName(entry.AssetPath)}: " +
                              $"{touched} nodo(s) {entry.NodeType.Name} → '{entry.Key}'.");
                }
                else
                {
                    Debug.LogWarning($"[AttackNames] {entry.AssetPath}: ningún {entry.NodeType.Name} " +
                                     "en el árbol — revisar el mapeo del sheet.");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[AttackNames] Listo. Correr también Rollgeon → Localization → Seed Content + UI.");
        }

        private static void Walk(object node, HashSet<object> visited, System.Action<AIDecisionNode> visit)
        {
            if (node is not AIDecisionNode decision || !visited.Add(node)) return;
            visit(decision);

            foreach (var field in node.GetType().GetFields(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var value = field.GetValue(node);
                switch (value)
                {
                    case AIDecisionNode child:
                        Walk(child, visited, visit);
                        break;
                    case IEnumerable list and not string:
                        foreach (var item in list)
                            if (item is AIDecisionNode listed)
                                Walk(listed, visited, visit);
                        break;
                }
            }
        }
    }
}
