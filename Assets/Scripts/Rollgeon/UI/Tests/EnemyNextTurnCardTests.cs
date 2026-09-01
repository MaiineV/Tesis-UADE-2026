using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities.Traits;
using Rollgeon.UI.HUD.Status;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Cómo se titula la tarjeta de próximo turno. El nodo genérico del bestiario rotula todo
    /// igual porque describe un efecto de daño y no sabe de qué bicho cuelga; la familia la sabe
    /// el panel, que ya la imprime dos renglones más arriba.
    /// </summary>
    [TestFixture]
    public sealed class EnemyNextTurnCardTests
    {
        private readonly List<AIIntent> _next = new();
        private readonly List<AIIntent> _standing = new();
        private readonly List<StatusIconState> _cards = new();
        private Guid _owner;

        [SetUp]
        public void SetUp()
        {
            _owner = Guid.NewGuid();
            _next.Clear();
            _standing.Clear();
            _cards.Clear();
        }

        /// <summary>
        /// Sobre ids y no sobre texto: las keys sembradas resuelven en el idioma del editor, y el
        /// título compuesto le suma el tipo de ataque. Lo que se afirma es de QUÉ key sale.
        /// </summary>
        private static string TitleOf(StatusIconState card) => card.DisplayName;

        private static AIIntent GenericAttack(int damage = 10) =>
            new AIIntent(AIIntentTextKeys.Attack, "Golpe", damage, AttackKind.BasicAttack);

        private string Collect(EnemyArchetype family)
        {
            _cards.Clear();
            EnemyStatusIconsView.AppendNextTurnCard(_next, _standing, _owner, null, _cards, family);
            Assert.AreEqual(1, _cards.Count, "El bloque de próximo turno tiene que salir.");
            return TitleOf(_cards[0]);
        }

        [Test]
        public void UnTiradorNoGolpea_SuTarjetaSeLlamaComoSuDisparo()
        {
            _next.Add(GenericAttack());

            string ranged = Collect(EnemyArchetype.Ranged);
            string melee = Collect(EnemyArchetype.Melee);

            Assert.AreNotEqual(melee, ranged,
                "El ranged común pega desde cinco casillas y su tarjeta decía la misma palabra que " +
                "la del que corre a trompearte.");
            StringAssert.Contains(
                Rollgeon.Localization.LocalizedContent.Name(AIIntentTextKeys.RangedShot,
                                                            AIIntentTextKeys.RangedShotFallback),
                ranged,
                "El título del tirador tiene que salir de la key del disparo.");
        }

        [Test]
        public void SinFamiliaAutorada_ElTituloEsElDelNodo()
        {
            _next.Add(GenericAttack());

            Assert.AreEqual(Collect(EnemyArchetype.Melee), Collect(EnemyArchetype.Unset),
                "Un bicho sin familia no se renombra: el default no puede inventar una lectura.");
        }

        /// <summary>
        /// El caso que protege la regla: un título autorado es una decisión de autoría y la familia
        /// no la conoce. El Cajero es ranged en su ficha y su mandoble no es un disparo.
        /// </summary>
        [Test]
        public void UnTituloAutorado_NoLoPisaLaFamilia()
        {
            _next.Add(new AIIntent(AIIntentTextKeys.BurnRoom, "Pleno y color", 7,
                                   AttackKind.ScriptedAbility));

            string ranged = Collect(EnemyArchetype.Ranged);

            StringAssert.Contains(
                Rollgeon.Localization.LocalizedContent.Name(AIIntentTextKeys.BurnRoom,
                                                            "Pleno y color"),
                ranged,
                "La familia le pisó un título que alguien autoró para ese nodo.");
        }

        /// <summary>El bloque se arma con lo que mantiene en el paño cuando no hay próximo turno, y
        /// la regla vale igual: es la misma tarjeta.</summary>
        [Test]
        public void SinProximoTurno_LaReglaValeParaLoQueMantiene()
        {
            _standing.Add(GenericAttack(8));

            string ranged = Collect(EnemyArchetype.Ranged);

            Assert.AreNotEqual(Collect(EnemyArchetype.Melee), ranged);
        }
    }
}
