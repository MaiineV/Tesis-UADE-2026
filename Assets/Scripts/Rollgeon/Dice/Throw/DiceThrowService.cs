using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.DiceBlock;
using UnityEngine;

namespace Rollgeon.Dice.Throw
{
    /// <summary>
    /// Implementación de <see cref="IDiceThrowService"/>. Pura C# (sin MonoBehaviour),
    /// registrada en el ServiceLocator por <see cref="DiceThrowServiceBootstrap"/>
    /// (scope Run). Ver el remarks de la interfaz para el contrato general.
    /// </summary>
    /// <remarks>
    /// Detalles de resolución:
    /// <list type="bullet">
    /// <item><b>Rig (DevConsole):</b> <c>RiggedRollState</c> se consume DENTRO de
    /// <c>DiceRoller.RollAll</c>, así que se snapshotea <c>HasPending</c> ANTES del
    /// roll — es la única forma de saber que la tirada fue riggeada. En 3D una tirada
    /// riggeada ignora la lectura física (mandan las caras del roller).</item>
    /// <item><b>No-d6 en 3D:</b> el cubo placeholder no representa d4/d8/etc — esos
    /// índices usan la cara precalculada aunque el modo sea 3D.</item>
    /// <item><b>Sin presenter ⇒ instantáneo:</b> elimina el soft-lock por diseño
    /// (AI, tests, escenas sin la UI de throw).</item>
    /// </list>
    /// </remarks>
    public sealed class DiceThrowService : IDiceThrowService
    {
        private readonly DiceThrowStateMachine _machine = new DiceThrowStateMachine();
        private readonly DiceThrowSettingsSO _settings;

        private DiceThrowMode _mode;
        private object _presenter;

        // ---- Estado de la sesión diferida ----
        private int[] _pendingFaces;
        private int[] _physicalFaces;
        private int[] _maxFaces;
        private bool[] _thrownMask;
        private bool _wasRigged;
        private Action<int[]> _onRevealed;
        private Guid _playerGuid;
        private DiceThrowPhase _lastPhase = DiceThrowPhase.Idle;

        public DiceThrowService(DiceThrowSettingsSO settings = null)
        {
            _settings = settings;
            _mode = settings != null ? settings.DefaultMode : DiceThrowMode.Classic;
        }

        public DiceThrowMode Mode => _mode;

        public bool SetMode(DiceThrowMode mode)
        {
            if (IsBusy) return false;
            _mode = mode;
            return true;
        }

        public DiceThrowPhase Phase => _machine.Phase;

        public bool IsBusy => _machine.IsActive;

        public bool WillDefer => _mode != DiceThrowMode.Classic && _presenter != null;

        public Guid ActivePlayerGuid => _playerGuid;

        public DiceThrowSettingsSO Settings => _settings;

        public event Action<DiceThrowPhase> OnPhaseChanged;
        public event Action OnSessionStarted;
        public event Action OnGrabCancelled;
        public event Action OnSessionAborted;

        public Func<bool[], bool> GrabRerollHandler { get; set; }

        public bool CanGrabReroll => !IsBusy && GrabRerollHandler != null && WillDefer;

        // ---- Lado caller -----------------------------------------------------

        public void RequestRoll(Guid playerGuid, DiceBagSO bag, Action<int[]> onRevealed)
        {
            if (IsBusy)
            {
                Debug.LogWarning("[DiceThrowService] RequestRoll con sesión en curso — ignorado.");
                return;
            }

            bool wasRigged = SnapshotRig();
            var faces = ResolveRoller().RollAll(bag);
            var thrown = BuildThrownMaskFromBlocks(faces.Length);
            StartOrResolve(playerGuid, bag, faces, thrown, wasRigged, onRevealed);
        }

        public void RequestReroll(Guid playerGuid, DiceBagSO bag, int[] previousFaces,
            bool[] keep, Action<int[]> onRevealed)
        {
            if (IsBusy)
            {
                Debug.LogWarning("[DiceThrowService] RequestReroll con sesión en curso — ignorado.");
                return;
            }

            bool wasRigged = SnapshotRig();
            var faces = ResolveRoller().Reroll(bag, previousFaces, keep);

            // En reroll los kept no participan del throw — conservan cara y slot.
            var thrown = new bool[faces.Length];
            for (int i = 0; i < faces.Length; i++)
                thrown[i] = !(keep != null && i < keep.Length && keep[i]);

            StartOrResolve(playerGuid, bag, faces, thrown, wasRigged, onRevealed);
        }

        public void Abort()
        {
            if (!IsBusy) return;
            ClearSession();
            OnSessionAborted?.Invoke();
            EmitPhaseIfChanged();
        }

        // ---- Lado presenter --------------------------------------------------

        public void AttachPresenter(object owner)
        {
            _presenter = owner;
        }

        public void DetachPresenter(object owner)
        {
            if (!ReferenceEquals(_presenter, owner)) return;
            _presenter = null;
            // Presenter muerto no puede completar el reveal — abortar evita el soft-lock.
            if (IsBusy) Abort();
        }

        public IReadOnlyList<bool> ThrownMask => _thrownMask ?? Array.Empty<bool>();

        public DieThrowState GetDieState(int index) => _machine.GetState(index);

        public int PeekPendingFace(int index)
        {
            if (_pendingFaces == null || index < 0 || index >= _pendingFaces.Length) return -1;
            return _pendingFaces[index];
        }

        public bool TryGrab(int index)
        {
            // Con todos asentados el presenter ya está alineando — no se re-agarra.
            if (_machine.AllSettled) return false;
            bool ok = _machine.TryGrab(index);
            if (ok) EmitPhaseIfChanged();
            return ok;
        }

        public IReadOnlyList<int> CancelGrab()
        {
            var cancelled = _machine.CancelGrab();
            if (cancelled.Count > 0)
            {
                OnGrabCancelled?.Invoke();
                EmitPhaseIfChanged();
            }
            return cancelled;
        }

        public IReadOnlyList<int> ReleaseGrabbed()
        {
            var released = _machine.ReleaseGrabbed();
            if (released.Count > 0) EmitPhaseIfChanged();
            return released;
        }

        public void NotifyDieSettled(int index, int physicalFace = -1)
        {
            if (!_machine.MarkSettled(index)) return;
            if (_physicalFaces != null && index >= 0 && index < _physicalFaces.Length)
                _physicalFaces[index] = physicalFace;
            EmitPhaseIfChanged();
        }

        public void CompleteReveal()
        {
            if (!IsBusy || !_machine.AllSettled)
            {
                Debug.LogWarning("[DiceThrowService] CompleteReveal sin sesión asentada — ignorado.");
                return;
            }

            var final = (int[])_pendingFaces.Clone();

            // 3D: la física dicta el resultado — salvo tiradas riggeadas (manda el rig,
            // el presenter snap-rotea el cubo) y dados no-d6 (el cubo no los representa).
            if (_mode == DiceThrowMode.ThreeD && !_wasRigged && _physicalFaces != null)
            {
                for (int i = 0; i < final.Length; i++)
                {
                    if (_thrownMask == null || i >= _thrownMask.Length || !_thrownMask[i]) continue;
                    if (_maxFaces != null && i < _maxFaces.Length && _maxFaces[i] != 6) continue;
                    if (_physicalFaces[i] > 0) final[i] = _physicalFaces[i];
                }
            }

            var callback = _onRevealed;
            var guid = _playerGuid;
            ClearSession();
            Reveal(guid, final, callback);
            EmitPhaseIfChanged();
        }

        // ---- Internals ---------------------------------------------------------

        private void StartOrResolve(Guid playerGuid, DiceBagSO bag, int[] faces,
            bool[] thrown, bool wasRigged, Action<int[]> onRevealed)
        {
            if (!WillDefer)
            {
                // Camino instantáneo: byte-idéntico al legacy (callback → evento).
                Reveal(playerGuid, faces, onRevealed);
                return;
            }

            _playerGuid = playerGuid;
            _pendingFaces = faces;
            _physicalFaces = new int[faces.Length];
            _thrownMask = thrown;
            _wasRigged = wasRigged;
            _onRevealed = onRevealed;

            _maxFaces = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++)
            {
                _maxFaces[i] = (bag != null && bag.Dice != null && i < bag.Dice.Count)
                    ? bag.Dice[i].MaxFace()
                    : 6;
            }

            var excluded = new bool[thrown.Length];
            for (int i = 0; i < thrown.Length; i++) excluded[i] = !thrown[i];
            _machine.Begin(faces.Length, excluded);

            OnSessionStarted?.Invoke();
            EmitPhaseIfChanged();
        }

        // El reveal es el único contrato con el resto del juego: primero el callback
        // (el caller setea su _lastFaces/_currentRoll), después el evento — mismo
        // orden que el código legacy que este servicio reemplaza.
        private static void Reveal(Guid playerGuid, int[] faces, Action<int[]> callback)
        {
            callback?.Invoke(faces);
            EventManager.Trigger(EventName.OnDiceRolled, playerGuid, (IReadOnlyList<int>)faces);
        }

        private void ClearSession()
        {
            _machine.Reset();
            _pendingFaces = null;
            _physicalFaces = null;
            _maxFaces = null;
            _thrownMask = null;
            _wasRigged = false;
            _onRevealed = null;
            _playerGuid = Guid.Empty;
        }

        private void EmitPhaseIfChanged()
        {
            var phase = _machine.Phase;
            if (phase == _lastPhase) return;
            _lastPhase = phase;
            OnPhaseChanged?.Invoke(phase);
        }

        private static IDiceRoller ResolveRoller()
        {
            ServiceLocator.TryGetService<IDiceRoller>(out var roller);
            if (roller == null)
                throw new InvalidOperationException(
                    "[DiceThrowService] IDiceRoller no registrado — el bootstrap del roller corre antes (Priority 72/80).");
            return roller;
        }

        private static bool SnapshotRig()
        {
            return ServiceLocator.TryGetService<RiggedRollState>(out var rig)
                   && rig != null && rig.HasPending;
        }

        private static bool[] BuildThrownMaskFromBlocks(int count)
        {
            var thrown = new bool[count];
            for (int i = 0; i < count; i++) thrown[i] = true;

            if (ServiceLocator.TryGetService<IDiceBlockService>(out var blocks) && blocks != null)
            {
                foreach (var idx in blocks.BlockedIndices)
                    if (idx >= 0 && idx < count) thrown[idx] = false;
            }
            return thrown;
        }
    }
}
