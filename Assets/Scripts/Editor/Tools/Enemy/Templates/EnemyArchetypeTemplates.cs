using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Rollgeon.Entities;
using Rollgeon.PreConditions.Concretes;
using static Rollgeon.Editor.Tools.Enemy.Templates.EnemyTreeKit;

namespace Rollgeon.Editor.Tools.Enemy.Templates
{
    /// <summary>
    /// Las diez fichas del GDD "Patrones de Ataque → Fichas de enemigo" como árboles genéricos
    /// (Move / KeepDistance / Telegraph / Behavior con Eff y PC). Con las piezas de runtime de
    /// Feature#0061 (empuje de grilla, aura, alineación/LoS, área instantánea) las diez juegan
    /// tal cual dice su ficha.
    /// </summary>
    public static class EnemyArchetypeTemplates
    {
        public const string FireTileName = "Tile_FireTemp";

        static List<EnemyTemplate> _all;

        public static IReadOnlyList<EnemyTemplate> All => _all ??= Build();

        public static EnemyTemplate Find(string id)
        {
            foreach (var t in All) if (t.Id == id) return t;
            return null;
        }

        static List<EnemyTemplate> Build() => new List<EnemyTemplate>
        {
            new EnemyTemplate("pursuer", "Pursuer", EnemyArchetype.Melee,
                "Se acerca por la ruta más corta y golpea a distancia 1 (daño = ATK).",
                so => Fill(so, 75, 20, 4, 1, AttackPatternKind.ContactAdjacent, AttackTiming.Instant,
                    atk => EnergyLoop(IfTargetInRange(1, AttackMelee(), Chase(3, 1), useOwnerRange: true)))),

            new EnemyTemplate("charger", "Charger", EnemyArchetype.Melee,
                "Se alinea a distancia 2 y telegrafía una banda de 3 casillas hacia el jugador; adyacente, " +
                "embiste: daño pleno + empuje de 1 casilla, y si el empuje queda bloqueado (pared/ocupante) " +
                "suma +ATK×0,5 en su lugar.",
                so => Fill(so, 90, 20, 4, 2, AttackPatternKind.StraightLine, AttackTiming.Telegraph,
                    atk => Sequence(ExecuteTelegraph(), EnergyLoop(
                        IfTargetInRange(1, AttackMeleeWithPush(),
                            Sequence(Chase(3, 2), Telegraph(ThreatShape.DirectionalBand, 1, atk, depth: 3))))))),

            new EnemyTemplate("sweeper", "Sweeper", EnemyArchetype.Melee,
                "Se mantiene pegado y barre en cono instantáneo hacia el jugador (1-3 casillas, en el mismo " +
                "turno, sin telegraph). Solo golpea al jugador — sin friendly fire.",
                so => Fill(so, 80, 15, 4, 1, AttackPatternKind.Cone, AttackTiming.Instant,
                    atk => EnergyLoop(
                        IfTargetInRange(1, SweepCone(), Chase(3, 1), DistanceMetric.Chebyshev,
                            useOwnerRange: true)))),

            new EnemyTemplate("skirmisher", "Skirmisher", EnemyArchetype.Ranged,
                "Dispara SOLO en diagonal exacta a ≤4 (Chebyshev) y se reposiciona a distancia 3. El filtro " +
                "gatea el disparo; el movimiento no busca posiciones diagonales (optimiza distancia).",
                so => Fill(so, 50, 12, 5, 4, AttackPatternKind.ContactDiagonal, AttackTiming.Instant,
                    atk => EnergyLoop(IfTargetInRange(4, AttackRanged(), Kite(3, 3), DistanceMetric.Chebyshev,
                        alignment: TargetAlignment.DiagonalOnly, useOwnerRange: true)))),

            new EnemyTemplate("kiter", "Kiter", EnemyArchetype.Ranged,
                "Dispara si el jugador está a ≤5 (diamante Manhattan) y mantiene distancia 3; si no, se acerca. " +
                "Mismo árbol que ED_RangedEnemy.",
                so => Fill(so, 50, 10, 5, 5, AttackPatternKind.DiamondArea, AttackTiming.Instant,
                    atk => EnergyLoop(IfTargetInRange(5, Sequence(AttackRanged(), Kite(3, 3)), Chase(3, 3),
                        useOwnerRange: true)))),

            new EnemyTemplate("sniper", "Sniper", EnemyArchetype.Ranged,
                "En la misma fila/columna que el jugador, a ≤8 y con línea de visión libre, telegrafía la " +
                "fila (se cobra al turno siguiente, no lo sigue); si no, busca distancia 5.",
                so => Fill(so, 45, 25, 4, 8, AttackPatternKind.StraightLine, AttackTiming.Telegraph,
                    atk => Sequence(ExecuteTelegraph(), EnergyLoop(
                        IfTargetInRange(8, Telegraph(ThreatShape.Row, 1, atk), Kite(2, 5),
                            alignment: TargetAlignment.SameRowOrColumn, lineOfSight: true,
                            useOwnerRange: true))))),

            new EnemyTemplate("artillery", "Artillery", EnemyArchetype.Ranged,
                "Casi no se mueve (SPD 1); a ≤6 telegrafía un 3×3 sobre el jugador y lo cobra al turno siguiente.",
                so => Fill(so, 70, 25, 1, 6, AttackPatternKind.DiamondArea, AttackTiming.Telegraph,
                    atk => Sequence(ExecuteTelegraph(), EnergyLoop(
                        IfTargetInRange(6, Telegraph(ThreatShape.SquareAroundPlayer, 1, atk), Wait(),
                            useOwnerRange: true))))),

            new EnemyTemplate("mago", "Mago", EnemyArchetype.Ranged,
                "A ≤5 telegrafía un 3×3 sobre el jugador y al turno siguiente lo prende con Fuego Temporal " +
                "(2 rondas); si no, mantiene distancia 4. Requiere el asset " + FireTileName + ".",
                so => Fill(so, 45, 10, 4, 5, AttackPatternKind.PersistentZone, AttackTiming.Telegraph,
                    atk => Sequence(Ignite(FindTile(FireTileName), 2), EnergyLoop(
                        IfTargetInRange(5, Telegraph(ThreatShape.SquareAroundPlayer, 1, atk), Kite(3, 4),
                            useOwnerRange: true))))),

            new EnemyTemplate("healer", "Healer", EnemyArchetype.Support,
                "Si hay un aliado herido, cura al de menos vida (HealStrength) a ≤2 o se le acerca; si no hay " +
                "a quién curar, dispara y mantiene distancia 3. Mismo árbol que ED_Healer.",
                so => Fill(so, 60, 10, 4, 2, AttackPatternKind.Unspecified, AttackTiming.Instant,
                    atk => EnergyLoop(IfAllyBelowMax(
                        IfTargetInRange(2, HealAlly(), MoveToAlly(3, 2), selector: LowestHpAlly(),
                            useOwnerRange: true),
                        Sequence(AttackRanged(), Kite(3, 3)))))),

            new EnemyTemplate("guardian", "Guardian", EnemyArchetype.Support,
                "Aura defensiva: los aliados a ≤2 casillas reciben −5 de daño entrante (piso 1) mientras " +
                "viva. Golpea a distancia 1; si no llega, se pega al aliado más cercano.",
                so =>
                {
                    Fill(so, 110, 15, 3, 1, AttackPatternKind.ContactAdjacent, AttackTiming.Instant,
                        atk => EnergyLoop(IfTargetInRange(1, AttackMelee(), MoveToAlly(3, 1), useOwnerRange: true)));
                    so.AuraRadius = 2;
                    so.AuraFlatReduction = 5;
                }),
        };

        /// <param name="root">Recibe el ATK ya decidido: los Telegraph llevan el daño como constante.</param>
        static void Fill(EnemyDataSO so, int hp, int atk, int spd, int range,
                         AttackPatternKind pattern, AttackTiming timing, System.Func<int, AIDecisionNode> root)
        {
            so.BaseHP = hp;
            so.BaseAttack = atk;
            so.BaseSpeed = spd;
            so.BaseAttackRange = range;
            so.MaxEnergy = 3;
            so.Design ??= new EnemyDesignSheet();
            so.Design.Pattern = pattern;
            so.Design.Timing = timing;
            so.AIRoot = root(atk);
            so.AIDetachedNodes?.Clear();
        }

        /// <summary>Identidad + ficha comunes a todas: el árbol lo pone cada plantilla en su <c>apply</c>.</summary>
        internal static void ApplyIdentity(EnemyTemplate template, EnemyDataSO so)
        {
            so.EntityId = "enemy." + template.Id;
            so.DisplayName = template.Name;
            so.Design ??= new EnemyDesignSheet();
            so.Design.Archetype = template.Archetype;
            so.Design.Notes = $"Plantilla «{template.Name}»: {template.Description}";
        }

        /// <summary>Aplica identidad, ficha, stats y árbol de la plantilla sobre <paramref name="so"/>.</summary>
        public static void ApplyTo(EnemyTemplate template, EnemyDataSO so)
        {
            ApplyIdentity(template, so);
            template.Apply(so);
        }
    }
}
