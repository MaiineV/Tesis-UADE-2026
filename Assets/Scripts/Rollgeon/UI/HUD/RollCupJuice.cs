using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Coreografía del vaso de generala del pool de rolls: bob idle, shake al
    /// gastar, flip a boca abajo al vaciarse y flip inverso al recuperar.
    /// Separación view/juice (patrón ChestReveal): <see cref="RollCupView"/> es
    /// dueña del estado; acá vive solo el movimiento. Ownership de canales
    /// (política DiceBoardSkinJuice): bob = posición Y, shake = rotación
    /// alrededor del reposo, flip = ángulo Z por float (un quaternion no
    /// codifica dirección en arcos de 180°), squash/punch = escala.
    /// Interrupciones: latest-wins — se frena todo y se parte de la pose
    /// objetivo (Tween.Stop congela valores, por eso siempre se rebasa).
    /// Todos los campos son opcionales: sin wiring, no-op.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Roll Cup Juice")]
    public sealed class RollCupJuice : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Optional, Tooltip("RectTransform del vaso (pivot centrado — el flip gira sobre su centro).")]
        private RectTransform _cup;

        [SerializeField, Optional, Tooltip("Burst de partículas UI (pool de Images). Sin wiring, sin partículas.")]
        private DiceThrowImpactBurst _burst;

        [Title("Sprites — swap a mitad del giro")]
        [SerializeField, Optional, Tooltip("Image del vaso. Sin wiring (o sin sprite Flip) el vaso gira con un solo dibujo.")]
        private Image _cupImage;

        [SerializeField, Optional, Tooltip("Vaso parado (VasoGenerala_0). Si queda vacío se toma el sprite que tenga la Image al arrancar.")]
        private Sprite _uprightSprite;

        [SerializeField, Optional, Tooltip("Vaso boca abajo (VasoGeneralaFlip_0, ya dibujado invertido). " +
                 "Se muestra a partir de la mitad del giro (90°→270°) con la rotación compensada.")]
        private Sprite _faceDownSprite;

        [Title("Bob idle")]
        [SerializeField, MinValue(0f), Tooltip("Amplitud (px) del bobbing en reposo.")]
        private float _bobAmplitude = 1.5f;

        [SerializeField, MinValue(0.1f), Tooltip("Período completo (s) del bobbing.")]
        private float _bobPeriod = 1.1f;

        [Title("Shake — gasto de roll")]
        [SerializeField, MinValue(0f), Tooltip("Amplitud (grados Z) del traqueteo del vaso.")]
        private float _shakeDegrees = 10f;

        [SerializeField, MinValue(0f), Tooltip("Duración (s) del traqueteo.")]
        private float _shakeSeconds = 0.28f;

        [SerializeField, MinValue(1f), Tooltip("Frecuencia (Hz) del traqueteo.")]
        private float _shakeFrequency = 14f;

        [SerializeField, Range(0f, 1f), Tooltip("Intensidad del burst hacia arriba al gastar un roll.")]
        private float _spendBurstIntensity = 0.35f;

        [Title("Flip down — sin rolls (deliberado y pesado)")]
        [SerializeField, MinValue(0f), Tooltip("Altura (px) a la que sube antes de girar.")]
        private float _flipRiseHeight = 28f;

        [SerializeField, MinValue(0f)] private float _flipRiseSeconds = 0.22f;
        [SerializeField, MinValue(0f)] private float _flipSpinSeconds = 0.28f;
        [SerializeField, MinValue(0f)] private float _flipFallSeconds = 0.22f;

        [SerializeField, Tooltip("Squash al apoyarse boca abajo (x ensancha, y aplasta).")]
        private Vector2 _landSquash = new Vector2(1.08f, 0.90f);

        [SerializeField, MinValue(0f)] private float _landSquashInSeconds = 0.06f;
        [SerializeField, MinValue(0f)] private float _landSquashOutSeconds = 0.10f;

        [SerializeField, Range(0f, 1f), Tooltip("Intensidad del burst de 'polvo' hacia abajo al apoyarse.")]
        private float _landBurstIntensity = 0.25f;

        [Title("Flip up — recupero desde 0 (rápido y elástico)")]
        [SerializeField, MinValue(0f), Tooltip("Hundida de anticipación (px) antes del pop — 'agarrar el vaso'.")]
        private float _recoverDipPixels = 4f;

        [SerializeField, MinValue(0f)] private float _recoverDipSeconds = 0.08f;

        [SerializeField, MinValue(0f), Tooltip("Altura (px) del pop — más alto que el flip-down a propósito.")]
        private float _recoverPopHeight = 40f;

        [SerializeField, MinValue(0f)] private float _recoverPopSeconds = 0.18f;
        [SerializeField, MinValue(0f)] private float _recoverSpinSeconds = 0.20f;
        [SerializeField, MinValue(0f)] private float _recoverFallSeconds = 0.16f;

        [SerializeField, Range(0f, 0.5f), Tooltip("Punch de escala al aterrizar parado.")]
        private float _recoverPunchScale = 0.15f;

        [SerializeField, Range(0f, 1f), Tooltip("Intensidad del burst grande hacia arriba — 'la energía volvió'.")]
        private float _recoverBurstIntensity = 0.7f;

        [Title("Recover sutil (con rolls restantes)")]
        [SerializeField, Range(0f, 0.5f), Tooltip("Punch de escala del grant por turno cuando el vaso ya está parado.")]
        private float _recoverSmallPunch = 0.08f;

        [SerializeField, MinValue(0f), Tooltip("Offset (px) desde el centro del vaso hasta la boca, para los bursts.")]
        private float _mouthOffsetY = 30f;

        private Vector2 _restPos;
        private bool _restCaptured;
        private bool _targetFaceDown;
        private RollCupTransition _pendingChain = RollCupTransition.None;

        private Tween _bob;
        private Tween _shake;
        private Tween _spin;
        private Tween _transient;
        private Tween _scaleTween;

        private static bool Active => Application.isPlaying;
        private static bool Motion => !DiceAnim.DiceUiMotionPrefs.ReducedMotion;
        private bool Particles => Active && Motion && _burst != null;

        private void Awake()
        {
            // Reposo capturado una sola vez con el vaso quieto — capturarlo a
            // mitad de tween correría la pose acumulativamente.
            CaptureRest();
            // El sprite de autoría de la Image es el vaso parado: sirve de default
            // para no exigir cablear lo que el prefab ya sabe.
            if (_uprightSprite == null && _cupImage != null) _uprightSprite = _cupImage.sprite;
        }

        private void OnDisable()
        {
            StopAll();
            SnapPose();
        }

        // ================================================================
        // API (la llama RollCupView; null-safe y fire-and-forget)
        // ================================================================

        /// <summary>Pose sin coreografía: primer fetch, reentrada a combate o reduced motion.</summary>
        public void SetPoseInstant(bool faceDown)
        {
            if (_cup == null) return;
            _targetFaceDown = faceDown;
            StopAll();
            SnapPose();
            if (!faceDown) StartBob();
        }

        public void OnTransition(RollCupTransition transition, bool faceDownTarget)
        {
            if (_cup == null) return;
            _targetFaceDown = faceDownTarget;

            if (!Active || !Motion)
            {
                StopAll();
                SnapPose();
                return;
            }

            switch (transition)
            {
                case RollCupTransition.Spend:
                    PlaySpendShake(chainFlip: false);
                    break;

                case RollCupTransition.SpendToEmpty:
                    PlaySpendShake(chainFlip: true);
                    break;

                case RollCupTransition.RecoverFromEmpty:
                    PlayFlipUp();
                    break;

                case RollCupTransition.Recover:
                    PlaySmallPunch();
                    break;

                default:
                    SetPoseInstant(faceDownTarget);
                    break;
            }
        }

        /// <summary>Corte al ocultarse el vaso (fuera de combate): frena todo y deja la pose limpia.</summary>
        public void StopAndRest()
        {
            _targetFaceDown = false;
            StopAll();
            SnapPose();
        }

        /// <summary>
        /// Shake del rechazo "no te alcanza" — típicamente con el vaso boca
        /// abajo, por eso sacude alrededor del reposo actual (0 o 180).
        /// </summary>
        public void PlayInsufficientShake()
        {
            if (_cup == null || !Active || !Motion) return;
            if (_shake.isAlive) return; // spam de rechazos: un shake por vez
            RebaseRotation();
            _shake = Tween.ShakeLocalRotation(_cup, new Vector3(0f, 0f, _shakeDegrees),
                _shakeSeconds, _shakeFrequency, useUnscaledTime: true);
        }

        // ================================================================
        // Coreografías
        // ================================================================

        private void PlaySpendShake(bool chainFlip)
        {
            EmitBurst(_mouthOffsetY, Vector2.up, _spendBurstIntensity);

            if (chainFlip)
            {
                // Reinicio limpio aunque hubiera un shake de un Spend anterior:
                // el flip se encadena en el OnComplete y un shake ya corriendo
                // no tiene esa continuación. El shake ocurre todavía parado —
                // aunque el target lógico ya sea boca abajo, ahí lo lleva el flip.
                _pendingChain = RollCupTransition.SpendToEmpty;
                if (_shake.isAlive) _shake.Stop();
                RebaseRotationTo(RollCupMath.UprightZ);
                _shake = Tween.ShakeLocalRotation(_cup, new Vector3(0f, 0f, _shakeDegrees),
                        _shakeSeconds, _shakeFrequency, useUnscaledTime: true)
                    .OnComplete(this, self => self.OnSpendShakeFinished());
                return;
            }

            _pendingChain = RollCupTransition.None;
            if (_shake.isAlive) return; // spend spam: el traqueteo en curso alcanza
            RebaseRotationTo(RollCupMath.UprightZ);
            _shake = Tween.ShakeLocalRotation(_cup, new Vector3(0f, 0f, _shakeDegrees),
                _shakeSeconds, _shakeFrequency, useUnscaledTime: true);
        }

        private void OnSpendShakeFinished()
        {
            // Frontera shake→flip: un recupero pudo colarse durante el shake y
            // entonces la pose objetivo ya no es boca abajo — no hay flip.
            if (!RollCupMath.ShouldChainFlipDown(_pendingChain, _targetFaceDown)) return;
            _pendingChain = RollCupTransition.None;
            PlayFlipDown();
        }

        private void PlayFlipDown()
        {
            StopBob();
            RebasePosition();
            RebaseRotationTo(RollCupMath.UprightZ);

            _transient = Tween.UIAnchoredPositionY(_cup, _restPos.y, _restPos.y + _flipRiseHeight,
                    _flipRiseSeconds, Ease.OutCubic, useUnscaledTime: true)
                .OnComplete(this, self => self.FlipDownSpin());
        }

        private void FlipDownSpin()
        {
            _transient = Tween.Custom(RollCupMath.UprightZ, RollCupMath.FaceDownZ, _flipSpinSeconds,
                    onValueChange: SetCupAngle, ease: Ease.InOutSine, useUnscaledTime: true)
                .OnComplete(this, self => self.FlipDownFall());
        }

        private void FlipDownFall()
        {
            _transient = Tween.UIAnchoredPositionY(_cup, _restPos.y + _flipRiseHeight, _restPos.y,
                    _flipFallSeconds, Ease.InCubic, useUnscaledTime: true)
                .OnComplete(this, self => self.FlipDownLand());
        }

        private void FlipDownLand()
        {
            EmitBurst(-_mouthOffsetY, Vector2.down, _landBurstIntensity);
            _scaleTween = Tween.Scale(_cup, new Vector3(_landSquash.x, _landSquash.y, 1f),
                    _landSquashInSeconds, Ease.OutQuad, useUnscaledTime: true)
                .OnComplete(this, self => self._scaleTween = Tween.Scale(self._cup, Vector3.one,
                    self._landSquashOutSeconds, Ease.OutQuad, useUnscaledTime: true));
        }

        private void PlayFlipUp()
        {
            StopAll();
            RebasePosition();
            RebaseRotationTo(RollCupMath.FaceDownZ);

            _transient = Tween.UIAnchoredPositionY(_cup, _restPos.y, _restPos.y - _recoverDipPixels,
                    _recoverDipSeconds, Ease.OutQuad, useUnscaledTime: true)
                .OnComplete(this, self => self.FlipUpPop());
        }

        private void FlipUpPop()
        {
            // Pop y giro en paralelo. El giro continúa hacia 360 (mismo sentido
            // que el flip-down): el vaso completa la vuelta, no la rebobina.
            _spin = Tween.Custom(RollCupMath.FaceDownZ, RollCupMath.FlipUpToZ, _recoverSpinSeconds,
                onValueChange: SetCupAngle, ease: Ease.OutBack, useUnscaledTime: true);
            _transient = Tween.UIAnchoredPositionY(_cup, _restPos.y - _recoverDipPixels,
                    _restPos.y + _recoverPopHeight, _recoverPopSeconds, Ease.OutBack, useUnscaledTime: true)
                .OnComplete(this, self => self.FlipUpFall());
        }

        private void FlipUpFall()
        {
            _transient = Tween.UIAnchoredPositionY(_cup, _restPos.y + _recoverPopHeight, _restPos.y,
                    _recoverFallSeconds, Ease.InQuad, useUnscaledTime: true)
                .OnComplete(this, self => self.FlipUpLand());
        }

        private void FlipUpLand()
        {
            SetCupAngle(RollCupMath.UprightZ);
            EmitBurst(_mouthOffsetY, Vector2.up, _recoverBurstIntensity);
            if (_recoverPunchScale > 0f)
            {
                _scaleTween = Tween.PunchScale(_cup, strength: Vector3.one * _recoverPunchScale,
                    duration: 0.2f, useUnscaledTime: true);
            }
            StartBob();
        }

        private void PlaySmallPunch()
        {
            // Grant de fin de turno con rolls restantes: no compite con el flujo
            // de End Turn — solo un latido de escala, el bob sigue siendo dueño
            // de la posición.
            if (_recoverSmallPunch <= 0f || _scaleTween.isAlive) return;
            _scaleTween = Tween.PunchScale(_cup, strength: Vector3.one * _recoverSmallPunch,
                duration: 0.25f, useUnscaledTime: true);
        }

        // ================================================================
        // Canales y pose
        // ================================================================

        private void StartBob()
        {
            if (!Active || !Motion || _cup == null) return;
            if (_bob.isAlive) return;
            RebasePosition();
            _bob = Tween.UIAnchoredPositionY(_cup, _restPos.y - _bobAmplitude, _restPos.y + _bobAmplitude,
                _bobPeriod * 0.5f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo,
                useUnscaledTime: true);
        }

        private void StopBob()
        {
            if (_bob.isAlive) _bob.Stop();
        }

        private void StopAll()
        {
            StopBob();
            if (_shake.isAlive) _shake.Stop();
            if (_spin.isAlive) _spin.Stop();
            if (_transient.isAlive) _transient.Stop();
            if (_scaleTween.isAlive) _scaleTween.Stop();
            _pendingChain = RollCupTransition.None;
        }

        /// <summary>Pose objetivo exacta — Tween.Stop congela valores, esto los repara.</summary>
        private void SnapPose()
        {
            if (_cup == null) return;
            CaptureRest();
            _cup.anchoredPosition = _restPos;
            _cup.localScale = Vector3.one;
            SetCupAngle(_targetFaceDown ? RollCupMath.FaceDownZ : RollCupMath.UprightZ);
        }

        /// <summary>
        /// Único punto de escritura de la rotación: <paramref name="z"/> es el
        /// ángulo LÓGICO (0 parado, 180 boca abajo, 360 vuelta completa). Acá se
        /// decide qué dibujo va y cuánto se compensa — el shake trabaja sobre la
        /// rotación física resultante, así que no necesita saber de sprites.
        /// </summary>
        private void SetCupAngle(float z)
        {
            if (_cup == null) return;

            bool flipShown = CanSwapSprite && RollCupMath.ShowsFlipSprite(z);
            if (CanSwapSprite)
            {
                var wanted = flipShown ? _faceDownSprite : _uprightSprite;
                if (_cupImage.sprite != wanted) _cupImage.sprite = wanted;
            }

            _cup.localEulerAngles = new Vector3(0f, 0f, RollCupMath.VisualZ(z, flipShown));
        }

        private bool CanSwapSprite => _cupImage != null && _uprightSprite != null && _faceDownSprite != null;

        private void RebasePosition()
        {
            CaptureRest();
            _cup.anchoredPosition = _restPos;
        }

        private void RebaseRotation()
        {
            SetCupAngle(_targetFaceDown ? RollCupMath.FaceDownZ : RollCupMath.UprightZ);
        }

        private void RebaseRotationTo(float z) => SetCupAngle(z);

        private void CaptureRest()
        {
            if (_restCaptured || _cup == null) return;
            _restPos = _cup.anchoredPosition;
            _restCaptured = true;
        }

        private void EmitBurst(float yOffsetFromCupCenter, Vector2 dir, float intensity01)
        {
            if (!Particles || _cup == null) return;
            var container = (RectTransform)_burst.transform;
            Vector2 local = (Vector2)container.InverseTransformPoint(_cup.position);
            _burst.Burst(local + new Vector2(0f, yOffsetFromCupCenter), dir, intensity01);
        }
    }
}
