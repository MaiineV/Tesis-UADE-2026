using Rollgeon.Dungeon;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Run
{
    /// <summary>
    /// MonoBehaviour en <c>00_Bootstrap</c> (DontDestroyOnLoad). Registra
    /// <see cref="RunController"/> como servicio Global en
    /// <see cref="Patterns.ServiceScope.Global"/> via
    /// <see cref="RunController.CreateAndRegister"/>.
    /// </summary>
    /// <remarks>
    /// [SETUP] Asignar <c>Floor1_Layout.asset</c> al field
    /// <c>_defaultLayout</c>. Execution order -9000 para correr despues del
    /// BootstrapRunner (-10000) y antes de cualquier gameplay MonoBehaviour.
    /// Como vive en un GO con DDOL, sobrevive al load de 01_MainMenu y
    /// 02_Gameplay — la scope Global se mantiene durante toda la sesion.
    /// </remarks>
    [DefaultExecutionOrder(-9000)]
    [AddComponentMenu("Rollgeon/Run/Run Controller Bootstrapper")]
    public sealed class RunControllerBootstrapper : MonoBehaviour
    {
        private const string LogPrefix = "[RunControllerBootstrapper] ";

        [Required("Arrastrar Floor1_Layout.asset (Assets/Rollgeon/Dungeon/Floor1_Layout.asset).")]
        [SerializeField] private FloorLayoutSO _defaultLayout;

        private void Awake()
        {
            if (_defaultLayout == null)
            {
                Debug.LogError(LogPrefix + "_defaultLayout is null. Arrastrar Floor1_Layout.asset en el Inspector.", this);
                return;
            }

            RunController.CreateAndRegister(_defaultLayout);
            Debug.Log(LogPrefix + "RunController registrado en ServiceScope.Global.", this);
        }
    }
}
