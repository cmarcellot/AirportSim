using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

/// <summary>
/// Bâtiment dépôt : gère le pool de FuelTrucks et les dispatche
/// automatiquement quand un avion arrive à la gate.
/// </summary>
public class Depot : MonoBehaviour
{
    [FoldoutGroup("Depot Config")]
    [SerializeField] private int maxTrucks = 3;

    [FoldoutGroup("Depot Config")]
    [Tooltip("Prefab FuelTruck. Si vide, modèle généré par code.")]
    [SerializeField] private GameObject truckPrefab;

    [FoldoutGroup("Depot State"), ShowInInspector, ReadOnly]
    private int _availableCount;

    [FoldoutGroup("Depot State"), ShowInInspector, ReadOnly]
    private int _queueCount;

    private readonly List<GroundVehicle> _idle  = new();
    private readonly Queue<Aircraft>     _queue = new();
    private FlightScheduler              _scheduler;
    private TextMeshPro                  _countLabel;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private IEnumerator Start()
    {
        yield return null;

        for (int i = 0; i < maxTrucks; i++)
            SpawnTruck(i);

        _scheduler = FindAnyObjectByType<FlightScheduler>();
        if (_scheduler != null)
            _scheduler.OnFlightStatusChanged += OnFlightStatus;

        BuildIndicator();
        UpdateIndicator();
    }

    private void OnDestroy()
    {
        if (_scheduler != null)
            _scheduler.OnFlightStatusChanged -= OnFlightStatus;
    }

    // ── Événements vols ────────────────────────────────────────────────────

    private void OnFlightStatus(ActiveFlight flight)
    {
        if (flight.Status == FlightStatus.AtGate && flight.Aircraft != null)
            RequestService(flight.Aircraft);
    }

    // ── API publique ───────────────────────────────────────────────────────

    public void RequestService(Aircraft aircraft)
    {
        if (aircraft == null) return;

        if (_idle.Count > 0)
        {
            var truck = _idle[0];
            _idle.RemoveAt(0);
            truck.DispatchToAircraft(aircraft);
        }
        else
        {
            _queue.Enqueue(aircraft);
        }
        UpdateIndicator();
    }

    /// <summary>Position de parking pour un véhicule au retour (décalage par index).</summary>
    public Vector3 ParkingSpot(GroundVehicle vehicle)
    {
        int idx = _idle.Count; // camions déjà rentrés → décalage
        return transform.position + new Vector3((idx % 3) * 5f, 0f, (idx / 3) * 5f);
    }

    public void OnVehicleReturned(GroundVehicle vehicle)
    {
        // Purger avions déjà partis de la file
        while (_queue.Count > 0 &&
               (_queue.Peek() == null || _queue.Peek().State == AircraftState.Departed))
            _queue.Dequeue();

        if (_queue.Count > 0)
        {
            vehicle.DispatchToAircraft(_queue.Dequeue());
        }
        else
        {
            _idle.Add(vehicle);
            vehicle.transform.position = ParkingSpot(vehicle);
        }
        UpdateIndicator();
    }

    // ── Spawn camions ──────────────────────────────────────────────────────

    private void SpawnTruck(int index)
    {
        GameObject go = truckPrefab != null
            ? Instantiate(truckPrefab)
            : BuildDefaultTruck();

        go.name             = $"FuelTruck_{index + 1}";
        go.transform.position = transform.position + new Vector3(index * 5f, 0f, 0f);

        var truck = go.GetComponent<FuelTruck>() ?? go.AddComponent<FuelTruck>();
        truck.Initialize(this);
        _idle.Add(truck);
    }

    /// <summary>Construit un modèle FuelTruck procédural (placeholder).</summary>
    public static GameObject BuildDefaultTruck()
    {
        var yellowMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        yellowMat.color = new Color(1f, 0.85f, 0.05f);

        var redMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        redMat.color = new Color(0.80f, 0.15f, 0.10f);

        var root = new GameObject("FuelTruckModel");

        // Corps
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale    = new Vector3(4f, 1.5f, 2f);
        body.GetComponent<MeshRenderer>().sharedMaterial = yellowMat;
        UnityEngine.Object.DestroyImmediate(body.GetComponent<BoxCollider>());

        // Citerne rouge sur le dessus
        var tank = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tank.name = "Tank";
        tank.transform.SetParent(root.transform);
        tank.transform.localPosition = new Vector3(0.5f, 1.25f, 0f);
        tank.transform.localScale    = new Vector3(2.5f, 1f, 1.6f);
        tank.GetComponent<MeshRenderer>().sharedMaterial = redMat;
        UnityEngine.Object.DestroyImmediate(tank.GetComponent<BoxCollider>());

        return root;
    }

    // ── Visuels dépôt ─────────────────────────────────────────────────────

    private void BuildIndicator()
    {
        // Indicateur flottant : nombre de camions disponibles
        if (transform.Find("DepotLabel") != null) return;

        var go      = new GameObject("DepotLabel");
        go.transform.SetParent(transform);
        go.transform.localPosition = new Vector3(0f, 5f, 0f);

        _countLabel           = go.AddComponent<TextMeshPro>();
        _countLabel.fontSize  = 4f;
        _countLabel.alignment = TextAlignmentOptions.Center;
        _countLabel.color     = new Color(1f, 0.9f, 0.3f);

        go.transform.DOScale(Vector3.one * 1.08f, 1.1f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetLink(gameObject);
    }

    private void UpdateIndicator()
    {
        _availableCount = _idle.Count;
        _queueCount     = _queue.Count;
        if (_countLabel == null) return;
        _countLabel.text = _availableCount > 0
            ? $"DEPOT\n{_availableCount} dispo"
            : $"DEPOT\nfile: {_queueCount}";
    }
}
