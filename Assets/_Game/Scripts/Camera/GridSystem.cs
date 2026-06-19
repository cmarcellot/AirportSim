using System;
using UnityEngine;
using Sirenix.OdinInspector;

public enum BuildingType
{
    None,
    Terminal,
    Runway,
    Taxiway,
    Hangar,
    ControlTower,
    Parking
}

public enum CellState
{
    Empty,
    Occupied
}

[Serializable]
public struct CellData
{
    public CellState State;
    public BuildingType Building;
}

public class GridSystem : MonoBehaviour
{
    [FoldoutGroup("Grid Settings")]
    [SerializeField] private int gridWidth = 128;

    [FoldoutGroup("Grid Settings")]
    [SerializeField] private int gridHeight = 128;

    [FoldoutGroup("Grid Settings")]
    [SerializeField] private float cellSize = 4f;

    [FoldoutGroup("Grid Settings")]
    [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.25f);

    [ShowInInspector, ReadOnly, FoldoutGroup("Grid Info")]
    private string GridDimensions => $"{gridWidth} x {gridHeight} ({gridWidth * gridHeight} cellules)";

    [ShowInInspector, ReadOnly, FoldoutGroup("Grid Info")]
    private string WorldSize => $"{gridWidth * cellSize} x {gridHeight * cellSize} unités";

    [ShowInInspector, ReadOnly, FoldoutGroup("Grid Info")]
    private int OccupiedCells => CountOccupied();

    private CellData[,] _grid;
    private Material _lineMaterial;

    private void Awake()
    {
        InitGrid();
        CreateLineMaterial();
    }

    private void OnDestroy()
    {
        if (_lineMaterial != null)
            Destroy(_lineMaterial);
    }

    private void InitGrid()
    {
        _grid = new CellData[gridWidth, gridHeight];
        for (int x = 0; x < gridWidth; x++)
            for (int z = 0; z < gridHeight; z++)
                _grid[x, z] = new CellData { State = CellState.Empty, Building = BuildingType.None };
    }

    private void CreateLineMaterial()
    {
        var shader = Shader.Find("Hidden/Internal-Colored");
        _lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _lineMaterial.SetInt("_Cull",    (int)UnityEngine.Rendering.CullMode.Off);
        _lineMaterial.SetInt("_ZWrite", 0);
    }

    private void OnRenderObject()
    {
        if (_lineMaterial == null) return;

        _lineMaterial.SetPass(0);

        float halfW = gridWidth  * cellSize * 0.5f;
        float halfH = gridHeight * cellSize * 0.5f;

        GL.PushMatrix();
        GL.Begin(GL.LINES);
        GL.Color(gridColor);

        for (int x = 0; x <= gridWidth; x++)
        {
            float xPos = -halfW + x * cellSize;
            GL.Vertex3(xPos, 0.02f, -halfH);
            GL.Vertex3(xPos, 0.02f,  halfH);
        }

        for (int z = 0; z <= gridHeight; z++)
        {
            float zPos = -halfH + z * cellSize;
            GL.Vertex3(-halfW, 0.02f, zPos);
            GL.Vertex3( halfW, 0.02f, zPos);
        }

        GL.End();
        GL.PopMatrix();
    }

    // ── API publique ──────────────────────────────────────────────────────────

    public Vector2Int GetCellFromWorldPos(Vector3 worldPos)
    {
        float halfW = gridWidth  * cellSize * 0.5f;
        float halfH = gridHeight * cellSize * 0.5f;

        int x = Mathf.FloorToInt((worldPos.x + halfW) / cellSize);
        int z = Mathf.FloorToInt((worldPos.z + halfH) / cellSize);

        return new Vector2Int(
            Mathf.Clamp(x, 0, gridWidth  - 1),
            Mathf.Clamp(z, 0, gridHeight - 1)
        );
    }

    public bool IsCellAvailable(Vector2Int origin, Vector2Int size)
    {
        for (int x = origin.x; x < origin.x + size.x; x++)
            for (int z = origin.y; z < origin.y + size.y; z++)
            {
                if (!IsInBounds(x, z)) return false;
                if (_grid[x, z].State != CellState.Empty) return false;
            }
        return true;
    }

    public void SetCellOccupied(Vector2Int origin, Vector2Int size, BuildingType type)
    {
        for (int x = origin.x; x < origin.x + size.x; x++)
            for (int z = origin.y; z < origin.y + size.y; z++)
            {
                if (!IsInBounds(x, z)) continue;
                _grid[x, z] = new CellData { State = CellState.Occupied, Building = type };
            }
    }

    public CellData GetCell(Vector2Int cell) =>
        IsInBounds(cell.x, cell.y) ? _grid[cell.x, cell.y] : default;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool IsInBounds(int x, int z) =>
        x >= 0 && x < gridWidth && z >= 0 && z < gridHeight;

    private int CountOccupied()
    {
        if (_grid == null) return 0;
        int count = 0;
        foreach (var cell in _grid)
            if (cell.State == CellState.Occupied) count++;
        return count;
    }
}
