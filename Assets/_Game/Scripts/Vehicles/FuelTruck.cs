using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Véhicule ravitailleur. Se rend à la gate dès qu'un avion arrive,
/// remplit le réservoir (barre de progression), puis rentre au dépôt.
/// </summary>
public class FuelTruck : GroundVehicle
{
    [FoldoutGroup("Fuel Service")]
    [SerializeField] private float serviceDurationMinutes = 2f;

    private GameObject _barRoot;
    private Image      _barFill;
    private Coroutine  _serviceCo;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        BuildProgressBar();
    }

    private void LateUpdate()
    {
        // La barre fait face à la caméra (vue isométrique)
        if (_barRoot != null && _barRoot.activeSelf && Camera.main != null)
            _barRoot.transform.rotation = Camera.main.transform.rotation;
    }

    protected override void OnDestroy()
    {
        if (_serviceCo != null) StopCoroutine(_serviceCo);
        base.OnDestroy();
    }

    // ── Service ────────────────────────────────────────────────────────────

    protected override void OnArrivedAtAircraft()
    {
        if (_serviceCo != null) StopCoroutine(_serviceCo);
        _serviceCo = StartCoroutine(ServiceCoroutine());
    }

    private IEnumerator ServiceCoroutine()
    {
        State = VehicleState.Servicing;
        float total = serviceDurationMinutes * 60f;
        float timer = total;
        SetBarVisible(true);
        if (_barFill != null) _barFill.fillAmount = 0f;

        while (timer > 0f)
        {
            if (AssignedAircraft == null) // avion parti pendant le service
            {
                SetBarVisible(false);
                ReturnToDepot();
                yield break;
            }
            timer -= Time.deltaTime;
            if (_barFill != null)
                _barFill.fillAmount = 1f - (timer / total);
            yield return null;
        }

        SetBarVisible(false);
        ReturnToDepot();
    }

    // ── Barre de progression ───────────────────────────────────────────────

    private void BuildProgressBar()
    {
        _barRoot = new GameObject("FuelProgressBar");
        _barRoot.transform.SetParent(transform);
        _barRoot.transform.localPosition = new Vector3(0f, 4f, 0f);
        _barRoot.transform.localScale    = Vector3.one * 0.05f;

        var canvas        = _barRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rt       = _barRoot.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(120f, 18f);

        // Fond sombre
        var bgGo         = new GameObject("BG");
        bgGo.transform.SetParent(_barRoot.transform, false);
        var bgRect       = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        bgGo.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.88f);

        // Remplissage (vert)
        var fillGo         = new GameObject("Fill");
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRect       = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);
        _barFill             = fillGo.AddComponent<Image>();
        _barFill.color       = new Color(0.15f, 0.80f, 0.25f);
        _barFill.type        = Image.Type.Filled;
        _barFill.fillMethod  = Image.FillMethod.Horizontal;
        _barFill.fillAmount  = 0f;

        _barRoot.SetActive(false);
    }

    private void SetBarVisible(bool show)
    {
        if (_barRoot != null) _barRoot.SetActive(show);
    }

    // ── Odin ──────────────────────────────────────────────────────────────

    [Button("Test Send to Aircraft"), FoldoutGroup("Vehicle State")]
    private void TestSendToAircraft()
    {
        if (!Application.isPlaying) return;
        var aircraft = FindAnyObjectByType<Aircraft>();
        if (aircraft == null) { Debug.LogWarning("[FuelTruck] Aucun avion dans la scène."); return; }
        if (_depot != null)   _depot.RequestService(aircraft);
        else                  DispatchToAircraft(aircraft);
    }
}
