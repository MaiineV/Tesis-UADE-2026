using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    [TestFixture]
    public class DiceBoardSkinViewTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        // ───── Logo en el catalog (resolución + degradación) ──────────────────

        [Test]
        public void TryGet_ReturnsLogoSpriteAndTint_ForAuthoredType()
        {
            // Arrange
            var logo = MakeSprite();
            var catalog = MakeCatalog(
                Entry(DiceBoardType.Default),
                Entry(DiceBoardType.Attack, logo, Color.red));

            // Act
            bool found = catalog.TryGet(DiceBoardType.Attack, out var skin);

            // Assert
            Assert.IsTrue(found);
            Assert.AreEqual(logo, skin.LogoSprite);
            Assert.AreEqual(Color.red, skin.LogoTint);
        }

        [Test]
        public void TryGet_DegradesLogoToDefault_WhenTypeMissing()
        {
            // Arrange — solo Default autorado, con logo propio.
            var logo = MakeSprite();
            var catalog = MakeCatalog(Entry(DiceBoardType.Default, logo, Color.green));

            // Act
            bool found = catalog.TryGet(DiceBoardType.Defense, out var skin);

            // Assert — la degradación arrastra la entry entera, logo incluido.
            Assert.IsTrue(found);
            Assert.AreEqual(logo, skin.LogoSprite);
            Assert.AreEqual(Color.green, skin.LogoTint);
        }

        // ───── Aplicación del logo en el View ─────────────────────────────────

        [Test]
        public void ApplyBoardType_SetsLogoSpriteTintAndEnables_WhenLogoAuthored()
        {
            // Arrange
            var logo = MakeSprite();
            var view = MakeView(out _, out var logoImage,
                Entry(DiceBoardType.Attack, logo, Color.red));
            logoImage.enabled = false; // quedó escondido por un tipo anterior sin logo

            // Act
            view.ApplyBoardType(DiceBoardType.Attack);

            // Assert
            Assert.IsTrue(logoImage.enabled);
            Assert.AreEqual(logo, logoImage.sprite);
            Assert.AreEqual(Color.red, logoImage.color);
        }

        [Test]
        public void ApplyBoardType_DisablesLogo_WhenEntryHasNoLogoSprite()
        {
            // Arrange — entry con board sprite pero sin logo autorado.
            var entry = Entry(DiceBoardType.Attack);
            var view = MakeView(out var boardImage, out var logoImage, entry);

            // Act
            view.ApplyBoardType(DiceBoardType.Attack);

            // Assert — el logo se esconde; el board igual se aplica.
            Assert.IsFalse(logoImage.enabled);
            Assert.AreEqual(entry.Sprite, boardImage.sprite);
            Assert.AreEqual(entry.Tint, boardImage.color);
        }

        [Test]
        public void ApplyBoardType_LogoNullRef_DoesNotThrow()
        {
            // Arrange — View sin _logoImage wireado (degradación sin wiring).
            var entry = Entry(DiceBoardType.Attack);
            var view = MakeView(out var boardImage, out _, entry);
            SetPrivateField(view, "_logoImage", null);

            // Act + Assert
            Assert.DoesNotThrow(() => view.ApplyBoardType(DiceBoardType.Attack));
            Assert.AreEqual(entry.Sprite, boardImage.sprite);
        }

        // ───── Evento BoardTypeChanged (gate de transición) ───────────────────

        [Test]
        public void ApplyBoardType_DoesNotRaiseEvent_OnFirstApplication()
        {
            // Arrange
            var view = MakeView(out _, out _, Entry(DiceBoardType.Default));
            int raised = 0;
            view.BoardTypeChanged += _ => raised++;

            // Act — la primera aplicación es estado inicial, no transición.
            view.ApplyBoardType(DiceBoardType.Default);

            // Assert
            Assert.AreEqual(0, raised);
        }

        [Test]
        public void ApplyBoardType_RaisesEventOncePerTypeChange_AndSkipsReapplications()
        {
            // Arrange
            var view = MakeView(out _, out _,
                Entry(DiceBoardType.Default),
                Entry(DiceBoardType.Attack),
                Entry(DiceBoardType.Defense));
            var raised = new List<DiceBoardType>();
            view.BoardTypeChanged += t => raised.Add(t);

            // Act — re-aplicaciones (OnPhaseChanged refresca por fase) no disparan.
            view.ApplyBoardType(DiceBoardType.Default);
            view.ApplyBoardType(DiceBoardType.Attack);
            view.ApplyBoardType(DiceBoardType.Attack);
            view.ApplyBoardType(DiceBoardType.Defense);
            view.ApplyBoardType(DiceBoardType.Defense);
            view.ApplyBoardType(DiceBoardType.Default);

            // Assert — un evento por cambio real, con el tipo destino como arg.
            CollectionAssert.AreEqual(
                new[] { DiceBoardType.Attack, DiceBoardType.Defense, DiceBoardType.Default },
                raised);
        }

        [Test]
        public void ApplyBoardType_DoesNotRaiseEvent_WhenNoUsableSkin()
        {
            // Arrange — catalog vacío: TryGet falla y no hay aplicación.
            var view = MakeView(out var boardImage, out _ /* sin entries */);
            var before = boardImage.sprite;
            int raised = 0;
            view.BoardTypeChanged += _ => raised++;

            // Act
            view.ApplyBoardType(DiceBoardType.Attack);

            // Assert
            Assert.AreEqual(0, raised);
            Assert.AreEqual(before, boardImage.sprite);
        }

        // ───── Juice en EditMode ──────────────────────────────────────────────

        [Test]
        public void Juice_HandlesEventWithoutWiring_InEditMode_DoesNotThrow()
        {
            // Arrange — Juice sin refs ni players; OnEnable no corre solo en EditMode
            // (sin ExecuteAlways), lo invocamos por reflection para que se suscriba.
            var view = MakeView(out _, out _,
                Entry(DiceBoardType.Default), Entry(DiceBoardType.Attack));
            var juice = view.gameObject.AddComponent<DiceBoardSkinJuice>();
            InvokePrivate(juice, "OnEnable");

            // Act + Assert — dos applies distintos disparan el evento; el guard de
            // Application.isPlaying deja el handler en no-op sin excepción.
            Assert.DoesNotThrow(() =>
            {
                view.ApplyBoardType(DiceBoardType.Default);
                view.ApplyBoardType(DiceBoardType.Attack);
            });
        }

        // ───── Helpers ────────────────────────────────────────────────────────

        private DiceBoardSkinView MakeView(out Image boardImage, out Image logoImage,
            params DiceBoardSkinEntry[] entries)
        {
            var go = new GameObject("BoardSkinView", typeof(Image));
            _created.Add(go);
            boardImage = go.GetComponent<Image>();

            var logoGo = new GameObject("DiceBoardLogo", typeof(Image));
            logoGo.transform.SetParent(go.transform);
            logoImage = logoGo.GetComponent<Image>();

            // ApplyBoardType se llama directo — no dependemos de OnEnable (que en Play
            // Mode arranca el retry del ServiceLocator).
            var view = go.AddComponent<DiceBoardSkinView>();
            SetPrivateField(view, "_boardImage", boardImage);
            SetPrivateField(view, "_logoImage", logoImage);
            SetPrivateField(view, "_catalog", MakeCatalog(entries));
            return view;
        }

        private DiceBoardSkinEntry Entry(DiceBoardType type, Sprite logoSprite = null,
            Color logoTint = default)
        {
            return new DiceBoardSkinEntry
            {
                Type = type,
                Sprite = MakeSprite(), // TryGetExact exige Sprite != null para considerar la entry usable
                Tint = Color.white,
                ImageType = Image.Type.Sliced,
                LogoSprite = logoSprite,
                LogoTint = logoTint == default ? Color.white : logoTint,
                TextColor = Color.white,
            };
        }

        private DiceBoardSkinCatalogSO MakeCatalog(params DiceBoardSkinEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<DiceBoardSkinCatalogSO>();
            catalog.Skins = new List<DiceBoardSkinEntry>(entries);
            _created.Add(catalog);
            return catalog;
        }

        private Sprite MakeSprite()
        {
            var tex = Texture2D.whiteTexture;
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            _created.Add(sprite);
            return sprite;
        }

        private static void SetPrivateField(object target, string field, object value)
        {
            var f = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(f, $"Campo privado '{field}' no encontrado en {target.GetType().Name}.");
            f.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string method)
        {
            var m = target.GetType().GetMethod(method,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(m, $"Método privado '{method}' no encontrado en {target.GetType().Name}.");
            m.Invoke(target, null);
        }
    }
}
