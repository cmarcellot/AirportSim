using UnityEngine;
using Sirenix.OdinInspector;

public enum ZoneType { None, Airside, Landside, Restricted }

public class ZoneSystem : MonoBehaviour
{
    [FoldoutGroup("Zone System")]
    [SerializeField] private float cellSize = 4f;
    [FoldoutGroup("Zone System")]
    [SerializeField] private Vector2Int gridDimensions = new Vector2Int(128, 128);

    [FoldoutGroup("Zone System")]
    [SerializeField] private Color airsideColor    = new Color(0.20f, 0.50f, 1.00f, 0.14f);
    [FoldoutGroup("Zone System")]
    [SerializeField] private Color landsideColor   = new Color(0.20f, 0.80f, 0.20f, 0.14f);
    [FoldoutGroup("Zone System")]
    [SerializeField] private Color restrictedColor = new Color(1.00f, 0.30f, 0.30f, 0.14f);

    [ShowInInspector, ReadOnly, FoldoutGroup("Zone System")]
    private bool _showZones;

    private GridSystem _grid;

    private void Awake() => _grid = FindAnyObjectByType<GridSystem>();

    // ── API publique ───────────────────────────────────────────────────────

    public ZoneType GetZone(Vector2Int cell)
    {
        if (_grid == null) return ZoneType.None;
        return Classify(_grid.GetCell(cell).Building);
    }

    [Button("Toggle Zone Visualization"), FoldoutGroup("Zone System")]
    public void ToggleZoneVisualization() => _showZones = !_showZones;

    // ── Classification ─────────────────────────────────────────────────────

    public static ZoneType Classify(BuildingType b) => b switch
    {
        BuildingType.Runway or BuildingType.Taxiway or BuildingType.Apron or
        BuildingType.Gate   or BuildingType.ControlTower or BuildingType.Hangar or
        BuildingType.FuelStation or BuildingType.CargoArea
            => ZoneType.Airside,

        BuildingType.SecurityCheckpoint or BuildingType.Customs or
        BuildingType.BoardingLounge
            => ZoneType.Restricted,

        BuildingType.Terminal or BuildingType.Hall or BuildingType.CheckIn or
        BuildingType.Shop or BuildingType.Restaurant or BuildingType.Parking or
        BuildingType.RoadAccess or BuildingType.BusStop or BuildingType.TaxiZone
            => ZoneType.Landside,

        _ => ZoneType.None
    };

    // ── Gizmos (éditeur) ───────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        if (!_showZones || _grid == null) return;

        float s = cellSize * 0.9f;
        for (int x = 0; x < gridDimensions.x; x++)
        for (int z = 0; z < gridDimensions.y; z++)
        {
            var cell = new Vector2Int(x, z);
            var zone = GetZone(cell);
            if (zone == ZoneType.None) continue;

            Gizmos.color = zone switch
            {
                ZoneType.Airside    => airsideColor,
                ZoneType.Landside   => landsideColor,
                ZoneType.Restricted => restrictedColor,
                _                   => Color.clear,
            };
            Gizmos.DrawCube(CellToWorld(cell) + Vector3.up * 0.05f,
                            new Vector3(s, 0.01f, s));
        }
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        float hw = gridDimensions.x * cellSize * 0.5f;
        float hh = gridDimensions.y * cellSize * 0.5f;
        return new Vector3((cell.x + 0.5f) * cellSize - hw, 0f,
                           (cell.y + 0.5f) * cellSize - hh);
    }
}
