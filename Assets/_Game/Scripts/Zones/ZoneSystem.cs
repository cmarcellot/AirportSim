using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

public class ZoneSystem : MonoBehaviour
{
    [FoldoutGroup("Zone Settings")] [SerializeField] private int gridWidth  = 128;
    [FoldoutGroup("Zone Settings")] [SerializeField] private int gridHeight = 128;

    [ShowInInspector, ReadOnly, FoldoutGroup("Zone Settings")]
    private Dictionary<ZoneType, int> _counts = new();

    public event Action OnZoneChanged;

    private ZoneType?[,] _grid;

    private void Awake()
    {
        _grid = new ZoneType?[gridWidth, gridHeight];
        foreach (ZoneType t in Enum.GetValues(typeof(ZoneType))) _counts[t] = 0;
    }

    // ── API publique ───────────────────────────────────────────────────────

    public void SetZone(Vector2Int cell, ZoneType? type)
    {
        if (!InBounds(cell.x, cell.y)) return;
        UpdateCount(_grid[cell.x, cell.y], type);
        _grid[cell.x, cell.y] = type;
        OnZoneChanged?.Invoke();
    }

    public void SetZoneBatch(IEnumerable<Vector2Int> cells, ZoneType? type)
    {
        bool changed = false;
        foreach (var cell in cells)
        {
            if (!InBounds(cell.x, cell.y)) continue;
            UpdateCount(_grid[cell.x, cell.y], type);
            _grid[cell.x, cell.y] = type;
            changed = true;
        }
        if (changed) OnZoneChanged?.Invoke();
    }

    public ZoneType? GetZone(Vector2Int cell)
        => InBounds(cell.x, cell.y) ? _grid[cell.x, cell.y] : null;

    public bool IsZone(Vector2Int cell, ZoneType type)
        => GetZone(cell) == type;

    public List<Vector2Int> GetAllCellsOfZone(ZoneType type)
    {
        var result = new List<Vector2Int>();
        for (int x = 0; x < gridWidth;  x++)
        for (int z = 0; z < gridHeight; z++)
            if (_grid[x, z] == type) result.Add(new Vector2Int(x, z));
        return result;
    }

    [Button("Clear All Zones"), FoldoutGroup("Zone Settings")]
    public void ClearAllZones()
    {
        _grid = new ZoneType?[gridWidth, gridHeight];
        foreach (ZoneType t in Enum.GetValues(typeof(ZoneType))) _counts[t] = 0;
        OnZoneChanged?.Invoke();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private bool InBounds(int x, int z) => x >= 0 && x < gridWidth && z >= 0 && z < gridHeight;

    private void UpdateCount(ZoneType? old, ZoneType? next)
    {
        if (old.HasValue)  _counts[old.Value]  = Mathf.Max(0, _counts[old.Value]  - 1);
        if (next.HasValue) _counts[next.Value] = _counts[next.Value] + 1;
    }
}
