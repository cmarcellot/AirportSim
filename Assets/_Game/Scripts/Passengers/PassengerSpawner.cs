using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Spawn des passagers pour chaque vol arrivant à la gate.
/// Calcule le chemin landside une seule fois (partagé entre tous les passagers du vol).
/// </summary>
public class PassengerSpawner : MonoBehaviour
{
    // ── Config ───────────────────────────────────────────────────────────────

    [FoldoutGroup("Positions")]
    [SerializeField] private Vector3 spawnPosition       = new(0f, 0f, -144f); // bord sud de RoadAccess
    [FoldoutGroup("Positions")]
    [SerializeField] private Vector3 terminalEntry       = new(0f, 0f, -112f); // entrée sud du TerminalHall
    [FoldoutGroup("Positions")]
    [SerializeField] private Vector3 checkInPosition     = new(0f, 0f, -90f);  // zone check-in landside

    [FoldoutGroup("Spawn")]
    [SerializeField] private float   totalSpawnDuration  = 100f;  // en secondes (réelles) pour faire sortir tout le groupe
    [FoldoutGroup("Spawn")]
    [SerializeField] private float   spawnXJitter        = 6f;    // dispersion latérale sur la route

    // ── État ─────────────────────────────────────────────────────────────────

    [FoldoutGroup("État"), ShowInInspector, ReadOnly]
    public int TotalPassengersAlive { get; private set; }

    // ── Interne ──────────────────────────────────────────────────────────────

    private FlightScheduler                           _scheduler;
    private PathfindingSystem                         _pathfinding;
    private List<Vector3>                             _sharedLandsidePath;
    private readonly Dictionary<ActiveFlight, List<Passenger>> _flightPassengers  = new();
    private readonly Dictionary<ActiveFlight, Coroutine>       _activeSpawnCoros  = new();

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        // Compatible Domain Reload désactivé : nettoyer les passagers de la session précédente
        foreach (var p in FindObjectsByType<Passenger>())
            Destroy(p.gameObject);

        _flightPassengers.Clear();
        _activeSpawnCoros.Clear();
        TotalPassengersAlive = 0;
        _sharedLandsidePath  = null;

        if (_scheduler != null)
            _scheduler.OnFlightStatusChanged -= OnFlightStatus;

        StartCoroutine(LateInit());
    }

    private void OnDisable()
    {
        if (_scheduler != null)
            _scheduler.OnFlightStatusChanged -= OnFlightStatus;
    }

    private IEnumerator LateInit()
    {
        yield return null; // attend un frame que les autres systèmes soient prêts

        _scheduler   = FindAnyObjectByType<FlightScheduler>();
        _pathfinding = FindAnyObjectByType<PathfindingSystem>();

        if (_scheduler != null)
        {
            _scheduler.OnFlightStatusChanged -= OnFlightStatus;
            _scheduler.OnFlightStatusChanged += OnFlightStatus;
        }
        else
        {
            Debug.LogWarning("[PassengerSpawner] FlightScheduler introuvable.");
        }

        // Pré-calculer le chemin landside (partagé entre tous les passagers)
        BuildSharedPath();
    }

    // ── Gestion des vols ─────────────────────────────────────────────────────

    private void OnFlightStatus(ActiveFlight flight)
    {
        if (flight.Status == FlightStatus.AtGate)
        {
            if (_activeSpawnCoros.ContainsKey(flight)) return; // déjà en cours
            var co = StartCoroutine(SpawnCoroutine(flight));
            _activeSpawnCoros[flight] = co;
        }
        else if (flight.Status == FlightStatus.Departing || flight.Status == FlightStatus.Departed)
        {
            // Arrêter le spawn si toujours en cours
            if (_activeSpawnCoros.TryGetValue(flight, out var co) && co != null)
                StopCoroutine(co);
            _activeSpawnCoros.Remove(flight);

            // Retirer les passagers du vol (ils ont « embarqué »)
            DespawnFlight(flight);
        }
    }

    private IEnumerator SpawnCoroutine(ActiveFlight flight)
    {
        float capacity = flight.AircraftType?.passengerCapacity ?? 150f;
        int   count    = Mathf.RoundToInt(capacity * Random.Range(0.6f, 0.95f));
        count = Mathf.Max(1, count);

        float interval = Mathf.Clamp(totalSpawnDuration / count, 0.1f, 3f);

        if (!_flightPassengers.ContainsKey(flight))
            _flightPassengers[flight] = new List<Passenger>();

        for (int i = 0; i < count; i++)
        {
            SpawnOnePassenger(flight);
            yield return new WaitForSeconds(interval);
        }

        _activeSpawnCoros.Remove(flight);
    }

    private void SpawnOnePassenger(ActiveFlight flight)
    {
        var go = new GameObject($"Passenger_{flight.FlightNumber}");

        // Décalage aléatoire en X pour simuler des gens qui arrivent séparément
        float xOffset = Random.Range(-spawnXJitter, spawnXJitter);
        go.transform.position = new Vector3(
            spawnPosition.x + xOffset,
            0f,
            spawnPosition.z);

        var p = go.AddComponent<Passenger>();
        p.Initialize(flight, terminalEntry, _sharedLandsidePath);

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

    // ── Chemin landside ───────────────────────────────────────────────────────

    private void BuildSharedPath()
    {
        if (_pathfinding == null) return;

        _sharedLandsidePath = _pathfinding.FindLandsidePath(terminalEntry, checkInPosition);

        if (_sharedLandsidePath == null || _sharedLandsidePath.Count == 0)
            Debug.LogWarning("[PassengerSpawner] Chemin landside introuvable. " +
                             "Le terminal est-il peint ? Les passagers iront en ligne droite.");
    }

    // ── Odin ─────────────────────────────────────────────────────────────────

    [Button("Rebuild Landside Path"), FoldoutGroup("État")]
    private void RebuildPath()
    {
        if (!Application.isPlaying) return;
        if (_pathfinding == null) _pathfinding = FindAnyObjectByType<PathfindingSystem>();
        BuildSharedPath();
        Debug.Log($"[PassengerSpawner] Chemin landside : {_sharedLandsidePath?.Count ?? 0} waypoints.");
    }
}
