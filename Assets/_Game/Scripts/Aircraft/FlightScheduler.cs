using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Fait apparaître des avions à intervalles réguliers, les assigne aux pistes
/// disponibles, puis gère le taxi vers une gate via PathfindingSystem.
/// </summary>
public class FlightScheduler : MonoBehaviour
{
    // ── Config ─────────────────────────────────────────────────────────────
    [FoldoutGroup("Flight Scheduler")]
    [SerializeField] private AircraftData aircraftData;

    [FoldoutGroup("Flight Scheduler")]
    [Tooltip("Intervalle entre deux arrivées (minutes de jeu)")]
    [SerializeField] private float spawnIntervalMinutes = 2f;

    // ── Stats ──────────────────────────────────────────────────────────────
    [FoldoutGroup("Flight Scheduler"), ShowInInspector, ReadOnly]
    private float _timerMinutes;

    [FoldoutGroup("Flight Scheduler"), ShowInInspector, ReadOnly]
    private int _totalSpawned;

    // ── Runtime ────────────────────────────────────────────────────────────
    private ZoneSystem        _zones;
    private PathfindingSystem _pathfinding;
    private bool              _ready;

    private const float CellSize = 4f;
    private const float HalfGrid = 128 * CellSize * 0.5f; // 256

    private readonly Dictionary<float, Aircraft> _runwayOccupants = new();

    private struct RunwayInfo
    {
        public float StartX, EndX, CenterZ;
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        _zones       = FindAnyObjectByType<ZoneSystem>();
        _pathfinding = FindAnyObjectByType<PathfindingSystem>();
    }

    private IEnumerator Start()
    {
        // Attendre une frame que AirportEnvironment.Start() ait peint les zones
        yield return null;

        if (_zones != null) PaintRunwayConnectors();

        // Premier avion quasi-immédiatement
        _timerMinutes = spawnIntervalMinutes - 0.1f;
        _ready        = true;
    }

    private void Update()
    {
        if (!_ready) return;

        float spd = TimeManager.Instance != null ? TimeManager.Instance.SpeedMultiplier : 1;
        _timerMinutes += Time.deltaTime * spd;

        if (_timerMinutes >= spawnIntervalMinutes)
        {
            _timerMinutes -= spawnIntervalMinutes;
            TrySpawnAircraft();
        }
    }

    // ── API publique ───────────────────────────────────────────────────────

    [Button("Spawn Test Aircraft"), FoldoutGroup("Flight Scheduler")]
    public void SpawnTestAircraft()
    {
        if (!Application.isPlaying)
        { Debug.LogWarning("[FlightScheduler] Disponible uniquement en Play."); return; }
        TrySpawnAircraft();
    }

    /// <summary>Cherche une gate libre et déclenche le taxi de l'avion.</summary>
    public void AssignGateToAircraft(Aircraft aircraft)
    {
        if (aircraft == null) return;

        var gates = FindObjectsByType<Gate>();
        Gate chosen = null;
        foreach (var g in gates)
            if (g.IsAvailable()) { chosen = g; break; }

        if (chosen == null)
        {
            Debug.Log("[FlightScheduler] Aucune gate disponible — l'avion reste sur la piste.");
            return;
        }

        chosen.Reserve(); // réservée avant que l'avion n'arrive

        List<Vector3> path = null;
        if (_pathfinding != null)
            path = _pathfinding.FindAirsidePath(aircraft.transform.position, chosen.transform.position);

        if (path == null || path.Count == 0)
        {
            Debug.LogWarning($"[FlightScheduler] Pas de chemin airside vers {chosen.name}. " +
                             "Vérifiez que les zones Runway/Taxiway/Apron sont connectées.");
            chosen.ReleaseAircraft();
            return;
        }

        // La piste est libérée dès que l'avion commence à rouler
        FreeRunwayOf(aircraft);
        aircraft.StartTaxi(path, chosen);

        Debug.Log($"[FlightScheduler] {aircraft.name} → {chosen.name} ({path.Count} waypoints)");
    }

    // ── Spawn ──────────────────────────────────────────────────────────────

    private void TrySpawnAircraft()
    {
        if (aircraftData == null)
        { Debug.LogWarning("[FlightScheduler] AircraftData non assigné."); return; }
        if (_zones == null) _zones = FindAnyObjectByType<ZoneSystem>();

        // Ne pas faire atterrir si aucune gate n'est disponible
        var gates = FindObjectsByType<Gate>();
        if (gates.Length > 0 && System.Array.TrueForAll(gates, g => !g.IsAvailable()))
        {
            Debug.Log("[FlightScheduler] Toutes les gates occupées — atterrissage suspendu.");
            return;
        }

        PurgeDestroyedAircraft();

        var runways = FindRunways();
        if (runways.Count == 0)
        { Debug.Log("[FlightScheduler] Aucune piste Runway dans ZoneSystem."); return; }

        RunwayInfo? chosen = null;
        foreach (var rw in runways)
        {
            if (!_runwayOccupants.TryGetValue(rw.CenterZ, out var ac) || ac == null)
            { chosen = rw; break; }
        }

        if (chosen == null)
        { Debug.Log("[FlightScheduler] Toutes les pistes occupées."); return; }

        SpawnOn(chosen.Value);
    }

    private void SpawnOn(RunwayInfo runway)
    {
        var go = aircraftData.prefab != null
            ? Instantiate(aircraftData.prefab)
            : BuildDefaultModel();

        go.name = $"Aircraft_{aircraftData.aircraftName}_{_totalSpawned}";

        var aircraft = go.AddComponent<Aircraft>();
        aircraft.OnLanded += OnAircraftLanded;
        aircraft.Initialize(aircraftData, runway.StartX, runway.EndX, runway.CenterZ);

        _runwayOccupants[runway.CenterZ] = aircraft;
        _totalSpawned++;

        Debug.Log($"[FlightScheduler] {go.name} → piste Z={runway.CenterZ:0}");
    }

    // ── Callback atterrissage ──────────────────────────────────────────────

    private void OnAircraftLanded(Aircraft aircraft)
    {
        aircraft.OnLanded -= OnAircraftLanded;
        AssignGateToAircraft(aircraft);
    }

    // ── Connecteur de zones ────────────────────────────────────────────────
    // Peint les cellules manquantes entre le haut des taxiways et le bas des pistes,
    // ainsi qu'entre les bandes de pistes, afin de rendre le graphe airside connexe.

    private void PaintRunwayConnectors()
    {
        var allRunway = _zones.GetAllCellsOfZone(ZoneType.Runway);
        var allTaxi   = _zones.GetAllCellsOfZone(ZoneType.Taxiway);

        if (allRunway == null || allRunway.Count == 0) return;
        if (allTaxi   == null || allTaxi.Count   == 0) return;

        // Borne supérieure des taxiways et plage X des colonnes verticales
        int maxTaxiY = 0, minTaxiX = int.MaxValue, maxTaxiX = 0;
        foreach (var c in allTaxi)
        {
            if (c.y > maxTaxiY) maxTaxiY = c.y;
            if (c.x < minTaxiX) minTaxiX = c.x;
            if (c.x > maxTaxiX) maxTaxiX = c.x;
        }

        // Borne supérieure des pistes (Y max toutes pistes)
        int maxRunwayY = 0;
        var runwayYSet = new HashSet<int>();
        foreach (var c in allRunway)
        {
            if (c.y > maxRunwayY) maxRunwayY = c.y;
            runwayYSet.Add(c.y);
        }

        if (maxRunwayY <= maxTaxiY) return; // déjà connecté

        // Remplie tous les trous entre le sommet des taxiways et le sommet des pistes
        var connectors = new List<Vector2Int>();
        for (int y = maxTaxiY + 1; y <= maxRunwayY; y++)
        {
            if (!runwayYSet.Contains(y)) // c'est un trou
            {
                for (int x = minTaxiX; x <= maxTaxiX; x++)
                    connectors.Add(new Vector2Int(x, y));
            }
        }

        if (connectors.Count > 0)
        {
            _zones.SetZoneBatch(connectors, ZoneType.Taxiway);
            Debug.Log($"[FlightScheduler] {connectors.Count} cellules taxiway connecteur peintes.");
        }
    }

    // ── Détection des pistes ───────────────────────────────────────────────

    private List<RunwayInfo> FindRunways()
    {
        var result = new List<RunwayInfo>();
        if (_zones == null) return result;

        var cells = _zones.GetAllCellsOfZone(ZoneType.Runway);
        if (cells == null || cells.Count == 0) return result;

        cells.Sort((a, b) => a.y.CompareTo(b.y));

        int i = 0;
        while (i < cells.Count)
        {
            int bandMinY = cells[i].y, bandMaxY = cells[i].y;
            int bandMinX = cells[i].x, bandMaxX = cells[i].x;

            while (i < cells.Count && cells[i].y <= bandMaxY + 1)
            {
                bandMaxY = Mathf.Max(bandMaxY, cells[i].y);
                bandMinX = Mathf.Min(bandMinX, cells[i].x);
                bandMaxX = Mathf.Max(bandMaxX, cells[i].x);
                i++;
            }

            if (bandMaxX - bandMinX < 8) continue;

            float worldMinX = bandMinX * CellSize - HalfGrid;
            float worldMaxX = (bandMaxX + 1) * CellSize - HalfGrid;
            float worldMinZ = bandMinY * CellSize - HalfGrid;
            float worldMaxZ = (bandMaxY + 1) * CellSize - HalfGrid;

            result.Add(new RunwayInfo
            {
                StartX  = worldMinX,
                EndX    = worldMaxX,
                CenterZ = (worldMinZ + worldMaxZ) * 0.5f,
            });
        }

        return result;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void FreeRunwayOf(Aircraft aircraft)
    {
        float key = float.NaN;
        foreach (var kvp in _runwayOccupants)
            if (kvp.Value == aircraft) { key = kvp.Key; break; }
        if (!float.IsNaN(key)) _runwayOccupants.Remove(key);
    }

    private void PurgeDestroyedAircraft()
    {
        var toRemove = new List<float>();
        foreach (var (cz, ac) in _runwayOccupants)
            if (ac == null) toRemove.Add(cz);
        foreach (var cz in toRemove)
            _runwayOccupants.Remove(cz);
    }

    private static GameObject BuildDefaultModel()
    {
        var mat  = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.white;

        var root = new GameObject("AircraftModel");

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform);
        body.transform.localScale = new Vector3(20f, 2f, 4f);
        body.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.Destroy(body.GetComponent<BoxCollider>());

        var wings = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wings.name = "Wings";
        wings.transform.SetParent(root.transform);
        wings.transform.localPosition = new Vector3(0f, -0.4f, 0f);
        wings.transform.localScale    = new Vector3(5f, 0.4f, 18f);
        wings.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.Destroy(wings.GetComponent<BoxCollider>());

        var tail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tail.name = "Tail";
        tail.transform.SetParent(root.transform);
        tail.transform.localPosition = new Vector3(-8f, 2f, 0f);
        tail.transform.localScale    = new Vector3(3f, 3f, 0.6f);
        tail.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.Destroy(tail.GetComponent<BoxCollider>());

        return root;
    }
}
