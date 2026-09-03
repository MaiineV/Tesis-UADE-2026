using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Valida que TODOS los prefabs con <see cref="WorldSpaceHealthBar"/> (enemigos comunes,
    /// minions/objetos de boss y cofres) estén en el visual Mewgenics. Si un prefab nuevo llega
    /// con la barra vieja, esto lo agarra: autorar la barra como la arma
    /// <see cref="BossVisualWrapperBuilder"/> (fondo apagado + fill vertical + borde + HP actual).
    /// Los BOSSES no llevan barra world-space: su vida vive en la BossBarView del HUD.
    /// </summary>
    public sealed class MewgenicsHealthbarWiringTests
    {
        /// <summary>Pawns de jefe: la vida la muestra la BossBarView del HUD, no el mundo.</summary>
        private static readonly string[] BossPrefabPaths =
        {
            "Assets/Prefabs/Enemies/GeneralDirector.prefab",
            "Assets/Prefabs/Enemies/SunkedGrand.prefab",
            "Assets/Prefabs/Enemies/SecurityGuardBoss.prefab",
            "Assets/Prefabs/Enemies/Bosses/PF_Boss_Croupier.prefab",
            "Assets/Prefabs/Enemies/Bosses/PF_Boss_Cajero.prefab",
            "Assets/Prefabs/Enemies/Bosses/PF_Boss_Generala.prefab",
        };

        [Test]
        public void BossPrefabs_HaveNoWorldSpaceBar_TheHudBossBarOwnsThem()
        {
            foreach (var path in BossPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, $"No se pudo cargar '{path}'.");
                Assert.IsNull(prefab.GetComponentInChildren<WorldSpaceHealthBar>(includeInactive: true),
                    $"'{path}': un jefe con barra world-space duplica la BossBarView del HUD.");
            }
        }
        [Test]
        public void FindHealthBarPrefabPaths_FindsThePrefabsWithBars()
        {
            // Guarda contra un falso verde: si la búsqueda no devuelve nada, los foreach de
            // abajo pasarían sin validar ningún prefab.
            Assert.IsNotEmpty(FindHealthBarPrefabPaths());
        }

        [Test]
        public void EveryHealthBarPrefab_UsesTheMewgenicsFill()
        {
            foreach (var path in FindHealthBarPrefabPaths())
            {
                var bar = LoadBar(path);
                var fill = bar.transform.Find("LifeFill")?.GetComponent<Image>();

                Assert.IsNotNull(fill, $"'{path}': falta el hijo 'LifeFill'.");
                Assert.AreEqual(Image.Type.Filled, fill.type, path);
                Assert.AreEqual(Image.FillMethod.Vertical, fill.fillMethod, path);
                Assert.AreEqual((int)Image.OriginVertical.Bottom, fill.fillOrigin, path);
                Assert.IsNotNull(fill.sprite, $"'{path}': LifeFill sin sprite.");
                Assert.AreEqual(BossVisualWrapperBuilder.HealthBarFillSpriteName,
                    fill.sprite.name, path);
            }
        }

        [Test]
        public void EveryHealthBarPrefab_KeepsTheFrameVisibleOverTheFill()
        {
            foreach (var path in FindHealthBarPrefabPaths())
            {
                var bar = LoadBar(path);
                var frame = bar.transform.Find("Frame")?.GetComponent<Image>();

                Assert.IsNotNull(frame, $"'{path}': falta el hijo 'Frame'.");
                Assert.IsNotNull(frame.sprite, $"'{path}': Frame sin sprite.");
                Assert.AreEqual(BossVisualWrapperBuilder.HealthBarFrameSpriteName,
                    frame.sprite.name, path);
                Assert.Less(bar.transform.Find("LifeFill").GetSiblingIndex(),
                    frame.transform.GetSiblingIndex(),
                    $"'{path}': el borde tiene que dibujarse encima del relleno.");
            }
        }

        [Test]
        public void EveryHealthBarPrefab_ShowsTheDimmedStackBehindTheFill()
        {
            foreach (var path in FindHealthBarPrefabPaths())
            {
                var bar = LoadBar(path);
                var background = bar.transform.Find("LifeBackground")?.GetComponent<Image>();

                Assert.IsNotNull(background, $"'{path}': falta el hijo 'LifeBackground'.");
                Assert.IsNotNull(background.sprite, $"'{path}': LifeBackground sin sprite.");
                Assert.AreEqual(BossVisualWrapperBuilder.HealthBarFillSpriteName,
                    background.sprite.name,
                    $"'{path}': el fondo es la misma pila que el fill, apagada.");
                Assert.AreEqual(BossVisualWrapperBuilder.HealthBarBackgroundTint,
                    background.color, path);
                Assert.Less(background.transform.GetSiblingIndex(),
                    bar.transform.Find("LifeFill").GetSiblingIndex(),
                    $"'{path}': el fondo tiene que dibujarse detrás del relleno.");
            }
        }

        [Test]
        public void EveryHealthBarPrefab_PutsTheCanvasOnTheWorldUiLayer()
        {
            // La Main Camera renderiza el mundo pixelado y excluye WorldUI; WorldUiCameraSync
            // dibuja ese layer aparte a resolución nativa. Un canvas en Default entra a la pasada
            // pixelada y la pila de 37×53 queda aplastada en un puré ilegible — el bug de las
            // bombas del Croupier (y el dado de La Generala y la Comisión, construidos por el
            // mismo builder sin layer).
            int worldUiLayer = LayerMask.NameToLayer(BossVisualWrapperBuilder.HealthBarLayerName);
            Assert.GreaterOrEqual(worldUiLayer, 0,
                $"El layer '{BossVisualWrapperBuilder.HealthBarLayerName}' no existe en TagManager.");

            foreach (var path in FindHealthBarPrefabPaths())
            {
                var bar = LoadBar(path);
                var canvas = bar.GetComponentInChildren<Canvas>(includeInactive: true);

                Assert.IsNotNull(canvas, $"'{path}': la barra no tiene Canvas.");
                Assert.AreEqual(worldUiLayer, canvas.gameObject.layer,
                    $"'{path}': el Canvas de la barra tiene que estar en el layer " +
                    $"'{BossVisualWrapperBuilder.HealthBarLayerName}' o se renderiza pixelado.");
            }
        }

        [Test]
        public void EveryHealthBarPrefab_ShowsOnlyCurrentHp()
        {
            // El centrado del texto lo cubre BuildWrapper (mismo helper); acá alcanza con el
            // formato y el cableado sobre los prefabs reales.
            foreach (var path in FindHealthBarPrefabPaths())
            {
                var bar = LoadBar(path);

                Assert.AreEqual(BossVisualWrapperBuilder.HealthBarTextFormat,
                    GetString(bar, "_textFormat"), path);
                Assert.IsNotNull(GetRef(bar, "_hpText") as Object,
                    $"'{path}': _hpText sin cablear.");
                Assert.IsNotNull(GetRef(bar, "_fillImage") as Image,
                    $"'{path}': _fillImage sin cablear.");
            }
        }

        /// <summary>Paths de todos los prefabs que contienen una <see cref="WorldSpaceHealthBar"/>.</summary>
        /// <remarks>
        /// Acotado a las carpetas donde viven los prefabs con barra: buscar en todo Assets
        /// cargaría los cientos de prefabs de Feel y demos.
        /// </remarks>
        private static List<string> FindHealthBarPrefabPaths()
        {
            var searchFolders = new[] { "Assets/Prefabs", "Assets/Rollgeon" };
            var result = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", searchFolders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                if (prefab.GetComponentInChildren<WorldSpaceHealthBar>(includeInactive: true) != null)
                    result.Add(path);
            }
            return result;
        }

        private static WorldSpaceHealthBar LoadBar(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"No se pudo cargar '{path}'.");

            var bar = prefab.GetComponentInChildren<WorldSpaceHealthBar>(includeInactive: true);
            Assert.IsNotNull(bar, $"'{path}' perdió su WorldSpaceHealthBar.");
            return bar;
        }

        private static object GetRef(Object target, string field) =>
            target.GetType()
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(target);

        private static string GetString(Object target, string field) => GetRef(target, field) as string;
    }
}
