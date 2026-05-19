using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.RoomTilePainter
{
    internal sealed class RoomTilePainterGhost
    {
        private GameObject _instance;
        private GameObject _sourcePrefab;

        public void UpdatePreview(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                Hide();
                return;
            }

            if (_sourcePrefab != prefab || _instance == null)
            {
                DestroyInstance();
                _instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (_instance == null) return;
                _instance.name = "[RoomTilePainter] Ghost";
                ApplyHideFlags(_instance);
                DisableColliders(_instance);
                _sourcePrefab = prefab;
            }

            _instance.transform.SetPositionAndRotation(position, rotation);
            if (!_instance.activeSelf) _instance.SetActive(true);
        }

        public void Hide()
        {
            if (_instance != null && _instance.activeSelf) _instance.SetActive(false);
        }

        public void Dispose() => DestroyInstance();

        private static void ApplyHideFlags(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.hideFlags |= HideFlags.HideAndDontSave | HideFlags.NotEditable;
            }
        }

        private static void DisableColliders(GameObject root)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        private void DestroyInstance()
        {
            if (_instance != null)
            {
                Object.DestroyImmediate(_instance);
            }
            _instance = null;
            _sourcePrefab = null;
        }
    }
}
