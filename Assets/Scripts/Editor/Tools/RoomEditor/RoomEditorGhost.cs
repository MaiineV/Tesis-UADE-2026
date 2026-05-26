using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rollgeon.Editor.Tools.RoomEditor
{
    internal sealed class RoomEditorGhost
    {
        private struct DrawEntry
        {
            public Mesh Mesh;
            public Matrix4x4 LocalMatrix;
            public int SubMeshCount;
        }

        private GameObject _sourcePrefab;
        private readonly List<DrawEntry> _entries = new();
        private Material _ghostMaterial;

        public void Render(GameObject prefab, Vector3 position, Quaternion rotation, Vector3? scale, Color tint)
        {
            if (prefab == null) return;

            EnsureMaterial();
            if (_ghostMaterial == null) return;

            if (_sourcePrefab != prefab)
            {
                RebuildEntries(prefab);
                _sourcePrefab = prefab;
            }

            var rootMatrix = Matrix4x4.TRS(position, rotation, scale ?? Vector3.one);
            _ghostMaterial.color = tint;

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Mesh == null) continue;

                var worldMatrix = rootMatrix * entry.LocalMatrix;
                for (int sub = 0; sub < entry.SubMeshCount; sub++)
                {
                    if (!_ghostMaterial.SetPass(0)) continue;
                    Graphics.DrawMeshNow(entry.Mesh, worldMatrix, sub);
                }
            }
        }

        public void Dispose()
        {
            _entries.Clear();
            _sourcePrefab = null;
            if (_ghostMaterial != null)
            {
                Object.DestroyImmediate(_ghostMaterial);
                _ghostMaterial = null;
            }
        }

        private void EnsureMaterial()
        {
            if (_ghostMaterial != null) return;
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) return;

            _ghostMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent,
            };
            _ghostMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _ghostMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _ghostMaterial.SetInt("_Cull", (int)CullMode.Back);
            _ghostMaterial.SetInt("_ZWrite", 0);
        }

        private void RebuildEntries(GameObject prefab)
        {
            _entries.Clear();
            if (prefab == null) return;

            var temp = Object.Instantiate(prefab);
            temp.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var rootInverse = temp.transform.worldToLocalMatrix;
                foreach (var mf in temp.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    var localMatrix = rootInverse * mf.transform.localToWorldMatrix;
                    _entries.Add(new DrawEntry
                    {
                        Mesh = mf.sharedMesh,
                        LocalMatrix = localMatrix,
                        SubMeshCount = mf.sharedMesh.subMeshCount,
                    });
                }
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }
    }
}
