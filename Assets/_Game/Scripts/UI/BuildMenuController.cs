using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;
using Sirenix.OdinInspector;

public class BuildMenuController : MonoBehaviour
{
    // ── Tab definitions ────────────────────────────────────────────────────
    private static readonly string[] TabNames = { "Tous", "Pistes", "Terminaux", "Services" };
    private static readonly BuildingCategory[][] TabFilters =
    {
        null,
        new[] { BuildingCategory.Runway },
        new[] { BuildingCategory.Terminal, BuildingCategory.Gate },
        new[] { BuildingCategory.Service },
    };

    // ── Serialized ─────────────────────────────────────────────────────────
    [FoldoutGroup("Buildings")]
    [SerializeField] private List<BuildingData> availableBuildings;

    [FoldoutGroup("UI")]
    [SerializeField] private RectTransform menuPanel;

    [FoldoutGroup("Visuals")]
    [SerializeField] private Color panelColor       = new Color(0.08f, 0.08f, 0.08f, 0.93f);
    [SerializeField] private Color tabNormalColor   = new Color(0.12f, 0.12f, 0.12f, 1f);
    [SerializeField] private Color tabActiveColor   = new Color(0.20f, 0.50f, 0.90f, 1f);
    [SerializeField] private Color btnNormalColor   = new Color(0.15f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color btnSelectedColor = new Color(0.18f, 0.45f, 0.80f, 1f);
    [SerializeField] private Color btnDisabledColor = new Color(0.10f, 0.10f, 0.10f, 0.60f);

    // ── Runtime ────────────────────────────────────────────────────────────
    private const float PanelHeight  = 170f;
    private const float TabBarHeight =  36f;

    private BuildSystem _buildSystem;
    private int _activeTab = 0;

    private readonly List<Image>                                        _tabBgs = new();
    private readonly List<(BuildingData data, GameObject go, Image bg, CanvasGroup cg)> _btns = new();

    // ── Unity lifecycle ────────────────────────────────────────────────────

    private void Awake()
    {
        _buildSystem = FindAnyObjectByType<BuildSystem>();
        if (_buildSystem != null)
            _buildSystem.OnBuildingChanged += _ => RefreshButtonHighlights();
    }

    private void Start()
    {
        BuildUI();
        SetTab(0);
        AnimateMenuIn();
    }

    private void Update()
    {
        HandleEscape();
        RefreshAffordability();
    }

    // ── UI construction ────────────────────────────────────────────────────

    private void BuildUI()
    {
        if (menuPanel.TryGetComponent<Image>(out var panelImg))
            panelImg.color = panelColor;

        // ── Tab bar (top strip) ──────────────────────────────────────────
        var tabBar = MakeRect(menuPanel, "TabBar",
            ancMin: new Vector2(0f, 1f), ancMax: new Vector2(1f, 1f),
            pivot: new Vector2(0.5f, 1f), pos: Vector2.zero,
            size: new Vector2(0f, TabBarHeight));

        var tabHLG = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabHLG.childControlWidth  = tabHLG.childForceExpandWidth  = true;
        tabHLG.childControlHeight = tabHLG.childForceExpandHeight = true;
        tabHLG.spacing  = 2f;
        tabHLG.padding  = new RectOffset(4, 4, 4, 0);

        for (int i = 0; i < TabNames.Length; i++)
            BuildTab(tabBar, i, TabNames[i]);

        // ── Button bar (fills remaining space) ──────────────────────────
        var btnBar = MakeRect(menuPanel, "ButtonBar",
            ancMin: Vector2.zero, ancMax: Vector2.one,
            pivot: new Vector2(0.5f, 0.5f), pos: Vector2.zero,
            size: Vector2.zero);
        btnBar.offsetMax = new Vector2(0f, -TabBarHeight);

        var btnHLG = btnBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        btnHLG.childAlignment      = TextAnchor.MiddleCenter;
        btnHLG.spacing             = 8f;
        btnHLG.padding             = new RectOffset(16, 16, 8, 8);
        btnHLG.childControlWidth   = btnHLG.childControlHeight   = false;
        btnHLG.childForceExpandWidth = btnHLG.childForceExpandHeight = false;

        foreach (var data in availableBuildings)
            BuildBuildingButton(btnBar, data);
    }

    private void BuildTab(RectTransform parent, int index, string label)
    {
        var go  = new GameObject($"Tab_{label}", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var bg  = go.AddComponent<Image>();
        bg.color = tabNormalColor;
        _tabBgs.Add(bg);

        var btn = go.AddComponent<Button>();
        DisableBuiltinTransition(btn);
        int i = index;
        btn.onClick.AddListener(() => SetTab(i));

        var txtGo = new GameObject("Text", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var rt = txtGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 13f;
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.color = Color.white; tmp.raycastTarget = false;
    }

    private void BuildBuildingButton(RectTransform parent, BuildingData data)
    {
        var go = new GameObject(data.buildingName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(110f, 130f);

        var bg = go.AddComponent<Image>();
        bg.color = btnNormalColor;

        var cg = go.AddComponent<CanvasGroup>();

        var btn = go.AddComponent<Button>();
        DisableBuiltinTransition(btn);

        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing  = 4f;
        vlg.padding  = new RectOffset(6, 6, 10, 6);
        vlg.childControlWidth  = vlg.childControlHeight  = false;
        vlg.childForceExpandWidth = vlg.childForceExpandHeight = false;

        // Icon
        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(go.transform, false);
        iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(60f, 60f);
        var iconImg = iconGo.AddComponent<Image>();
        iconImg.color = data.iconColor; iconImg.raycastTarget = false;

        // Name + cost
        MakeLabel(go.transform, data.buildingName, 13f, Color.white,           98f, 20f);
        MakeLabel(go.transform, FormatCost(data.cost), 11f, new Color(1f, 0.85f, 0.3f), 98f, 18f);

        _btns.Add((data, go, bg, cg));

        var capturedData      = data;
        var capturedTransform = go.transform;
        btn.onClick.AddListener(() =>
        {
            capturedTransform.DOPunchScale(Vector3.one * 0.12f, 0.22f, 5, 0.8f);
            _buildSystem?.SelectBuilding(capturedData);
        });
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static RectTransform MakeRect(RectTransform parent, string name,
        Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    private static void MakeLabel(Transform parent, string text, float size, Color color, float w, float h)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color; tmp.raycastTarget = false;
    }

    private static void DisableBuiltinTransition(Button btn)
    {
        btn.transition = Selectable.Transition.None;
        var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
    }

    private static string FormatCost(float cost)
    {
        int v = Mathf.RoundToInt(cost);
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0,0}", v)
                     .Replace(",", " ") + " $";
    }

    // ── Tab logic ──────────────────────────────────────────────────────────

    private void SetTab(int index)
    {
        _activeTab = index;

        for (int i = 0; i < _tabBgs.Count; i++)
            _tabBgs[i].color = i == index ? tabActiveColor : tabNormalColor;

        var filter = TabFilters[index];
        foreach (var (data, go, _, _) in _btns)
        {
            bool visible = filter == null || Array.Exists(filter, c => c == data.category);
            go.SetActive(visible);
        }
    }

    // ── Refresh ────────────────────────────────────────────────────────────

    private void RefreshButtonHighlights()
    {
        var current = _buildSystem?.CurrentBuilding;
        foreach (var (data, _, bg, _) in _btns)
            if (EconomySystem.Instance == null || EconomySystem.Instance.Budget >= data.cost)
                bg.color = data == current ? btnSelectedColor : btnNormalColor;
    }

    private void RefreshAffordability()
    {
        if (EconomySystem.Instance == null) return;
        float budget = EconomySystem.Instance.Budget;
        var   current = _buildSystem?.CurrentBuilding;

        foreach (var (data, _, bg, cg) in _btns)
        {
            bool canAfford = budget >= data.cost;
            cg.alpha  = canAfford ? 1f : 0.45f;
            if (data != current)
                bg.color = canAfford ? btnNormalColor : btnDisabledColor;
        }
    }

    // ── Input ──────────────────────────────────────────────────────────────

    private void HandleEscape()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            _buildSystem?.CancelSelection();
    }

    // ── DOTween ────────────────────────────────────────────────────────────

    private void AnimateMenuIn()
    {
        menuPanel.anchoredPosition = new Vector2(0f, -PanelHeight);
        menuPanel.DOAnchorPosY(0f, 0.45f).SetEase(Ease.OutBack).SetDelay(0.1f);
    }
}
