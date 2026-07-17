using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Dice.Tests
{
    /// <summary>
    /// Cubre <see cref="DiceShapeCatalogSO"/>: lookup por tipo y por rol, la cadena de
    /// degradación (rol → frontal → fallback), los rechazos de
    /// <see cref="DiceShapeCatalogSO.Validate"/> (vacío, duplicado, sprite null, tipo faltante),
    /// los de <see cref="DiceShapeCatalogSO.ValidateRoles"/> (set incompleto) y
    /// <see cref="DiceShapeCatalogSO.Resolve"/>.
    /// </summary>
    [TestFixture]
    public class DiceShapeCatalogSOTests
    {
        private readonly List<UnityEngine.Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        // Sprite.Create + new Texture2D filtran entre tests EditMode: ambos van al teardown.
        private Sprite MakeSprite()
        {
            var texture = new Texture2D(2, 2);
            _created.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            _created.Add(sprite);
            return sprite;
        }

        private DiceShapeCatalogSO MakeCatalog(params DiceShapeEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<DiceShapeCatalogSO>();
            catalog.name = "TestShapeCatalog";
            catalog.Shapes = new List<DiceShapeEntry>(entries);
            _created.Add(catalog);
            return catalog;
        }

        /// <summary>Entrada con SOLO el frontal — el resto de los roles quedan sin arte.</summary>
        private DiceShapeEntry Entry(DiceType type, Sprite shape) =>
            new DiceShapeEntry { Type = type, Front = shape };

        /// <summary>Entrada con los 5 roles en sprites distintos, para distinguirlos al asertar.</summary>
        private DiceShapeEntry FullEntry(DiceType type) =>
            new DiceShapeEntry
            {
                Type = type,
                Front = MakeSprite(),
                SideA = MakeSprite(),
                SideB = MakeSprite(),
                Hover = MakeSprite(),
                Selected = MakeSprite(),
            };

        /// <summary>Catálogo válido: una entrada con frontal real por cada <see cref="DiceType"/>.</summary>
        private DiceShapeCatalogSO MakeFullCatalog()
        {
            var entries = new List<DiceShapeEntry>();
            foreach (DiceType type in Enum.GetValues(typeof(DiceType)))
                entries.Add(Entry(type, MakeSprite()));
            return MakeCatalog(entries.ToArray());
        }

        /// <summary>Catálogo con los 5 roles autorados en cada <see cref="DiceType"/>.</summary>
        private DiceShapeCatalogSO MakeFullRoleCatalog()
        {
            var entries = new List<DiceShapeEntry>();
            foreach (DiceType type in Enum.GetValues(typeof(DiceType)))
                entries.Add(FullEntry(type));
            return MakeCatalog(entries.ToArray());
        }

        // -------------------------------------------------------------------
        // GetShape
        // -------------------------------------------------------------------

        [Test]
        public void GetShape_ReturnsSpriteForItsType()
        {
            // Arrange
            var d4 = MakeSprite();
            var d20 = MakeSprite();
            var catalog = MakeCatalog(Entry(DiceType.D4, d4), Entry(DiceType.D20, d20));

            // Act
            var result = catalog.GetShape(DiceType.D20);

            // Assert
            Assert.AreSame(d20, result);
        }

        [Test]
        public void GetShape_ReturnsFallback_WhenTypeHasNoEntry()
        {
            // Arrange
            var fallback = MakeSprite();
            var catalog = MakeCatalog(Entry(DiceType.D4, MakeSprite()));
            catalog.FallbackShape = fallback;

            // Act
            var result = catalog.GetShape(DiceType.D12);

            // Assert
            Assert.AreSame(fallback, result);
        }

        [Test]
        public void GetShape_ReturnsFallback_WhenEntryHasNullSprite()
        {
            // Arrange
            var fallback = MakeSprite();
            var catalog = MakeCatalog(Entry(DiceType.D6, null));
            catalog.FallbackShape = fallback;

            // Act
            var result = catalog.GetShape(DiceType.D6);

            // Assert
            Assert.AreSame(fallback, result);
        }

        [Test]
        public void GetShape_ReturnsNull_WhenNoEntryAndNoFallback()
        {
            // Arrange — sin sprite, Image degrada al quad de color plano: es un caller-safe null.
            var catalog = MakeCatalog(Entry(DiceType.D4, MakeSprite()));

            // Act
            var result = catalog.GetShape(DiceType.D12);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void GetShape_ReturnsFirstMatch_WhenTypeIsDuplicated()
        {
            // Arrange
            var first = MakeSprite();
            var catalog = MakeCatalog(Entry(DiceType.D6, first), Entry(DiceType.D6, MakeSprite()));

            // Act
            var result = catalog.GetShape(DiceType.D6);

            // Assert
            Assert.AreSame(first, result);
        }

        // -------------------------------------------------------------------
        // GetShape — roles
        // -------------------------------------------------------------------

        [Test]
        public void GetShape_ReturnsSpriteOfTheRequestedRole()
        {
            // Arrange
            var entry = FullEntry(DiceType.D6);
            var catalog = MakeCatalog(entry);

            // Act & Assert — cada rol devuelve SU sprite, no el de al lado.
            Assert.AreSame(entry.Front, catalog.GetShape(DiceType.D6, DiceShapeRole.Front));
            Assert.AreSame(entry.SideA, catalog.GetShape(DiceType.D6, DiceShapeRole.SideA));
            Assert.AreSame(entry.SideB, catalog.GetShape(DiceType.D6, DiceShapeRole.SideB));
            Assert.AreSame(entry.Hover, catalog.GetShape(DiceType.D6, DiceShapeRole.Hover));
            Assert.AreSame(entry.Selected, catalog.GetShape(DiceType.D6, DiceShapeRole.Selected));
        }

        [Test]
        public void GetShape_DefaultsToFront_WhenRoleIsNotGiven()
        {
            // Arrange
            var entry = FullEntry(DiceType.D8);
            var catalog = MakeCatalog(entry);

            // Act — el call-site que no conoce roles (DiceThrowDieView).
            var result = catalog.GetShape(DiceType.D8);

            // Assert
            Assert.AreSame(entry.Front, result);
        }

        [Test]
        public void GetShape_FallsBackToFront_WhenRoleHasNoSprite()
        {
            // Arrange — catálogo autorado antes de que existieran los roles.
            var front = MakeSprite();
            var catalog = MakeCatalog(Entry(DiceType.D6, front));

            // Act
            var result = catalog.GetShape(DiceType.D6, DiceShapeRole.Hover);

            // Assert — degrada al frontal, no al cuadrado plano.
            Assert.AreSame(front, result);
        }

        [Test]
        public void GetShape_FallsBackToFallbackShape_WhenNeitherRoleNorFrontExist()
        {
            // Arrange
            var fallback = MakeSprite();
            var catalog = MakeCatalog(Entry(DiceType.D6, null));
            catalog.FallbackShape = fallback;

            // Act
            var result = catalog.GetShape(DiceType.D6, DiceShapeRole.SideA);

            // Assert
            Assert.AreSame(fallback, result);
        }

        // -------------------------------------------------------------------
        // Validate
        // -------------------------------------------------------------------

        [Test]
        public void Validate_AcceptsCatalogCoveringEveryDiceType()
        {
            // Arrange
            var catalog = MakeFullCatalog();

            // Act
            bool valid = catalog.Validate(out var error);

            // Assert
            Assert.IsTrue(valid, "Expected valid; error='{0}'", error);
            Assert.IsNull(error);
        }

        [Test]
        public void Validate_RejectsEmptyCatalog()
        {
            // Arrange
            var catalog = MakeCatalog();

            // Act
            bool valid = catalog.Validate(out var error);

            // Assert
            Assert.IsFalse(valid);
            Assert.IsNotNull(error);
        }

        [Test]
        public void Validate_RejectsDuplicatedDiceType()
        {
            // Arrange
            var catalog = MakeFullCatalog();
            catalog.Shapes.Add(Entry(DiceType.D6, MakeSprite()));

            // Act
            bool valid = catalog.Validate(out var error);

            // Assert
            Assert.IsFalse(valid);
            StringAssert.Contains("D6", error);
        }

        [Test]
        public void Validate_RejectsEntryWithNullSprite()
        {
            // Arrange
            var catalog = MakeFullCatalog();
            catalog.Shapes[0] = Entry(catalog.Shapes[0].Type, null);

            // Act
            bool valid = catalog.Validate(out var error);

            // Assert
            Assert.IsFalse(valid);
            Assert.IsNotNull(error);
        }

        /// <summary>
        /// El caso que se gana el sueldo: DiceType.cs documenta que los tipos nuevos se agregan
        /// al final. Sin este chequeo, sumar un tipo shippearía un dado sin forma.
        /// </summary>
        [Test]
        public void Validate_RejectsCatalogMissingADiceType()
        {
            // Arrange
            var catalog = MakeFullCatalog();
            var dropped = catalog.Shapes[catalog.Shapes.Count - 1].Type;
            catalog.Shapes.RemoveAt(catalog.Shapes.Count - 1);

            // Act
            bool valid = catalog.Validate(out var error);

            // Assert
            Assert.IsFalse(valid);
            StringAssert.Contains(dropped.ToString(), error);
        }

        // -------------------------------------------------------------------
        // ValidateRoles
        // -------------------------------------------------------------------

        [Test]
        public void ValidateRoles_AcceptsCatalogWithEveryRoleAuthored()
        {
            // Arrange
            var catalog = MakeFullRoleCatalog();

            // Act
            bool valid = catalog.ValidateRoles(out var error);

            // Assert
            Assert.IsTrue(valid, "Expected valid; error='{0}'", error);
            Assert.IsNull(error);
        }

        [Test]
        public void ValidateRoles_RejectsEntryMissingASideSprite()
        {
            // Arrange — el caso real: se autoró el frontal y se olvidó el resto del set.
            var catalog = MakeFullRoleCatalog();
            var entry = catalog.Shapes[0];
            entry.SideB = null;
            catalog.Shapes[0] = entry;

            // Act
            bool valid = catalog.ValidateRoles(out var error);

            // Assert
            Assert.IsFalse(valid);
            StringAssert.Contains(nameof(DiceShapeRole.SideB), error);
        }

        /// <summary>
        /// ValidateRoles delega en Validate primero: un tipo sin entrada tiene que reportarse
        /// como tipo faltante y no como "sin sprite para el rol Front", que mandaría a buscar
        /// el bug al lado equivocado.
        /// </summary>
        [Test]
        public void ValidateRoles_ReportsMissingType_BeforeMissingRoles()
        {
            // Arrange
            var catalog = MakeFullRoleCatalog();
            var dropped = catalog.Shapes[catalog.Shapes.Count - 1].Type;
            catalog.Shapes.RemoveAt(catalog.Shapes.Count - 1);

            // Act
            bool valid = catalog.ValidateRoles(out var error);

            // Assert
            Assert.IsFalse(valid);
            StringAssert.Contains(dropped.ToString(), error);
        }

        // -------------------------------------------------------------------
        // Resolve
        // -------------------------------------------------------------------

        [Test]
        public void Resolve_ReturnsAuthored_WhenNotNull()
        {
            // Arrange
            var authored = MakeFullCatalog();

            // Act
            var result = DiceShapeCatalogSO.Resolve(authored);

            // Assert
            Assert.AreSame(authored, result);
        }

        [Test]
        public void Resolve_FallsBackToResources_WhenAuthoredIsNull()
        {
            // Arrange
            var shipped = Resources.Load<DiceShapeCatalogSO>(DiceShapeCatalogSO.ResourcePath);

            // Act
            var result = DiceShapeCatalogSO.Resolve(null);

            // Assert — sin asset shippeado, Resolve devuelve null y el caller degrada.
            Assert.AreSame(shipped, result);
        }

        // -------------------------------------------------------------------
        // Asset real
        // -------------------------------------------------------------------

        /// <summary>
        /// Valida el asset que se shippea, no un fixture. NO va a <c>_created</c>:
        /// <c>DestroyImmediate</c> sobre un asset real borra el archivo del disco.
        /// </summary>
        [Test]
        public void EveryDiceType_HasAnEntry_InShippedResourcesCatalog()
        {
            // Arrange
            var shipped = Resources.Load<DiceShapeCatalogSO>(DiceShapeCatalogSO.ResourcePath);
            if (shipped == null)
                Assert.Ignore($"No hay catálogo en Resources/{DiceShapeCatalogSO.ResourcePath} todavía.");

            // Act
            bool valid = shipped.Validate(out var error);

            // Assert
            Assert.IsTrue(valid, "Catálogo shippeado inválido: {0}", error);
        }

        /// <summary>
        /// La red que avisa cuando llega un dado nuevo y solo se autoró su frontal: sin el set
        /// completo el dado se vería quieto durante el giro y sin feedback de hover/held, que es
        /// un bug silencioso. Se arregla corriendo
        /// <c>Tools/Rollgeon/Dice/Author Shape Catalog From Sheet</c>.
        /// </summary>
        [Test]
        public void EveryDiceType_HasEveryRole_InShippedResourcesCatalog()
        {
            // Arrange
            var shipped = Resources.Load<DiceShapeCatalogSO>(DiceShapeCatalogSO.ResourcePath);
            if (shipped == null)
                Assert.Ignore($"No hay catálogo en Resources/{DiceShapeCatalogSO.ResourcePath} todavía.");

            // Act
            bool valid = shipped.ValidateRoles(out var error);

            // Assert
            Assert.IsTrue(valid, "Catálogo shippeado sin el set completo: {0}", error);
        }
    }
}
