using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

public class GroundRenderer : MonoBehaviour
{
    [FoldoutGroup("Ground Renderer")] [SerializeField] private List<ZoneData>  zoneDataList;
    [FoldoutGroup("Ground Renderer")] [SerializeField] private float           cellSize       = 4f;
    [FoldoutGroup("Ground Renderer")] [SerializeField] private Vector2Int      gridDimensions = new(128, 128);

    private ZoneSystem _zones;
    private Mesh       _quad;
    private bool       _dirty = true;

    private readonly Dictionary<ZoneType, Material>        _matMap    = new();
    private readonly Dictionary<ZoneType, List<Matrix4x4>> _matrices  = new();

    private static readonly Matrix4x4[] _batchBuf = new Matrix4x4[1023];

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        _zones = FindAnyObjectByType<ZoneSystem>();
        if (_zones != null) _zones.OnZoneChanged += () => _dirty = true;

        _quad = CreateQuadMesh();
        BuildMaterialMap();

        foreach (ZoneType t in Enum.GetValues(typeof(ZoneType)))
            _matrices[t] = new List<Matrix4x4>();
    }

    private void OnDestroy()
    {
        if (_zones != null) _zones.OnZoneChanged -= () => _dirty = true;
    }

    private void Update()
    {
        if (_dirty) RebuildMatrices();
        DrawInstances();
    }

    // ── Build ──────────────────────────────────────────────────────────────

    [Button("Rebuild Ground"), FoldoutGroup("Ground Renderer")]
    public void RebuildMatrices()
    {
        foreach (var list in _matrices.Values) list.Clear();

        if (_zones == null) { _dirty = false; return; }

        float scale = cellSize * 0.98f;
        for (int x = 0; x < gridDimensions.x; x++)
        for (int z = 0; z < gridDimensions.y; z++)
        {
            var zone = _zones.GetZone(new Vector2Int(x, z));
            if (!zone.HasValue) continue;

            var pos = CellToWorld(new Vector2Int(x, z)) + Vector3.up * 0.01f;
            _matrices[zone.Value].Add(Matrix4x4.TRS(pos, Quaternion.identity,
                                                    new Vector3(scale, 1f, scale)));
        }
        _dirty = false;
    }

    // ── Draw ───────────────────────────────────────────────────────────────

    private void DrawInstances()
    {
        if (_quad == null) return;
        foreach (var (type, list) in _matrices)
        {
            if (list.Count == 0) continue;
            if (!_matMap.TryGetValue(type, out var mat) || mat == null) continue;

            int drawn = 0;
            while (drawn < list.Count)
            {
                int count = Mathf.Min(1023, list.Count - drawn);
                for (int i = 0; i < count; i++) _batchBuf[i] = list[drawn + i];
                Graphics.DrawMeshInstanced(_quad, 0, mat, _batchBuf, count);
                drawn += count;
            }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void BuildMaterialMap()
    {
        _matMap.Clear();
        if (zoneDataList == null) return;
        foreach (var zd in zoneDataList)
            if (zd != null && zd.groundMaterial != null)
            {
                zd.groundMaterial.enableInstancing = true;
                _matMap[zd.type] = zd.groundMaterial;
            }
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        float hw = gridDimensions.x * cellSize * 0.5f;
        float hh = gridDimensions.y * cellSize * 0.5f;
        return new Vector3((cell.x + 0.5f) * cellSize - hw, 0f,
                           (cell.y + 0.5f) * cellSize - hh);
    }

    private static Mesh CreateQuadMesh()
    {
        var m = new Mesh { name = "GroundQuad" };
        m.vertices  = new[] { new Vector3(-0.5f,0,-0.5f), new Vector3(0.5f,0,-0.5f),
                               new Vector3(0.5f,0, 0.5f), new Vector3(-0.5f,0, 0.5f) };
        m.uv        = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
