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
/// Passager individuel.
/// Flux : spawn → terminalEntry → checkIn → sécurité → WaitingGate.
/// La satisfaction décroît pendant l'attente au check-in (après 60 s) et à la sécurité (3 pts/min).
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
    private const float SatisfactionDecayDelay = 60f;  // secondes avant décroissance au check-in
    private const float SatisfactionDecayRate  = 3f;   // pts/s après délai au check-in
    private const float SecurityDecayRate      = 3f / 60f; // pts/s à la sécurité (3/min)

    // ── Interne ─────────────────────────────────────────────────────────────

    private Coroutine    _moveCo;
    private MeshRenderer _bodyRenderer;
    private bool         _securityCleared;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake() => BuildVisual();

    private void OnDestroy()
    {
        if (_moveCo != null) StopCoroutine(_moveCo);
        DOTween.Kill(transform);
    }

    private void Update()
    {
        // Décroissance satisfaction pendant l'attente au check-in uniquement.
        // La sécurité est gérée directement dans NavigateToSecurity.
        if (State != PassengerState.WaitingCheckin) return;
        WaitTime += Time.deltaTime;
        if (WaitTime > SatisfactionDecayDelay)
            Satisfaction = Mathf.Max(0f, Satisfaction - SatisfactionDecayRate * Time.deltaTime);
    }

    // ── API ─────────────────────────────────────────────────────────────────

    public void Initialize(ActiveFlight flight, Vector3 terminalEntry,
                           List<Vector3> landsidePath, Vector3 finalDest)
    {
        AssignedFlight = flight;
        SetColor(flight?.AirlineColor ?? Color.white);
        State   = PassengerState.Arriving;
        _moveCo = StartCoroutine(MovementCoroutine(terminalEntry, landsidePath, finalDest));
    }

    // Appelé par SecurityCheckpoint quand c'est le tour de ce passager
    public void BeginSecurityProcessing() => State = PassengerState.PassingSecurity;

    // Appelé par SecurityCheckpoint en cas d'alarme (5 %)
    public void TriggerAlarm()
    {
        Satisfaction = Mathf.Max(0f, Satisfaction - 20f);
        // Animation de recul
        transform.DOPunchPosition(-transform.forward * 1.5f, 0.8f, 1, 0f)
                 .SetLink(gameObject);
    }

    // Appelé par SecurityCheckpoint quand le contrôle est terminé
    public void CompleteSecurityProcessing() => _securityCleared = true;

    // Appelé par SecurityCheckpoint pour repositionner visuellement dans la file
    public void MoveToQueuePosition(Vector3 pos)
    {
        DOTween.Kill(transform);
        transform.DOMove(pos, 0.5f).SetEase(Ease.OutQuad).SetLink(gameObject);
    }

    // ── Mouvement ────────────────────────────────────────────────────────────

    private IEnumerator MovementCoroutine(Vector3 terminalEntry,
                                          List<Vector3> landsidePath,
                                          Vector3 finalDest)
    {
        // Étape 1 — marche extérieure : spawn → entrée terminal
        yield return StartCoroutine(WalkTo(Flat(terminalEntry)));

        // Étape 2 — intérieur terminal → zone check-in
        if (landsidePath != null && landsidePath.Count >= 2)
        {
            for (int i = 1; i < landsidePath.Count; i++)
                yield return StartCoroutine(WalkTo(Flat(landsidePath[i])));
            yield return StartCoroutine(WalkTo(Flat(finalDest)));
        }
        else
        {
            yield return StartCoroutine(WalkTo(Flat(finalDest)));
        }

        State    = PassengerState.WaitingCheckin;
        WaitTime = 0f;

        // Étape 3 — sécurité (si SecurityArea présente dans la scène)
        yield return StartCoroutine(NavigateToSecurity());
    }

    private IEnumerator NavigateToSecurity()
    {
        var secArea = SecurityArea.Instance;
        if (secArea == null) { State = PassengerState.WaitingGate; yield break; }

        var checkpoint = secArea.GetLeastBusyCheckpoint();
        if (checkpoint == null) { State = PassengerState.WaitingGate; yield break; }

        // Marche vers le poste de contrôle
        yield return StartCoroutine(WalkTo(Flat(checkpoint.transform.position)));

        // Mise en file
        _securityCleared = false;
        checkpoint.Enqueue(this);
        State = PassengerState.WaitingSecurity;

        // Attente du feu vert + décroissance satisfaction 3 pts/min
        while (!_securityCleared)
        {
            Satisfaction = Mathf.Max(0f, Satisfaction - SecurityDecayRate * Time.deltaTime);
            yield return null;
        }

        State = PassengerState.WaitingGate;
    }

    private IEnumerator WalkTo(Vector3 target)
    {
        Vector3 origin = Flat(transform.position);
        float   dist   = Vector3.Distance(origin, target);
        if (dist < 0.1f) yield break;

        // Rotation instantanée (économise ~150 tweens DOTween simultanés)
        var dir = (target - origin).normalized;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

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

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
