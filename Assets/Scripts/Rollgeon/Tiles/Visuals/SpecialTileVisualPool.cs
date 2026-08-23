using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Tiles.Visuals
{
    /// <summary>
    /// Un visual alquilado al pool: el clon más los componentes opcionales ya resueltos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Guarda el prefab de origen porque el pool tiene un balde por prefab: devolver el clon
    /// al balde equivocado lo haría reaparecer con el arte de otra casilla.
    /// </para>
    /// <para>
    /// <see cref="Binding"/> y <see cref="Tooltip"/> se resuelven una sola vez, al crear el
    /// clon: la ignición de un jefe alquila ~112 visuales en un frame y dos
    /// <c>GetComponentInChildren</c> por alquiler serían dos recorridos de jerarquía que el
    /// pool existe justo para evitar.
    /// </para>
    /// </remarks>
    public sealed class PooledTileVisual
    {
        public GameObject Go;
        public SpecialTileVisualBinding Binding;
        public SpecialTileTooltipInfo Tooltip;

        /// <summary>Balde de origen. Sin él el clon no sabe volver a casa.</summary>
        internal GameObject Prefab;

        /// <summary>Un doble park duplicaría el clon en la pila y dos casillas terminarían
        /// compartiendo el mismo visual.</summary>
        internal bool Parked;
    }

    /// <summary>
    /// Pool de visuales de casilla especial, con un balde por prefab
    /// (<see cref="SpecialTileDefinitionSO.VisualPrefab"/> cambia por definición).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Un ataque de jefe enciende ~112 casillas en un frame: la primera vez se paga el
    /// <c>Instantiate</c>, de ahí en adelante los clones se reciclan y las bandas que se
    /// repiten cada dos rondas no asignan nada.
    /// </para>
    /// <para>
    /// Mismo criterio que el pool de quads de <c>ThreatTelegraphOverlay</c>: el root se
    /// chequea con <c>== null</c> para atrapar también el fake-null de Unity tras un cambio
    /// de escena, y al recrearlo se tiran TODOS los baldes, porque cada clon estacionado
    /// murió con el root y cada referencia cacheada quedó colgada.
    /// </para>
    /// </remarks>
    public sealed class SpecialTileVisualPool : IDisposable
    {
        public const string DefaultRootName = "SpecialTileVisualPool";

        /// <summary>
        /// Tope de clones estacionados POR PREFAB. Existe porque
        /// <c>SpecialTileServiceBootstrap</c> nunca llama a <see cref="Dispose"/>: sin tope la
        /// free list sería inmortal por todo el proceso, acumulando el pico de cada sala
        /// visitada. 128 cubre con aire la peor ignición medida (~112 casillas de un mismo
        /// prefab en un frame) — un tope más chico obligaría a destruir justo lo que se va a
        /// volver a pedir dos rondas después.
        /// </summary>
        public const int DefaultMaxFreePerPrefab = 128;

        /// <summary>Sufijo del clon estacionado. Un clon que sigue firmado con su casilla
        /// hace pasar por encendida una casilla que ya se apagó (y los tests cuentan por
        /// nombre).</summary>
        public const string ParkedSuffix = "(pooled)";

        private readonly Dictionary<GameObject, Stack<PooledTileVisual>> _free =
            new Dictionary<GameObject, Stack<PooledTileVisual>>();

        private readonly string _rootName;
        private readonly int _maxFreePerPrefab;

        private GameObject _root;

        public SpecialTileVisualPool(string rootName = DefaultRootName,
            int maxFreePerPrefab = DefaultMaxFreePerPrefab)
        {
            _rootName = string.IsNullOrEmpty(rootName) ? DefaultRootName : rootName;
            _maxFreePerPrefab = Mathf.Max(1, maxFreePerPrefab);
        }

        public int MaxFreePerPrefab => _maxFreePerPrefab;

        /// <summary>Clones estacionados en todos los baldes.</summary>
        public int FreeCount
        {
            get
            {
                int count = 0;
                foreach (var bucket in _free.Values) count += bucket.Count;
                return count;
            }
        }

        /// <summary>Clones estacionados del balde de <paramref name="prefab"/>.</summary>
        public int FreeCountOf(GameObject prefab)
            => prefab != null && _free.TryGetValue(prefab, out var bucket) ? bucket.Count : 0;

        /// <summary>
        /// Un clon de <paramref name="prefab"/> listo para usar en <paramref name="world"/>,
        /// reciclado si hay alguno estacionado. Null solo si no hay prefab.
        /// </summary>
        public PooledTileVisual Rent(GameObject prefab, Vector3 world)
        {
            if (prefab == null) return null;

            // Primero el root: si murió con la escena esto tira los baldes viejos, y hacerlo
            // después de sacar de la pila dejaría el entry recién popeado apuntando a un clon
            // muerto.
            var root = Root;

            var bucket = BucketOf(prefab);

            PooledTileVisual entry = null;

            // Descarta los muertos en vez de cortar: un clon que murió por afuera no invalida
            // a los que siguen vivos detrás suyo en la pila.
            while (bucket.Count > 0)
            {
                var pooled = bucket.Pop();
                if (pooled?.Go != null)
                {
                    entry = pooled;
                    break;
                }
            }

            entry ??= Create(prefab);

            var t = entry.Go.transform;

            // El padre se re-afirma acá y no solo al crear: un caller que reparente el visual
            // (engancharlo a una unidad, por ejemplo) lo dejaría colgado de una jerarquía que
            // no es la del pool, y el Dispose no se lo llevaría.
            if (t.parent != root.transform) t.SetParent(root.transform, worldPositionStays: false);

            // SetActive ANTES de posicionar: cualquier OnEnable del prefab que inicialice
            // su pose (SpikeArmedVisual re-armando el pincho) corre primero, y la colocación
            // autoritativa del pool siempre gana. Al revés, un clon reciclado se
            // teletransportaba al origen del mundo apenas se prendía.
            entry.Go.SetActive(true);

            // Rotación identidad a propósito: el Instantiate(prefab, world, identity) que había
            // antes descartaba la rotación propia del prefab (VFX_Fire trae 180° en el root),
            // así que un clon reciclado con la rotación vieja se vería distinto al primero.
            t.SetPositionAndRotation(world, Quaternion.identity);

            // El root tiene transform identidad, así que la escala local del clon es la del
            // prefab tal cual, igual que un Instantiate fresco.
            t.localScale = prefab.transform.localScale;

            entry.Parked = false;
            return entry;
        }

        /// <summary>
        /// Estaciona el clon para el próximo alquiler. Lo destruye de verdad solo si ya no hay
        /// dónde guardarlo (root muerto, prefab muerto) o si el balde llegó al tope.
        /// </summary>
        public void Park(PooledTileVisual entry)
        {
            if (entry == null || entry.Parked) return;
            entry.Parked = true;

            var go = entry.Go;
            var prefab = entry.Prefab;

            // Se lee el field y no la propiedad: crear un root nuevo para guardar un cadáver
            // dejaría un GameObject huérfano en pleno teardown de sala.
            if (go == null || prefab == null || _root == null)
            {
                DestroyCompat(go);
                entry.Go = null;
                return;
            }

            if (FreeCountOf(prefab) >= _maxFreePerPrefab)
            {
                DestroyCompat(go);
                entry.Go = null;
                return;
            }

            // Explícito y no vía el OnDisable del binding: en EditMode Unity no corre
            // OnEnable/OnDisable en clones instanciados en runtime, así que apoyarse en el callback
            // deja el clon estacionado contestando eventos de una casilla apagada — y encima
            // justamente en el único lugar donde se lo puede testear.
            entry.Binding?.Unbind();

            go.SetActive(false);
            go.name = $"{prefab.name} {ParkedSuffix}";

            var t = go.transform;
            if (t.parent != _root.transform) t.SetParent(_root.transform, worldPositionStays: false);

            BucketOf(prefab).Push(entry);
        }

        public void Dispose()
        {
            _free.Clear();

            // El root es el padre de todo clon, estacionado o alquilado: matarlo se lleva la
            // jerarquía entera y no queda ningún visual huérfano en la escena.
            DestroyCompat(_root);
            _root = null;
        }

        // ======================================================================
        // Interno
        // ======================================================================

        private GameObject Root
        {
            get
            {
                // == null cubre también el fake-null de Unity tras un cambio de escena: el root
                // murió con ella, y con él cada clon estacionado — los baldes solo guardan
                // referencias colgadas. Los clones ya alquiridos los limpia su dueño
                // (SpecialTileService), que es el único que los tiene en la mano.
                if (_root == null)
                {
                    _free.Clear();
                    _root = new GameObject(_rootName);
                }
                return _root;
            }
        }

        private Stack<PooledTileVisual> BucketOf(GameObject prefab)
        {
            if (_free.TryGetValue(prefab, out var bucket)) return bucket;

            // Solo al abrir un balde nuevo (una vez por prefab autorado, es barato): un prefab
            // que quedó en fake-null tras un domain reload deja un balde al que nadie va a
            // volver a pegarle, porque el caller corta antes con prefab == null.
            PruneDeadBuckets();

            bucket = new Stack<PooledTileVisual>();
            _free[prefab] = bucket;
            return bucket;
        }

        private void PruneDeadBuckets()
        {
            List<GameObject> dead = null;
            foreach (var pair in _free)
            {
                if (pair.Key != null) continue;

                foreach (var entry in pair.Value)
                    if (entry?.Go != null) DestroyCompat(entry.Go);

                (dead ??= new List<GameObject>()).Add(pair.Key);
            }

            if (dead == null) return;
            foreach (var key in dead) _free.Remove(key);
        }

        private PooledTileVisual Create(GameObject prefab)
        {
            // El overload (prefab, parent) parentea sin conservar la pose de mundo, que es lo que
            // queremos: Rent la fija después. El nombre del parámetro no se pasa porque el
            // overload genérico no lo declara igual que el no-genérico.
            var go = Object.Instantiate<GameObject>(prefab, Root.transform);

            return new PooledTileVisual
            {
                Go = go,
                Prefab = prefab,

                // include inactive: el prefab puede traer el nodo de arte apagado y el binding
                // igual tiene que encontrarse.
                Binding = go.GetComponentInChildren<SpecialTileVisualBinding>(true),
                Tooltip = go.GetComponentInChildren<SpecialTileTooltipInfo>(true),
            };
        }

        /// <summary>Fuera de play mode <c>Destroy</c> es diferido y no llega a aplicarse en un test.</summary>
        private static void DestroyCompat(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
