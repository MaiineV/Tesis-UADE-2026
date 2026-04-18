using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Placeholder del minimapa con API <see cref="SetRotation"/> para corregir la
    /// rotacion isometrica del mundo. El render real del minimap (RenderTexture, camara
    /// top-down, fog of war) queda para un worktree dedicado.
    /// </summary>
    /// <remarks>
    /// Plan §4.8. <see cref="Bind"/>/<see cref="Unbind"/> son no-ops por ahora — existen
    /// por consistencia con el resto de las sub-views para que <c>ExplorationHUDView</c>
    /// pueda tratarlas uniformemente.
    /// </remarks>
    // [STUB] Minimap real rendering — this is placeholder; full impl in worktree TBD.
    [AddComponentMenu("Rollgeon/UI/HUD/Minimap View")]
    public class MinimapView : MonoBehaviour
    {
        [Title("Minimap — Widget refs")]
        [Tooltip("Transform al que se le aplica la rotacion corregida. Si null, se usa " +
                 "el Transform de este GameObject.")]
        [SerializeField]
        private Transform _mapPivot;

        [Tooltip("RawImage placeholder. Opcional; el render real usa RenderTexture.")]
        [SerializeField]
        private RawImage _placeholder;

        [Title("Minimap — Rotation correction")]
        [InfoBox("Correccion por default para mundos isometricos: -45° en Z alinea " +
                 "la camara top-down con el eje visual del mundo. Tunear en Inspector.")]
        [SerializeField]
        private Vector3 _rotationCorrectionEuler = new Vector3(0f, 0f, -45f);

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private Quaternion RotationCorrection => Quaternion.Euler(_rotationCorrectionEuler);

        private void Start()
        {
            // Aplicar la correccion inicial para que el placeholder arranque alineado.
            ApplyRotation(Quaternion.identity);
        }

        /// <summary>No-op por ahora. Existe por simetria con las otras sub-views.</summary>
        public void Bind(Guid playerGuid)
        {
            _bound = true;
        }

        /// <summary>No-op por ahora.</summary>
        public void Unbind()
        {
            _bound = false;
        }

        /// <summary>
        /// Hook publico para que el sistema real de minimap (futuro worktree) empuje
        /// la rotacion del jugador / camara. El metodo multiplica por la correccion
        /// isometrica configurada en <see cref="_rotationCorrectionEuler"/>.
        /// </summary>
        public void SetRotation(Quaternion worldRotation)
        {
            ApplyRotation(worldRotation);
        }

        private void ApplyRotation(Quaternion worldRotation)
        {
            var pivot = _mapPivot != null ? _mapPivot : transform;
            pivot.localRotation = worldRotation * RotationCorrection;
        }
    }
}
