using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Fait apparaître des avions à intervalles réguliers sur les pistes disponibles.
/// </summary>
public class FlightScheduler : MonoBehaviour
{
    // ── Données du vol ─────────────────────────────────────────────────────
    [FoldoutGroup("Flight Scheduler")]
    [SerializeField] private AircraftData aircraftData;

    [FoldoutGroup("Flight Scheduler")]
    [Tooltip("Intervalle entre deux arrivées (minutes de jeu). 1 minute de jeu = 1 seconde réelle × SpeedMultiplier")]
    [SerializeField] private float spawnIntervalMinutes = 2f;

    // ── Stats ──────────────────────────────────────────────────────────────
    [FoldoutGroup("Flight Scheduler"), ShowInInspector, ReadOnly]
    private float _timerMinutes;

    [FoldoutGroup("Flight Scheduler"), ShowInInspector, ReadOnly]
    private int _totalSpawned;

    // ── Runtime ────────────────────────────────────────────────────────────
    private ZoneSystem          _zones;
    private const float         CellSize = 4f;
    private const float         HalfGrid = 128 * CellSize * 0.5f; // 256

    // Clé = CenterZ de la piste, valeur = avion en cours (null = libre)
    private readonly Dictionary<float, Aircraft> _runwayOccupants = new();

    // ── Struct piste ───────────────────────────────────────────────────────
    private struct RunwayInfo
    {
        public float StartX;   // seuil ouest (world)
        public float EndX;     // seuil est (world)
        public float CenterZ;  // axe central (world)
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        _zones = FindAnyObjectByType<ZoneSystem>();
    }

    private void Start()
    {
        // Premier avion après un court délai pour laisser AirportEnvironment peindre les zones
        _timerMinutes = spawnIntervalMinutes - 0.1f;
    }

    private void Update()
    {
        // Avance le timer en minutes de jeu (1 sec réelle × SpeedMultiplier = N min de jeu)
        float spd = TimeManager.Instance != null ? TimeManager.Instance.SpeedMultiplier : 1;
        _timerMinutes += Time.deltaTime * spd; // 1 réelle × spd = spd minutes de jeu

        if (_timerMinutes >= spawnIntervalMinutes)
        {
            _timerMinutes -= spawnIntervalMinutes;
            TrySpawnAircraft();
        }
    }

    // ── API ────────────────────────────────────────────────────────────────

    [Button("Spawn Test Aircraft"), FoldoutGroup("Flight Scheduler")]
    public void SpawnTestAircraft()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[FlightScheduler] Spawn disponible uniquement en mode Play.");
            return;
        }
        TrySpawnAircraft();
    }

    // ── Logique de spawn ───────────────────────────────────────────────────

    private void TrySpawnAircraft()
    {
        if (aircraftData == null)
        {
            Debug.LogWarning("[FlightScheduler] AircraftData non assigné.");
            return;
        }
        if (_zones == null) _zones = FindAnyObjectByType<ZoneSystem>();

        var runways = FindRunways();
        if (runways.Count == 0)
        {
            Debug.Log("[FlightScheduler] Aucune piste (Runway) trouvée dans ZoneSystem.");
            return;
        }

        // Cherche une piste sans avion actif
        PurgeDestroyedAircraft();

        RunwayInfo? chosen = null;
        foreach (var rw in runways)
        {
            if (!_runwayOccupants.TryGetValue(rw.CenterZ, out var ac) || ac == null)
            {
                chosen = rw;
                break;
            }
        }

        if (chosen == null)
        {
            Debug.Log("[FlightScheduler] Toutes les pistes occupées, avion en attente.");
            return;
        }

        SpawnOn(chosen.Value);
    }

    private void SpawnOn(RunwayInfo runway)
    {
        GameObject go;

        if (aircraftData.prefab != null)
        {
            go = Instantiate(aircraftData.prefab);
        }
        else
        {
            go = BuildDefaultModel();
        }

        go.name = $"Aircraft_{aircraftData.aircraftName}_{_totalSpawned}";

        var aircraft = go.AddComponent<Aircraft>();
        aircraft.Initialize(aircraftData, runway.StartX, runway.EndX, runway.CenterZ);

        _runwayOccupants[runway.CenterZ] = aircraft;
        _totalSpawned++;

        Debug.Log($"[FlightScheduler] {go.name} → piste Z={runway.CenterZ:0}");
    }

    // ── Détection des pistes ───────────────────────────────────────────────

    private List<RunwayInfo> FindRunways()
    {
        var result = new List<RunwayInfo>();
        if (_zones == null) return result;

        var cells = _zones.GetAllCellsOfZone(ZoneType.Runway);
        if (cells == null || cells.Count == 0) return result;

        // Trier par Y (profondeur Z dans le monde)
        cells.Sort((a, b) => a.y.CompareTo(b.y));

        // Grouper les cellules adjacentes en bandes horizontales (= pistes)
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

            // Ignorer les petites zones (pas une vraie piste)
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

    private void PurgeDestroyedAircraft()
    {
        var toRemove = new List<float>();
        foreach (var (cz, ac) in _runwayOccupants)
            if (ac == null) toRemove.Add(cz);
        foreach (var cz in toRemove)
            _runwayOccupants.Remove(cz);
    }

    // ── Modèle par défaut (cube allongé blanc) ────────────────────────────

    private static GameObject BuildDefaultModel()
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.white;

        // Fuselage
        var root   = new GameObject("AircraftModel");
        var body   = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name  = "Body";
        body.transform.SetParent(root.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale    = new Vector3(20f, 2f, 4f);
        body.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.Destroy(body.GetComponent<BoxCollider>());

        // Ailes
        var wings  = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wings.name = "Wings";
        wings.transform.SetParent(root.transform);
        wings.transform.localPosition = new Vector3(0f, -0.4f, 0f);
        wings.transform.localScale    = new Vector3(5f, 0.4f, 18f);
        wings.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.Destroy(wings.GetComponent<BoxCollider>());

        // Dérive (empennage vertical)
        var tail   = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tail.name  = "Tail";
        tail.transform.SetParent(root.transform);
        tail.transform.localPosition = new Vector3(-8f, 2f, 0f);
        tail.transform.localScale    = new Vector3(3f, 3f, 0.6f);
        tail.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.Destroy(tail.GetComponent<BoxCollider>());

        return root;
    }
}
