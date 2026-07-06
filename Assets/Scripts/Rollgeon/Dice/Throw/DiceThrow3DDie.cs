using UnityEngine;

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

        public Rigidbody Body => _rb != null ? _rb : (_rb = GetComponent<Rigidbody>());

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
