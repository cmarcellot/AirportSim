using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public enum PassengerState
{
    Arriving, WaitingCheckin, CheckingIn,
    WaitingSecurity, PassingSecurity,
    WaitingGate, Boarding, Boarded
}

/// <summary>
/// Passager individuel. Reçoit son vol et son chemin depuis PassengerSpawner.
/// </summary>
public class Passenger : MonoBehaviour
{
    // ── État ───────────────────────────────────────────────────────────────

    [FoldoutGroup("Passenger Info"), ShowInInspector, ReadOnly]
    public PassengerState State { get; private set; }

    [FoldoutGroup("Passenger Info"), ShowInInspector, ReadOnly]
    public string FlightInfo => AssignedFlight != null
        ? $"{AssignedFlight.FlightNumber} | {AssignedFlight.Airline}"
        : "—";

    [FoldoutGroup("Passenger Info"), ShowInInspector, ReadOnly]
    public float Satisfaction { get; private set; } = 80f;

    [FoldoutGroup("Passenger Info"), ShowInInspector, ReadOnly]
    public float WaitTime { get; private set; }

    // ── Données ─────────────────────────────────────────────────────────────

    public ActiveFlight AssignedFlight { get; private set; }

    // ── Config ──────────────────────────────────────────────────────────────

    private const float MoveSpeed              = 2f;
    private const float SatisfactionDecayDelay = 60f;   // secondes avant perte de satisfaction
    private const float SatisfactionDecayRate  = 3f;    // points / seconde

    // ── Interne ─────────────────────────────────────────────────────────────

    private Coroutine    _moveCo;
    private MeshRenderer _bodyRenderer;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        BuildVisual();
    }

    private void OnDestroy()
    {
        if (_moveCo != null) StopCoroutine(_moveCo);
        DOTween.Kill(transform);
    }

    private void Update()
    {
        bool waiting = State == PassengerState.WaitingCheckin  ||
                       State == PassengerState.WaitingSecurity ||
                       State == PassengerState.WaitingGate;

        if (!waiting) return;

        WaitTime += Time.deltaTime;
        if (WaitTime > SatisfactionDecayDelay)
            Satisfaction = Mathf.Max(0f, Satisfaction - SatisfactionDecayRate * Time.deltaTime);
    }

    // ── API ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Démarre le passager.
    /// terminalEntry : seuil d'entrée du terminal (fin du trajet à pied en dehors).
    /// landsidePath  : chemin pré-calculé à l'intérieur du terminal (peut être vide).
    /// </summary>
    public void Initialize(ActiveFlight flight, Vector3 terminalEntry, List<Vector3> landsidePath)
    {
        AssignedFlight = flight;

        if (_bodyRenderer != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = flight?.AirlineColor ?? Color.white;
            _bodyRenderer.sharedMaterial = mat;
        }

        State   = PassengerState.Arriving;
        _moveCo = StartCoroutine(MovementCoroutine(terminalEntry, landsidePath));
    }

    // ── Déplacement ──────────────────────────────────────────────────────────

    private IEnumerator MovementCoroutine(Vector3 terminalEntry, List<Vector3> landsidePath)
    {
        // Phase 1 : ligne droite depuis le spawn jusqu'à l'entrée du terminal
        yield return StartCoroutine(WalkStraightTo(terminalEntry));

        // Phase 2 : chemin landside à l'intérieur du terminal
        if (landsidePath != null && landsidePath.Count > 0)
            yield return StartCoroutine(FollowPath(landsidePath));

        // Arrivé au check-in
        State    = PassengerState.WaitingCheckin;
        WaitTime = 0f;
    }

    private IEnumerator WalkStraightTo(Vector3 target)
    {
        target.y = 0f;
        var flat = new Vector3(transform.position.x, 0f, transform.position.z);

        while (Vector3.Distance(flat, target) > 0.25f)
        {
            flat = new Vector3(transform.position.x, 0f, transform.position.z);
            var dir = (target - flat).normalized;

            // Rotation fluide
            if (dir.sqrMagnitude > 0.001f)
            {
                var targetRot = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
            }

            transform.position += new Vector3(dir.x, 0f, dir.z) * MoveSpeed * Time.deltaTime;
            yield return null;
        }
        transform.position = target;
    }

    private IEnumerator FollowPath(List<Vector3> path)
    {
        foreach (var waypoint in path)
        {
            var target = new Vector3(waypoint.x, 0f, waypoint.z);
            var delta  = target - new Vector3(transform.position.x, 0f, transform.position.z);

            if (delta.sqrMagnitude < 0.04f) continue;

            // Rotation DOTween vers le waypoint
            var targetRot = Quaternion.LookRotation(delta.normalized, Vector3.up);
            float angle   = Quaternion.Angle(transform.rotation, targetRot);
            if (angle > 5f)
            {
                float rotDur = Mathf.Clamp(angle / 180f, 0.05f, 0.25f);
                yield return transform.DORotateQuaternion(targetRot, rotDur)
                                      .SetEase(Ease.InOutSine)
                                      .SetLink(gameObject)
                                      .WaitForCompletion();
            }

            float dist = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), target);
            if (dist > 0.1f)
                yield return transform.DOMove(target, dist / MoveSpeed)
                                      .SetEase(Ease.Linear)
                                      .SetLink(gameObject)
                                      .WaitForCompletion();
        }
    }

    // ── Prefab procédural ────────────────────────────────────────────────────

    private void BuildVisual()
    {
        var body  = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "PassengerBody";
        body.transform.SetParent(transform);
        body.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        body.transform.localScale    = new Vector3(0.3f, 0.6f, 0.3f);
        UnityEngine.Object.DestroyImmediate(body.GetComponent<BoxCollider>());

        _bodyRenderer = body.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.white;
        _bodyRenderer.sharedMaterial = mat;
    }
}
