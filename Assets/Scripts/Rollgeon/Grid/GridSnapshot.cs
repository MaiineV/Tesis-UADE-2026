using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Grid
{
    /// <summary>
    /// Descripción serializable de una grilla de sala. TECHNICAL.md §13.3 / §17.§I.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bakeado en el prefab de la sala desde un editor tool. Unity no serializa
    /// <c>bool[,]</c> directamente, así que persistimos un array flat + <see cref="Width"/>
    /// y <see cref="Height"/>. <see cref="IsWalkable(GridCoord)"/> hace la traducción.
    /// </para>
    /// <para>
    /// Sin valores seteados el snapshot es <see cref="Empty"/>: 0x0 sin tiles. El
    /// <see cref="Grid.GridManager"/> detecta ese caso y trata todo el mapa como walkable
    /// (útil para tests o salas sin layout autorado todavía).
    /// </para>
    /// </remarks>
    [Serializable]
    public struct GridSnapshot
    {
        [SerializeField] private int _width;
        [SerializeField] private int _height;
        [SerializeField] private bool[] _walkable;

        public int Width => _width;
        public int Height => _height;
        public bool IsEmpty => _width <= 0 || _height <= 0 || _walkable == null || _walkable.Length == 0;

        public static GridSnapshot Empty => new GridSnapshot();

        public GridSnapshot(int width, int height, bool[] walkable)
        {
            if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));
            _width = width;
            _height = height;
            _walkable = walkable ?? Array.Empty<bool>();
            if (_walkable.Length != 0 && _walkable.Length != width * height)
            {
                throw new ArgumentException(
                    $"walkable.Length ({_walkable.Length}) debe ser Width*Height ({width * height}).",
                    nameof(walkable));
            }
        }

        /// <summary>Snapshot rectangular sin obstáculos (todo walkable).</summary>
        public static GridSnapshot Rect(int width, int height)
        {
            var w = new bool[width * height];
            for (int i = 0; i < w.Length; i++) w[i] = true;
            return new GridSnapshot(width, height, w);
        }

        public bool InBounds(GridCoord c) =>
            c.X >= 0 && c.Y >= 0 && c.X < _width && c.Y < _height;

        public bool IsWalkable(GridCoord c)
        {
            if (IsEmpty) return true;
            if (!InBounds(c)) return false;
            return _walkable[c.Y * _width + c.X];
        }

        public IEnumerable<GridCoord> AllCoords()
        {
            for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
                yield return new GridCoord(x, y);
        }
    }
}
