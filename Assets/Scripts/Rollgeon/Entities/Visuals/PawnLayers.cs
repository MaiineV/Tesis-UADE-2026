using UnityEngine;

namespace Rollgeon.Entities.Visuals
{
    /// <summary>
    /// Layers físicas de los pawns (<c>Player</c> / <c>Entity</c>), asignadas al spawnear.
    /// Son lo que permite que el raycast de targeting sea contextual: un movimiento
    /// enmascara a todos los pawns, un ataque solo al héroe (ver <c>SelectionPickMask</c>
    /// y <c>PawnPicker</c>).
    /// </summary>
    /// <remarks>
    /// <b>Por qué en runtime y no en los prefabs</b>: los prefabs de enemigos, bosses y
    /// props llegan del artista en Default y cada uno nuevo volvería a llegar así. Ponerlo
    /// en el spawn lo hace imposible de olvidar. Se toca el root y los GOs con collider; los
    /// hijos que ya tienen layer propia (barras de HP en WorldUI) quedan intactos.
    /// <para>
    /// <b>Guardia de render</b>: el root suele tener el MeshRenderer, así que cambia de layer
    /// con él. Hoy la cámara principal, el URP renderer y las luces tienen máscara "todo";
    /// cualquier máscara futura autorada por nombre tiene que incluir Player y Entity.
    /// </para>
    /// </remarks>
    public static class PawnLayers
    {
        public const string PlayerLayerName = "Player";
        public const string EntityLayerName = "Entity";

        // Índices en TagManager.asset. El fallback cubre un proyecto donde la layer no se
        // agregó todavía (mismo criterio que ActionDragController.ResolveTileLayer).
        private const int PlayerLayerFallback = 10;
        private const int EntityLayerFallback = 11;

        private static int _playerLayer = -1;
        private static int _entityLayer = -1;

        public static int PlayerLayer
        {
            get
            {
                if (_playerLayer < 0) _playerLayer = Resolve(PlayerLayerName, PlayerLayerFallback);
                return _playerLayer;
            }
        }

        public static int EntityLayer
        {
            get
            {
                if (_entityLayer < 0) _entityLayer = Resolve(EntityLayerName, EntityLayerFallback);
                return _entityLayer;
            }
        }

        public static int PlayerMask => 1 << PlayerLayer;
        public static int EntityMask => 1 << EntityLayer;
        public static int AllPawnsMask => PlayerMask | EntityMask;

        public static int LayerFor(EntityPawn.PawnKind kind)
            => kind == EntityPawn.PawnKind.Hero ? PlayerLayer : EntityLayer;

        /// <summary>
        /// Pone la layer de <paramref name="kind"/> en el root y en cada GO con collider de la
        /// jerarquía. Solo pisa GOs en Default: un hijo con layer propia (WorldUI) es una
        /// decisión del prefab y se respeta.
        /// </summary>
        public static void Apply(GameObject root, EntityPawn.PawnKind kind)
        {
            if (root == null) return;
            int layer = LayerFor(kind);

            // El root va siempre, tenga o no collider: ChestService cuelga el collider trigger
            // del cofre en el root DESPUÉS del spawn, y un componente nuevo usa la layer del GO.
            SetIfDefault(root, layer);

            foreach (var collider in root.GetComponentsInChildren<Collider>(includeInactive: true))
                SetIfDefault(collider.gameObject, layer);
        }

        private static void SetIfDefault(GameObject go, int layer)
        {
            if (go.layer != 0) return;
            go.layer = layer;
        }

        private static int Resolve(string name, int fallback)
        {
            int layer = LayerMask.NameToLayer(name);
            return layer >= 0 ? layer : fallback;
        }
    }
}
