using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Combat.Actions;
using UnityEngine;

namespace Rollgeon.Combat.Actions.Tests
{
    /// <summary>
    /// EditMode tests del <see cref="ActionCatalogSO"/>. Cubre el contrato del §4.3 del plan:
    /// <list type="bullet">
    ///   <item><c>AllIds</c> (heredado del <c>BaseCatalogSO&lt;T&gt;</c>).</item>
    ///   <item><c>GetById</c> (heredado).</item>
    ///   <item><c>GetIdsByType(ActionType)</c> filtro por tipo.</item>
    ///   <item><c>GetBackingAsset&lt;T&gt;</c> cast seguro + null-safe.</item>
    /// </list>
    /// </summary>
    [TestFixture]
    public class ActionCatalogSOTests
    {
        private ActionCatalogSO _catalog;
        private List<ActionDefinitionSO> _created;
        private List<ScriptableObject> _createdBackings;

        [SetUp]
        public void Setup()
        {
            _catalog = ScriptableObject.CreateInstance<ActionCatalogSO>();
            _created = new List<ActionDefinitionSO>();
            _createdBackings = new List<ScriptableObject>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var def in _created)
            {
                if (def != null) Object.DestroyImmediate(def);
            }
            foreach (var so in _createdBackings)
            {
                if (so != null) Object.DestroyImmediate(so);
            }
            if (_catalog != null)
            {
                Object.DestroyImmediate(_catalog);
                _catalog = null;
            }
        }

        // --- Helpers -----------------------------------------------------

        private ActionDefinitionSO MakeAction(string id, ActionType type, int energyCost = 0,
                                              bool blockOnRepeat = true, ScriptableObject backing = null)
        {
            var def = ScriptableObject.CreateInstance<ActionDefinitionSO>();
            def.ActionId = id;
            def.Type = type;
            def.EnergyCost = energyCost;
            def.BlockOnRepeat = blockOnRepeat;
            def.BackingAsset = backing;
            _created.Add(def);
            return def;
        }

        /// <summary>
        /// Inyecta entries al catalog via reflection — el campo <c>_entries</c> de
        /// <c>BaseCatalogSO&lt;T&gt;</c> es protected, pero no hay API publica para
        /// setterlo desde runtime. Patron equivalente al que usan los tests del
        /// ComboCatalog.
        /// </summary>
        private void SetEntries(params ActionDefinitionSO[] entries)
        {
            var field = typeof(Rollgeon.Patterns.Catalogs.BaseCatalogSO<ActionDefinitionSO>)
                .GetField("_entries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(field, "Campo _entries no encontrado en BaseCatalogSO<T>.");
            field.SetValue(_catalog, new List<ActionDefinitionSO>(entries));
        }

        // --- Tests -------------------------------------------------------

        [Test]
        public void AllIds_AggregatesEntriesInOrder()
        {
            var a = MakeAction("move", ActionType.Move);
            var b = MakeAction("attack.basic", ActionType.Attack);
            var c = MakeAction("skill.heal", ActionType.SkillCheck);
            SetEntries(a, b, c);

            var ids = _catalog.AllIds.ToList();

            CollectionAssert.AreEqual(new[] { "move", "attack.basic", "skill.heal" }, ids);
        }

        [Test]
        public void AllIds_SkipsNullEntries()
        {
            var a = MakeAction("attack.basic", ActionType.Attack);
            SetEntries(a, null);

            var ids = _catalog.AllIds.ToList();

            CollectionAssert.AreEqual(new[] { "attack.basic" }, ids);
        }

        [Test]
        public void GetById_Exists_ReturnsEntry()
        {
            var a = MakeAction("attack.basic", ActionType.Attack, energyCost: 1);
            SetEntries(a);

            var found = _catalog.GetById("attack.basic");

            Assert.AreSame(a, found);
            Assert.AreEqual(1, found.EnergyCost);
        }

        [Test]
        public void GetById_Missing_ReturnsNull()
        {
            SetEntries(MakeAction("attack.basic", ActionType.Attack));

            var found = _catalog.GetById("does.not.exist");

            Assert.IsNull(found);
        }

        [Test]
        public void GetById_NullOrEmpty_ReturnsNull()
        {
            SetEntries(MakeAction("attack.basic", ActionType.Attack));

            Assert.IsNull(_catalog.GetById(null));
            Assert.IsNull(_catalog.GetById(string.Empty));
        }

        [Test]
        public void Contains_TrueOnlyForPresentIds()
        {
            SetEntries(MakeAction("move", ActionType.Move));

            Assert.IsTrue(_catalog.Contains("move"));
            Assert.IsFalse(_catalog.Contains("attack.basic"));
        }

        [Test]
        public void GetIdsByType_FiltersByTypeOnly()
        {
            SetEntries(
                MakeAction("move", ActionType.Move),
                MakeAction("attack.basic", ActionType.Attack),
                MakeAction("attack.special", ActionType.Attack),
                MakeAction("skill.heal", ActionType.SkillCheck)
            );

            var attacks = _catalog.GetIdsByType(ActionType.Attack).ToList();

            CollectionAssert.AreEquivalent(new[] { "attack.basic", "attack.special" }, attacks);
        }

        [Test]
        public void GetIdsByType_NoMatch_ReturnsEmpty()
        {
            SetEntries(MakeAction("move", ActionType.Move));

            var combos = _catalog.GetIdsByType(ActionType.Combo).ToList();

            Assert.AreEqual(0, combos.Count);
        }

        [Test]
        public void GetIdsByType_SkipsNullEntries()
        {
            SetEntries(MakeAction("attack.basic", ActionType.Attack), null);

            var attacks = _catalog.GetIdsByType(ActionType.Attack).ToList();

            CollectionAssert.AreEqual(new[] { "attack.basic" }, attacks);
        }

        [Test]
        public void GetBackingAsset_CorrectType_Returns()
        {
            var backing = ScriptableObject.CreateInstance<ActionCatalogSO>(); // cualquier SO concreto sirve.
            _createdBackings.Add(backing);
            var def = MakeAction("combo.full_house", ActionType.Combo, backing: backing);
            SetEntries(def);

            var got = _catalog.GetBackingAsset<ActionCatalogSO>("combo.full_house");

            Assert.AreSame(backing, got);
        }

        [Test]
        public void GetBackingAsset_WrongType_ReturnsNull()
        {
            var backing = ScriptableObject.CreateInstance<ActionCatalogSO>();
            _createdBackings.Add(backing);
            var def = MakeAction("combo.full_house", ActionType.Combo, backing: backing);
            SetEntries(def);

            // Pedimos como ActionDefinitionSO — el cast falla y devuelve null.
            var got = _catalog.GetBackingAsset<ActionDefinitionSO>("combo.full_house");

            Assert.IsNull(got);
        }

        [Test]
        public void GetBackingAsset_NullBacking_ReturnsNull()
        {
            var def = MakeAction("attack.basic", ActionType.Attack, backing: null);
            SetEntries(def);

            var got = _catalog.GetBackingAsset<ScriptableObject>("attack.basic");

            Assert.IsNull(got);
        }

        [Test]
        public void GetBackingAsset_MissingId_ReturnsNull()
        {
            SetEntries(MakeAction("attack.basic", ActionType.Attack));

            var got = _catalog.GetBackingAsset<ScriptableObject>("does.not.exist");

            Assert.IsNull(got);
        }

        [Test]
        public void DuplicateIds_ValidatorDetects()
        {
            // Two entries con el mismo ActionId.
            SetEntries(
                MakeAction("attack.basic", ActionType.Attack),
                MakeAction("attack.basic", ActionType.Attack)
            );

            // AllIds sigue funcionando (no dedupea) — esta es la funcion observable
            // desde runtime. El validator de Odin se ejerce en inspector; aqui lo
            // verificamos invocandolo via reflection.
            var method = typeof(Rollgeon.Patterns.Catalogs.BaseCatalogSO<ActionDefinitionSO>)
                .GetMethod("ValidateNoDuplicateIds",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method, "ValidateNoDuplicateIds no encontrado en BaseCatalogSO<T>.");

            var entriesField = typeof(Rollgeon.Patterns.Catalogs.BaseCatalogSO<ActionDefinitionSO>)
                .GetField("_entries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var entries = entriesField.GetValue(_catalog);

            var ok = (bool)method.Invoke(_catalog, new object[] { entries });
            Assert.IsFalse(ok, "Validator debe detectar duplicate ids.");
        }
    }
}
