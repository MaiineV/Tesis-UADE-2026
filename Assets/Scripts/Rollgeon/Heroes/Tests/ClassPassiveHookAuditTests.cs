using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Effects.Concretes;
using UnityEditor;

namespace Rollgeon.Heroes.Tests
{
    /// <summary>
    /// Audita los hooks serializados de TODAS las pasivas del proyecto contra el enum
    /// vigente, POR NOMBRE. <c>PassiveHook.TriggerEvent</c> se guarda como int: borrar un
    /// miembro del medio de <c>EventName</c> corre todo lo de abajo y el hook queda apuntando
    /// a otro evento en silencio — ya pasó TRES veces (la última: el commit del roll pool
    /// borró OnEnergyChanged y la Furia del Guerrero dejó de re-evaluarse al cambiar la vida).
    /// La suite estaba verde las tres veces porque ningún test miraba el int del asset.
    /// </summary>
    [TestFixture]
    public class ClassPassiveHookAuditTests
    {
        [Test]
        public void should_keep_low_hp_buff_hooks_bound_to_OnAttributeChanged_in_every_passive_asset()
        {
            // Arrange
            var guids = AssetDatabase.FindAssets("t:ClassPassiveSO");
            Assert.IsNotEmpty(guids, "No hay ClassPassiveSO en el proyecto — ¿se movieron los assets?");

            // Act + Assert
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var passive = AssetDatabase.LoadAssetAtPath<ClassPassiveSO>(path);
                if (passive == null || passive.Hooks == null) continue;

                foreach (var hook in passive.Hooks)
                {
                    if (hook?.Effect?.Effects == null) continue;
                    if (!hook.Effect.Effects.Any(e => e is EffLowHpAttackBuff)) continue;

                    Assert.AreEqual(EventName.OnAttributeChanged, hook.TriggerEvent,
                        $"'{path}': el hook del buff de vida baja quedó en '{hook.TriggerEvent}'. " +
                        "Corrimiento del enum EventName — re-correr Rollgeon/Player Icons/3.");
                }
            }
        }
    }
}
