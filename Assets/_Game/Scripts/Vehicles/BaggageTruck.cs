using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Camion bagagiste. Arrive à l'arrière de l'avion, ouvre le hayon (DOTween rotation),
/// charge les bagages 4 min (temps de jeu), referme et rentre au dépôt.
/// </summary>
public class BaggageTruck : GroundVehicle
{
    [FoldoutGroup("Baggage State")]
    [SerializeField, ShowInInspector] private float serviceMinutes = 1f; // 60 s

    [FoldoutGroup("Baggage State"), ShowInInspector, ReadOnly]
    public bool TailgateOpen { get; private set; }

    [FoldoutGroup("Baggage State"), ShowInInspector, ReadOnly]
    public Aircraft AssignedAircraftInspector => AssignedAircraft;

    private Transform  _tailgatePivot;
    private Tweener    _tailgateTween;
    private GameObject _barRoot;
    private Transform  _barFill;
    private Coroutine  _serviceCo;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        _tailgatePivot = transform.Find("TailgatePivot");
        BuildProgressBar();
    }

    private void LateUpdate()
    {
        if (_barRoot != null && _barRoot.activeSelf && Camera.main != null)
            _barRoot.transform.rotation = Camera.main.transform.rotation;
    }

    protected override void OnDestroy()
    {
        _tailgateTween?.Kill();
        if (_serviceCo != null) StopCoroutine(_serviceCo);
        base.OnDestroy();
    }

    // ── Positionnement à l'arrière de l'avion ─────────────────────────────

    public override void DispatchToAircraft(Aircraft aircraft)
    {
        if (aircraft == null) { ReturnToDepot(); return; }

        AssignedAircraft = aircraft;
        State            = VehicleState.MovingToAircraft;

        Vector3 gatePos = aircraft.AssignedGate != null
            ? aircraft.AssignedGate.transform.position
            : aircraft.transform.position;

        // Arrière de l'avion : opposé à la direction de déplacement
        Vector3 rearDir = -aircraft.transform.forward;
        rearDir.y = 0f;
        if (rearDir.sqrMagnitude < 0.01f) rearDir = Vector3.back;
        Vector3 target = gatePos + rearDir.normalized * 10f;
        Destination    = target;

        if (_moveCo != null) StopCoroutine(_moveCo);
        _moveCo = StartCoroutine(MoveToPoint(target, OnArrivedAtAircraft));
    }

    // ── Service ────────────────────────────────────────────────────────────

    protected override void OnArrivedAtAircraft()
    {
        OpenTailgate(() =>
        {
            if (_serviceCo != null) StopCoroutine(_serviceCo);
            _serviceCo = StartCoroutine(ServiceCoroutine());
        });
    }

    private IEnumerator ServiceCoroutine()
    {
        State = VehicleState.Servicing;
        float total   = serviceMinutes * 60f;
        float elapsed = 0f;

        SetBarProgress(0f);
        SetBarVisible(true);

        while (elapsed < total)
        {
            if (AssignedAircraft == null)
            {
                SetBarVisible(false);
                CloseTailgateAndReturn();
                yield break;
            }
            elapsed += Time.deltaTime;
            SetBarProgress(Mathf.Clamp01(elapsed / total));
            yield return null;
        }

        SetBarProgress(1f);
        SetBarVisible(false);

        if (AssignedAircraft != null)
            AssignedAircraft.SetBaggageReady(true);

        CloseTailgateAndReturn();
    }

    // ── Animation hayon ────────────────────────────────────────────────────

    private void OpenTailgate(System.Action onComplete = null)
    {
        TailgateOpen = true;
        if (_tailgatePivot == null) { onComplete?.Invoke(); return; }
        _tailgateTween?.Kill();
        _tailgateTween = _tailgatePivot.DOLocalRotate(new Vector3(0f, 0f, -90f), 1f)
                                        .SetEase(Ease.OutQuad)
                                        .SetLink(gameObject)
                                        .OnComplete(() => onComplete?.Invoke());
    }

    private void CloseTailgateAndReturn()
    {
        TailgateOpen = false;
        if (_tailgatePivot == null) { ReturnToDepot(); return; }
        _tailgateTween?.Kill();
        _tailgateTween = _tailgatePivot.DOLocalRotate(Vector3.zero, 0.8f)
                                        .SetEase(Ease.InQuad)
                                        .SetLink(gameObject)
                                        .OnComplete(() => ReturnToDepot());
    }

    // ── Barre de progression (orange) ─────────────────────────────────────

    private void BuildProgressBar()
    {
        const float barW = 120f;
        const float barH = 18f;
        const float pad  = 3f;

        _barRoot = new GameObject("BaggageProgressBar");
        _barRoot.transform.SetParent(transform);
        _barRoot.transform.localPosition = new Vector3(0f, 4.5f, 0f);
        _barRoot.transform.localScale    = Vector3.one * 0.05f;

        var canvas        = _barRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt            = _barRoot.GetComponent<RectTransform>();
        rt.sizeDelta      = new Vector2(barW, barH);

        var bgGo   = new GameObject("BG");
        bgGo.transform.SetParent(_barRoot.transform, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        bgGo.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.90f);

        var fillGo   = new GameObject("Fill");
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin        = Vector2.zero;
        fillRect.anchorMax        = new Vector2(0f, 1f);
        fillRect.pivot            = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = new Vector2(pad, 0f);
        fillRect.sizeDelta        = new Vector2(barW - pad * 2f, -(pad * 2f));
        fillGo.AddComponent<Image>().color = new Color(0.95f, 0.55f, 0.10f); // orange bagages

        _barFill = fillGo.transform;
        _barFill.localScale = new Vector3(0f, 1f, 1f);
        _barRoot.SetActive(false);
    }

    private void SetBarProgress(float t)
    {
        if (_barFill != null)
            _barFill.localScale = new Vector3(Mathf.Clamp01(t), 1f, 1f);
    }

    private void SetBarVisible(bool show)
    {
        if (_barRoot != null) _barRoot.SetActive(show);
    }

    // ── Odin ──────────────────────────────────────────────────────────────

    [Button("Test Send to Aircraft"), FoldoutGroup("Baggage State")]
    private void TestSendToAircraft()
    {
        if (!Application.isPlaying) return;
        var aircraft = FindAnyObjectByType<Aircraft>();
        if (aircraft == null) { Debug.LogWarning("[BaggageTruck] Aucun avion."); return; }
        if (_depot != null) _depot.RequestBaggageService(aircraft);
        else                DispatchToAircraft(aircraft);
    }
}
