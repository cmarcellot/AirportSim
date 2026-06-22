using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Panneau latéral droit affichant tous les vols actifs et planifiés.
/// Toggle avec la touche F.
/// </summary>
public class FlightBoard : MonoBehaviour
{
    private FlightScheduler  _scheduler;
    private RectTransform    _panel;
    private Transform        _rowContainer;
    private bool             _open;
    private bool             _animating;

    private const float PanelWidth = 420f;

    private readonly List<FlightRowUI> _rows = new();

    private struct FlightRowUI
    {
        public ActiveFlight   Flight;
        public RectTransform  Root;
        public Image          Stripe;
        public TextMeshProUGUI FlightNum;
        public TextMeshProUGUI Airline;
        public TextMeshProUGUI Arrival;
        public TextMeshProUGUI Gate;
        public TextMeshProUGUI Status;
    }

    private IEnumerator Start()
    {
        yield return null; // attendre que HUD canvas + FlightScheduler soient prêts

        Canvas hud = null;
        foreach (var c in FindObjectsByType<Canvas>())
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) { hud = c; break; }
        if (hud == null) { Debug.LogError("[FlightBoard] Pas de canvas HUD."); yield break; }

        _scheduler = FindAnyObjectByType<FlightScheduler>();
        if (_scheduler == null) { Debug.LogError("[FlightBoard] FlightScheduler introuvable."); yield break; }

        BuildUI(hud.transform);

        _scheduler.OnFlightStatusChanged += OnStatusChanged;
        RefreshAllRows();
    }

    private void OnDestroy()
    {
        if (_scheduler != null) _scheduler.OnFlightStatusChanged -= OnStatusChanged;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.fKey.wasPressedThisFrame && !_animating)
            Toggle();
    }

    // ── UI construction ────────────────────────────────────────────────────

    private void BuildUI(Transform canvasRoot)
    {
        // Panneau principal ancré à droite, hors écran par défaut
        var panelGo = new GameObject("FlightBoardPanel");
        panelGo.transform.SetParent(canvasRoot, false);

        _panel = panelGo.AddComponent<RectTransform>();
        _panel.anchorMin        = new Vector2(1f, 0f);
        _panel.anchorMax        = new Vector2(1f, 1f);
        _panel.pivot            = new Vector2(1f, 0.5f);
        _panel.sizeDelta        = new Vector2(PanelWidth, 0f);
        _panel.anchoredPosition = new Vector2(PanelWidth, 0f); // hors écran à droite

        var bg    = panelGo.AddComponent<Image>();
        bg.color  = new Color(0.05f, 0.07f, 0.12f, 0.96f);

        // En-tête
        BuildHeader(panelGo.transform);

        // Sous-titre colonnes
        BuildColumnHeader(panelGo.transform);

        // Zone scrollable
        BuildScrollArea(panelGo.transform);
    }

    private void BuildHeader(Transform parent)
    {
        var go   = new GameObject("Header");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin   = new Vector2(0f, 1f);
        rect.anchorMax   = new Vector2(1f, 1f);
        rect.pivot       = new Vector2(0.5f, 1f);
        rect.sizeDelta   = new Vector2(0f, 48f);
        rect.anchoredPosition = Vector2.zero;

        var bg   = go.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.11f, 0.20f);

        // Titre
        var title = MakeTMP(go.transform, "Title");
        var tr    = title.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0f);
        tr.anchorMax = new Vector2(0.75f, 1f);
        tr.offsetMin = new Vector2(14f, 0f);
        tr.offsetMax = Vector2.zero;
        title.text      = "TABLEAU DES VOLS";
        title.fontSize  = 15f;
        title.fontStyle = FontStyles.Bold;
        title.color     = new Color(0.55f, 0.80f, 1f);
        title.alignment = TextAlignmentOptions.MidlineLeft;

        // Hint touche
        var hint = MakeTMP(go.transform, "Hint");
        var hr   = hint.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0.75f, 0f);
        hr.anchorMax = new Vector2(1f, 1f);
        hr.offsetMin = Vector2.zero;
        hr.offsetMax = new Vector2(-10f, 0f);
        hint.text      = "[F]";
        hint.fontSize  = 11f;
        hint.color     = new Color(0.45f, 0.45f, 0.55f);
        hint.alignment = TextAlignmentOptions.MidlineRight;
    }

    private void BuildColumnHeader(Transform parent)
    {
        var go   = new GameObject("ColHeader");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0f, 1f);
        rect.anchorMax        = new Vector2(1f, 1f);
        rect.pivot            = new Vector2(0.5f, 1f);
        rect.sizeDelta        = new Vector2(0f, 24f);
        rect.anchoredPosition = new Vector2(0f, -48f);

        var bg   = go.AddComponent<Image>();
        bg.color = new Color(0.07f, 0.09f, 0.16f);

        SetupRowTexts(go.transform, "VOL", "COMPAGNIE", "ARRIVÉE", "GATE", "STATUT",
                      new Color(0.45f, 0.55f, 0.70f), 10f, FontStyles.Bold);
    }

    private void BuildScrollArea(Transform parent)
    {
        // ScrollView
        var svGo   = new GameObject("ScrollView");
        svGo.transform.SetParent(parent, false);
        var svRect = svGo.AddComponent<RectTransform>();
        svRect.anchorMin        = Vector2.zero;
        svRect.anchorMax        = new Vector2(1f, 1f);
        svRect.offsetMin        = new Vector2(0f, 0f);
        svRect.offsetMax        = new Vector2(0f, -72f);

        var sv        = svGo.AddComponent<ScrollRect>();
        sv.horizontal = false;
        sv.vertical   = true;
        sv.scrollSensitivity = 30f;

        // Mask
        var mask = svGo.AddComponent<RectMask2D>();

        // Content
        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(svGo.transform, false);
        var contentRect = contentGo.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot     = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 0f);

        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing               = 1f;
        vlg.childControlHeight    = true;
        vlg.childControlWidth     = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sv.content     = contentRect;
        _rowContainer  = contentGo.transform;
    }

    // ── Rows ───────────────────────────────────────────────────────────────

    private void RefreshAllRows()
    {
        // Supprimer lignes existantes
        foreach (var r in _rows) if (r.Root != null) Destroy(r.Root.gameObject);
        _rows.Clear();

        var flights = _scheduler.GetAllFlights();
        foreach (var f in flights)
            AddRow(f);
    }

    private void AddRow(ActiveFlight flight)
    {
        var go   = new GameObject($"Row_{flight.FlightNumber}");
        go.transform.SetParent(_rowContainer, false);

        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 36f);

        var bg   = go.AddComponent<Image>();
        bg.color = new Color(0.07f, 0.09f, 0.15f);

        // Bande airline
        var stripeGo = new GameObject("Stripe");
        stripeGo.transform.SetParent(go.transform, false);
        var stripeRect = stripeGo.AddComponent<RectTransform>();
        stripeRect.anchorMin = Vector2.zero;
        stripeRect.anchorMax = new Vector2(0f, 1f);
        stripeRect.offsetMin = Vector2.zero;
        stripeRect.offsetMax = new Vector2(5f, 0f);
        var stripeImg   = stripeGo.AddComponent<Image>();
        stripeImg.color = flight.AirlineColor;

        // Textes colonnes
        var (fnTmp, alTmp, arTmp, gtTmp, stTmp) =
            SetupRowTexts(go.transform,
                flight.FlightNumber,
                flight.Airline,
                flight.ArrivalLabel,
                flight.GateLabel,
                flight.StatusLabel,
                Color.white, 12f, FontStyles.Normal);
        stTmp.color = flight.StatusColor;

        var row = new FlightRowUI
        {
            Flight    = flight,
            Root      = rect,
            Stripe    = stripeImg,
            FlightNum = fnTmp,
            Airline   = alTmp,
            Arrival   = arTmp,
            Gate      = gtTmp,
            Status    = stTmp,
        };
        _rows.Add(row);
    }

    private void UpdateRow(ActiveFlight flight)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Flight != flight) continue;
            var r = _rows[i];
            r.Gate.text   = flight.GateLabel;
            r.Status.text  = flight.StatusLabel;
            r.Status.color = flight.StatusColor;
            return;
        }
        // Vol non encore dans la liste → l'ajouter
        AddRow(flight);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private (TextMeshProUGUI fn, TextMeshProUGUI al, TextMeshProUGUI ar,
             TextMeshProUGUI gt, TextMeshProUGUI st)
        SetupRowTexts(Transform parent,
                      string fn, string al, string ar, string gt, string st,
                      Color color, float size, FontStyles style)
    {
        // Proportions X en pourcentage de la largeur totale (bande 5px déduite)
        // flight#: 0..19%, airline: 19..46%, arrival: 46..62%, gate: 62..72%, status: 72..100%
        (string text, float xMin, float xMax)[] cols =
        {
            (fn, 0.05f, 0.24f),
            (al, 0.24f, 0.51f),
            (ar, 0.51f, 0.67f),
            (gt, 0.67f, 0.77f),
            (st, 0.77f, 1.00f),
        };

        var tmps = new TextMeshProUGUI[5];
        for (int i = 0; i < cols.Length; i++)
        {
            var tmp  = MakeTMP(parent, cols[i].text);
            var r    = tmp.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(cols[i].xMin, 0f);
            r.anchorMax = new Vector2(cols[i].xMax, 1f);
            r.offsetMin = new Vector2(4f, 2f);
            r.offsetMax = new Vector2(-2f, -2f);
            tmp.text      = cols[i].text;
            tmp.fontSize  = size;
            tmp.color     = color;
            tmp.fontStyle = style;
            tmp.alignment = i == 0 ? TextAlignmentOptions.MidlineLeft
                                   : TextAlignmentOptions.MidlineLeft;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmps[i] = tmp;
        }
        return (tmps[0], tmps[1], tmps[2], tmps[3], tmps[4]);
    }

    private static TextMeshProUGUI MakeTMP(Transform parent, string name)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go.AddComponent<TextMeshProUGUI>();
    }

    // ── Toggle ─────────────────────────────────────────────────────────────

    private void Toggle()
    {
        _animating = true;
        float target = _open ? PanelWidth : 0f;
        _panel.DOAnchorPosX(target, 0.30f)
              .SetEase(_open ? Ease.InCubic : Ease.OutCubic)
              .OnComplete(() => { _open = !_open; _animating = false; });
    }

    // ── Events ─────────────────────────────────────────────────────────────

    private void OnStatusChanged(ActiveFlight flight)
    {
        UpdateRow(flight);
    }
}
