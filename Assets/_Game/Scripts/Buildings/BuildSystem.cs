using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Sirenix.OdinInspector;

public class BuildSystem : MonoBehaviour
{
    [FoldoutGroup("Build Settings")] [SerializeField] private BuildingData startBuilding;
    [FoldoutGroup("Build Settings")] [SerializeField] private Material ghostValidMaterial;
    [FoldoutGroup("Build Settings")] [SerializeField] private Material ghostInvalidMaterial;

    // Doit correspondre aux valeurs de GridSystem
    [FoldoutGroup("Build Settings")] [SerializeField] private float cellSize = 4f;
    [FoldoutGroup("Build Settings")] [SerializeField] private Vector2Int gridDimensions = new Vector2Int(128, 128);

    [ShowInInspector, ReadOnly, FoldoutGroup("Runtime")] private Vector2Int _hoveredCell;
    [ShowInInspector, ReadOnly, FoldoutGroup("Runtime")] private bool _placementValid;

    public BuildingData CurrentBuilding => _selectedBuilding;
    public event System.Action<BuildingData> OnBuildingChanged;

    private BuildingData _selectedBuilding;
    private GameObject _ghostObject;
    private GridSystem _grid;
    private Camera _cam;

    private void Awake()
    {
        _grid = FindAnyObjectByType<GridSystem>();
        _cam = Camera.main ?? FindAnyObjectByType<Camera>();
    }

    private void Start()
    {
        if (startBuilding != null)
            SelectBuilding(startBuilding);
    }

    private void Update()
    {
        if (_selectedBuilding == null || _ghostObject == null) return;
        UpdateGhost();
        HandleInput();
    }

    public void SelectBuilding(BuildingData data)
    {
        DestroyGhost();
        if (data == null) { _selectedBuilding = null; OnBuildingChanged?.Invoke(null); return; }

        _selectedBuilding = data;
        _ghostObject = Instantiate(data.prefab);
        _ghostObject.name = "Ghost";
        _ghostObject.transform.localScale = BuildingScale(data);

        foreach (var col in _ghostObject.GetComponentsInChildren<Collider>())
            Destroy(col);

        foreach (var r in _ghostObject.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        OnBuildingChanged?.Invoke(_selectedBuilding);
    }

    private void UpdateGhost()
    {
        Vector3 worldPos = GetMouseWorldPos();
        _hoveredCell = _grid.GetCellFromWorldPos(worldPos);

        bool budgetOk = EconomySystem.Instance != null
                        && EconomySystem.Instance.Budget >= _selectedBuilding.cost;
        _placementValid = budgetOk
                          && _grid.IsCellAvailable(_hoveredCell, _selectedBuilding.sizeInCells);

        _ghostObject.transform.position = CellToWorldCenter(_hoveredCell, _selectedBuilding);

        var mat = _placementValid ? ghostValidMaterial : ghostInvalidMaterial;
        foreach (var r in _ghostObject.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = mat;
    }

    private void HandleInput()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (mouse.leftButton.wasPressedThisFrame && _placementValid)
            PlaceBuilding();

        if (mouse.rightButton.wasPressedThisFrame)
            CancelSelection();
    }

    private void PlaceBuilding()
    {
        if (!EconomySystem.Instance.TrySpend(_selectedBuilding.cost)) return;

        var placed = Instantiate(
            _selectedBuilding.prefab,
            CellToWorldCenter(_hoveredCell, _selectedBuilding),
            Quaternion.identity
        );
        placed.name = _selectedBuilding.buildingName;
        placed.transform.localScale = BuildingScale(_selectedBuilding);

        _grid.SetCellOccupied(_hoveredCell, _selectedBuilding.sizeInCells, _selectedBuilding.gridType);
    }

    public void CancelSelection()
    {
        DestroyGhost();
        _selectedBuilding = null;
        OnBuildingChanged?.Invoke(null);
    }

    private void DestroyGhost()
    {
        if (_ghostObject != null) Destroy(_ghostObject);
        _ghostObject = null;
    }

    private Vector3 GetMouseWorldPos()
    {
        var screenPos = Mouse.current.position.ReadValue();
        Ray ray = _cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));

        if (Mathf.Abs(ray.direction.y) < 0.001f) return Vector3.zero;
        float t = -ray.origin.y / ray.direction.y;
        return ray.origin + ray.direction * t;
    }

    private Vector3 CellToWorldCenter(Vector2Int cell, BuildingData data)
    {
        float halfW = gridDimensions.x * cellSize * 0.5f;
        float halfH = gridDimensions.y * cellSize * 0.5f;

        float wx = (cell.x + data.sizeInCells.x * 0.5f) * cellSize - halfW;
        float wz = (cell.y + data.sizeInCells.y * 0.5f) * cellSize - halfH;
        float wy = data.height * 0.5f;

        return new Vector3(wx, wy, wz);
    }

    private Vector3 BuildingScale(BuildingData data)
        => new Vector3(data.sizeInCells.x * cellSize, data.height, data.sizeInCells.y * cellSize);
}
