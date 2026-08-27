using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Polymorphic
{
    public abstract partial class BlockEditorWindow<T> where T : ScriptableObject
    {
        // ============================ Tab — Raw Data ============================

        Vector2 _dataScroll;

        void DrawRawData()
        {
            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Select an asset on the left.", MessageType.Info);
                return;
            }
            _ctx.UpdateTree();
            _dataScroll = EditorGUILayout.BeginScrollView(_dataScroll);
            _ctx.Tree?.Draw(false);
            EditorGUILayout.EndScrollView();
            _ctx.ApplyChanges();
        }
    }
}
