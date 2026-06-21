using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public enum AircraftState { Approaching, Landing, Idle }

public class Aircraft : MonoBehaviour
{
    // ── État ───────────────────────────────────────────────────────────────
    [FoldoutGroup("Aircraft State"), ShowInInspector, ReadOnly]
    public AircraftState State { get; private set; }

    [FoldoutGroup("Aircraft State"), ShowInInspector, ReadOnly]
    public float Speed { get; private set; }

    // ── Données de vol ─────────────────────────────────────────────────────
    [FoldoutGroup("Flight Data"), ShowInInspector, ReadOnly]
    public AircraftData Data { get; private set; }

    [FoldoutGroup("Flight Data"), ShowInInspector, ReadOnly]
    public float RunwayCenterZ { get; private set; }

    // ── Interne ────────────────────────────────────────────────────────────
    private Sequence       _seq;
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
        float stopX      = runwayStartX + (runwayEndX - runwayStartX) * 0.45f; // ~45 % piste

        var spawnPos     = new Vector3(spawnX,     altitude, centerZ);
        var touchdownPos = new Vector3(touchdownX, 0f,       centerZ);
        var stopPos      = new Vector3(stopX,      0f,       centerZ);

        transform.position = spawnPos;
        transform.rotation = Quaternion.LookRotation(Vector3.right); // cap est

        _prevPos = spawnPos;
        _smoke   = BuildSmokeEffect();

        // Durées basées sur la distance et les vitesses
        float approachDist     = Vector3.Distance(spawnPos, touchdownPos);
        float approachDuration = approachDist / data.approachSpeed;

        float rollDist     = stopX - touchdownX;
        float avgRollSpeed = (data.approachSpeed + data.landingSpeed) * 0.5f;
        float rollDuration = rollDist / avgRollSpeed;

        // ── Séquence DOTween ───────────────────────────────────────────────
        _seq = DOTween.Sequence();

        // Phase 1 : descente progressive → piste
        _seq.Append(
            transform.DOMove(touchdownPos, approachDuration)
                .SetEase(Ease.InSine)
        );

        // Toucher des roues
        _seq.AppendCallback(OnTouchdown);

        // Phase 2 : freinage sur la piste
        _seq.Append(
            transform.DOMove(stopPos, rollDuration)
                .SetEase(Ease.OutCubic)
        );

        // Immobilisé
        _seq.AppendCallback(() =>
        {
            State = AircraftState.Idle;
            Speed = 0f;
        });

        // Disparaît après 90 s pour libérer la piste
        _seq.AppendInterval(90f);
        _seq.Append(transform.DOScale(Vector3.zero, 1.5f).SetEase(Ease.InBack));
        _seq.AppendCallback(() => Destroy(gameObject));
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Update()
    {
        if (State == AircraftState.Idle) return;
        Speed    = Vector3.Distance(transform.position, _prevPos) / Time.deltaTime;
        _prevPos = transform.position;
    }

    private void OnDestroy() => _seq?.Kill();

    // ── Atterrissage ───────────────────────────────────────────────────────

    private void OnTouchdown()
    {
        State = AircraftState.Landing;
        Speed = Data != null ? Data.approachSpeed : 80f;

        _smoke?.Play();

        // Légère secousse caméra
        Camera.main?.transform.DOShakePosition(0.6f, 0.3f, 12, 90f, false);
    }

    // ── Effets ─────────────────────────────────────────────────────────────

    private ParticleSystem BuildSmokeEffect()
    {
        var go = new GameObject("TouchdownSmoke");
        go.transform.SetParent(transform);
        go.transform.localPosition = new Vector3(-4f, -0.5f, 0f); // à hauteur des roues

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
            new[] { new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.7f, 0f),
                    new GradientAlphaKey(0f,   1f) });
        col.color = grad;

        return ps;
    }
}
