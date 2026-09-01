using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Tiles;
using Rollgeon.UI.HUD.Status;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// La regla de una tarjeta de intención. Sin escena: es una función pura.
    /// </summary>
    /// <remarks>
    /// Los asserts miran los NÚMEROS y no las palabras: el locale de EditMode sale de un
    /// PlayerPref y comparar contra el castellano pondría esto en rojo con el editor en inglés
    /// (mismo criterio que <see cref="StatusTooltipTextTests"/>). Lo que se fija acá es qué dato
    /// llega a la frase, no cómo se traduce.
    /// </remarks>
    [TestFixture]
    public class AIIntentTextTests
    {
        private readonly List<SpecialTileDefinitionSO> _created = new List<SpecialTileDefinitionSO>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _created.Clear();
        }

        [Test]
        public void LaSiembra_DiceCuantasBombasSon()
        {
            var intent = new AIIntent(AIIntentTextKeys.BombField, "Siembra bombas",
                                      damage: 0, kind: AttackKind.Environmental, amount: 3);

            string rule = AIIntentText.Describe(intent);

            StringAssert.Contains("3", rule,
                "La tarjeta dice que siembra bombas y no cuántas. Tres bombas y una bomba se " +
                "juegan distinto, y el número es lo único que el jugador puede usar para decidir.");
        }

        [Test]
        public void ElDisparo_EsUnaTarjetaDeSoloTitulo()
        {
            var intent = new AIIntent(AIIntentTextKeys.RangedShot, "Te dispara",
                                      damage: 24, kind: AttackKind.BasicAttack);

            string rule = AIIntentText.Describe(intent);

            // El número no desapareció: se mudó al costado del título de la tarjeta, que es dónde
            // un rebalanceo lo cambia sin retraducir nada (ver TooltipCardViewDamageTests).
            Assert.IsEmpty(rule,
                "\"Disparo\" más su número ya lo dicen entero, y \"desde lejos\" lo dice la " +
                "familia del bicho arriba del panel. No quedaba nada para una frase.");
        }

        [Test]
        public void ElEstallido_EsUnaTarjetaDeSoloTitulo()
        {
            var intent = new AIIntent(AIIntentTextKeys.BombBlast, "Detonar la bomba",
                                      damage: 0, kind: AttackKind.Environmental,
                                      leaves: Fire(enter: 15, turnStart: 15), leavesRounds: 4);

            string rule = AIIntentText.Describe(intent);

            Assert.IsEmpty(rule,
                "\"Detonar la bomba\" ya dice todo lo que pasa, y el badge dice cuánto falta. " +
                "Una intención con la frase vacía tampoco arrastra la de lo que deja: si la " +
                "arrastrara, vaciar la entry en la tabla no serviría para pedir esta tarjeta.");
        }

        [Test]
        public void DosFuegosDistintos_CadaUnoConSusNumeros()
        {
            var bombFire = Fire(enter: 15, turnStart: 15);
            var coneFire = Fire(enter: 6, turnStart: 10);

            // BombField y no Ignite: la bola de fuego es tarjeta de sólo título, así que su
            // regla vacía tampoco arrastra lo que deja y acá no habría nada que comparar.
            string bomb = AIIntentText.Describe(new AIIntent(
                AIIntentTextKeys.BombField, "Bombas", 12, AttackKind.Environmental,
                leaves: bombFire, leavesRounds: 4));
            string cone = AIIntentText.Describe(new AIIntent(
                AIIntentTextKeys.BombField, "Bombas", 12, AttackKind.Environmental,
                leaves: coneFire, leavesRounds: 3));

            // Los dos fuegos comparten SpecialTileType y cobran distinto: los números tienen que
            // salir de la definición de CADA uno, no de la key compartida.
            StringAssert.Contains("15", bomb);
            StringAssert.Contains("6", cone);
            StringAssert.DoesNotContain("15", cone,
                "La banda del cono se describió con los números del fuego de las bombas.");
        }

        [Test]
        public void SinNadaQueDejar_NoArrastraLaFraseDeLoQueDeja()
        {
            // Con BombField y no con el disparo ni la bola de fuego: esos son tarjetas de sólo
            // título, así que los dos lados quedarían vacíos y el test pasaría sin mirar nada.
            var conLeaves = AIIntentText.Describe(new AIIntent(
                AIIntentTextKeys.BombField, "Bombas", 30, AttackKind.Environmental,
                leaves: Fire(enter: 7, turnStart: 9), leavesRounds: 4));
            var sinLeaves = AIIntentText.Describe(new AIIntent(
                AIIntentTextKeys.BombField, "Bombas", 30, AttackKind.Environmental));

            Assert.Less(sinLeaves.Length, conLeaves.Length,
                "Una intención que no deja nada en el piso arrastró igual la frase de lo que " +
                "deja, con los ceros de una definición que no existe.");
            StringAssert.DoesNotContain("7", sinLeaves);
            StringAssert.DoesNotContain("9", sinLeaves);
        }

        [Test]
        public void UnaKeyVacia_NoRompeYNoInventa()
        {
            string rule = AIIntentText.Describe(new AIIntent(
                null, "Algo", 5, AttackKind.BasicAttack));

            Assert.IsEmpty(rule,
                "Una intención sin key devolvió texto. El fallback de una key que no existe es " +
                "no decir nada, nunca un renglón inventado adentro de un tooltip.");
        }

        private SpecialTileDefinitionSO Fire(int enter, int turnStart)
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            def.TileType = SpecialTileType.Fire;
            def.EnterDamage = enter;
            def.TurnStartDamage = turnStart;
            _created.Add(def);
            return def;
        }
    }
}
