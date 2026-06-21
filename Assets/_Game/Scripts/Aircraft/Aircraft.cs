using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

public enum AircraftState
{
    Approaching, Landing, Idle, Taxiing, AtGate, Boarding,
    Departing, TaxiingToRunway, TakingOff, Departed
}

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

    [FoldoutGroup("Aircraft State"), ShowInInspector, ReadOnly]
    private float _gateTimer;

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
    private ParticleSystem _exhaustPS;
    private AudioSource    _audioSource;
    private Vector3        _prevPos;
    private float          _runwayStartX;
    private float          _runwayEndX;
    private bool           _forceDeparture;

    // ── Init (appelé par FlightScheduler) ─────────────────────────────────

    public void Initialize(AircraftData data, float runwayStartX, float runwayEndX, float centerZ)
    {
        Data          = data;
        RunwayCenterZ = centerZ;
        _runwayStartX = runwayStartX;
        _runwayEndX   = runwayEndX;
        State         = AircraftState.Approaching;

        _audioSource              = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake  = false;
        _audioSource.spatialBlend = 1f;

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
            OnLanded?.Invoke(this);
        });

        // Fallback si aucune gate assignée après 15 s
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

    // ── Odin buttons ───────────────────────────────────────────────────────

    [Button("Test Taxi to Gate"), FoldoutGroup("Aircraft State")]
    private void TestTaxiToGate()
    {
        if (!Application.isPlaying) return;
        FindAnyObjectByType<FlightScheduler>()?.AssignGateToAircraft(this);
    }

    [Button("Force Departure"), FoldoutGroup("Aircraft State")]
    private void ForceDeparture()
    {
        if (!Application.isPlaying || State != AircraftState.AtGate) return;
        _forceDeparture = true;
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Update()
    {
        if (State == AircraftState.Idle      || State == AircraftState.AtGate  ||
            State == AircraftState.Boarding  || State == AircraftState.TakingOff ||
            State == AircraftState.Departed) return;
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

    // ── Taxi vers gate ─────────────────────────────────────────────────────

    private IEnumerator TaxiCoroutine(List<Vector3> path)
    {
        State = AircraftState.Taxiing;
        yield return StartCoroutine(FollowWaypointPath(path));

        State = AircraftState.AtGate;
        Speed = 0f;
        AssignedGate?.AssignAircraft(this);

        // Attente au gate (3 minutes de jeu)
        yield return StartCoroutine(WaitAtGate());

        // Cycle de départ
        yield return StartCoroutine(DepartureCoroutine());
    }

    // ── Attente à la gate ──────────────────────────────────────────────────

    private IEnumerator WaitAtGate()
    {
        _gateTimer = 3f * 60f; // 3 min de jeu → secondes

        while (_gateTimer > 0f && !_forceDeparture)
        {
            float spd = TimeManager.Instance != null ? TimeManager.Instance.SpeedMultiplier : 1f;
            _gateTimer -= Time.deltaTime * spd;
            yield return null;
        }

        _gateTimer      = 0f;
        _forceDeparture = false;
    }

    // ── Départ ─────────────────────────────────────────────────────────────

    private IEnumerator DepartureCoroutine()
    {
        State = AircraftState.Departing;
        AssignedGate?.ReleaseAircraft();
        AssignedGate = null;

        // Pushback : reculer de ~20 u depuis la gate
        var pushbackTarget   = transform.position - transform.forward * 20f;
        pushbackTarget.y     = 0f;
        yield return transform.DOMove(pushbackTarget, 4f)
                              .SetEase(Ease.OutCubic)
                              .WaitForCompletion();

        // Taxi vers la piste
        State = AircraftState.TaxiingToRunway;

        var pathfinding = FindAnyObjectByType<PathfindingSystem>();
        var runwayEntry = new Vector3(_runwayStartX, 0f, RunwayCenterZ);

        if (pathfinding != null)
        {
            var taxiPath = pathfinding.FindAirsidePath(transform.position, runwayEntry);
            if (taxiPath != null && taxiPath.Count > 0)
                yield return StartCoroutine(FollowWaypointPath(taxiPath));
        }

        // Alignement cap est (direction de décollage)
        yield return transform.DORotateQuaternion(Quaternion.LookRotation(Vector3.right), 1f)
                              .SetEase(Ease.InOutSine)
                              .WaitForCompletion();

        yield return StartCoroutine(TakeoffSequence());
    }

    // ── Décollage ──────────────────────────────────────────────────────────

    private IEnumerator TakeoffSequence()
    {
        State = AircraftState.TakingOff;

        // Positionnement au seuil de piste
        transform.position = new Vector3(_runwayStartX, 0f, RunwayCenterZ);
        transform.rotation = Quaternion.LookRotation(Vector3.right);

        _exhaustPS = BuildExhaustEffect();
        _exhaustPS?.Play();

        const float vMax      = 130f;
        const float accel     = 20f;
        const float v1        = 80f;   // vitesse de rotation (nez)
        const float climbRate = 28f;   // montée verticale u/s
        float       speed     = 0f;
        bool        rotated   = false;

        while (true)
        {
            float dt = Time.deltaTime;
            speed    = Mathf.Min(speed + accel * dt, vMax);
            Speed    = speed;

            var move = Vector3.right * speed * dt;

            if (speed >= v1)
                move.y = climbRate * (speed / vMax) * dt;

            transform.position += move;

            if (speed >= v1 && !rotated)
            {
                rotated = true;
                // Rotation nez vers le haut (pitch −15°, cap est Y=90°)
                transform.DORotateQuaternion(Quaternion.Euler(-15f, 90f, 0f), 2f)
                         .SetEase(Ease.InOutSine);
                Camera.main?.transform.DOShakePosition(0.8f, 0.4f, 10, 90f, false);
            }

            // Hors carte et altitude suffisante → décollage réussi
            if (transform.position.x > 350f && transform.position.y >= 60f) break;
            // Sécurité : hors carte très loin
            if (transform.position.x > 700f) break;

            yield return null;
        }

        EconomySystem.Instance?.AddRevenue(50_000f);
        ShowRevenueNotification();

        State = AircraftState.Departed;
        UnityEngine.Object.Destroy(gameObject);
    }

    // ── Suivi de waypoints (réutilisé taxi→gate et taxi→piste) ────────────

    private IEnumerator FollowWaypointPath(List<Vector3> path)
    {
        float taxiSpeed = Data?.taxiSpeed ?? 10f;

        for (int i = 0; i < path.Count; i++)
        {
            var target = new Vector3(path[i].x, 0f, path[i].z);
            var delta  = target - new Vector3(transform.position.x, 0f, transform.position.z);

            if (delta.sqrMagnitude < 0.04f) continue;

            var targetRot = Quaternion.LookRotation(delta.normalized);
            float angle   = Quaternion.Angle(transform.rotation, targetRot);
            if (angle > 2f)
            {
                float rotDur = Mathf.Clamp(angle / 120f, 0.1f, 1.2f);
                yield return transform.DORotateQuaternion(targetRot, rotDur)
                                      .SetEase(Ease.InOutSine)
                                      .WaitForCompletion();
            }

            float dist = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z), target);
            if (dist > 0.1f)
                yield return transform.DOMove(target, dist / taxiSpeed)
                                      .SetEase(Ease.Linear)
                                      .WaitForCompletion();
        }
    }

    // ── Disparition ────────────────────────────────────────────────────────

    private void StartDespawn()
    {
        transform.DOScale(Vector3.zero, 1.5f)
                 .SetEase(Ease.InBack)
                 .OnComplete(() => UnityEngine.Object.Destroy(gameObject));
    }

    // ── Notification revenue ───────────────────────────────────────────────

    private void ShowRevenueNotification()
    {
        // Cherche le canvas Screen Space Overlay du HUD
        Canvas hud = null;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) { hud = c; break; }
        if (hud == null) return;

        var go   = new GameObject("RevenueNotif");
        go.transform.SetParent(hud.transform, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = rect.anchorMax = new Vector2(0.5f, 0.6f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta        = new Vector2(300f, 80f);

        var tmp       = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = "+50 000 $";
        tmp.fontSize  = 42f;
        tmp.color     = new Color(0.15f, 0.95f, 0.15f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        rect.DOAnchorPos(new Vector2(0f, 120f), 2f).SetEase(Ease.OutCubic);
        DOTween.To(() => tmp.color, c => tmp.color = c,
                   new Color(0.15f, 0.95f, 0.15f, 0f), 1.5f)
               .SetDelay(0.5f)
               .OnComplete(() => UnityEngine.Object.Destroy(go));
    }

    // ── Effets visuels ─────────────────────────────────────────────────────

    private void SetupVisuals()
    {
        var discMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        discMat.SetColor("_BaseColor", new Color(0.25f, 0.25f, 0.25f));

        CreateEngineDisc(new Vector3(3f, -1.2f, -7f), discMat);
        CreateEngineDisc(new Vector3(3f, -1.2f,  7f), discMat);

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
        UnityEngine.Object.Destroy(go.GetComponent<CapsuleCollider>());

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
        UnityEngine.Object.Destroy(go.GetComponent<SphereCollider>());

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

    // ── Traînée de décollage ───────────────────────────────────────────────

    private ParticleSystem BuildExhaustEffect()
    {
        var go = new GameObject("EngineExhaust");
        go.transform.SetParent(transform);
        go.transform.localPosition = new Vector3(-6f, -1f, 0f);

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(4f, 10f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1f, 1f, 1f, 0.5f),
                                   new Color(0.7f, 0.7f, 0.7f, 0.25f));
        main.maxParticles    = 200;
        main.loop            = true;
        main.playOnAwake     = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 30f;

        var shape       = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.5f;

        var col     = ps.colorOverLifetime;
        col.enabled = true;
        var grad    = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.45f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        return ps;
    }
}
