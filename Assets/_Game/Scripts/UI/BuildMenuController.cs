using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Sirenix.OdinInspector;

public class BuildMenuController : MonoBehaviour
{
    // ── Serialized ─────────────────────────────────────────────────────────
    [FoldoutGroup("Data")]
    [SerializeField] private List<ZoneData>     availableZones;
    [FoldoutGroup("Data")]
    [SerializeField] private List<BuildingData> availableBuildings;

    [FoldoutGroup("UI")]
    [SerializeField] private RectTransform menuPanel;

    [FoldoutGroup("Visuals")]
    [SerializeField] private Color panelColor       = new(0.08f, 0.08f, 0.08f, 0.93f);
    [FoldoutGroup("Visuals")]
    [SerializeField] private Color tabNormalColor   = new(0.12f, 0.12f, 0.12f, 1f);
    [FoldoutGroup("Visuals")]
    [SerializeField] private Color tabActiveColor   = new(0.20f, 0.50f, 0.90f, 1f);
    [FoldoutGroup("Visuals")]
    [SerializeField] private Color btnNormalColor   = new(0.15f, 0.15f, 0.15f, 1f);
    [FoldoutGroup("Visuals")]
    [SerializeField] private Color btnSelectedColor = new(0.18f, 0.45f, 0.80f, 1f);
    [FoldoutGroup("Visuals")]
    [SerializeField] private Color btnDisabledColor = new(0.10f, 0.10f, 0.10f, 0.60f);

    // ── Runtime ────────────────────────────────────────────────────────────
    private const float PanelHeight  = 170f;
    private const float TabBarHeight =  36f;
    private const float FooterHeight =  22f;

    private BuildSystem  _buildSystem;
    private ZonePainter  _zonePainter;
    private int          _activeTab; // 0=Zones, 1=Bâtiments

    private readonly List<Image> _tabBgs = new();

    private RectTransform _zonePanel;
    private RectTransform _buildPanel;

    // Zone buttons: (ZoneData, go, bg, cg)
    private readonly List<(ZoneData data, GameObject go, Image bg, CanvasGroup cg)> _zoneBtns = new();
    // Building buttons: (BuildingData, go, bg, cg)
    private readonly List<(BuildingData data, GameObject go, Image bg, CanvasGroup cg)> _buildBtns = new();

    private TextMeshProUGUI _costLabel;
    private ZoneData        _selectedZone;
    private BuildingData    _selectedBuilding;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        _buildSystem = FindAnyObjectByType<BuildSystem>();
        _zonePainter = FindAnyObjectByType<ZonePainter>();

        if (_buildSystem != null)
            _buildSystem.OnBuildingChanged += OnBuildingChanged;
        if (_zonePainter != null)
            _zonePainter.OnSelectionChanged += OnZoneSelectionChanged;
    }

    private void Start()
    {
        BuildUI();
        SetTab(0);
        AnimateMenuIn();
    }

    private void Update()
    {
        RefreshAffordability();
        UpdateCostPreview();
    }

    private void OnDestroy()
    {
        if (_buildSystem != null) _buildSystem.OnBuildingChanged -= OnBuildingChanged;
        if (_zonePainter  != null) _zonePainter.OnSelectionChanged -= OnZoneSelectionChanged;
    }

    // ── Construction UI ────────────────────────────────────────────────────

    private void BuildUI()
    {
        if (menuPanel.TryGetComponent<Image>(out var panelImg))
            panelImg.color = panelColor;

        // ── Tab bar ───────────────────────────────────────────────────────
        var tabBar = MakeRect(menuPanel, "TabBar",
            new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1),
            Vector2.zero, new Vector2(0, TabBarHeight));

        var tabHLG = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabHLG.childControlWidth  = tabHLG.childForceExpandWidth  = true;
        tabHLG.childControlHeight = tabHLG.childForceExpandHeight = true;
        tabHLG.spacing = 2f; tabHLG.padding = new RectOffset(4,4,4,0);

        BuildTab(tabBar, 0, "Zones");
        BuildTab(tabBar, 1, "Bâtiments");

        // ── Content area ─────────────────────────────────────────────────
        var content = MakeRect(menuPanel, "Content",
            Vector2.zero, Vector2.one, new Vector2(0.5f,0.5f),
            Vector2.zero, Vector2.zero);
        content.offsetMin = new Vector2(0,  FooterHeight);
        content.offsetMax = new Vector2(0, -TabBarHeight);

        // Zone panel
        _zonePanel  = MakeButtonBar(content, "ZonePanel");
        foreach (var zd in availableZones ?? new()) BuildZoneButton(_zonePanel, zd);

        // Building panel
        _buildPanel = MakeButtonBar(content, "BuildPanel");
        foreach (var bd in availableBuildings ?? new()) BuildBuildingButton(_buildPanel, bd);

        // ── Footer (cost preview) ─────────────────────────────────────────
        var footer = MakeRect(menuPanel, "Footer",
            new Vector2(0,0), new Vector2(1,0), new Vector2(0.5f,0),
            Vector2.zero, new Vector2(0, FooterHeight));
        footer.gameObject.AddComponent<Image>().color = new Color(0,0,0,0.4f);

        var footerGo = new GameObject("CostLabel", typeof(RectTransform));
        footerGo.transform.SetParent(footer, false);
        var frt = footerGo.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(12,0); frt.offsetMax = Vector2.zero;
        _costLabel = footerGo.AddComponent<TextMeshProUGUI>();
        _costLabel.fontSize = 11f;
        _costLabel.color = new Color(1f, 0.85f, 0.3f);
        _costLabel.alignment = TextAlignmentOptions.MidlineLeft;
        _costLabel.raycastTarget = false;
    }

    private void BuildTab(RectTransform parent, int index, string label)
    {
        var go  = new GameObject($"Tab_{label}", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var bg  = go.AddComponent<Image>();
        bg.color = tabNormalColor;
        _tabBgs.Add(bg);

        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
        int i = index;
        btn.onClick.AddListener(() => SetTab(i));

        var txtGo = new GameObject("Label", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var rt = txtGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 13f;
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.color = Color.white; tmp.raycastTarget = false;
    }

    private static RectTransform MakeButtonBar(RectTransform parent, string name)
    {
        var rt = MakeRect(parent, name, Vector2.zero, Vector2.one,
                          new Vector2(0.5f,0.5f), Vector2.zero, Vector2.zero);
        var hlg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment        = TextAnchor.MiddleCenter;
        hlg.spacing               = 8f;
        hlg.padding               = new RectOffset(12, 12, 6, 6);
        hlg.childControlWidth     = hlg.childControlHeight     = false;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        return rt;
    }

    private void BuildZoneButton(RectTransform parent, ZoneData data)
    {
        var go = new GameObject(data.displayName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 120f);

        var bg = go.AddComponent<Image>(); bg.color = btnNormalColor;
        var cg = go.AddComponent<CanvasGroup>();
        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;

        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 3f; vlg.padding = new RectOffset(5,5,8,5);
        vlg.childControlWidth = vlg.childControlHeight = false;
        vlg.childForceExpandWidth = vlg.childForceExpandHeight = false;

        // Icon carré coloré
        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(go.transform, false);
        iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(52f, 52f);
        iconGo.AddComponent<Image>().color = data.zoneColor;

        MakeLabel(go.transform, data.displayName, 11f, Color.white, 88f, 18f);
        MakeLabel(go.transform, FormatMoneyCeil(data.costPerCell) + "/case", 9.5f,
                  new Color(1f, 0.85f, 0.3f), 88f, 16f);

        _zoneBtns.Add((data, go, bg, cg));

        var d = data;
        var t = go.transform;
        btn.onClick.AddListener(() =>
        {
            t.DOPunchScale(Vector3.one * 0.10f, 0.20f, 5, 0.8f);
            SelectZone(d);
        });
    }

    private void BuildBuildingButton(RectTransform parent, BuildingData data)
    {
        var go = new GameObject(data.buildingName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(110f, 124f);

        var bg = go.AddComponent<Image>(); bg.color = btnNormalColor;
        var cg = go.AddComponent<CanvasGroup>();
        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;

        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 3f; vlg.padding = new RectOffset(5,5,8,5);
        vlg.childControlWidth = vlg.childControlHeight = false;
        vlg.childForceExpandWidth = vlg.childForceExpandHeight = false;

        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(go.transform, false);
        iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(56f, 56f);
        iconGo.AddComponent<Image>().color = data.iconColor;

        MakeLabel(go.transform, data.buildingName, 11f, Color.white, 98f, 18f);
        MakeLabel(go.transform, FormatMoney(data.cost), 10f,
                  new Color(1f, 0.85f, 0.3f), 98f, 16f);

        _buildBtns.Add((data, go, bg, cg));

        var d = data;
        var t = go.transform;
        btn.onClick.AddListener(() =>
        {
            t.DOPunchScale(Vector3.one * 0.12f, 0.22f, 5, 0.8f);
            SelectBuilding(d);
        });
    }

    // ── Logique onglet ─────────────────────────────────────────────────────

    private void SetTab(int index)
    {
        _activeTab = index;

        for (int i = 0; i < _tabBgs.Count; i++)
            _tabBgs[i].color = i == index ? tabActiveColor : tabNormalColor;

        bool isZone = index == 0;
        _zonePanel.gameObject.SetActive(isZone);
        _buildPanel.gameObject.SetActive(!isZone);

        if (!isZone)
        {
            _zonePainter?.ClearSelection();
            _selectedZone = null;
        }
        else
        {
            _buildSystem?.CancelSelection();
            _selectedBuilding = null;
        }

        RefreshZoneHighlights();
        RefreshBuildingHighlights();
    }

    // ── Sélection zone ─────────────────────────────────────────────────────

    private void SelectZone(ZoneData data)
    {
        bool deselect = _selectedZone == data;
        _selectedZone = deselect ? null : data;

        if (deselect) _zonePainter?.ClearSelection();
        else          _zonePainter?.SelectZone(data);

        RefreshZoneHighlights();
    }

    private void OnZoneSelectionChanged()
    {
        _selectedZone = _zonePainter?.SelectedZone;
        RefreshZoneHighlights();
    }

    // ── Sélection bâtiment ─────────────────────────────────────────────────

    private void SelectBuilding(BuildingData data)
    {
        _selectedBuilding = data;
        _buildSystem?.SelectBuilding(data);
    }

    private void OnBuildingChanged(BuildingData data)
    {
        _selectedBuilding = data;
        RefreshBuildingHighlights();
    }

    // ── Refresh ────────────────────────────────────────────────────────────

    private void RefreshZoneHighlights()
    {
        foreach (var (data, _, bg, _) in _zoneBtns)
            bg.color = data == _selectedZone ? btnSelectedColor : btnNormalColor;
    }

    private void RefreshBuildingHighlights()
    {
        foreach (var (data, _, bg, _) in _buildBtns)
            if (EconomySystem.Instance == null || EconomySystem.Instance.Budget >= data.cost)
                bg.color = data == _selectedBuilding ? btnSelectedColor : btnNormalColor;
    }

    private void RefreshAffordability()
    {
        if (EconomySystem.Instance == null) return;
        float budget = EconomySystem.Instance.Budget;

        foreach (var (data, _, bg, cg) in _buildBtns)
        {
            bool can = budget >= data.cost;
            cg.alpha = can ? 1f : 0.45f;
            if (data != _selectedBuilding)
                bg.color = can ? btnNormalColor : btnDisabledColor;
        }
    }

    private void UpdateCostPreview()
    {
        if (_costLabel == null) return;
        _costLabel.text = (_activeTab == 0 && _zonePainter != null)
            ? _zonePainter.CostPreview
            : "";
    }

    // ── Animation ──────────────────────────────────────────────────────────

    private void AnimateMenuIn()
    {
        menuPanel.anchoredPosition = new Vector2(0f, -PanelHeight);
        menuPanel.DOAnchorPosY(0f, 0.45f).SetEase(Ease.OutBack).SetDelay(0.1f);
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
        var go = new GameObject("L", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color; tmp.raycastTarget = false;
    }

    private static string FormatMoney(float v)
        => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0,0}", Mathf.RoundToInt(v))
                 .Replace(",", " ") + " $";

    private static string FormatMoneyCeil(float v)
    {
        if (v >= 1000f) return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                              "{0:0,0}", Mathf.RoundToInt(v)).Replace(",", " ") + " $";
        return Mathf.RoundToInt(v) + " $";
    }
}
