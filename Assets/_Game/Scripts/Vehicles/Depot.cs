using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Bâtiment dépôt : gère un pool de FuelTrucks et un pool de CateringTrucks.
/// Les deux types sont dépêchés en parallèle dès qu'un avion arrive à la gate.
/// </summary>
public class Depot : MonoBehaviour
{
    // ── Config ─────────────────────────────────────────────────────────────

    [FoldoutGroup("Depot Config")]
    [FormerlySerializedAs("maxTrucks")]
    [SerializeField] private int maxFuelTrucks = 3;

    [FoldoutGroup("Depot Config")]
    [SerializeField] private int maxCateringTrucks = 2;

    [FoldoutGroup("Depot Config")]
    [Tooltip("Prefab FuelTruck. Si vide, modèle généré par code.")]
    [FormerlySerializedAs("truckPrefab")]
    [SerializeField] private GameObject fuelTruckPrefab;

    [FoldoutGroup("Depot Config")]
    [Tooltip("Prefab CateringTruck. Si vide, modèle généré par code.")]
    [SerializeField] private GameObject cateringTruckPrefab;

    // ── État ───────────────────────────────────────────────────────────────

    [FoldoutGroup("Depot State"), ShowInInspector, ReadOnly]
    private int _fuelAvailable;

    [FoldoutGroup("Depot State"), ShowInInspector, ReadOnly]
    private int _cateringAvailable;

    [FoldoutGroup("Depot State"), ShowInInspector, ReadOnly]
    private int _fuelQueueCount;

    [FoldoutGroup("Depot State"), ShowInInspector, ReadOnly]
    private int _cateringQueueCount;

    // ── Pools ──────────────────────────────────────────────────────────────

    private readonly List<FuelTruck>    _idleFuel     = new();
    private readonly List<CateringTruck>_idleCatering = new();
    private readonly Queue<Aircraft>    _fuelQueue    = new();
    private readonly Queue<Aircraft>    _cateringQueue= new();

    private FlightScheduler _scheduler;
    private TextMeshPro     _countLabel;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void OnEnable()
    {
        // Détruire tous les véhicules de la session précédente (sans Scene Reload).
        foreach (var v in FindObjectsByType<FuelTruck>())     Destroy(v.gameObject);
        foreach (var v in FindObjectsByType<CateringTruck>()) Destroy(v.gameObject);
        _idleFuel.Clear();
        _idleCatering.Clear();
        _fuelQueue.Clear();
        _cateringQueue.Clear();

        // Anti-double-subscription : désabonner avant de ré-abonner.
        if (_scheduler != null) _scheduler.OnFlightStatusChanged -= OnFlightStatus;

        StartCoroutine(Init());
    }

    private IEnumerator Init()
    {
        yield return null;

        for (int i = 0; i < maxFuelTrucks;    i++) SpawnFuelTruck(i);
        for (int i = 0; i < maxCateringTrucks; i++) SpawnCateringTruck(i);

        _scheduler = FindAnyObjectByType<FlightScheduler>();
        if (_scheduler != null)
            _scheduler.OnFlightStatusChanged += OnFlightStatus;

        if (_countLabel == null) BuildIndicator();
        UpdateIndicator();
    }

    private void OnDisable()
    {
        if (_scheduler != null)
            _scheduler.OnFlightStatusChanged -= OnFlightStatus;
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
        {
            RequestFuelService(flight.Aircraft);
            RequestCateringService(flight.Aircraft);
        }
    }

    // ── API publique ───────────────────────────────────────────────────────

    public void RequestFuelService(Aircraft aircraft)
    {
        if (aircraft == null || maxFuelTrucks == 0) return;

        aircraft.SetFuelReady(false);

        if (_idleFuel.Count > 0)
        {
            var truck = _idleFuel[0];
            _idleFuel.RemoveAt(0);
            truck.DispatchToAircraft(aircraft);
        }
        else
        {
            _fuelQueue.Enqueue(aircraft);
        }
        UpdateIndicator();
    }

    public void RequestCateringService(Aircraft aircraft)
    {
        if (aircraft == null || maxCateringTrucks == 0) return;

        aircraft.SetCateringReady(false);

        if (_idleCatering.Count > 0)
        {
            var truck = _idleCatering[0];
            _idleCatering.RemoveAt(0);
            truck.DispatchToAircraft(aircraft);
        }
        else
        {
            _cateringQueue.Enqueue(aircraft);
        }
        UpdateIndicator();
    }

    /// <summary>Position de parking selon le type et l'index dans le pool idle.</summary>
    public Vector3 ParkingSpot(GroundVehicle vehicle)
    {
        if (vehicle is FuelTruck)
        {
            // Côté gauche du dépôt (-X)
            int idx = _idleFuel.Count;
            return transform.position + new Vector3(-(idx + 1) * 5f, 0f, 0f);
        }
        else
        {
            // Côté droit du dépôt (+X)
            int idx = _idleCatering.Count;
            return transform.position + new Vector3((idx + 1) * 5f, 0f, 0f);
        }
    }

    public void OnVehicleReturned(GroundVehicle vehicle)
    {
        if (vehicle is FuelTruck fuelTruck)
            HandleFuelReturn(fuelTruck);
        else if (vehicle is CateringTruck cateringTruck)
            HandleCateringReturn(cateringTruck);

        UpdateIndicator();
    }

    // ── Retour au dépôt par type ───────────────────────────────────────────

    private void HandleFuelReturn(FuelTruck truck)
    {
        PurgeDeparted(_fuelQueue);

        if (_fuelQueue.Count > 0)
        {
            truck.DispatchToAircraft(_fuelQueue.Dequeue());
        }
        else
        {
            int parkIdx = _idleFuel.Count;
            _idleFuel.Add(truck);
            truck.transform.position = transform.position + new Vector3(-(parkIdx + 1) * 5f, 0f, 0f);
        }
    }

    private void HandleCateringReturn(CateringTruck truck)
    {
        PurgeDeparted(_cateringQueue);

        if (_cateringQueue.Count > 0)
        {
            truck.DispatchToAircraft(_cateringQueue.Dequeue());
        }
        else
        {
            int parkIdx = _idleCatering.Count;
            _idleCatering.Add(truck);
            truck.transform.position = transform.position + new Vector3((parkIdx + 1) * 5f, 0f, 0f);
        }
    }

    private static void PurgeDeparted(Queue<Aircraft> queue)
    {
        while (queue.Count > 0 &&
               (queue.Peek() == null || queue.Peek().State == AircraftState.Departed))
            queue.Dequeue();
    }

    // ── Spawn ──────────────────────────────────────────────────────────────

    private void SpawnFuelTruck(int index)
    {
        GameObject go = fuelTruckPrefab != null
            ? Instantiate(fuelTruckPrefab)
            : BuildDefaultFuelTruck();

        go.name               = $"FuelTruck_{index + 1}";
        go.transform.position = transform.position + new Vector3(-(index + 1) * 5f, 0f, 0f);

        var truck = go.GetComponent<FuelTruck>() ?? go.AddComponent<FuelTruck>();
        truck.Initialize(this);
        _idleFuel.Add(truck);
    }

    private void SpawnCateringTruck(int index)
    {
        GameObject go = cateringTruckPrefab != null
            ? Instantiate(cateringTruckPrefab)
            : BuildDefaultCateringTruck();

        go.name               = $"CateringTruck_{index + 1}";
        go.transform.position = transform.position + new Vector3((index + 1) * 5f, 0f, 0f);

        var truck = go.GetComponent<CateringTruck>() ?? go.AddComponent<CateringTruck>();
        truck.Initialize(this);
        _idleCatering.Add(truck);
    }

    // ── Modèles procéduraux ────────────────────────────────────────────────

    public static GameObject BuildDefaultFuelTruck()
    {
        var yellowMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        yellowMat.color = new Color(1f, 0.85f, 0.05f);

        var redMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        redMat.color = new Color(0.80f, 0.15f, 0.10f);

        var root = new GameObject("FuelTruckModel");

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale    = new Vector3(4f, 1.5f, 2f);
        body.GetComponent<MeshRenderer>().sharedMaterial = yellowMat;
        UnityEngine.Object.DestroyImmediate(body.GetComponent<BoxCollider>());

        var tank = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tank.name = "Tank";
        tank.transform.SetParent(root.transform);
        tank.transform.localPosition = new Vector3(0.5f, 1.25f, 0f);
        tank.transform.localScale    = new Vector3(2.5f, 1f, 1.6f);
        tank.GetComponent<MeshRenderer>().sharedMaterial = redMat;
        UnityEngine.Object.DestroyImmediate(tank.GetComponent<BoxCollider>());

        return root;
    }

    public static GameObject BuildDefaultCateringTruck()
    {
        var whiteMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        whiteMat.color = new Color(0.95f, 0.95f, 0.95f);

        var blueMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        blueMat.color = new Color(0.15f, 0.35f, 0.90f);

        var root = new GameObject("CateringTruckModel");

        // Châssis blanc allongé
        var chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chassis.name = "Chassis";
        chassis.transform.SetParent(root.transform);
        chassis.transform.localPosition = Vector3.zero;
        chassis.transform.localScale    = new Vector3(4f, 1f, 2f);
        chassis.GetComponent<MeshRenderer>().sharedMaterial = whiteMat;
        UnityEngine.Object.DestroyImmediate(chassis.GetComponent<BoxCollider>());

        // Plateforme bleue élévatrice (DOTween sur Y dans CateringTruck)
        var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "Platform";
        platform.transform.SetParent(root.transform);
        platform.transform.localPosition = new Vector3(0f, 1f, 0f);
        platform.transform.localScale    = new Vector3(3f, 0.4f, 1.8f);
        platform.GetComponent<MeshRenderer>().sharedMaterial = blueMat;
        UnityEngine.Object.DestroyImmediate(platform.GetComponent<BoxCollider>());

        return root;
    }

    // ── Indicateur visuel ─────────────────────────────────────────────────

    private void BuildIndicator()
    {
        if (transform.Find("DepotLabel") != null) return;

        var go = new GameObject("DepotLabel");
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
        _fuelAvailable     = _idleFuel.Count;
        _cateringAvailable = _idleCatering.Count;
        _fuelQueueCount    = _fuelQueue.Count;
        _cateringQueueCount= _cateringQueue.Count;

        if (_countLabel == null) return;

        string fuelLine    = _fuelAvailable > 0
            ? $"Fuel: {_fuelAvailable} dispo"
            : $"Fuel: file {_fuelQueueCount}";

        string cateringLine = _cateringAvailable > 0
            ? $"Cat.: {_cateringAvailable} dispo"
            : $"Cat.: file {_cateringQueueCount}";

        _countLabel.text = $"DEPOT\n{fuelLine}\n{cateringLine}";
    }
}
