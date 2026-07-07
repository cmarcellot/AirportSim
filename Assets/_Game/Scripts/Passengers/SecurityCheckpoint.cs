using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public class SecurityCheckpoint : MonoBehaviour
{
    public enum SecurityState { Closed, Open, Processing }

    // ── Config ───────────────────────────────────────────────────────────────

    [FoldoutGroup("Security Config")]
    [SerializeField] private float processingDuration = 10f;   // secondes de jeu (Time.timeScale l'accélère)
    [FoldoutGroup("Security Config")]
    [SerializeField] private float alarmExtraDuration = 15f;   // secondes de jeu supplémentaires si alarme
    [FoldoutGroup("Security Config")]
    [SerializeField, Range(0f, 1f)] private float alarmProbability = 0.05f;

    // ── État (Odin) ──────────────────────────────────────────────────────────

    [FoldoutGroup("Security Info"), ShowInInspector, ReadOnly]
    public SecurityState State { get; private set; } = SecurityState.Open;

    [FoldoutGroup("Security Info"), ShowInInspector, ReadOnly]
    private Passenger CurrentPassenger => _current;

    [FoldoutGroup("Security Info"), ShowInInspector, ReadOnly]
    private int QueueCount => _queue.Count;

    [FoldoutGroup("Security Info"), ShowInInspector, ReadOnly]
    private Passenger[] QueueContents => _queue.ToArray();

    // Nombre total : passager en cours + file → utilisé par SecurityArea.GetLeastBusyCheckpoint
    public int QueueLength => _queue.Count + (_current != null ? 1 : 0);

    // ── Interne ──────────────────────────────────────────────────────────────

    private readonly Queue<Passenger> _queue = new();
    private Passenger   _current;
    private MeshRenderer _lightRenderer;
    private Coroutine   _processCo;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake() => BuildVisual();

    private void OnEnable()
    {
        _queue.Clear();
        _current = null;
        State    = SecurityState.Open;
        UpdateLight();
    }

    private void OnDestroy() => DOTween.Kill(transform);

    // ── API ──────────────────────────────────────────────────────────────────

    public void Enqueue(Passenger p)
    {
        _queue.Enqueue(p);
        UpdateQueuePositions();
        TryProcessNext();
    }

    // ── Traitement ───────────────────────────────────────────────────────────

    private void TryProcessNext()
    {
        if (State != SecurityState.Open) return;
        if (_queue.Count == 0) return;

        _current = _queue.Dequeue();
        State    = SecurityState.Processing;
        UpdateLight();
        _processCo = StartCoroutine(ProcessPassenger(_current));
    }

    private IEnumerator ProcessPassenger(Passenger p)
    {
        p.BeginSecurityProcessing();

        yield return new WaitForSeconds(processingDuration);

        // Guard : ForceOpen peut avoir avancé la file pendant l'attente
        if (_current != p) yield break;

        if (Random.value < alarmProbability)
        {
            p.TriggerAlarm();
            yield return new WaitForSeconds(alarmExtraDuration);
            if (_current != p) yield break;
        }

        CompleteCurrentPassenger();
    }

    private void CompleteCurrentPassenger()
    {
        if (_current == null) return;
        _current.CompleteSecurityProcessing();
        _current = null;
        State    = SecurityState.Open;
        UpdateLight();
        UpdateQueuePositions();
        TryProcessNext();
    }

    // ── File d'attente ───────────────────────────────────────────────────────

    private void UpdateQueuePositions()
    {
        var arr = _queue.ToArray();
        for (int i = 0; i < arr.Length; i++)
        {
            // Les passagers se rangent derrière le poste (−forward = sud si le poste face nord)
            var pos = transform.position - transform.forward * ((i + 1) * 1.2f);
            arr[i].MoveToQueuePosition(new Vector3(pos.x, 0f, pos.z));
        }
    }

    // ── Visuel ───────────────────────────────────────────────────────────────

    private void BuildVisual()
    {
        var litShader   = Shader.Find("Universal Render Pipeline/Lit");
        var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");

        // Arche de sécurité (cadre gris)
        var arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arch.name = "Arch";
        arch.transform.SetParent(transform);
        arch.transform.localPosition = new Vector3(0f, 1f, 0f);
        arch.transform.localScale    = new Vector3(1.8f, 2f, 0.25f);
        DestroyImmediate(arch.GetComponent<BoxCollider>());
        var archMat = new Material(litShader);
        archMat.color = new Color(0.55f, 0.60f, 0.65f);
        arch.GetComponent<MeshRenderer>().sharedMaterial = archMat;

        // Voyant d'état (vert = libre, rouge = en cours / fermé)
        var lightSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lightSphere.name = "StatusLight";
        lightSphere.transform.SetParent(transform);
        lightSphere.transform.localPosition = new Vector3(0f, 2.3f, 0f);
        lightSphere.transform.localScale    = Vector3.one * 0.35f;
        DestroyImmediate(lightSphere.GetComponent<SphereCollider>());
        _lightRenderer = lightSphere.GetComponent<MeshRenderer>();
        var lightMat = new Material(unlitShader);
        lightMat.SetColor("_BaseColor", Color.green);
        _lightRenderer.sharedMaterial = lightMat;
    }

    private void UpdateLight()
    {
        if (_lightRenderer == null) return;
        bool busy = State != SecurityState.Open;
        _lightRenderer.sharedMaterial.SetColor("_BaseColor", busy ? Color.red : Color.green);
    }

    // ── Odin Buttons ─────────────────────────────────────────────────────────

    [Button("Force Open"), FoldoutGroup("Security Info")]
    private void ForceOpen()
    {
        if (!Application.isPlaying) return;
        if (_processCo != null) StopCoroutine(_processCo);
        _processCo = null;
        CompleteCurrentPassenger();
        // Si State n'est toujours pas Open (pas de passager en cours), forcer
        if (_current == null)
        {
            State = SecurityState.Open;
            UpdateLight();
            TryProcessNext();
        }
    }

    [Button("Trigger Alarm"), FoldoutGroup("Security Info")]
    private void TriggerAlarmManual()
    {
        if (!Application.isPlaying) return;
        if (_current != null)
            _current.TriggerAlarm();
        else
            Debug.LogWarning("[SecurityCheckpoint] Aucun passager en cours de contrôle.");
    }
}
