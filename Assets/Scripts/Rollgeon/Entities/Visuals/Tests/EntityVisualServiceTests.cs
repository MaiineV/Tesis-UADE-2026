using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Movement;
using Rollgeon.UI.Tooltips;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Entities.Visuals.Tests
{
    [TestFixture]
    public class EntityVisualServiceTests
    {
        private GridManager _grid;
        private MovementService _movement;
        private EntityVisualService _service;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(5, 5));
            _movement = new MovementService(_grid);

            _service = new EntityVisualService(_grid, _movement);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        private GameObject MakePrefab(string name)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.AddComponent<EntityPawn>();
            _created.Add(go);
            return go;
        }

        private ClassHeroSO MakeHero(string prefabName)
        {
            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            hero.VisualPrefab = MakePrefab(prefabName);
            _created.Add(hero);
            return hero;
        }

        private EnemyDataSO MakeEnemy(string prefabName)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.VisualPrefab = MakePrefab(prefabName);
            _created.Add(data);
            return data;
        }

        [Test]
        public void SpawnEnemy_PropagatesFootprintToPawn_AndCentersIt()
        {
            var data = MakeEnemy("Big");
            data.Footprint = new Vector2Int(2, 2);

            var pawn = _service.SpawnEnemy(Guid.NewGuid(), data, new GridCoord(1, 1));

            Assert.AreEqual(new Vector2Int(2, 2), pawn.Footprint);
            var expected = _grid.GridToWorld(new GridCoord(1, 1)) + new Vector3(0.5f, 0.1f, 0.5f);
            Assert.AreEqual(expected.x, pawn.transform.position.x, 1e-4f);
            Assert.AreEqual(expected.z, pawn.transform.position.z, 1e-4f);
        }

        /// <summary>
        /// Enemigo cuyo arte trae collider — la única condición bajo la que <c>AttachTooltip</c>
        /// cuelga algo. <c>EntityId</c> vacío a propósito: así <c>EnemyTooltipInfo</c> lee el
        /// <c>DisplayName</c> del SO derecho y ningún assert de acá depende de Localization.
        /// </summary>
        private EnemyDataSO MakeEnemyWithCollider(string prefabName, string displayName = "Enemigo")
        {
            var data = MakeEnemy(prefabName);
            data.hideFlags = HideFlags.HideAndDontSave;
            data.EntityId = string.Empty;
            data.DisplayName = displayName;
            data.VisualPrefab.AddComponent<BoxCollider>();
            return data;
        }

        [Test]
        public void SpawnHero_RegistersPawnAtCoord()
        {
            // Arrange
            var hero = MakeHero("HeroPrefab");
            var guid = Guid.NewGuid();

            // Act
            var pawn = _service.SpawnHero(guid, hero, new GridCoord(1, 2));
            _created.Add(pawn.gameObject);

            // Assert
            Assert.IsNotNull(pawn);
            Assert.AreEqual(guid, pawn.EntityGuid);
            Assert.AreEqual(EntityPawn.PawnKind.Hero, pawn.Kind);
            Assert.IsTrue(_service.TryGetPawn(guid, out var same));
            Assert.AreSame(pawn, same);
            // La colocación en grilla es un asunto XZ; el pawn además se eleva PawnYOffset
            // en Y (lift visual sobre el piso), así que comparamos solo el plano.
            var expected = _grid.GridToWorld(new GridCoord(1, 2));
            Assert.AreEqual(expected.x, pawn.transform.position.x, 1e-4f);
            Assert.AreEqual(expected.z, pawn.transform.position.z, 1e-4f);
        }

        [Test]
        public void SpawnHero_UsesVisualPrefabFromClassHeroSO()
        {
            // Arrange
            var hero = MakeHero("WarriorVisual");

            // Act
            var pawn = _service.SpawnHero(Guid.NewGuid(), hero, GridCoord.Zero);
            _created.Add(pawn.gameObject);

            // Assert
            Assert.IsNotNull(pawn);
            Assert.AreEqual(EntityPawn.PawnKind.Hero, pawn.Kind);
        }

        [Test]
        public void SpawnHero_LogsErrorAndReturnsNull_WhenVisualPrefabMissing()
        {
            // Arrange
            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            hero.VisualPrefab = null;
            _created.Add(hero);

            // Act + Assert
            LogAssert.Expect(LogType.Error, new Regex("no tiene VisualPrefab"));
            var pawn = _service.SpawnHero(Guid.NewGuid(), hero, GridCoord.Zero);
            Assert.IsNull(pawn);
        }

        [Test]
        public void SpawnHero_Throws_WhenHeroIsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => _service.SpawnHero(Guid.NewGuid(), null, GridCoord.Zero));
        }

        [Test]
        public void SpawnEnemy_UsesVisualPrefabFromData()
        {
            // Arrange
            var data = MakeEnemy("CustomEnemyVisual");

            // Act
            var pawn = _service.SpawnEnemy(Guid.NewGuid(), data, GridCoord.Zero);
            _created.Add(pawn.gameObject);

            // Assert
            Assert.IsNotNull(pawn);
            Assert.AreEqual(EntityPawn.PawnKind.Enemy, pawn.Kind);
        }

        [Test]
        public void SpawnEnemy_LogsErrorAndReturnsNull_WhenVisualPrefabMissing()
        {
            // Arrange
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.VisualPrefab = null;
            _created.Add(data);

            // Act + Assert
            LogAssert.Expect(LogType.Error, new Regex("no tiene VisualPrefab"));
            var pawn = _service.SpawnEnemy(Guid.NewGuid(), data, GridCoord.Zero);
            Assert.IsNull(pawn);
        }

        [Test]
        public void SpawnEnemy_Throws_WhenDataIsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => _service.SpawnEnemy(Guid.NewGuid(), null, GridCoord.Zero));
        }

        [Test]
        public void SpawnEnemy_AttachesTooltipInfoAndTrigger_WhenVisualHasCollider()
        {
            // Arrange
            var data = MakeEnemyWithCollider("EnemyVisual_WithCollider");

            // Act
            var pawn = _service.SpawnEnemy(Guid.NewGuid(), data, GridCoord.Zero);
            _created.Add(pawn.gameObject);

            // Assert
            Assert.IsNotNull(pawn.GetComponent<EnemyTooltipInfo>(),
                "El pawn quedó sin EnemyTooltipInfo: el enemigo no tiene con qué describirse y el " +
                "jugador pierde la única explicación de la pelea que puede leer sin morir primero.");
            Assert.IsNotNull(pawn.GetComponent<WorldTooltipTrigger>(),
                "El pawn quedó sin WorldTooltipTrigger: el texto existe pero nada lo abre.");

            // El collider puede vivir en el hijo del modelo, como en los prefabs de arte reales:
            // AttachTooltip lo busca con GetComponentInChildren, no solo en el root.
            var nested = MakeEnemy("EnemyVisual_ChildCollider");
            var model = new GameObject("Model");
            model.transform.SetParent(nested.VisualPrefab.transform);
            model.AddComponent<BoxCollider>();

            var nestedPawn = _service.SpawnEnemy(Guid.NewGuid(), nested, new GridCoord(1, 0));
            _created.Add(nestedPawn.gameObject);

            Assert.IsNotNull(nestedPawn.GetComponent<EnemyTooltipInfo>(),
                "Un collider en el hijo del modelo dejó al pawn sin tooltip: así vienen los prefabs " +
                "de arte, así que esto apagaría el tooltip de casi todo el catálogo.");
        }

        [Test]
        public void SpawnEnemy_LeavesTheTriggerOnHover_NotOnClick()
        {
            // Arrange
            var data = MakeEnemyWithCollider("EnemyVisual_WithCollider");

            // Act
            var pawn = _service.SpawnEnemy(Guid.NewGuid(), data, GridCoord.Zero);
            _created.Add(pawn.gameObject);

            // Assert
            var trigger = pawn.GetComponent<WorldTooltipTrigger>();
            Assert.IsNotNull(trigger, "Fixture roto: el pawn no tiene WorldTooltipTrigger.");
            Assert.AreEqual(WorldTooltipMode.Hover, trigger.Mode,
                "El trigger quedó en Click, que es el default serializado del componente: el click " +
                "sobre una casilla ocupada por un enemigo ya significa 'atacar a este enemigo', y " +
                "abrir el panel se lo robaría.");
        }

        [Test]
        public void SpawnEnemy_AttachesNothing_WhenVisualHasNoCollider()
        {
            // Arrange
            var data = MakeEnemy("EnemyVisual_NoCollider");

            // Act
            var pawn = _service.SpawnEnemy(Guid.NewGuid(), data, GridCoord.Zero);
            _created.Add(pawn.gameObject);

            // Assert
            string why = "El trigger raycastea en su Update todos los frames y en un pawn sin " +
                         "collider no puede acertar nunca: es costo por frame por enemigo sin " +
                         "ningún resultado posible.";
            Assert.IsNull(pawn.GetComponentInChildren<WorldTooltipTrigger>(true), why);
            Assert.IsNull(pawn.GetComponentInChildren<EnemyTooltipInfo>(true), why);
        }

        [Test]
        public void SpawnEnemy_BindsTheDataIntoTheTooltip_NotJustTheComponent()
        {
            // Arrange
            var data = MakeEnemyWithCollider("EnemyVisual_WithCollider", "El Croupier");

            // Act
            var pawn = _service.SpawnEnemy(Guid.NewGuid(), data, GridCoord.Zero);
            _created.Add(pawn.gameObject);

            // Assert
            var info = pawn.GetComponent<EnemyTooltipInfo>();
            Assert.IsNotNull(info, "Fixture roto: el pawn no tiene EnemyTooltipInfo.");
            Assert.IsTrue(info.BuildTooltip().Contains("El Croupier"),
                "El componente está colgado pero sin Bind: un AddComponent que se olvida del Bind " +
                "deja el tooltip mudo, y eso solo se ve pasando el mouse en juego.");
        }

        [Test]
        public void Despawn_RemovesFromLookup_AndDestroysGO()
        {
            var hero = MakeHero("HeroPrefab");
            var guid = Guid.NewGuid();
            _service.SpawnHero(guid, hero, GridCoord.Zero);

            _service.Despawn(guid);

            Assert.IsFalse(_service.TryGetPawn(guid, out _));
        }

        [Test]
        public void OnEntityMoved_UpdatesPawnPosition()
        {
            var hero = MakeHero("HeroPrefab");
            var guid = Guid.NewGuid();
            _grid.Register(guid, new GridCoord(0, 0));
            var pawn = _service.SpawnHero(guid, hero, new GridCoord(0, 0));
            _created.Add(pawn.gameObject);

            _movement.Move(guid, new GridCoord(3, 0));

            // Solo XZ: el PawnYOffset eleva el pawn en Y (lift visual), no afecta la celda.
            var expected = _grid.GridToWorld(new GridCoord(3, 0));
            Assert.AreEqual(expected.x, pawn.transform.position.x, 1e-4f);
            Assert.AreEqual(expected.z, pawn.transform.position.z, 1e-4f);
        }

        [Test]
        public void TryGetWorldPosition_ReturnsPawnPosition()
        {
            var hero = MakeHero("HeroPrefab");
            var guid = Guid.NewGuid();
            var pawn = _service.SpawnHero(guid, hero, new GridCoord(2, 2));
            _created.Add(pawn.gameObject);

            var pos = _service.TryGetWorldPosition(guid);
            Assert.IsTrue(pos.HasValue);
            Assert.AreEqual(pawn.transform.position, pos.Value);
        }

        [Test]
        public void TryGetWorldPosition_Null_WhenUnregistered()
        {
            Assert.IsNull(_service.TryGetWorldPosition(Guid.NewGuid()));
        }

        [Test]
        public void SpawnHero_Twice_DespawnsPrevious()
        {
            var hero = MakeHero("HeroPrefab");
            var guid = Guid.NewGuid();
            var first = _service.SpawnHero(guid, hero, new GridCoord(0, 0));
            var second = _service.SpawnHero(guid, hero, new GridCoord(1, 0));

            Assert.AreNotSame(first, second);
            Assert.IsTrue(_service.TryGetPawn(guid, out var current));
            Assert.AreSame(second, current);
        }

        [Test]
        public void SpawnHero_EmptyGuid_Throws()
        {
            var hero = MakeHero("HeroPrefab");
            Assert.Throws<ArgumentException>(
                () => _service.SpawnHero(Guid.Empty, hero, GridCoord.Zero));
        }

        [Test]
        public void DespawnAll_ClearsLookup()
        {
            var hero = MakeHero("HeroPrefab");
            var enemy = MakeEnemy("EnemyVisual");

            _service.SpawnHero(Guid.NewGuid(), hero, GridCoord.Zero);
            _service.SpawnEnemy(Guid.NewGuid(), enemy, new GridCoord(1, 0));

            _service.DespawnAll();
            Assert.IsFalse(_service.TryGetPawn(Guid.NewGuid(), out _));
        }

        // ---- Layers de targeting (PawnLayers) --------------------------------

        [Test]
        public void SpawnHero_PutsRootAndColliderOnPlayerLayer()
        {
            var hero = MakeHero("HeroPrefab");
            var model = new GameObject("Model");
            model.transform.SetParent(hero.VisualPrefab.transform);
            model.AddComponent<CapsuleCollider>();

            var pawn = _service.SpawnHero(Guid.NewGuid(), hero, GridCoord.Zero);

            Assert.AreEqual(PawnLayers.PlayerLayer, pawn.gameObject.layer,
                "El héroe en Default no se puede excluir del raycast de un ataque: su cuerpo tapa a los enemigos de atrás.");
            Assert.AreEqual(PawnLayers.PlayerLayer, pawn.GetComponentInChildren<Collider>().gameObject.layer);
        }

        [Test]
        public void SpawnEnemy_PutsNestedColliderOnEntityLayer()
        {
            var enemy = MakeEnemy("EnemyVisual");
            var model = new GameObject("Model");
            model.transform.SetParent(enemy.VisualPrefab.transform);
            model.AddComponent<BoxCollider>();

            var pawn = _service.SpawnEnemy(Guid.NewGuid(), enemy, new GridCoord(1, 0));

            // La layer se pone en la INSTANCIA clonada; el prefab plantilla queda en Default.
            var spawnedModel = pawn.transform.Find("Model");
            Assert.AreEqual(PawnLayers.EntityLayer, pawn.gameObject.layer);
            Assert.AreEqual(PawnLayers.EntityLayer, spawnedModel.gameObject.layer,
                "El collider del arte vive en el hijo del modelo: si queda en Default, un movimiento sigue viendo al enemigo.");
            Assert.AreEqual(0, model.layer, "El prefab plantilla no se toca, solo la instancia.");
        }

        [Test]
        public void SpawnProp_PutsRootOnEntityLayer_EvenWithoutCollider()
        {
            // El cofre no trae collider: ChestService se lo agrega al root DESPUÉS del spawn,
            // y ese componente usa la layer que el root tenga en ese momento.
            var prefab = MakePrefab("ChestVisual");

            var pawn = _service.SpawnProp(Guid.NewGuid(), prefab, new GridCoord(2, 0));

            Assert.AreEqual(PawnLayers.EntityLayer, pawn.gameObject.layer);
        }

        [Test]
        public void SpawnEnemy_LeavesWorldUiChildrenUntouched()
        {
            const int worldUiLayer = 9;
            var enemy = MakeEnemy("EnemyVisual");
            var healthBar = new GameObject("HealthBar");
            healthBar.transform.SetParent(enemy.VisualPrefab.transform);
            healthBar.layer = worldUiLayer;
            healthBar.AddComponent<BoxCollider>();

            var pawn = _service.SpawnEnemy(Guid.NewGuid(), enemy, new GridCoord(1, 0));

            var spawnedBar = pawn.transform.Find("HealthBar");
            Assert.AreEqual(worldUiLayer, spawnedBar.gameObject.layer,
                "La barra de HP tiene cámara propia por layer: pisarla la saca de pantalla.");
        }
    }
}
