using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Items;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Editor.Tools.Item.Tests
{
    /// <summary>
    /// Fija el contrato de <see cref="ItemTriggerCatalog"/>: la lista es la única fuente de
    /// disparadores que se le ofrecen al diseñador, así que una entrada mal armada le devuelve el
    /// problema que el catálogo vino a sacar — un ítem que nunca dispara y nadie sabe por qué.
    /// </summary>
    public sealed class ItemTriggerCatalogTests
    {
        [Test]
        public void All_IsNotEmpty()
        {
            CollectionAssert.IsNotEmpty(ItemTriggerCatalog.All);
        }

        [Test]
        public void All_IdsAreUnique()
        {
            var dupes = ItemTriggerCatalog.All
                .GroupBy(o => o.Id, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            CollectionAssert.IsEmpty(dupes, "los Id son la clave estable del popup");
        }

        [Test]
        public void All_DisplayNamesAreUnique()
        {
            var dupes = ItemTriggerCatalog.All
                .GroupBy(o => o.DisplayName, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            CollectionAssert.IsEmpty(dupes,
                "dos entradas con el mismo nombre son indistinguibles en el desplegable");
        }

        [Test]
        public void All_HaveDisplayNameAndHelp()
        {
            foreach (var option in ItemTriggerCatalog.All)
            {
                Assert.IsNotEmpty(option.DisplayName, $"'{option.Id}' sin nombre.");
                Assert.IsNotEmpty(option.Help, $"'{option.Id}' sin ayuda — es la línea que evita la consulta.");
            }
        }

        /// <summary>
        /// El enum se serializa por int y borrar un miembro del medio ya corrió los hooks tres
        /// veces en silencio (ver <c>ClassPassiveHookAuditTests</c>). Si alguien saca un
        /// <see cref="EventName"/> que el catálogo ofrece, esto lo frena acá y no en un playtest.
        /// </summary>
        [Test]
        public void BusOptions_PointToAnEventThatStillExists()
        {
            foreach (var option in ItemTriggerCatalog.All)
            {
                if (option.Kind != PassiveHookKind.EventBus) continue;

                Assert.IsTrue(Enum.IsDefined(typeof(EventName), option.Event),
                    $"'{option.Id}' apunta a un EventName que ya no existe.");
            }
        }

        /// <summary>
        /// Dos entradas que se traducen al mismo estado del hook son un bug: el popup mostraría dos
        /// opciones y <see cref="ItemTriggerCatalog.Match"/> devolvería siempre la primera, así que
        /// elegir la segunda se vería como que no se guardó.
        /// </summary>
        [Test]
        public void Options_MapToDistinctHookStates()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var option in ItemTriggerCatalog.All)
            {
                var key = option.Kind == PassiveHookKind.ComboPlayed
                    ? $"combo:{option.UsesComboIds}"
                    : $"bus:{option.Event}:{option.Subject}";

                Assert.IsTrue(seen.Add(key), $"'{option.Id}' colisiona con otra entrada en '{key}'.");
            }
        }

        [Test]
        public void Apply_ThenMatch_ReturnsTheSameOption()
        {
            foreach (var option in ItemTriggerCatalog.All)
            {
                var hook = new PassiveItemHook();
                ItemTriggerCatalog.Apply(hook, option);

                var matched = ItemTriggerCatalog.Match(hook);

                Assert.IsTrue(matched.HasValue, $"'{option.Id}' no se reconoce después de aplicarse.");
                Assert.AreEqual(option.Id, matched.Value.Id);
            }
        }

        [Test]
        public void Match_EventOutsideTheCatalog_IsNull()
        {
            var hook = new PassiveItemHook
            {
                Kind = PassiveHookKind.EventBus,
                TriggerEvent = EventName.OnSceneLoaded,
            };

            Assert.IsNull(ItemTriggerCatalog.Match(hook),
                "OnSceneLoaded no lo escucha ningún ítem — tiene que caer en Problemas");
        }

        /// <summary>
        /// La trampa que motivó todo: mismo evento, sujeto distinto, significados opuestos.
        /// </summary>
        [Test]
        public void Match_SameEventDifferentSubject_AreDifferentOptions()
        {
            var pegas = new PassiveItemHook
            {
                TriggerEvent = EventName.OnDamageIncoming,
                Subject = PassiveHookSubject.Source,
            };
            var tePegan = new PassiveItemHook
            {
                TriggerEvent = EventName.OnDamageIncoming,
                Subject = PassiveHookSubject.Target,
            };

            var a = ItemTriggerCatalog.Match(pegas);
            var b = ItemTriggerCatalog.Match(tePegan);

            Assert.IsTrue(a.HasValue && b.HasValue);
            Assert.AreNotEqual(a.Value.Id, b.Value.Id);
            Assert.AreEqual("damage.taken", b.Value.Id);
        }

        [Test]
        public void Describe_ComboWithIds_ListsThem()
        {
            var hook = new PassiveItemHook { Kind = PassiveHookKind.ComboPlayed };
            hook.ComboFilter.Mode = ComboFilterMode.ComboIds;
            hook.ComboFilter.ComboIds = new List<string> { "combo.pair", "combo.trio" };

            StringAssert.Contains("combo.pair", ItemTriggerCatalog.Describe(hook));
            StringAssert.Contains("combo.trio", ItemTriggerCatalog.Describe(hook));
        }

        [Test]
        public void Describe_ComboRestrictedToAttacks_SaysSo()
        {
            var hook = new PassiveItemHook
            {
                Kind = PassiveHookKind.ComboPlayed,
                ActionKindFilter = Rollgeon.Combat.Rolls.RollActionKind.Attack,
            };

            StringAssert.Contains("ataques", ItemTriggerCatalog.Describe(hook));
        }

        [Test]
        public void Describe_UnknownTrigger_SaysItIsUnknown()
        {
            var hook = new PassiveItemHook { TriggerEvent = EventName.OnSceneLoaded };

            StringAssert.Contains("desconocido", ItemTriggerCatalog.Describe(hook));
        }

        [Test]
        public void Describe_Null_IsEmpty()
        {
            Assert.AreEqual(string.Empty, ItemTriggerCatalog.Describe(null));
        }
    }
}
