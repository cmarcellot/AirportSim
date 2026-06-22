using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Gère le planning des vols, le spawn des avions sur les pistes,
/// l'assignation des gates, et diffuse les événements vers le FlightBoard.
/// </summary>
public class FlightScheduler : MonoBehaviour
{
    // ── Configuration ──────────────────────────────────────────────────────
    [FoldoutGroup("Scheduled Flights")]
    [Tooltip("Vols planifiés manuellement. Si vide, des vols aléatoires sont générés.")]
    [SerializeField] private List<FlightData> scheduledFlights = new();

    [FoldoutGroup("Scheduled Flights")]
    [Tooltip("Intervalle en minutes de jeu entre deux vols auto-générés")]
    [SerializeField] private float autoSpawnIntervalMinutes = 3f;

    [FoldoutGroup("Scheduled Flights")]
    [Tooltip("Nombre de vols auto-générés si la liste est vide")]
    [SerializeField] private int autoFlightCount = 20;

    // ── Runtime inspector ──────────────────────────────────────────────────
    [FoldoutGroup("Active Flights"), ShowInInspector, ReadOnly]
    private List<ActiveFlight> _activeFlights = new();

    [FoldoutGroup("Active Flights"), ShowInInspector, ReadOnly]
    private List<ActiveFlight> _pendingFlights = new();

    [FoldoutGroup("Active Flights"), ShowInInspector, ReadOnly]
    private List<ActiveFlight> _waitingFlights = new(); // prêts mais en attente runway/gate

    // ── Événements UI ──────────────────────────────────────────────────────
    public event Action<ActiveFlight> OnFlightStatusChanged;

    // ── Interne ────────────────────────────────────────────────────────────
    private ZoneSystem        _zones;
    private PathfindingSystem _pathfinding;
    private bool              _ready;
    public  bool              IsReady => _ready;

    private const float CellSize = 4f;
    private const float HalfGrid = 128 * CellSize * 0.5f;

    private readonly Dictionary<float, Aircraft> _runwayOccupants = new();

    private static readonly (string airline, string prefix, Color color)[] Airlines =
    {
        ("Air France",      "AF",  new Color(0.01f, 0.34f, 0.72f)),
        ("KLM",             "KL",  new Color(0.00f, 0.46f, 0.69f)),
        ("Lufthansa",       "LH",  new Color(0.97f, 0.77f, 0.01f)),
        ("British Airways", "BA",  new Color(0.47f, 0.09f, 0.12f)),
        ("EasyJet",         "EZY", new Color(1.00f, 0.55f, 0.00f)),
        ("Ryanair",         "FR",  new Color(0.00f, 0.45f, 0.10f)),
    };

    private struct RunwayInfo { public float StartX, EndX, CenterZ; }

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        _zones       = FindAnyObjectByType<ZoneSystem>();
        _pathfinding = FindAnyObjectByType<PathfindingSystem>();
    }

    private void OnEnable()
    {
        // Détruire les avions résiduels de la session précédente (sans Scene Reload).
        foreach (var f in _activeFlights)
            if (f.Aircraft != null) Destroy(f.Aircraft.gameObject);

        _activeFlights.Clear();
        _pendingFlights.Clear();
        _waitingFlights.Clear();
        _runwayOccupants.Clear();
        _ready         = false;
        _flightCounter = 0;

        StartCoroutine(Init());
    }

    private IEnumerator Init()
    {
        yield return null; // attendre AirportEnvironment.Start() / OnEnable()

        if (_zones != null) PaintRunwayConnectors();

        if (scheduledFlights.Count > 0)
            BuildPendingFromAssets();
        else
            GenerateRandomSchedule();

        _pendingFlights.Sort((a, b) => a.ScheduledArrival.CompareTo(b.ScheduledArrival));

        _ready = true;

        foreach (var f in _pendingFlights)
            OnFlightStatusChanged?.Invoke(f);
    }

    private void Update()
    {
        if (!_ready) return;
        CheckPendingFlights();
        RetryWaitingFlights();
        PollActiveFlights();
    }

    // ── API publique ───────────────────────────────────────────────────────

    /// <summary>Retourne tous les vols (pending + waiting + actifs) pour le FlightBoard.</summary>
    public List<ActiveFlight> GetAllFlights()
    {
        var all = new List<ActiveFlight>(_pendingFlights);
        all.AddRange(_waitingFlights);
        all.AddRange(_activeFlights);
        all.Sort((a, b) => a.ScheduledArrival.CompareTo(b.ScheduledArrival));
        return all;
    }

    public void AssignGateToAircraft(Aircraft aircraft)
    {
        if (aircraft == null) return;

        var gates = FindObjectsByType<Gate>();
        Gate chosen = null;
        foreach (var g in gates)
            if (g.IsAvailable()) { chosen = g; break; }

        if (chosen == null)
        {
            Debug.Log("[FlightScheduler] Aucune gate disponible.");
            return;
        }

        chosen.Reserve();

        List<Vector3> path = null;
        if (_pathfinding != null)
            path = _pathfinding.FindAirsidePath(aircraft.transform.position, chosen.transform.position);

        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("[FlightScheduler] Pas de chemin airside vers la gate.");
            chosen.ReleaseAircraft();
            return;
        }

        FreeRunwayOf(aircraft);
        aircraft.StartTaxi(path, chosen);

        // Mettre à jour le vol actif correspondant
        var flight = FindActiveFlightFor(aircraft);
        if (flight != null)
        {
            flight.AssignedGate = chosen;
            OnFlightStatusChanged?.Invoke(flight);
        }
    }

    // ── Odin buttons ───────────────────────────────────────────────────────

    [Button("Add Random Flight"), FoldoutGroup("Scheduled Flights")]
    public void AddRandomFlight()
    {
        if (!Application.isPlaying) return;
        float hour  = TimeManager.Instance != null
                      ? TimeManager.Instance.CurrentHour + 0.05f // +3 min de jeu
                      : 6.1f;
        var flight = MakeRandomFlight(hour);
        _pendingFlights.Add(flight);
        OnFlightStatusChanged?.Invoke(flight);
        Debug.Log($"[FlightScheduler] Vol ajouté : {flight}");
    }

    [Button("Clear All Flights"), FoldoutGroup("Scheduled Flights")]
    public void ClearAllFlights()
    {
        if (!Application.isPlaying) return;
        foreach (var f in _activeFlights)
            if (f.Aircraft != null) Destroy(f.Aircraft.gameObject);
        _activeFlights.Clear();
        _pendingFlights.Clear();
        _waitingFlights.Clear();
        _runwayOccupants.Clear();
    }

    // ── Scheduling ─────────────────────────────────────────────────────────

    private void BuildPendingFromAssets()
    {
        foreach (var data in scheduledFlights)
        {
            if (data == null) continue;
            _pendingFlights.Add(new ActiveFlight
            {
                FlightNumber     = data.flightNumber,
                Airline          = data.airline,
                AirlineColor     = data.airlineColor,
                ScheduledArrival = data.scheduledArrival,
                AircraftType     = data.aircraftType,
                Status           = FlightStatus.Scheduled,
            });
        }
    }

    private void GenerateRandomSchedule()
    {
        float startHour = TimeManager.Instance != null
                          ? TimeManager.Instance.CurrentHour + 0.05f
                          : 6.1f;
        float intervalH = autoSpawnIntervalMinutes / 60f;

        for (int i = 0; i < autoFlightCount; i++)
            _pendingFlights.Add(MakeRandomFlight(startHour + i * intervalH));
    }

    private void CheckPendingFlights()
    {
        if (TimeManager.Instance == null) return;
        float now = TimeManager.Instance.CurrentHour;

        for (int i = _pendingFlights.Count - 1; i >= 0; i--)
        {
            var f = _pendingFlights[i];
            if (now < f.ScheduledArrival) continue;
            _pendingFlights.RemoveAt(i);
            TrySpawnFlight(f);
        }
    }

    private void RetryWaitingFlights()
    {
        for (int i = _waitingFlights.Count - 1; i >= 0; i--)
        {
            var f = _waitingFlights[i];
            if (!CanSpawn()) break; // plus de place, inutile de continuer
            _waitingFlights.RemoveAt(i);
            TrySpawnFlight(f);
        }
    }

    private bool CanSpawn()
    {
        // Gates libres > avions déjà en approche
        var gates = FindObjectsByType<Gate>();
        if (gates.Length == 0) return false;
        int free = 0;
        foreach (var g in gates) if (g.IsAvailable()) free++;
        int inbound = 0;
        foreach (var ac in FindObjectsByType<Aircraft>())
            if (ac.State == AircraftState.Approaching ||
                ac.State == AircraftState.Landing     ||
                ac.State == AircraftState.Idle) inbound++;
        return free > inbound;
    }

    private void TrySpawnFlight(ActiveFlight flight)
    {
        if (!CanSpawn())
        {
            _waitingFlights.Add(flight);
            return;
        }

        PurgeDestroyedAircraft();
        var runways = FindRunways();
        RunwayInfo? chosen = null;
        foreach (var rw in runways)
        {
            if (!_runwayOccupants.TryGetValue(rw.CenterZ, out var ac) || ac == null)
            { chosen = rw; break; }
        }

        if (chosen == null)
        {
            _waitingFlights.Add(flight);
            return;
        }

        SpawnOn(chosen.Value, flight);
    }

    // ── Spawn ──────────────────────────────────────────────────────────────

    private void SpawnOn(RunwayInfo runway, ActiveFlight flight)
    {
        // Choisir l'AircraftData : celle du vol ou la première disponible dans la scène
        AircraftData data = flight.AircraftType;
        if (data == null)
        {
            // Récupérer la première AircraftData connue via un autre flight ou un composant existant
            var existingScheduler = this;
            data = null;
            // Fallback : chercher dans les assets ScriptableObject en mémoire
            foreach (var f in scheduledFlights) if (f != null && f.aircraftType != null) { data = f.aircraftType; break; }
        }

        var prefabGo = (data != null && data.prefab != null) ? Instantiate(data.prefab) : BuildDefaultModel();
        prefabGo.name = $"Aircraft_{flight.FlightNumber}";

        var aircraft = prefabGo.AddComponent<Aircraft>();
        aircraft.OnLanded += OnAircraftLanded;

        // Utiliser AircraftData fictive si nécessaire (vitesses par défaut)
        if (data == null) data = ScriptableObject.CreateInstance<AircraftData>();
        aircraft.Initialize(data, runway.StartX, runway.EndX, runway.CenterZ);

        _runwayOccupants[runway.CenterZ] = aircraft;

        flight.Aircraft = aircraft;
        flight.Status   = FlightStatus.Approaching;
        _activeFlights.Add(flight);

        OnFlightStatusChanged?.Invoke(flight);
        Notify(flight, FlightStatus.Approaching);

        Debug.Log($"[FlightScheduler] {flight.FlightNumber} → piste Z={runway.CenterZ:0}");
    }

    // ── Polling état vols ──────────────────────────────────────────────────

    private void PollActiveFlights()
    {
        foreach (var flight in _activeFlights)
        {
            if (flight.Aircraft != null)
            {
                // Capture gate dès qu'elle est assignée
                if (flight.AssignedGate == null && flight.Aircraft.AssignedGate != null)
                {
                    flight.AssignedGate = flight.Aircraft.AssignedGate;
                    OnFlightStatusChanged?.Invoke(flight);
                }

                var newStatus = MapState(flight.Aircraft.State);
                if (newStatus != flight.Status)
                    UpdateFlightStatus(flight, newStatus);
            }
            else if (flight.Status != FlightStatus.Departed &&
                     flight.Status != FlightStatus.Scheduled)
            {
                // Aircraft détruit → décollé
                UpdateFlightStatus(flight, FlightStatus.Departed);
            }
        }
    }

    private void UpdateFlightStatus(ActiveFlight flight, FlightStatus newStatus)
    {
        flight.Status = newStatus;
        OnFlightStatusChanged?.Invoke(flight);
        Notify(flight, newStatus);
    }

    private static FlightStatus MapState(AircraftState s) => s switch
    {
        AircraftState.Approaching     => FlightStatus.Approaching,
        AircraftState.Landing         => FlightStatus.Landing,
        AircraftState.Idle            => FlightStatus.Landing,
        AircraftState.Taxiing         => FlightStatus.Landing,
        AircraftState.AtGate          => FlightStatus.AtGate,
        AircraftState.Boarding        => FlightStatus.AtGate,
        AircraftState.Departing       => FlightStatus.Departing,
        AircraftState.TaxiingToRunway => FlightStatus.Departing,
        AircraftState.TakingOff       => FlightStatus.Departing,
        AircraftState.Departed        => FlightStatus.Departed,
        _                             => FlightStatus.Scheduled
    };

    // ── Notifications ──────────────────────────────────────────────────────

    private static void Notify(ActiveFlight flight, FlightStatus status)
    {
        if (FlightNotificationSystem.Instance == null) return;
        string msg = status switch
        {
            FlightStatus.Approaching => $"[VOL] {flight.FlightNumber} en approche",
            FlightStatus.AtGate      => $"[VOL] {flight.FlightNumber} à la gate {flight.GateLabel}",
            FlightStatus.Departed    => $"[VOL] {flight.FlightNumber} décollé — +50 000 $",
            _                        => null
        };
        if (msg != null) FlightNotificationSystem.Instance.Show(msg);
    }

    // ── Callback atterrissage ──────────────────────────────────────────────

    private void OnAircraftLanded(Aircraft aircraft)
    {
        aircraft.OnLanded -= OnAircraftLanded;
        AssignGateToAircraft(aircraft);
    }

    // ── Générateur aléatoire ───────────────────────────────────────────────

    private int _flightCounter;

    private ActiveFlight MakeRandomFlight(float scheduledHour)
    {
        var (airline, prefix, color) = Airlines[_flightCounter % Airlines.Length];
        _flightCounter++;
        int num = 1000 + UnityEngine.Random.Range(0, 9000);
        return new ActiveFlight
        {
            FlightNumber     = $"{prefix}{num}",
            Airline          = airline,
            AirlineColor     = color,
            ScheduledArrival = scheduledHour,
            AircraftType     = null,
            Status           = FlightStatus.Scheduled,
        };
    }

    // ── Connexion zones ────────────────────────────────────────────────────

    private void PaintRunwayConnectors()
    {
        var allRunway = _zones.GetAllCellsOfZone(ZoneType.Runway);
        var allTaxi   = _zones.GetAllCellsOfZone(ZoneType.Taxiway);
        if (allRunway == null || allRunway.Count == 0) return;
        if (allTaxi   == null || allTaxi.Count   == 0) return;

        int maxTaxiY = 0, minTaxiX = int.MaxValue, maxTaxiX = 0;
        foreach (var c in allTaxi)
        {
            if (c.y > maxTaxiY) maxTaxiY = c.y;
            if (c.x < minTaxiX) minTaxiX = c.x;
            if (c.x > maxTaxiX) maxTaxiX = c.x;
        }

        int maxRunwayY = 0;
        var runwayYSet = new HashSet<int>();
        foreach (var c in allRunway) { if (c.y > maxRunwayY) maxRunwayY = c.y; runwayYSet.Add(c.y); }

        if (maxRunwayY <= maxTaxiY) return;

        var connectors = new List<Vector2Int>();
        for (int y = maxTaxiY + 1; y <= maxRunwayY; y++)
            if (!runwayYSet.Contains(y))
                for (int x = minTaxiX; x <= maxTaxiX; x++)
                    connectors.Add(new Vector2Int(x, y));

        if (connectors.Count > 0)
        {
            _zones.SetZoneBatch(connectors, ZoneType.Taxiway);
            Debug.Log($"[FlightScheduler] {connectors.Count} cellules connecteur peintes.");
        }
    }

    // ── Détection pistes ───────────────────────────────────────────────────

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

            result.Add(new RunwayInfo
            {
                StartX  = bandMinX * CellSize - HalfGrid,
                EndX    = (bandMaxX + 1) * CellSize - HalfGrid,
                CenterZ = ((bandMinY * CellSize - HalfGrid) + ((bandMaxY + 1) * CellSize - HalfGrid)) * 0.5f,
            });
        }
        return result;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private ActiveFlight FindActiveFlightFor(Aircraft aircraft) =>
        _activeFlights.Find(f => f.Aircraft == aircraft);

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
        foreach (var cz in toRemove) _runwayOccupants.Remove(cz);
    }

    private static GameObject BuildDefaultModel()
    {
        var mat  = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.white;
        var root  = new GameObject("AircraftModel");

        void MakePart(string n, Vector3 pos, Vector3 scale)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = n;
            p.transform.SetParent(root.transform);
            p.transform.localPosition = pos;
            p.transform.localScale    = scale;
            p.GetComponent<MeshRenderer>().sharedMaterial = mat;
            UnityEngine.Object.Destroy(p.GetComponent<BoxCollider>());
        }

        MakePart("Body",  Vector3.zero,            new Vector3(20f, 2f, 4f));
        MakePart("Wings", new Vector3(0f, -0.4f, 0f), new Vector3(5f, 0.4f, 18f));
        MakePart("Tail",  new Vector3(-8f, 2f, 0f),   new Vector3(3f, 3f, 0.6f));
        return root;
    }
}
