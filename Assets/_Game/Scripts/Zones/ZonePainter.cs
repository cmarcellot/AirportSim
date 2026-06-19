using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

public class ZonePainter : MonoBehaviour
{
    // ── Config ─────────────────────────────────────────────────────────────
    [FoldoutGroup("Zone Painter")] [SerializeField] private float      cellSize       = 4f;
    [FoldoutGroup("Zone Painter")] [SerializeField] private Vector2Int gridDimensions = new(128, 128);

    // ── Etat ───────────────────────────────────────────────────────────────
    [ShowInInspector, ReadOnly, FoldoutGroup("Zone Painter")]
    public ZoneData SelectedZone { get; private set; }

    [ShowInInspector, ReadOnly, FoldoutGroup("Zone Painter")]
    public string CostPreview { get; private set; } = "";

    public event System.Action OnSelectionChanged;

    // ── Runtime ────────────────────────────────────────────────────────────
    private ZoneSystem _zones;
    private GridSystem _grid;
    private Camera     _cam;

    private bool         _isPainting;
    private bool         _isErasing;
    private Vector2Int   _dragStart;
    private Vector2Int   _lastCell;

    private readonly HashSet<Vector2Int> _previewCells = new();

    private Mesh     _quadMesh;
    private Material _previewMat;

    private static readonly Matrix4x4[] _batchBuf = new Matrix4x4[1023];

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        _zones = FindAnyObjectByType<ZoneSystem>();
        _grid  = FindAnyObjectByType<GridSystem>();
        _cam   = Camera.main ?? FindAnyObjectByType<Camera>();
        _quadMesh  = CreateQuadMesh();
        _previewMat = CreateTransparentMat();
    }

    private void Update()
    {
        if (SelectedZone == null) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ClearSelection();
            return;
        }

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        var cell    = GetCellUnderMouse();

        if (overUI || !cell.HasValue)
        {
            if (!_isPainting && !_isErasing) _previewCells.Clear();
            RenderPreview();
            return;
        }

        // ── Left button: paint ─────────────────────────────────────────────
        if (mouse.leftButton.wasPressedThisFrame && !_isErasing)
        {
            _isPainting = true;
            _dragStart  = cell.Value;
        }
        if (mouse.leftButton.wasReleasedThisFrame && _isPainting)
        {
            ConfirmPaint();
            _isPainting = false;
        }

        // ── Right button: erase ────────────────────────────────────────────
        if (mouse.rightButton.wasPressedThisFrame && !_isPainting)
        {
            _isErasing = true;
            _dragStart = cell.Value;
        }
        if (mouse.rightButton.wasReleasedThisFrame && _isErasing)
        {
            ConfirmErase();
            _isErasing = false;
        }

        // ── Preview ────────────────────────────────────────────────────────
        if (_lastCell != cell.Value || _isPainting || _isErasing)
        {
            _lastCell = cell.Value;
            UpdatePreview(cell.Value, (_isPainting || _isErasing) ? _dragStart : cell.Value);
        }

        RenderPreview();
    }

    // ── API publique ───────────────────────────────────────────────────────

    public void SelectZone(ZoneData data)
    {
        SelectedZone = data;
        _previewCells.Clear();
        CostPreview = "";
        OnSelectionChanged?.Invoke();
    }

    public void ClearSelection()
    {
        SelectedZone = null;
        _isPainting  = false;
        _isErasing   = false;
        _previewCells.Clear();
        CostPreview = "";
        OnSelectionChanged?.Invoke();
    }

    // ── Preview ────────────────────────────────────────────────────────────

    private void UpdatePreview(Vector2Int current, Vector2Int anchor)
    {
        _previewCells.Clear();

        int minX = Mathf.Min(anchor.x, current.x);
        int maxX = Mathf.Max(anchor.x, current.x);
        int minZ = Mathf.Min(anchor.y, current.y);
        int maxZ = Mathf.Max(anchor.y, current.y);

        for (int x = minX; x <= maxX; x++)
        for (int z = minZ; z <= maxZ; z++)
            _previewCells.Add(new Vector2Int(x, z));

        // Coût : seulement les cellules nouvelles (pas déjà ce type)
        if (_isPainting && SelectedZone != null && _zones != null)
        {
            int newCount = 0;
            foreach (var c in _previewCells)
                if (_zones.GetZone(c) != SelectedZone.type) newCount++;

            float total  = newCount * SelectedZone.costPerCell;
            CostPreview  = newCount > 0
                ? $"{newCount} case{(newCount > 1 ? "s" : "")} × {FormatMoney(SelectedZone.costPerCell)} = {FormatMoney(total)}"
                : "";
        }
        else
        {
            CostPreview = _isErasing ? $"{_previewCells.Count} case(s) à effacer" : "";
        }
    }

    private void RenderPreview()
    {
        if (_quadMesh == null || _previewMat == null || _previewCells.Count == 0) return;

        var c = _isErasing
            ? new Color(1f, 0.2f, 0.2f, 0.40f)
            : new Color(SelectedZone.zoneColor.r, SelectedZone.zoneColor.g,
                        SelectedZone.zoneColor.b, 0.50f);
        _previewMat.SetColor("_BaseColor", c);

        float scale = cellSize * 0.97f;
        int   i     = 0;
        foreach (var cell in _previewCells)
        {
            var pos = CellToWorld(cell) + Vector3.up * 0.05f;
            _batchBuf[i++] = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(scale, 1f, scale));

            if (i == 1023)
            {
                Graphics.DrawMeshInstanced(_quadMesh, 0, _previewMat, _batchBuf, i);
                i = 0;
            }
        }
        if (i > 0) Graphics.DrawMeshInstanced(_quadMesh, 0, _previewMat, _batchBuf, i);
    }

    // ── Confirmation ───────────────────────────────────────────────────────

    private void ConfirmPaint()
    {
        if (SelectedZone == null || _previewCells.Count == 0 || _zones == null) return;

        int newCount = 0;
        foreach (var c in _previewCells)
            if (_zones.GetZone(c) != SelectedZone.type) newCount++;

        float cost = newCount * SelectedZone.costPerCell;
        if (cost > 0 && EconomySystem.Instance != null && !EconomySystem.Instance.TrySpend(cost))
            return;

        _zones.SetZoneBatch(_previewCells, SelectedZone.type);
        _previewCells.Clear();
        CostPreview = "";
    }

    private void ConfirmErase()
    {
        if (_zones == null) return;
        _zones.SetZoneBatch(_previewCells, null);
        _previewCells.Clear();
        CostPreview = "";
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private Vector2Int? GetCellUnderMouse()
    {
        if (_cam == null) return null;
        var ray   = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(ray, out float dist)) return null;
        var world = ray.GetPoint(dist);

        if (_grid != null) return _grid.GetCellFromWorldPos(world);

        float hw = gridDimensions.x * cellSize * 0.5f;
        float hh = gridDimensions.y * cellSize * 0.5f;
        int x = Mathf.FloorToInt((world.x + hw) / cellSize);
        int z = Mathf.FloorToInt((world.z + hh) / cellSize);
        if (x < 0 || x >= gridDimensions.x || z < 0 || z >= gridDimensions.y) return null;
        return new Vector2Int(x, z);
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
        var mesh = new Mesh { name = "ZoneQuad" };
        mesh.vertices  = new[] { new Vector3(-0.5f,0,-0.5f), new Vector3(0.5f,0,-0.5f),
                                  new Vector3(0.5f,0, 0.5f), new Vector3(-0.5f,0, 0.5f) };
        mesh.uv        = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateTransparentMat()
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend",   0f);
        mat.SetFloat("_ZWrite",  0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue    = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.enableInstancing = true;
        return mat;
    }

    private static string FormatMoney(float v)
        => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0,0}", Mathf.RoundToInt(v))
                 .Replace(",", " ") + " $";
}
