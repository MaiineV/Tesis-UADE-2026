using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace Rollgeon.Input.Tests
{
    /// <summary>
    /// El contrato silencioso del <c>GameplayHotkeyService</c>: resuelve cada action por
    /// <c>hotkey.ToString()</c>, así que un valor del enum sin action gemela en el map
    /// "Gameplay" queda inerte con apenas un warning. Esto lo vuelve un rojo de suite.
    /// </summary>
    [TestFixture]
    public sealed class GameplayHotkeyActionMatchTests
    {
        private const string AssetPath = "Assets/InputSystem_Actions.inputactions";
        private const string MapName = "Gameplay";

        [Test]
        public void should_have_a_gameplay_action_for_every_hotkey_enum_value()
        {
            // Arrange
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.IsNotNull(asset, $"No se pudo cargar '{AssetPath}'.");

            var map = asset.FindActionMap(MapName);
            Assert.IsNotNull(map, $"'{AssetPath}' no tiene el map '{MapName}'.");

            foreach (GameplayHotkey hotkey in Enum.GetValues(typeof(GameplayHotkey)))
            {
                // Act
                var action = map.FindAction(hotkey.ToString());

                // Assert
                Assert.IsNotNull(action,
                    $"GameplayHotkey.{hotkey} no tiene action gemela en el map '{MapName}': " +
                    "el hotkey queda inerte en runtime (solo warning).");
                Assert.IsNotEmpty(action.bindings,
                    $"La action '{hotkey}' existe pero no tiene ningún binding de teclado.");
            }
        }
    }
}
