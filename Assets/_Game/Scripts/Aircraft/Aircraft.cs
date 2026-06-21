using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public enum AircraftState { Approaching, Landing, Idle, Taxiing, AtGate, Boarding }

public class Aircraft : MonoBehaviour
{
    // ── État ───────────────────────────────────────────────────────────────
    [FoldoutGroup("Aircraft State"), ShowInInspector, ReadOnly]
    public AircraftState State { get; private set; }

    [FoldoutGroup("Aircraft State"), ShowInInspector, ReadOnly]
    public float Speed { get; private set; }

    [FoldoutGroup("Aircraft State"), ShowInInspector, ReadOnly]
    public Gate AssignedGate { get; private set; }

    [FoldoutGroup("Aircraft State"), ShowInInspector, ReadOnly]
    public List<Vector3> TaxiPath { get; private set; } = new();

    // ── Données de vol ─────────────────────────────────────────────────────
    [FoldoutGroup("Flight Data"), ShowInInspector, ReadOnly]
    public AircraftData Data { get; private set; }

    [FoldoutGroup("Flight Data"), ShowInInspector, ReadOnly]
    public float RunwayCenterZ { get; private set; }

    // ── Événements ─────────────────────────────────────────────────────────
    public event Action<Aircraft> OnLanded;

    // ── Interne ────────────────────────────────────────────────────────────
    private Sequence       _seq;
    private Coroutine      _taxiCo;
    private ParticleSystem _smoke;
    private Vector3        _prevPos;

    // ── Init (appelé par FlightScheduler) ─────────────────────────────────

    public void Initialize(AircraftData data, float runwayStartX, float runwayEndX, float centerZ)
    {
        Data          = data;
        RunwayCenterZ = centerZ;
        State         = AircraftState.Approaching;

        const float altitude  = 80f;
        const float spawnDist = 500f;

        float spawnX     = runwayStartX - spawnDist;
        float touchdownX = runwayStartX;
        float stopX      = runwayStartX + (runwayEndX - runwayStartX) * 0.45f;

        var spawnPos     = new Vector3(spawnX,     altitude, centerZ);
        var touchdownPos = new Vector3(touchdownX, 0f,       centerZ);
        var stopPos      = new Vector3(stopX,      0f,       centerZ);

        transform.position = spawnPos;
        transform.rotation = Quaternion.LookRotation(Vector3.right);
        _prevPos           = spawnPos;

        _smoke = BuildSmokeEffect();
        SetupVisuals();

        float approachDist     = Vector3.Distance(spawnPos, touchdownPos);
        float approachDuration = approachDist / data.approachSpeed;
        float rollDist         = stopX - touchdownX;
        float avgRollSpeed     = (data.approachSpeed + data.landingSpeed) * 0.5f;
        float rollDuration     = rollDist / Mathf.Max(avgRollSpeed, 1f);

        _seq = DOTween.Sequence();
        _seq.Append(transform.DOMove(touchdownPos, approachDuration).SetEase(Ease.InSine));
        _seq.AppendCallback(OnTouchdown);
        _seq.Append(transform.DOMove(stopPos, rollDuration).SetEase(Ease.OutCubic));
        _seq.AppendCallback(() =>
        {
            State = AircraftState.Idle;
            Speed = 0f;
            OnLanded?.Invoke(this); // FlightScheduler assigne le taxi
        });

        // Fallback : si aucune gate n'est assignée après 15 s, l'avion disparaît
        _seq.AppendInterval(15f);
        _seq.AppendCallback(() =>
        {
            if (State == AircraftState.Idle) StartDespawn();
        });
    }

    // ── Taxi ───────────────────────────────────────────────────────────────

    public void StartTaxi(List<Vector3> path, Gate gate)
    {
        if (path == null || path.Count == 0) { StartDespawn(); return; }
        _seq?.Kill();
        AssignedGate = gate;
        TaxiPath     = path;
        _taxiCo      = StartCoroutine(TaxiCoroutine(path));
    }

    [Button("Test Taxi to Gate"), FoldoutGroup("Aircraft State")]
    private void TestTaxiToGate()
    {
        if (!Application.isPlaying) return;
        FindAnyObjectByType<FlightScheduler>()?.AssignGateToAircraft(this);
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Update()
    {
        if (State == AircraftState.Idle || State == AircraftState.AtGate ||
            State == AircraftState.Boarding) return;
        Speed    = Vector3.Distance(transform.position, _prevPos) / Time.deltaTime;
        _prevPos = transform.position;
    }

    private void OnDestroy()
    {
        _seq?.Kill();
        if (_taxiCo != null) StopCoroutine(_taxiCo);
    }

    // ── Atterrissage ───────────────────────────────────────────────────────

    private void OnTouchdown()
    {
        State = AircraftState.Landing;
        Speed = Data != null ? Data.approachSpeed : 80f;
        _smoke?.Play();
        Camera.main?.transform.DOShakePosition(0.6f, 0.3f, 12, 90f, false);
    }

    // ── Coroutine taxi ─────────────────────────────────────────────────────

    private IEnumerator TaxiCoroutine(List<Vector3> path)
    {
        State = AircraftState.Taxiing;
        float taxiSpeed = Data?.taxiSpeed ?? 10f;

        for (int i = 0; i < path.Count; i++)
        {
            var target = new Vector3(path[i].x, 0f, path[i].z);
            var delta  = target - new Vector3(transform.position.x, 0f, transform.position.z);

            if (delta.sqrMagnitude < 0.04f) continue;

            // Rotation fluide vers le prochain waypoint
            var targetRot = Quaternion.LookRotation(delta.normalized);
            float angle   = Quaternion.Angle(transform.rotation, targetRot);
            if (angle > 2f)
            {
                float rotDur = Mathf.Clamp(angle / 120f, 0.1f, 1.2f);
                yield return transform.DORotateQuaternion(targetRot, rotDur)
                                      .SetEase(Ease.InOutSine)
                                      .WaitForCompletion();
            }

            // Déplacement vers le waypoint
            float dist = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z), target);
            if (dist > 0.1f)
                yield return transform.DOMove(target, dist / taxiSpeed)
                                      .SetEase(Ease.Linear)
                                      .WaitForCompletion();
        }

        // Arrivée à la gate
        State = AircraftState.AtGate;
        Speed = 0f;
        AssignedGate?.AssignAircraft(this);

        // Départ après 120 s
        yield return new WaitForSeconds(120f);
        AssignedGate?.ReleaseAircraft();
        StartDespawn();
    }

    // ── Disparition ────────────────────────────────────────────────────────

    private void StartDespawn()
    {
        transform.DOScale(Vector3.zero, 1.5f)
                 .SetEase(Ease.InBack)
                 .OnComplete(() => Destroy(gameObject));
    }

    // ── Effets visuels ─────────────────────────────────────────────────────

    private void SetupVisuals()
    {
        var discMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        discMat.SetColor("_BaseColor", new Color(0.25f, 0.25f, 0.25f));

        // Disques moteurs (sur les ailes)
        CreateEngineDisc(new Vector3(3f, -1.2f, -7f), discMat);
        CreateEngineDisc(new Vector3(3f, -1.2f,  7f), discMat);

        // Feux de navigation (rouge gauche / vert droit)
        CreateNavLight(new Vector3(2f, 0f, -9.5f), Color.red,   0f);
        CreateNavLight(new Vector3(2f, 0f,  9.5f), Color.green, 0.35f);
    }

    private void CreateEngineDisc(Vector3 localPos, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "EngineDisc";
        go.transform.SetParent(transform);
        go.transform.localPosition = localPos;
        go.transform.localScale    = new Vector3(1.8f, 0.05f, 1.8f);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.Destroy(go.GetComponent<CapsuleCollider>());

        // Rotation continue autour de l'axe avant (local X)
        go.transform.DOLocalRotate(
            new Vector3(360f, 0f, 0f), 0.18f, RotateMode.FastBeyond360)
           .SetLoops(-1, LoopType.Restart)
           .SetEase(Ease.Linear);
    }

    private void CreateNavLight(Vector3 localPos, Color color, float blinkOffset)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "NavLight";
        go.transform.SetParent(transform);
        go.transform.localPosition = localPos;
        go.transform.localScale    = Vector3.one * 0.55f;
        Object.Destroy(go.GetComponent<SphereCollider>());

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", color);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;

        var offColor = new Color(color.r * 0.05f, color.g * 0.05f, color.b * 0.05f);
        DOVirtual.DelayedCall(blinkOffset, () =>
        {
            DOTween.To(
                () => mat.GetColor("_BaseColor"),
                c  => mat.SetColor("_BaseColor", c),
                offColor, 0.1f
            ).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.Flash);
        });
    }

    // ── Fumée d'atterrissage ───────────────────────────────────────────────

    private ParticleSystem BuildSmokeEffect()
    {
        var go = new GameObject("TouchdownSmoke");
        go.transform.SetParent(transform);
        go.transform.localPosition = new Vector3(-4f, -0.5f, 0f);

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1f, 2f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.85f, 0.85f, 0.85f, 0.75f),
                                   new Color(0.5f,  0.5f,  0.5f,  0.4f));
        main.maxParticles    = 60;
        main.loop            = false;
        main.playOnAwake     = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 50, 50, 1, 0f) });

        var shape       = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 1.2f;

        var col     = ps.colorOverLifetime;
        col.enabled = true;
        var grad    = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.7f, 0f),        new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        return ps;
    }
}
