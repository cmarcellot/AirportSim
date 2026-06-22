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
    [SerializeField, ShowInInspector] private float serviceDurationMinutes = 0.5f; // 30 s

    private GameObject _barRoot;
    private Transform  _barFill;       // scale.x : 0 = vide  →  1 = plein
    private float      _fillMaxWidth;  // largeur naturelle de la barre en unités rect
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
        float total   = serviceDurationMinutes * 60f; // secondes de jeu
        float elapsed = 0f;

        SetBarProgress(0f);
        SetBarVisible(true);

        while (elapsed < total)
        {
            if (AssignedAircraft == null) // avion parti pendant le service
            {
                SetBarVisible(false);
                ReturnToDepot();
                yield break;
            }

            elapsed += Time.deltaTime;
            SetBarProgress(Mathf.Clamp01(elapsed / total));
            yield return null;
        }

        SetBarProgress(1f);
        SetBarVisible(false);

        if (AssignedAircraft != null)
            AssignedAircraft.SetFuelReady(true);

        ReturnToDepot();
    }

    // ── Barre de progression ───────────────────────────────────────────────
    // Approche : localScale.x (pivot gauche) — fiable sans sprite dans WorldSpace.

    private void BuildProgressBar()
    {
        const float barW = 120f;
        const float barH = 18f;
        const float pad  = 3f;

        _barRoot = new GameObject("FuelProgressBar");
        _barRoot.transform.SetParent(transform);
        _barRoot.transform.localPosition = new Vector3(0f, 4f, 0f);
        _barRoot.transform.localScale    = Vector3.one * 0.05f;

        var canvas        = _barRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rt       = _barRoot.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(barW, barH);

        // Fond sombre
        var bgGo     = new GameObject("BG");
        bgGo.transform.SetParent(_barRoot.transform, false);
        var bgRect   = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        bgGo.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.90f);

        // Remplissage — ancré à gauche, pivot gauche → localScale.x pilote la largeur
        _fillMaxWidth = barW - pad * 2f; // 114 unités
        var fillGo  = new GameObject("Fill");
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin        = Vector2.zero;
        fillRect.anchorMax        = new Vector2(0f, 1f); // ancré sur le bord gauche
        fillRect.pivot            = new Vector2(0f, 0.5f); // pivot gauche → croît à droite
        fillRect.anchoredPosition = new Vector2(pad, 0f);
        fillRect.sizeDelta        = new Vector2(_fillMaxWidth, -(pad * 2f));
        fillGo.AddComponent<Image>().color = new Color(0.15f, 0.80f, 0.25f);

        _barFill = fillGo.transform;
        // Initialiser à 0 : scale.x=0 → barre invisible
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

    [Button("Test Send to Aircraft"), FoldoutGroup("Vehicle State")]
    private void TestSendToAircraft()
    {
        if (!Application.isPlaying) return;
        var aircraft = FindAnyObjectByType<Aircraft>();
        if (aircraft == null) { Debug.LogWarning("[FuelTruck] Aucun avion dans la scène."); return; }
        if (_depot != null)   _depot.RequestFuelService(aircraft);
        else                  DispatchToAircraft(aircraft);
    }
}
