using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Grid;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// El número cantado, escrito sobre el bloque del paño que va a caer. Es la mitad que ata la
    /// ruleta al piso: sin él, el jugador ve un sector encendido y un disco girando y nada dice que
    /// son el mismo dato.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Uno por slot, no uno por casilla.</b> A diferencia de <c>ThreatTelegraphOverlay</c>, que
    /// pinta un quad por tile, acá va un solo label en el centro del sector: un número repetido doce
    /// veces sería ruido, y lo que hay que leer es "este bloque es el 3", no "esta casilla es del 3".
    /// </para>
    /// <para>
    /// <b>Mismo ciclo de vida que el overlay de amenaza</b> — Global, lazy vía
    /// <see cref="ResolveOrCreate"/>, y se apaga en <c>OnCombatEnd</c> / <c>OnRunEnd</c>. Se registra
    /// bajo su propio tipo concreto y no bajo una interfaz porque tiene un solo consumidor
    /// (<see cref="CroupierSectorTelegraph"/>) y una sola implementación posible: inventar la interfaz
    /// sería una indirección sin segunda implementación que la justifique.
    /// </para>
    /// <para>
    /// <b>Los labels se poolean</b> igual que los quads: en fase 2 hay dos slots vivos y el sector se
    /// re-marca cada vez que el jugador corre la rueda, así que crear y destruir por marca haría
    /// basura todos los turnos.
    /// </para>
    /// </remarks>
    public sealed class CroupierSectorNumberOverlay : IDisposable
    {
        /// <summary>Levantado del piso apenas más que el quad del telegraph, para quedar encima.</summary>
        public float YOffset = 0.09f;

        /// <summary>
        /// Lado de la caja del número, en casillas. El label autoescala a esta caja, así que el
        /// tamaño no depende de la fuente.
        /// </summary>
        public float LabelTiles = 2.2f;

        /// <summary>Latón del sector — el mismo matiz que el quad y que el número de la ruleta.</summary>
        public Color Tint = new Color(1f, 0.92f, 0.70f, 0.95f);

        /// <summary>
        /// Fuente de los números. Null ⇒ la default de TMP. Es settable para que un instalador pueda
        /// pasarle la pixel font del HUD sin que este archivo tenga que conocer una ruta de assets.
        /// </summary>
        public TMP_FontAsset Font;

        private readonly Dictionary<Guid, SectorLabel> _activeBySource = new Dictionary<Guid, SectorLabel>();
        private readonly Stack<SectorLabel> _free = new Stack<SectorLabel>();

        private GameObject _root;

        private EventManager.EventReceiver _onScopeEnded;

        /// <summary>Labels visibles — para asserts de tests y debugging.</summary>
        public int ActiveLabelCount
        {
            get
            {
                int count = 0;
                foreach (var label in _activeBySource.Values)
                    if (label?.Go != null && label.Go.activeSelf) count++;
                return count;
            }
        }

        /// <summary>
        /// Qué número muestra el slot <paramref name="sourceGuid"/>, o 0 si no tiene ninguno. Seam de
        /// lectura para tests — mismo criterio que <c>ThreatTelegraphOverlay.ActiveQuadsOf</c>.
        /// </summary>
        public int NumberOf(Guid sourceGuid) =>
            _activeBySource.TryGetValue(sourceGuid, out var label) ? label.Number : 0;

        /// <summary>Dónde está parado el número del slot, o <c>null</c> si no hay ninguno.</summary>
        public GridCoord? CoordOf(Guid sourceGuid) =>
            _activeBySource.TryGetValue(sourceGuid, out var label) ? label.Coord : (GridCoord?)null;

        // ======================================================================
        // Ciclo de vida
        // ======================================================================

        /// <summary>Devuelve el registrado o crea + registra uno (Global).</summary>
        public static CroupierSectorNumberOverlay ResolveOrCreate()
        {
            if (ServiceLocator.TryGetService<CroupierSectorNumberOverlay>(out var existing) && existing != null)
                return existing;

            var created = new CroupierSectorNumberOverlay();
            created.RegisterGlobal();
            return created;
        }

        private void RegisterGlobal()
        {
            _onScopeEnded = OnScopeEndedExternal;
            EventManager.Subscribe(EventName.OnCombatEnd, _onScopeEnded);
            EventManager.Subscribe(EventName.OnRunEnd, _onScopeEnded);

            ServiceLocator.AddService<CroupierSectorNumberOverlay>(this, ServiceScope.Global);
        }

        public void Dispose()
        {
            if (_onScopeEnded != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onScopeEnded);
                EventManager.UnSubscribe(EventName.OnRunEnd, _onScopeEnded);
                _onScopeEnded = null;
            }

            _activeBySource.Clear();
            _free.Clear();
            DestroyCompat(_root);
            _root = null;
        }

        // ======================================================================
        // API
        // ======================================================================

        /// <summary>
        /// Escribe <paramref name="number"/> en el centro de <paramref name="tiles"/>. Re-llamar con
        /// el mismo <paramref name="sourceGuid"/> mueve el label en vez de agregar otro — que es lo
        /// que pasa cada vez que el jugador corre la rueda.
        /// </summary>
        public void Show(Guid sourceGuid, int number, IEnumerable<GridCoord> tiles)
        {
            if (sourceGuid == Guid.Empty || tiles == null) return;
            if (!TryCenter(tiles, out var center)) return;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null)
            {
                Debug.LogWarning("[CroupierSectorNumberOverlay] IGridManager no registrado — el sector " +
                                 "queda sin número.");
                return;
            }

            if (!_activeBySource.TryGetValue(sourceGuid, out var label) || label?.Go == null)
            {
                label = NextFreeLabel();
                _activeBySource[sourceGuid] = label;
            }

            label.Number = number;
            label.Coord = center;

            label.Go.transform.position = grid.GridToWorld(center) + Vector3.up * YOffset;
            label.Go.transform.localScale = Vector3.one * Mathf.Max(grid.TileSize, 0.01f);

            if (label.Text != null)
            {
                if (Font != null) label.Text.font = Font;
                label.Text.color = Tint;
                label.Text.text = number.ToString();
                label.Text.rectTransform.sizeDelta = new Vector2(LabelTiles, LabelTiles);
            }

            label.Go.SetActive(true);
        }

        public void Clear(Guid sourceGuid)
        {
            if (!_activeBySource.TryGetValue(sourceGuid, out var label)) return;

            if (label?.Go != null)
            {
                label.Go.SetActive(false);
                _free.Push(label);
            }
            _activeBySource.Remove(sourceGuid);
        }

        public void ClearAll()
        {
            var sources = new List<Guid>(_activeBySource.Keys);
            foreach (var source in sources) Clear(source);
        }

        // ======================================================================
        // Geometría
        // ======================================================================

        /// <summary>
        /// La casilla del conjunto más cercana a su centro de masa. No es el promedio crudo: el
        /// promedio de un sector recortado por props puede caer en una casilla que no pertenece al
        /// sector, y el número quedaría flotando fuera del bloque que anuncia.
        /// </summary>
        /// <remarks>Puro y estático para poder testear la elección sin grilla ni GameObjects.</remarks>
        public static bool TryCenter(IEnumerable<GridCoord> tiles, out GridCoord center)
        {
            center = default;
            if (tiles == null) return false;

            long sumX = 0;
            long sumY = 0;
            int count = 0;
            foreach (var tile in tiles)
            {
                sumX += tile.X;
                sumY += tile.Y;
                count++;
            }
            if (count == 0) return false;

            float avgX = (float)sumX / count;
            float avgY = (float)sumY / count;

            bool found = false;
            float best = float.MaxValue;
            foreach (var tile in tiles)
            {
                float dx = tile.X - avgX;
                float dy = tile.Y - avgY;
                float distance = dx * dx + dy * dy;

                // Estrictamente menor: con empate gana el primero que llega, así que un mismo
                // conjunto siempre elige la misma casilla (los tests no dependen del orden del hash).
                if (found && distance >= best) continue;

                best = distance;
                center = tile;
                found = true;
            }
            return found;
        }

        // ======================================================================
        // Pool
        // ======================================================================

        private GameObject Root
        {
            get
            {
                // == null cubre también el fake-null de Unity tras un cambio de escena: el root murió
                // con la escena y hay que rearmar el pool.
                if (_root == null)
                {
                    _activeBySource.Clear();
                    _free.Clear();
                    _root = new GameObject("CroupierSectorNumberOverlay");
                }
                return _root;
            }
        }

        private SectorLabel NextFreeLabel()
        {
            while (_free.Count > 0)
            {
                var pooled = _free.Pop();
                if (pooled?.Go != null) return pooled;
            }
            return CreateLabel();
        }

        private SectorLabel CreateLabel()
        {
            var go = new GameObject("SectorNumber");
            go.transform.SetParent(Root.transform, worldPositionStays: false);

            // Mismo plano que los quads del telegraph: acostado sobre el piso. Cualquier otra
            // rotación lo dejaría cortando el suelo desde la cámara del juego.
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.SetActive(false);

            var text = go.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 1f;
            text.fontSizeMax = 300f;
            text.raycastTarget = false;

            return new SectorLabel { Go = go, Text = text };
        }

        private static void DestroyCompat(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        private void OnScopeEndedExternal(params object[] args) => ClearAll();

        /// <summary>Un label activo con el dato que muestra.</summary>
        private sealed class SectorLabel
        {
            public GameObject Go;
            public TMP_Text Text;
            public int Number;
            public GridCoord Coord;
        }
    }
}
