using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public enum VehicleState
{
    Idle,
    MovingToAircraft,
    Servicing,
    Returning
}

/// <summary>
/// Classe de base pour tous les véhicules au sol.
/// Gère le déplacement via PathfindingSystem et la rotation fluide dans les virages.
/// </summary>
public abstract class GroundVehicle : MonoBehaviour
{
    [FoldoutGroup("Vehicle Config")]
    [SerializeField] protected float moveSpeed = 8f;

    [FoldoutGroup("Vehicle State"), ShowInInspector, ReadOnly]
    public VehicleState State { get; protected set; }

    [FoldoutGroup("Vehicle State"), ShowInInspector, ReadOnly]
    public Vector3 Destination { get; protected set; }

    [FoldoutGroup("Vehicle State"), ShowInInspector, ReadOnly]
    public Aircraft AssignedAircraft { get; protected set; }

    protected Depot            _depot;
    protected PathfindingSystem _pathfinding;
    protected Coroutine        _moveCo;

    protected virtual void Awake()
    {
        _pathfinding = FindAnyObjectByType<PathfindingSystem>();
    }

    protected virtual void OnDestroy()
    {
        if (_moveCo != null) StopCoroutine(_moveCo);
        foreach (var t in GetComponentsInChildren<Transform>(true))
            DOTween.Kill(t);
    }

    // ── API ────────────────────────────────────────────────────────────────

    public virtual void Initialize(Depot depot)
    {
        _depot = depot;
        State  = VehicleState.Idle;
    }

    public virtual void DispatchToAircraft(Aircraft aircraft)
    {
        if (aircraft == null) { ReturnToDepot(); return; }

        AssignedAircraft = aircraft;
        State            = VehicleState.MovingToAircraft;

        Vector3 target = aircraft.AssignedGate != null
            ? aircraft.AssignedGate.transform.position
            : aircraft.transform.position;
        Destination = target;

        if (_moveCo != null) StopCoroutine(_moveCo);
        _moveCo = StartCoroutine(MoveToPoint(target, OnArrivedAtAircraft));
    }

    public virtual void ReturnToDepot()
    {
        AssignedAircraft = null;
        State            = VehicleState.Returning;
        Vector3 home     = _depot != null ? _depot.ParkingSpot(this) : transform.position;
        Destination      = home;

        if (_moveCo != null) StopCoroutine(_moveCo);
        _moveCo = StartCoroutine(MoveToPoint(home, () =>
        {
            State = VehicleState.Idle;
            _depot?.OnVehicleReturned(this);
        }));
    }

    protected virtual void OnArrivedAtAircraft() { }

    // ── Mouvement ──────────────────────────────────────────────────────────

    protected IEnumerator MoveToPoint(Vector3 target, System.Action onArrived)
    {
        List<Vector3> path = null;
        if (_pathfinding != null)
            path = _pathfinding.FindAirsidePath(transform.position, target);

        if (path == null || path.Count == 0)
            path = new List<Vector3> { target };

        yield return StartCoroutine(FollowPath(path));
        onArrived?.Invoke();
    }

    protected IEnumerator FollowPath(List<Vector3> path)
    {
        float groundY = transform.position.y;

        foreach (var rawWp in path)
        {
            var wp  = new Vector3(rawWp.x, groundY, rawWp.z);
            var dir = new Vector3(wp.x - transform.position.x, 0f, wp.z - transform.position.z);

            if (dir.sqrMagnitude > 0.01f)
                transform.DORotateQuaternion(Quaternion.LookRotation(dir), 0.2f)
                         .SetEase(Ease.InOutSine)
                         .SetLink(gameObject);

            while (Vector3.Distance(
                       new Vector3(transform.position.x, 0f, transform.position.z),
                       new Vector3(wp.x, 0f, wp.z)) > 0.15f)
            {
                transform.position = Vector3.MoveTowards(transform.position, wp,
                                                          moveSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = wp;
        }
    }
}
