using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Editor.Tools.RoomEditor
{
    // Filename prefixed with "0" so this partial sorts first alphabetically.
    // C# compiles partial classes in file-name order, and Odin discovers tabs in
    // member-metadata order — so this is the only reliable way to pin the tab
    // sequence to Tool → Room → Palette → Doors → Spawn Points without touching
    // every per-tab field decoration. Do not rename without keeping the leading "0".
    public sealed partial class RoomEditorWindow
    {
        [TabGroup(Tabs, TabTool)]
        [TabGroup(Tabs, TabRoom)]
        [TabGroup(Tabs, TabPalette)]
        [TabGroup(Tabs, TabDoors)]
        [TabGroup(Tabs, TabSpawn)]
        [HideInInspector, SerializeField] private bool _tabOrderSentinel;
    }
}
