using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Spawn des passagers pour chaque vol arrivant à la gate.
/// - Cherche les vraies cellules CheckInArea (ou TerminalHall en fallback) via ZoneSystem.
/// - Calcule un chemin A* landside pour chaque passager.
/// - Chaque passager reçoit une cellule de destination aléatoire + jitter sub-cellule.
/// </summary>
public class PassengerSpawner : MonoBehaviour
{
    // ── Config ───────────────────────────────────────────────────────────────

    [FoldoutGroup("Positions")]
    [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 0f, -144f); // bord sud RoadAccess
    [FoldoutGroup("Positions")]
    [SerializeField] private Vector3 terminalEntry = new Vector3(0f, 0f, -112f); // entrée sud TerminalHall

    [FoldoutGroup("Spawn")]
    [SerializeField] private float totalSpawnDuration = 100f; // secondes réelles pour sortir tout le groupe
    [FoldoutGroup("Spawn")]
    [SerializeField] private float spawnXJitter       = 6f;   // dispersion latérale au spawn (±)

    // ── État ─────────────────────────────────────────────────────────────────

    [FoldoutGroup("État"), ShowInInspector, ReadOnly]
    public int TotalPassengersAlive { get; private set; }

    [FoldoutGroup("État"), ShowInInspector, ReadOnly]
    private int CheckInCellCount => _checkInCells?.Count ?? 0;

    // ── Interne ──────────────────────────────────────────────────────────────

    private FlightScheduler   _scheduler;
    private ZoneSystem        _zones;
    private PathfindingSystem _pathfinding;

    // Cellules de destination (CheckInArea, ou TerminalHall en fallback)
    private List<Vector2Int> _checkInCells;

    private readonly Dictionary<ActiveFlight, List<Passenger>> _flightPassengers = new();
    private readonly Dictionary<ActiveFlight, Coroutine>       _spawnCoros       = new();

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        foreach (var p in FindObjectsByType<Passenger>())
            Destroy(p.gameObject);

        _flightPassengers.Clear();
        _spawnCoros.Clear();
        TotalPassengersAlive = 0;
        _checkInCells        = null;

        if (_scheduler != null)
            _scheduler.OnFlightStatusChanged -= OnFlightStatus;
        if (_zones != null)
            _zones.OnZoneChanged -= InvalidateDestCache;

        StartCoroutine(LateInit());
    }

    private void OnDisable()
    {
        if (_scheduler != null)
            _scheduler.OnFlightStatusChanged -= OnFlightStatus;
        if (_zones != null)
            _zones.OnZoneChanged -= InvalidateDestCache;
    }

    private IEnumerator LateInit()
    {
        yield return null; // attend que AirportEnvironment ait peint les zones

        _scheduler   = FindAnyObjectByType<FlightScheduler>();
        _zones       = FindAnyObjectByType<ZoneSystem>();
        _pathfinding = FindAnyObjectByType<PathfindingSystem>();

        if (_scheduler != null)
        {
            _scheduler.OnFlightStatusChanged -= OnFlightStatus;
            _scheduler.OnFlightStatusChanged += OnFlightStatus;
        }
        else Debug.LogWarning("[PassengerSpawner] FlightScheduler introuvable.");

        if (_zones != null)
        {
            _zones.OnZoneChanged -= InvalidateDestCache;
            _zones.OnZoneChanged += InvalidateDestCache;
        }

        BuildDestinationCache();
    }

    // ── Cache de cellules ────────────────────────────────────────────────────

    private void InvalidateDestCache() => _checkInCells = null;

    private void BuildDestinationCache()
    {
        if (_zones == null) return;

        // Préférence : CheckInArea → sinon TerminalHall
        _checkInCells = _zones.GetAllCellsOfZone(ZoneType.CheckInArea);
        if (_checkInCells.Count == 0)
        {
            _checkInCells = _zones.GetAllCellsOfZone(ZoneType.TerminalHall);
            if (_checkInCells.Count > 0)
                Debug.Log("[PassengerSpawner] Aucune CheckInArea — utilise TerminalHall.");
        }

        if (_checkInCells.Count == 0)
            Debug.LogWarning("[PassengerSpawner] Aucune zone landside peinte. " +
                             "Les passagers s'arrêteront à l'entrée du terminal.");
    }

    // ── Gestion des vols ─────────────────────────────────────────────────────

    private void OnFlightStatus(ActiveFlight flight)
    {
        if (flight.Status == FlightStatus.AtGate)
        {
            if (_spawnCoros.ContainsKey(flight)) return;
            _spawnCoros[flight] = StartCoroutine(SpawnCoroutine(flight));
        }
        else if (flight.Status == FlightStatus.Departing || flight.Status == FlightStatus.Departed)
        {
            if (_spawnCoros.TryGetValue(flight, out var co) && co != null)
                StopCoroutine(co);
            _spawnCoros.Remove(flight);
            DespawnFlight(flight);
        }
    }

    private IEnumerator SpawnCoroutine(ActiveFlight flight)
    {
        if (!_flightPassengers.ContainsKey(flight))
            _flightPassengers[flight] = new List<Passenger>();

        if (_checkInCells == null) BuildDestinationCache();

        float capacity = flight.AircraftType?.passengerCapacity ?? 150f;
        int   count    = Mathf.Max(1, Mathf.RoundToInt(capacity * Random.Range(0.6f, 0.95f)));
        float interval = Mathf.Clamp(totalSpawnDuration / count, 0.15f, 3f);

        for (int i = 0; i < count; i++)
        {
            SpawnOnePassenger(flight);
            yield return new WaitForSeconds(interval);
        }

        _spawnCoros.Remove(flight);
    }

    private void SpawnOnePassenger(ActiveFlight flight)
    {
        var go = new GameObject($"Pax_{flight.FlightNumber}");
        float xSpawn = spawnPosition.x + Random.Range(-spawnXJitter, spawnXJitter);
        go.transform.position = new Vector3(xSpawn, 0f, spawnPosition.z);

        List<Vector3> path     = new List<Vector3>();
        Vector3       finalDest;

        if (_checkInCells != null && _checkInCells.Count > 0)
        {
            // Cellule de destination aléatoire
            var cell       = _checkInCells[Random.Range(0, _checkInCells.Count)];
            var cellCenter = CellCenterWorld(cell);

            // Jitter sub-cellule (cellSize=4 → ±1.5 reste dans la cellule)
            finalDest = new Vector3(
                cellCenter.x + Random.Range(-1.5f, 1.5f),
                0f,
                cellCenter.z + Random.Range(-1.5f, 1.5f));

            // Chemin A* landside vers le centre de la cellule
            if (_pathfinding != null)
                path = _pathfinding.FindLandsidePath(terminalEntry, cellCenter);
        }
        else
        {
            // Aucune zone peinte : s'arrête à l'entrée du terminal
            finalDest = terminalEntry;
        }

        var p = go.AddComponent<Passenger>();
        p.Initialize(flight, terminalEntry, path, finalDest);

        _flightPassengers[flight].Add(p);
        TotalPassengersAlive++;
    }

    private void DespawnFlight(ActiveFlight flight)
    {
        if (!_flightPassengers.TryGetValue(flight, out var list)) return;
        foreach (var p in list)
            if (p != null)
            {
                TotalPassengersAlive = Mathf.Max(0, TotalPassengersAlive - 1);
                Destroy(p.gameObject);
            }
        _flightPassengers.Remove(flight);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Reproduit PathfindingSystem.CellToWorld (privé).
    /// Paramètres du projet : gridDimensions=128×128, cellSize=4 → half=256.
    /// </summary>
    private static Vector3 CellCenterWorld(Vector2Int cell) =>
        new Vector3((cell.x + 0.5f) * 4f - 256f, 0f, (cell.y + 0.5f) * 4f - 256f);

    // ── Odin ─────────────────────────────────────────────────────────────────

    [Button("Refresh Zone Cache"), FoldoutGroup("État")]
    private void RefreshCache()
    {
        if (!Application.isPlaying) return;
        if (_zones == null) _zones = FindAnyObjectByType<ZoneSystem>();
        _checkInCells = null;
        BuildDestinationCache();
        Debug.Log($"[PassengerSpawner] {_checkInCells?.Count ?? 0} cellule(s) de destination.");
    }
}
