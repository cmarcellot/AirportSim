using System.Collections;
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
/// Passager individuel. Reçoit une destination personnelle (avec jitter) depuis PassengerSpawner.
/// Mouvement : DOTween DOMove en 2 étapes — entrée route → terminal → zone check-in.
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
    private const float SatisfactionDecayDelay = 60f;
    private const float SatisfactionDecayRate  = 3f;

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
    /// terminalEntry  : point d'entrée du terminal (fin de la marche extérieure).
    /// finalDest      : destination finale intérieure (check-in), avec jitter appliqué par le spawner.
    /// </summary>
    public void Initialize(ActiveFlight flight, Vector3 terminalEntry, Vector3 finalDest)
    {
        AssignedFlight = flight;
        SetColor(flight?.AirlineColor ?? Color.white);
        State = PassengerState.Arriving;
        _moveCo = StartCoroutine(MovementCoroutine(terminalEntry, finalDest));
    }

    // ── Mouvement ────────────────────────────────────────────────────────────

    private IEnumerator MovementCoroutine(Vector3 terminalEntry, Vector3 finalDest)
    {
        // Étape 1 — marche depuis le spawn vers l'entrée du terminal
        yield return StartCoroutine(WalkTo(Flat(terminalEntry)));

        // Étape 2 — marche vers la zone check-in (destination personnelle)
        yield return StartCoroutine(WalkTo(Flat(finalDest)));

        State    = PassengerState.WaitingCheckin;
        WaitTime = 0f;
    }

    /// <summary>Déplacement DOTween vers un point (Y=0). Rotation d'abord, ensuite translation.</summary>
    private IEnumerator WalkTo(Vector3 target)
    {
        Vector3 origin = Flat(transform.position);
        float   dist   = Vector3.Distance(origin, target);

        if (dist < 0.1f) yield break;

        var dir = (target - origin).normalized;

        // Rotation vers la cible
        if (dir.sqrMagnitude > 0.001f)
        {
            var targetRot = Quaternion.LookRotation(dir, Vector3.up);
            float angle   = Quaternion.Angle(transform.rotation, targetRot);
            if (angle > 3f)
            {
                yield return transform
                    .DORotateQuaternion(targetRot, Mathf.Clamp(angle / 360f, 0.05f, 0.25f))
                    .SetEase(Ease.OutSine)
                    .SetLink(gameObject)
                    .WaitForCompletion();
            }
        }

        // Translation
        yield return transform
            .DOMove(target, dist / MoveSpeed)
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .WaitForCompletion();
    }

    // ── Visuel ───────────────────────────────────────────────────────────────

    private void BuildVisual()
    {
        var body  = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "PassengerBody";
        body.transform.SetParent(transform);
        body.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        body.transform.localScale    = new Vector3(0.3f, 0.6f, 0.3f);
        DestroyImmediate(body.GetComponent<BoxCollider>());

        _bodyRenderer = body.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.white;
        _bodyRenderer.sharedMaterial = mat;
    }

    private void SetColor(Color color)
    {
        if (_bodyRenderer == null) return;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        _bodyRenderer.sharedMaterial = mat;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
