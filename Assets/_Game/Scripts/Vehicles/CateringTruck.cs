using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Camion de catering. Arrive côté opposé au FuelTruck,
/// monte une plateforme vers la porte de l'avion, sert 3 min (temps de jeu),
/// puis redescend et rentre au dépôt.
/// </summary>
public class CateringTruck : GroundVehicle
{
    [FoldoutGroup("Catering State")]
    [SerializeField, ShowInInspector] private float serviceMinutes = 0.75f; // 45 s

    [FoldoutGroup("Catering State"), ShowInInspector, ReadOnly]
    public float PlatformHeight { get; private set; }

    [FoldoutGroup("Catering State"), ShowInInspector, ReadOnly]
    public Aircraft AssignedAircraftInspector => AssignedAircraft;

    private const float LiftTargetY = 3f;

    private Transform  _platform;
    private Tweener    _liftTween;
    private GameObject _barRoot;
    private Transform  _barFill;
    private Coroutine  _serviceCo;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        _platform = transform.Find("Platform"); // enfant nommé "Platform" (prefab / BuildDefaultCateringTruck)
        BuildProgressBar();
    }

    private void LateUpdate()
    {
        if (_barRoot != null && _barRoot.activeSelf && Camera.main != null)
            _barRoot.transform.rotation = Camera.main.transform.rotation;
    }

    protected override void OnDestroy()
    {
        _liftTween?.Kill();
        if (_serviceCo != null) StopCoroutine(_serviceCo);
        base.OnDestroy();
    }

    // ── Positionnement côté opposé au FuelTruck ────────────────────────────

    public override void DispatchToAircraft(Aircraft aircraft)
    {
        if (aircraft == null) { ReturnToDepot(); return; }

        AssignedAircraft = aircraft;
        State            = VehicleState.MovingToAircraft;

        Vector3 gatePos = aircraft.AssignedGate != null
            ? aircraft.AssignedGate.transform.position
            : aircraft.transform.position;

        // Côté +X : opposé au FuelTruck qui va directement sur la gate
        Vector3 target = gatePos + new Vector3(6f, 0f, 0f);
        Destination    = target;

        if (_moveCo != null) StopCoroutine(_moveCo);
        _moveCo = StartCoroutine(MoveToPoint(target, OnArrivedAtAircraft));
    }

    // ── Service ────────────────────────────────────────────────────────────

    protected override void OnArrivedAtAircraft()
    {
        LiftPlatform(LiftTargetY, () =>
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
                LowerAndReturn();
                yield break;
            }
            elapsed += Time.deltaTime;
            SetBarProgress(Mathf.Clamp01(elapsed / total));
            yield return null;
        }

        SetBarProgress(1f);
        SetBarVisible(false);

        if (AssignedAircraft != null)
            AssignedAircraft.SetCateringReady(true);

        LowerAndReturn();
    }

    private void LiftPlatform(float targetY, System.Action onComplete = null)
    {
        if (_platform == null) { onComplete?.Invoke(); return; }
        _liftTween?.Kill();
        _liftTween = _platform.DOLocalMoveY(targetY, 1.5f)
                              .SetEase(Ease.OutQuad)
                              .SetLink(gameObject)
                              .OnUpdate(() => PlatformHeight = _platform.localPosition.y)
                              .OnComplete(() => onComplete?.Invoke());
    }

    private void LowerAndReturn()
    {
        LiftPlatform(0f, () => ReturnToDepot());
    }

    // ── Barre de progression (bleu catering) ──────────────────────────────

    private void BuildProgressBar()
    {
        const float barW = 120f;
        const float barH = 18f;
        const float pad  = 3f;

        _barRoot = new GameObject("CateringProgressBar");
        _barRoot.transform.SetParent(transform);
        _barRoot.transform.localPosition = new Vector3(0f, 5f, 0f);
        _barRoot.transform.localScale    = Vector3.one * 0.05f;

        var canvas        = _barRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt            = _barRoot.GetComponent<RectTransform>();
        rt.sizeDelta      = new Vector2(barW, barH);

        var bgGo     = new GameObject("BG");
        bgGo.transform.SetParent(_barRoot.transform, false);
        var bgRect   = bgGo.AddComponent<RectTransform>();
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
        fillGo.AddComponent<Image>().color = new Color(0.25f, 0.55f, 1.00f);

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

    [Button("Test Send to Aircraft"), FoldoutGroup("Catering State")]
    private void TestSendToAircraft()
    {
        if (!Application.isPlaying) return;
        var aircraft = FindAnyObjectByType<Aircraft>();
        if (aircraft == null) { Debug.LogWarning("[CateringTruck] Aucun avion."); return; }
        if (_depot != null) _depot.RequestCateringService(aircraft);
        else                DispatchToAircraft(aircraft);
    }
}
