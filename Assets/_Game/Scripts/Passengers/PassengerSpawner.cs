using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Spawn des passagers pour chaque vol arrivant à la gate.
/// Chaque passager reçoit une destination finale avec jitter — ils ne se superposent pas.
/// </summary>
public class PassengerSpawner : MonoBehaviour
{
    // ── Config ───────────────────────────────────────────────────────────────

    [FoldoutGroup("Positions")]
    [SerializeField] private Vector3 spawnPosition   = new Vector3(0f, 0f, -144f); // bord sud RoadAccess
    [FoldoutGroup("Positions")]
    [SerializeField] private Vector3 terminalEntry   = new Vector3(0f, 0f, -112f); // entrée sud terminal
    [FoldoutGroup("Positions")]
    [SerializeField] private Vector3 checkInPosition = new Vector3(0f, 0f, -90f);  // centre zone check-in

    [FoldoutGroup("Spawn")]
    [SerializeField] private float totalSpawnDuration  = 100f; // secondes réelles pour faire sortir le groupe
    [FoldoutGroup("Spawn")]
    [SerializeField] private float spawnXJitter        = 6f;   // dispersion latérale au spawn (±)
    [FoldoutGroup("Spawn")]
    [SerializeField] private float destJitter          = 5f;   // dispersion autour du check-in (±)

    // ── État ─────────────────────────────────────────────────────────────────

    [FoldoutGroup("État"), ShowInInspector, ReadOnly]
    public int TotalPassengersAlive { get; private set; }

    // ── Interne ──────────────────────────────────────────────────────────────

    private FlightScheduler                                    _scheduler;
    private readonly Dictionary<ActiveFlight, List<Passenger>> _flightPassengers = new();
    private readonly Dictionary<ActiveFlight, Coroutine>       _spawnCoros       = new();

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        // Domain Reload désactivé : nettoyer les passagers de la session précédente
        foreach (var p in FindObjectsByType<Passenger>())
            Destroy(p.gameObject);

        _flightPassengers.Clear();
        _spawnCoros.Clear();
        TotalPassengersAlive = 0;

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

        _scheduler = FindAnyObjectByType<FlightScheduler>();
        if (_scheduler != null)
        {
            _scheduler.OnFlightStatusChanged -= OnFlightStatus;
            _scheduler.OnFlightStatusChanged += OnFlightStatus;
        }
        else
        {
            Debug.LogWarning("[PassengerSpawner] FlightScheduler introuvable.");
        }
    }

    // ── Gestion des vols ─────────────────────────────────────────────────────

    private void OnFlightStatus(ActiveFlight flight)
    {
        if (flight.Status == FlightStatus.AtGate)
        {
            if (_spawnCoros.ContainsKey(flight)) return;
            var co = StartCoroutine(SpawnCoroutine(flight));
            _spawnCoros[flight] = co;
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

        // Position de spawn : entrée de la route, dispersée en X
        float xSpawn = spawnPosition.x + Random.Range(-spawnXJitter, spawnXJitter);
        go.transform.position = new Vector3(xSpawn, 0f, spawnPosition.z);

        // Destination finale : zone check-in, dispersée en X et Z
        float xDest = checkInPosition.x + Random.Range(-destJitter, destJitter);
        float zDest = checkInPosition.z + Random.Range(-destJitter * 0.5f, destJitter * 0.5f);
        var   dest  = new Vector3(xDest, 0f, zDest);

        var p = go.AddComponent<Passenger>();
        p.Initialize(flight, terminalEntry, dest);

        _flightPassengers[flight].Add(p);
        TotalPassengersAlive++;
    }

    private void DespawnFlight(ActiveFlight flight)
    {
        if (!_flightPassengers.TryGetValue(flight, out var list)) return;
        foreach (var p in list)
        {
            if (p != null)
            {
                TotalPassengersAlive = Mathf.Max(0, TotalPassengersAlive - 1);
                Destroy(p.gameObject);
            }
        }
        _flightPassengers.Remove(flight);
    }
}
