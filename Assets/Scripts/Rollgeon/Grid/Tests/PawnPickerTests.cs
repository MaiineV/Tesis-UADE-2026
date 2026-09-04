using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Entities.Visuals;
using UnityEngine;

namespace Rollgeon.Grid.Tests
{
    /// <summary>
    /// El pick de celda con máscara: la layer de cada pawn decide si su cuerpo captura el
    /// rayo. Un movimiento (máscara vacía) tiene que atravesar héroe y enemigo hasta el piso;
    /// un ataque (Entity) tiene que atravesar al héroe que está delante y quedarse con el
    /// enemigo. Se arma una escena mínima lejos del origen: la suite corre en la escena que
    /// esté abierta y 02_Gameplay trae ~325 colliders en Default alrededor de (0,0,0).
    /// </summary>
    [TestFixture]
    public sealed class PawnPickerTests
    {
        private static readonly Vector3 Origin = new Vector3(5000f, 0f, 5000f);
        private static readonly GridCoord WallCell = new GridCoord(0, 2);
        private static readonly GridCoord HeroCell = new GridCoord(1, 2);
        private static readonly GridCoord EnemyCell = new GridCoord(2, 2);
        private static readonly GridCoord FloorCell = new GridCoord(3, 2);

        private GridManager _grid;
        private readonly List<GameObject> _created = new List<GameObject>();
        private Ray _ray;

        [SetUp]
        public void SetUp()
        {
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(5, 5), Origin);

            // Pared en Default DELANTE del héroe: nunca bloquea el pick (CNF-002), solo
            // verifica que el recorrido de todos los hits sigue intacto con máscara.
            MakeBody("Wall", WallCell, layer: 0);
            MakePawn("Hero", HeroCell, EntityPawn.PawnKind.Hero, PawnLayers.PlayerLayer);
            MakePawn("Enemy", EnemyCell, EntityPawn.PawnKind.Enemy, PawnLayers.EntityLayer);

            // m_AutoSyncTransforms está apagado en DynamicsManager: sin esto la query ve
            // los colliders donde estaban al crearse, no donde los movimos.
            Physics.SyncTransforms();

            // Rayo casi rasante que cruza pared → héroe → enemigo y toca el piso en FloorCell:
            // arranca 2 celdas antes del héroe a media altura y baja 0.125 por unidad de avance,
            // así que llega a y=0 a 4 unidades del origen, dentro de la celda (3,2).
            var heroCenter = _grid.GridToWorld(HeroCell);
            _ray = new Ray(
                new Vector3(heroCenter.x - 2f, Origin.y + 0.5f, heroCenter.z),
                new Vector3(1f, -0.125f, 0f).normalized);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _created.Clear();
        }

        [Test]
        public void ResolveCoord_Unfiltered_ReturnsNearestPawn_IgnoringWall()
        {
            var coord = PawnPicker.ResolveCoord(_ray, _grid);

            Assert.AreEqual(HeroCell, coord,
                "Sin máscara gana el pawn más cercano a cámara; la pared de Default no puede bloquearlo.");
        }

        [Test]
        public void ResolveCoord_EntityMask_SkipsHeroInFront_ReturnsEnemyCell()
        {
            var coord = PawnPicker.ResolveCoord(_ray, _grid, PawnLayers.EntityMask);

            Assert.AreEqual(EnemyCell, coord,
                "En un ataque el cuerpo del héroe es transparente: apuntar al enemigo de atrás tiene que dar SU celda.");
        }

        [Test]
        public void ResolveCoord_PlayerMask_ReturnsHeroCell()
        {
            var coord = PawnPicker.ResolveCoord(_ray, _grid, PawnLayers.PlayerMask);

            Assert.AreEqual(HeroCell, coord);
        }

        [Test]
        public void ResolveCoord_NoneMask_FallsThroughEveryPawnToFloor()
        {
            var coord = PawnPicker.ResolveCoord(_ray, _grid, 0);

            Assert.AreEqual(FloorCell, coord,
                "En un movimiento ningún pawn cuenta: el click sobre un cuerpo cae al piso que hay debajo del cursor.");
        }

        [Test]
        public void TryPickPawn_EntityMask_ReturnsTheEnemy()
        {
            bool picked = PawnPicker.TryPickPawn(_ray, out var pawn, PawnLayers.EntityMask);

            Assert.IsTrue(picked);
            Assert.AreEqual(EntityPawn.PawnKind.Enemy, pawn.Kind);
        }

        [Test]
        public void TryPickPawn_NoneMask_ReturnsFalse()
        {
            bool picked = PawnPicker.TryPickPawn(_ray, out var pawn, 0);

            Assert.IsFalse(picked);
            Assert.IsNull(pawn);
        }

        private GameObject MakeBody(string name, GridCoord cell, int layer)
        {
            var go = new GameObject(name);
            _created.Add(go);
            go.layer = layer;
            go.transform.position = _grid.GridToWorld(cell);

            // Caja de 1u apoyada sobre el piso (y ∈ [0, 1]), como el cuerpo de un pawn.
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = Vector3.one;
            return go;
        }

        private void MakePawn(string name, GridCoord cell, EntityPawn.PawnKind kind, int layer)
        {
            var go = MakeBody(name, cell, layer);
            var pawn = go.AddComponent<EntityPawn>();
            var guid = Guid.NewGuid();
            pawn.Bind(guid, kind);
            _grid.Register(guid, cell);
        }
    }
}
