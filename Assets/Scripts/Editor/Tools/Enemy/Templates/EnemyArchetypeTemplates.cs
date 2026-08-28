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
    /// (Move / KeepDistance / Telegraph / Behavior con Eff y PC). Lo que el GDD dejó TBD o el
    /// runtime no tiene (empuje de grilla, solo-diagonales, aura) queda dicho en la descripción
    /// para que el designer sepa qué modelar.
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
                    atk => EnergyLoop(IfTargetInRange(1, AttackMelee(), Chase(3, 1))))),

            new EnemyTemplate("charger", "Charger", EnemyArchetype.Melee,
                "Se alinea a distancia 2 y telegrafía una banda de 3 casillas hacia el jugador; adyacente, " +
                "usa un golpe reducido (×0,5). TBD del GDD: el empuje de 1 casilla y el +50% si está bloqueado " +
                "(no hay efecto de empuje de grilla; EffApplyImpulse es solo visual).",
                so => Fill(so, 90, 20, 4, 2, AttackPatternKind.StraightLine, AttackTiming.Telegraph,
                    atk => Sequence(ExecuteTelegraph(), EnergyLoop(
                        IfTargetInRange(1, AttackMelee("Golpe reducido", 0.5f),
                            Sequence(Chase(3, 2), Telegraph(ThreatShape.DirectionalBand, 1, atk, depth: 3))))))),

            new EnemyTemplate("sweeper", "Sweeper", EnemyArchetype.Melee,
                "Se mantiene pegado y telegrafía un 3×3 alrededor de sí (se cobra al turno siguiente). " +
                "TBD: el barrido instantáneo en arco/cono/cruz no existe sin código; queda como telegraph corto.",
                so => Fill(so, 80, 15, 4, 1, AttackPatternKind.ArcSweep, AttackTiming.Telegraph,
                    atk => Sequence(ExecuteTelegraph(), EnergyLoop(
                        IfTargetInRange(1, Telegraph(ThreatShape.SquareAroundSelf, 1, atk), Chase(3, 1),
                            DistanceMetric.Chebyshev))))),

            new EnemyTemplate("skirmisher", "Skirmisher", EnemyArchetype.Ranged,
                "Dispara a ≤4 (Chebyshev) y se reposiciona a distancia 3. TBD: el GDD lo restringe a " +
                "diagonales; el runtime no tiene ese chequeo, hoy dispara en cualquier dirección.",
                so => Fill(so, 50, 12, 5, 4, AttackPatternKind.ContactDiagonal, AttackTiming.Instant,
                    atk => EnergyLoop(IfTargetInRange(4, AttackRanged(), Kite(3, 3), DistanceMetric.Chebyshev)))),

            new EnemyTemplate("kiter", "Kiter", EnemyArchetype.Ranged,
                "Dispara si el jugador está a ≤5 (diamante Manhattan) y mantiene distancia 3; si no, se acerca. " +
                "Mismo árbol que ED_RangedEnemy.",
                so => Fill(so, 50, 10, 5, 5, AttackPatternKind.DiamondArea, AttackTiming.Instant,
                    atk => EnergyLoop(IfTargetInRange(5, Sequence(AttackRanged(), Kite(3, 3)), Chase(3, 3))))),

            new EnemyTemplate("sniper", "Sniper", EnemyArchetype.Ranged,
                "A ≤8 telegrafía la fila del jugador (se cobra al turno siguiente, no lo sigue); si no, busca " +
                "distancia 5. TBD: línea de visión (no existe chequeo de LoS).",
                so => Fill(so, 45, 25, 4, 8, AttackPatternKind.StraightLine, AttackTiming.Telegraph,
                    atk => Sequence(ExecuteTelegraph(), EnergyLoop(
                        IfTargetInRange(8, Telegraph(ThreatShape.Row, 1, atk), Kite(2, 5)))))),

            new EnemyTemplate("artillery", "Artillery", EnemyArchetype.Ranged,
                "Casi no se mueve (SPD 1); a ≤6 telegrafía un 3×3 sobre el jugador y lo cobra al turno siguiente.",
                so => Fill(so, 70, 25, 1, 6, AttackPatternKind.DiamondArea, AttackTiming.Telegraph,
                    atk => Sequence(ExecuteTelegraph(), EnergyLoop(
                        IfTargetInRange(6, Telegraph(ThreatShape.SquareAroundPlayer, 1, atk), Wait()))))),

            new EnemyTemplate("mago", "Mago", EnemyArchetype.Ranged,
                "A ≤5 telegrafía un 3×3 sobre el jugador y al turno siguiente lo prende con Fuego Temporal " +
                "(2 rondas); si no, mantiene distancia 4. Requiere el asset " + FireTileName + ".",
                so => Fill(so, 45, 10, 4, 5, AttackPatternKind.PersistentZone, AttackTiming.Telegraph,
                    atk => Sequence(Ignite(FindTile(FireTileName), 2), EnergyLoop(
                        IfTargetInRange(5, Telegraph(ThreatShape.SquareAroundPlayer, 1, atk), Kite(3, 4)))))),

            new EnemyTemplate("healer", "Healer", EnemyArchetype.Support,
                "Si hay un aliado herido, cura al de menos vida (HealStrength) a ≤2 o se le acerca; si no hay " +
                "a quién curar, dispara y mantiene distancia 3. Mismo árbol que ED_Healer.",
                so => Fill(so, 60, 10, 4, 2, AttackPatternKind.Unspecified, AttackTiming.Instant,
                    atk => EnergyLoop(IfAllyBelowMax(
                        IfTargetInRange(2, HealAlly(), MoveToAlly(3, 2), selector: LowestHpAlly()),
                        Sequence(AttackRanged(), Kite(3, 3)))))),

            new EnemyTemplate("guardian", "Guardian", EnemyArchetype.Support,
                "Golpea a distancia 1; si no llega, se pega al aliado más cercano. TBD: el aura de +defensa " +
                "a 2 casillas no existe como efecto; el validador avisa 'Support sin cura/buff' hasta que se agregue.",
                so => Fill(so, 110, 15, 3, 1, AttackPatternKind.ContactAdjacent, AttackTiming.Instant,
                    atk => EnergyLoop(IfTargetInRange(1, AttackMelee(), MoveToAlly(3, 1))))),
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
