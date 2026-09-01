using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Feedback.Tests
{
    /// <summary>
    /// <see cref="StunStarsSpin"/>: el bob tiene que anclarse al offset que
    /// <see cref="StunVfxBinder"/> setea DESPUÉS de instanciar, no a la posición del
    /// prefab. Regresión del bug "las estrellitas aparecen en los pies".
    /// </summary>
    [TestFixture]
    public class StunStarsSpinTests
    {
        [Test]
        public void should_bob_around_the_offset_set_after_instantiate()
        {
            // Arrange — misma secuencia que el binder: Instantiate dispara OnEnable con el
            // transform todavía en la posición del prefab, y el offset llega después.
            var go = new GameObject("stars");
            try
            {
                var spin = go.AddComponent<StunStarsSpin>();
                Invoke(spin, "OnEnable");
                go.transform.localPosition = new Vector3(0f, 1.8f, 0f);

                // Act
                Invoke(spin, "Update");

                // Assert — 0.06 de tolerancia: la amplitud default del bob es 0.05.
                Assert.AreEqual(1.8f, go.transform.localPosition.y, 0.06f,
                    "El bob se ancló a la posición del prefab (los pies), no al offset del binder.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void Invoke(object target, string method) =>
            target.GetType()
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, null);
    }
}
