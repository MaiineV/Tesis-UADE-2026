using System;
using System.Collections;
using System.Collections.Generic;
using Rollgeon.Grid;
using Rollgeon.Movement;
using UnityEngine;

namespace Rollgeon.Entities.Visuals
{
    /// <summary>
    /// MonoBehaviour que vive en el prefab del GameObject de una entidad
    /// (hero / enemy). Expone el Guid lógico y una API para que el
    /// <see cref="EntityVisualService"/> actualice su posición al moverse en grilla.
    /// </summary>
    /// <remarks>
    /// Placeholder: el FP usa primitives coloreados — más adelante la capa de art
    /// reemplaza prefabs sin cambiar el contrato. El pawn también puede referenciar
    /// una barra de HP y un animator, pero no se requieren para FP.
    /// </remarks>
    [AddComponentMenu("Rollgeon/Entities/Entity Pawn")]
    public sealed class EntityPawn : MonoBehaviour, Rollgeon.UI.Cursor.ICursorHoverable
    {
        // Offset Y aplicado a TODOS los pawns (hero + enemies) para que ninguno
        // clipée con el piso/grid. Antes solo el hero se elevaba — los enemigos
        // quedaban a Y=0 y aparecían "muy encima" visualmente por diferencia de
        // pivot en el modelo del enemigo vs el del hero.
        private const float PawnYOffset = 0.1f;

        // Default por step — corto para que el movimiento se vea fluido sin frenar
        // el ritmo (≈8 tiles/s a 0.12). Override por arg si querés tunear.
        private const float DefaultSecondsPerStep = 0.12f;

        // Bool del Animator que gatea Idle ⇄ Run. Lo declaran AnimCon_Warrior, _Goblin
        // (CardEnemy), _Healer, _Mecha y _RangedMachine. Los que todavía no tienen clip de
        // caminata (SunkedGrand, GeneralDirector) no lo declaran — ver _hasMovementParam.
        private const string MovementParam = "Movement";

        [SerializeField, Tooltip("Barra de HP world-space. Null en heroes o pawns sin barra.")]
        private WorldSpaceHealthBar _healthBar;

        [SerializeField]
        [Tooltip("Cómo se desplaza. Walk = lerp casilla a casilla. Blink = desaparece y aparece en " +
                 "el destino, sin pasar por el camino. Elegir Blink SOLO si el clip de movimiento " +
                 "del rig es un teletransporte.")]
        private LocomotionStyle _locomotion = LocomotionStyle.Walk;

        /// <summary>Cómo se ve moverse este pawn.</summary>
        public LocomotionStyle Locomotion => _locomotion;

        [SerializeField, Min(0f)]
        [Tooltip("Segundos que se queda en el origen antes de saltar — el tramo en que el clip lo " +
                 "hace desaparecer. Solo para Blink.")]
        private float _blinkOutSeconds = 0.14f;

        [SerializeField, Min(0f)]
        [Tooltip("Segundos que se queda en el destino después de saltar — el tramo en que el clip " +
                 "lo hace aparecer. Solo para Blink.")]
        private float _blinkInSeconds = 0.14f;

        private Coroutine _moveAnim;

        private Animator _animator;
        private bool _animatorResolved;
        private bool _hasMovementParam;

        public WorldSpaceHealthBar HealthBar => _healthBar;

        public Guid EntityGuid { get; private set; }
        public PawnKind Kind { get; private set; }

        /// <summary>Dirección actual del pawn. Default <see cref="Cardinal.South"/> (mira al
        /// jugador en cámara iso).</summary>
        public Cardinal Facing { get; private set; } = Cardinal.South;

        public bool IsMoving => _moveAnim != null;

        public void Bind(Guid guid, PawnKind kind)
        {
            EntityGuid = guid;
            Kind = kind;
            gameObject.name = $"{kind}_{guid.ToString().Substring(0, 8)}";
        }

        public void SetWorldPosition(Vector3 world)
        {
            transform.position = world;
        }

        /// <summary>
        /// Cancela la animación de path en curso (si la hay) dejando al pawn donde está.
        /// </summary>
        /// <remarks>
        /// BUG-021: la coroutine de <see cref="AnimatePath"/> recalcula
        /// <c>grid.GridToWorld(next)</c> por step. Al cruzar una sala, LoadRoom cambia el
        /// GridOrigin y los steps restantes del path viejo se remapean al espacio de la
        /// sala nueva — el pawn "seguía de largo" hasta la puerta siguiente.
        /// </remarks>
        public void StopMovement()
        {
            // Incondicional (antes del guard): StopCoroutine no corre el cierre de la
            // corutina, así que este es el único lugar que garantiza que el pawn no quede
            // corriendo en el lugar cuando se le corta el path.
            SetMovementAnim(false);

            // Un hard-stop pisa cualquier soft-stop pendiente (cruce de sala, snap).
            _stopAtStepEnd = false;
            _onStoppedAtStep = null;

            if (_moveAnim == null) return;
            StopCoroutine(_moveAnim);
            _moveAnim = null;
        }

        private bool _stopAtStepEnd;
        private Action<GridCoord> _onStoppedAtStep;

        /// <summary>
        /// Soft-stop: pide frenar la caminata al COMPLETAR el step en curso, con el
        /// pawn parado en una celda exacta. <paramref name="onStopped"/> recibe esa
        /// celda ANTES del resync final (BUG-069) — es la ventana para truncar la
        /// posición lógica del grid, que <c>MovementService.Move</c> ya adelantó al
        /// destino. Devuelve <c>false</c> si no hay caminata que frenar (no está en
        /// movimiento, locomoción Blink, o EditMode donde el path snapea).
        /// </summary>
        public bool RequestStopAtStepEnd(Action<GridCoord> onStopped)
        {
            if (!IsMoving || !Application.isPlaying) return false;
            if (_locomotion == LocomotionStyle.Blink) return false;

            _stopAtStepEnd = true;
            _onStoppedAtStep = onStopped;
            return true;
        }

        /// <summary>
        /// Celdas que ocupa la entidad (ancho × alto desde el ancla). Solo afecta dónde se
        /// dibuja el pawn: un 2×2 se centra entre sus cuatro celdas en vez de pararse en el ancla.
        /// </summary>
        public Vector2Int Footprint { get; private set; } = Vector2Int.one;

        public void SetFootprint(Vector2Int footprint) => Footprint = GridFootprint.Normalize(footprint);

        /// <summary>Posición del pawn para una celda lógica: centro del rectángulo + alto del pawn.</summary>
        private Vector3 WorldFor(IGridManager grid, GridCoord anchor)
        {
            var pos = grid.GridToWorld(anchor) + GridFootprint.CenterOffset(Footprint, grid.TileSize);
            pos.y += PawnYOffset;
            return pos;
        }

        public void SnapToGrid(IGridManager grid, GridCoord coord)
        {
            if (grid == null) return;
            // Un snap es posición autoritativa: cualquier path en vuelo quedó obsoleto.
            StopMovement();
            transform.position = WorldFor(grid, coord);
        }

        /// <summary>
        /// Setea instantáneamente la rotación del pawn a la cardinal dada. Sin lerp —
        /// en pixel art con cámara iso fija, los lerps intermedios pueden generar frames
        /// "borrosos" que rompen la estética (TECHNICAL.md §17.E shader pixel art).
        /// </summary>
        public void SetFacing(Cardinal facing)
        {
            Facing = facing;
            transform.rotation = facing.ToRotation();
        }

        /// <summary>
        /// Conveniencia: deriva la cardinal dominante del vector <paramref name="from"/> →
        /// <paramref name="to"/> y aplica. Si el delta es cero, no hace nada (preserva
        /// el facing previo).
        /// </summary>
        public void FaceCoord(GridCoord from, GridCoord to)
        {
            if (from == to) return;
            SetFacing(CardinalExtensions.FromDelta(from, to, Facing));
        }

        /// <summary>
        /// Anima al pawn caminando casilla-a-casilla por <paramref name="path"/>. Cada step
        /// se mueve via lerp lineal en <paramref name="secondsPerStep"/> y rota el facing
        /// al inicio del segmento.
        /// <para>
        /// Si <paramref name="movement"/> está provisto, antes de cada step revisa si el
        /// próximo tile fue ocupado por otra entidad mientras tanto y recalcula el path
        /// (A*) para rodear el obstáculo. Útil cuando otra entidad se mueve mid-animación.
        /// </para>
        /// <para>
        /// Cancela cualquier animación en curso. En EditMode (sin coroutines), o si el
        /// path tiene menos de 2 nodos, snapea al destino directamente — esto preserva
        /// los tests EditMode existentes que esperan la posición final inmediata.
        /// </para>
        /// </summary>
        public void AnimatePath(
            IGridManager grid,
            IReadOnlyList<GridCoord> path,
            float secondsPerStep = DefaultSecondsPerStep,
            IMovementService movement = null)
        {
            if (grid == null || path == null || path.Count == 0) return;

            StopMovement();

            // Sin coroutines (EditMode) o path trivial → snap al destino y listo.
            if (!Application.isPlaying || path.Count < 2)
            {
                SnapToGrid(grid, path[path.Count - 1]);
                return;
            }

            SetMovementAnim(true);

            if (_locomotion == LocomotionStyle.Blink)
            {
                _moveAnim = StartCoroutine(BlinkCoroutine(grid, path[0], path[path.Count - 1]));
                return;
            }

            _moveAnim = StartCoroutine(AnimatePathCoroutine(grid, path, Mathf.Max(0.01f, secondsPerStep), movement));
        }

        /// <summary>
        /// Desplazamiento por teletransporte: se queda en el origen mientras el clip lo hace
        /// desaparecer, salta al destino, y se queda mientras el clip lo hace aparecer.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>No recorre el camino.</b> Va del origen al destino y nada más — pasar por las casillas
        /// intermedias es justamente lo que no hace un teletransporte. La posición lógica ya está en
        /// el destino (la setea <c>MovementService.Move</c>), así que saltarse el path no desincroniza
        /// nada.
        /// </para>
        /// <para>
        /// <b>Por qué existe.</b> Hay rigs cuyo clip de movimiento ES un teletransporte —
        /// <c>Anim_Healer_Teleport_1/_2</c> y <c>Anim_SunkedGrand_Teleport_1/_2</c>, un tramo para
        /// desaparecer y otro para aparecer. Con el lerp de <see cref="AnimatePathCoroutine"/> el
        /// cuerpo se deslizaba suave mientras la animación decía "me desvanecí", que es la peor
        /// combinación posible: ni se lee como caminata ni como salto.
        /// </para>
        /// <para>
        /// <b>Tampoco hay recalc por bloqueo.</b> El de la caminata existe porque el pawn puede
        /// chocarse con alguien que se movió a mitad del trayecto; acá no hay trayecto que rodear.
        /// </para>
        /// </remarks>
        private IEnumerator BlinkCoroutine(IGridManager grid, GridCoord from, GridCoord to)
        {
            FaceCoord(from, to);

            if (_blinkOutSeconds > 0f) yield return new WaitForSeconds(_blinkOutSeconds);

            transform.position = WorldFor(grid, to);

            if (_blinkInSeconds > 0f) yield return new WaitForSeconds(_blinkInSeconds);

            SetMovementAnim(false);
            _moveAnim = null;
        }

        private IEnumerator AnimatePathCoroutine(
            IGridManager grid,
            IReadOnlyList<GridCoord> initialPath,
            float secondsPerStep,
            IMovementService movement)
        {
            // Copiamos a List para poder reemplazar el path al recalcular sin tocar el
            // IReadOnlyList del caller. El destino original es path[Count-1] — lo guardamos
            // por si recalculamos varias veces (mantenemos el target).
            var path = new List<GridCoord>(initialPath);
            var destination = path[path.Count - 1];

            int i = 1;
            while (i < path.Count)
            {
                var prev = path[i - 1];
                var next = path[i];

                // Recalc on block: si el próximo tile fue ocupado por otra entidad mientras
                // animábamos, intentar rodear. Si no hay alternativa, abortamos en prev.
                if (movement != null && IsBlockedByOther(grid, next))
                {
                    var rerouted = movement.FindPath(prev, destination);
                    if (rerouted == null || rerouted.Count < 2)
                    {
                        // No hay forma de seguir — paramos acá. La posición lógica en grid
                        // sigue apuntando al destino original (lo setea MovementService.Move),
                        // pero visualmente quedamos atascados; el siguiente OnEntityMoved del
                        // mismo guid resincroniza si hace falta.
                        break;
                    }
                    path = rerouted;
                    i = 1;
                    continue;
                }

                FaceCoord(prev, next);

                Vector3 startPos = transform.position;
                Vector3 endPos = WorldFor(grid, next);

                float elapsed = 0f;
                while (elapsed < secondsPerStep)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / secondsPerStep);
                    transform.position = Vector3.Lerp(startPos, endPos, t);
                    yield return null;
                }
                transform.position = endPos;
                i++;

                // Soft-stop (cancel del jugador): frenamos con el pawn parado en una
                // celda exacta. El callback reconcilia la posición lógica del grid
                // ANTES del resync BUG-069 de abajo — si la truncación funcionó, ese
                // resync es no-op; si falló, snapea al destino lógico (sin desync).
                if (_stopAtStepEnd)
                {
                    _stopAtStepEnd = false;
                    var callback = _onStoppedAtStep;
                    _onStoppedAtStep = null;
                    callback?.Invoke(next);
                    break;
                }
            }

            // BUG-069: si el loop terminó por 'break' (reroute fallido), el transform se quedó
            // en 'prev' mientras la posición lógica en el grid YA está en el destino — la setea
            // MovementService.Move de forma síncrona antes de animar, no esta corutina. Sin este
            // resync el pawn se ve "atascado" hasta el próximo OnEntityMoved del mismo guid. En
            // el camino feliz es un no-op: el último endPos del loop ya coincide con
            // GridToWorld(destination).
            if (grid.TryGetPosition(EntityGuid, out var logicalCoord))
            {
                transform.position = WorldFor(grid, logicalCoord);
            }

            SetMovementAnim(false);
            _moveAnim = null;
        }

        /// <summary>
        /// Prende/apaga el bool <see cref="MovementParam"/> del Animator del modelo.
        /// No-op si el pawn no tiene Animator (primitives del FP) o si su controller no
        /// declara el param — setearlo igual haría que Unity logueara un warning por step.
        /// </summary>
        private void SetMovementAnim(bool moving)
        {
            ResolveAnimator();
            if (_animator == null || !_hasMovementParam) return;
            _animator.SetBool(MovementParam, moving);
        }

        /// <summary>
        /// Dispara un Trigger del Animator del modelo (ej. "Awaken" del Mimic al
        /// activarse). No-op que devuelve false si el pawn no tiene Animator o su
        /// controller no declara el param como Trigger.
        /// </summary>
        public bool TrySetTrigger(string trigger)
        {
            ResolveAnimator();
            if (_animator == null
                || !HasParam(_animator, trigger, AnimatorControllerParameterType.Trigger))
                return false;
            _animator.SetTrigger(trigger);
            return true;
        }

        private void ResolveAnimator()
        {
            if (_animatorResolved) return;
            _animatorResolved = true;
            // El Animator vive en el hijo del modelo rigeado, no en la raíz del pawn.
            // Mismo cuidado con el fake-null que FeedbackManager.ResolveAnimator: con
            // `??` el fallback al hijo no dispara porque GetComponent devuelve un
            // UnityEngine.Object "null" que no es null para el operador.
            var own = GetComponent<Animator>();
            _animator = own != null ? own : GetComponentInChildren<Animator>(includeInactive: true);
            _hasMovementParam = HasParam(_animator, MovementParam, AnimatorControllerParameterType.Bool);
        }

        private static bool HasParam(Animator animator, string param, AnimatorControllerParameterType type)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;

            var parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == type && parameters[i].name == param)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True si <paramref name="coord"/> está ocupado por una entidad distinta al pawn
        /// actual. El propio pawn aparece como ocupante en <paramref name="grid"/> mientras
        /// está en su tile, así que filtramos por <see cref="EntityGuid"/>.
        /// </summary>
        private bool IsBlockedByOther(IGridManager grid, GridCoord coord)
        {
            if (!grid.IsOccupied(coord)) return false;
            if (!grid.TryGetOccupant(coord, out var occupant)) return true;
            return occupant != EntityGuid;
        }

        public IEnumerator WaitUntilMoveComplete(float timeout = 10f)
        {
            float deadline = Time.time + timeout;
            while (_moveAnim != null && Time.time < deadline)
                yield return null;
        }

        public enum PawnKind { Hero, Enemy, Boss, Prop }

        /// <summary>Cómo se ve moverse este pawn.</summary>
        public enum LocomotionStyle
        {
            /// <summary>Lerp casilla a casilla por el camino. El default de todo el bestiario.</summary>
            Walk,

            /// <summary>
            /// Desaparece en el origen y aparece en el destino, sin recorrer el camino. Para los
            /// rigs cuyo clip de movimiento es un teletransporte (Healer, Sunked Grand).
            /// </summary>
            Blink,
        }
    }
}
