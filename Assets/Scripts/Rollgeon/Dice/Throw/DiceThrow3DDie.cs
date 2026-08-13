using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Rollgeon.Dice.Throw
{
    /// <summary>
    /// Dado físico del modo 3D — prefab autorado (<c>Assets/Prefabs/Dice/DiceThrow3D_Die.prefab</c>):
    /// cubo + Rigidbody + números por cara (placeholder). El presenter lo instancia en
    /// la bandeja y este componente solo encapsula el rigidbody y la lectura de cara.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Rollgeon/Dice/Dice Throw 3D Die")]
    public sealed class DiceThrow3DDie : MonoBehaviour
    {
        private Rigidbody _rb;
        private float _nextImpactAt;

        public Rigidbody Body => _rb != null ? _rb : (_rb = GetComponent<Rigidbody>());

        /// <summary>(magnitud del impulso) — colisión física contra bandeja/otros dados.</summary>
        public event Action<float> Impacted;

        /// <summary>
        /// El presenter lo prende SOLO en vuelo (flick→settle): los dados carried
        /// flotan chocándose entre sí y spamearían clatter sin sentido.
        /// </summary>
        public bool EmitImpacts { get; set; }

        private void OnCollisionEnter(Collision collision)
        {
            if (!EmitImpacts || Impacted == null) return;
            // Rate-limit por dado en unscaled: el hitstop no debe congelar el gate.
            if (Time.unscaledTime < _nextImpactAt) return;
            _nextImpactAt = Time.unscaledTime + 0.06f;
            Impacted.Invoke(collision.impulse.magnitude);
        }

        /// <summary>Velocidad combinada (lineal² + angular²) para el detector de settle.</summary>
        public float CombinedSpeedSq
            => Body.linearVelocity.sqrMagnitude + Body.angularVelocity.sqrMagnitude;

        public bool IsPhysicallyStill => Body.IsSleeping() || CombinedSpeedSq < 0.01f;

        /// <summary>Cara superior actual según la rotación (ver <see cref="DiceFaceReader"/>).</summary>
        public int ReadTopFace(out float dot) => DiceFaceReader.ReadTopFace(transform.rotation, out dot);

        /// <summary>Empujoncito para dados de canto — impulso + torque aleatorios.</summary>
        public void Nudge(float impulse)
        {
            Body.AddForce(new Vector3(Random.Range(-1f, 1f), 1.2f, Random.Range(-1f, 1f)).normalized * impulse,
                ForceMode.Impulse);
            Body.AddTorque(Random.onUnitSphere * impulse, ForceMode.Impulse);
        }

        /// <summary>Endereza el dado a su cara dominante (timeout / rig) sin física.</summary>
        public void SnapUpright()
        {
            transform.rotation = DiceFaceReader.SnapToNearestFace(transform.rotation);
        }
    }
}
