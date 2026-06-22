using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class AirportSceneSetup
{
    private const string ScenePath = "Assets/_Game/Scenes/Airport.unity";

    [MenuItem("AirportSim/Create Airport Scene", true)]
    public static bool CreateAirportSceneValidate() => !Application.isPlaying;

    [MenuItem("AirportSim/Create Airport Scene")]
    public static void CreateAirportScene()
    {
        EnsureFolder("Assets", "_Game");
        EnsureFolder("Assets/_Game", "Scenes");
        EnsureFolder("Assets/_Game", "Materials");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Lumière directionnelle
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        lightGo.transform.eulerAngles = new Vector3(50f, -30f, 0f);

        // Sol 512x512 — 128 cellules × 4u (Plane Unity = 10x10, scale 51.2)
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(51.2f, 1f, 51.2f);

        const string matPath = "Assets/_Game/Materials/Ground.mat";
        var groundMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (groundMat == null)
        {
            groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMat.color = Color.white;
            AssetDatabase.CreateAsset(groundMat, matPath);
        }
        ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

        // Caméra RTS — pitch 45°, Y=40, Z=-40 pour centrer la vue sur l'origine (carte 512×512)
        var cameraGo = new GameObject("RTS Camera");
        cameraGo.tag = "MainCamera";
        var cam = cameraGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 30f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1500f;
        cameraGo.AddComponent<AudioListener>();
        cameraGo.transform.SetPositionAndRotation(
            new Vector3(0f, 40f, -40f),
            Quaternion.Euler(45f, 0f, 0f)
        );
        cameraGo.AddComponent<RTSCamera>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Debug.Log("[AirportSim] Scène créée : " + ScenePath);
        EditorUtility.DisplayDialog("AirportSim", "Scène créée :\n" + ScenePath, "OK");
    }

    [MenuItem("AirportSim/Add Grid System to Scene", true)]
    public static bool AddGridSystemValidate() => !Application.isPlaying;

    [MenuItem("AirportSim/Add Grid System to Scene")]
    public static void AddGridSystem()
    {
        // Cherche un GridSystem existant pour ne pas en créer deux
        var existing = UnityEngine.Object.FindAnyObjectByType<GridSystem>();
        if (existing != null)
        {
            EditorUtility.DisplayDialog("AirportSim", "Un GridSystem existe déjà dans la scène.", "OK");
            return;
        }

        var go = new GameObject("Grid System");
        go.AddComponent<GridSystem>();

        // Sauvegarde la scène active
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("[AirportSim] GridSystem ajouté à la scène.");
        EditorUtility.DisplayDialog("AirportSim", "GridSystem ajouté.\nSauvegarde la scène (Ctrl+S).", "OK");
    }

    [MenuItem("AirportSim/Add HUD to Scene", true)]
    public static bool AddHUDValidate() => !Application.isPlaying;

    [MenuItem("AirportSim/Add HUD to Scene")]
    public static void AddHUD()
    {
        if (Object.FindAnyObjectByType<HUDController>() != null)
        {
            EditorUtility.DisplayDialog("AirportSim", "Un HUD existe déjà dans la scène.", "OK");
            return;
        }

        // ── Systèmes ──────────────────────────────────────────────────────
        if (Object.FindAnyObjectByType<EconomySystem>() == null)
        {
            var eco = new GameObject("Economy System");
            eco.AddComponent<EconomySystem>();
        }
        if (Object.FindAnyObjectByType<TimeManager>() == null)
        {
            var tm = new GameObject("Time Manager");
            tm.AddComponent<TimeManager>();
        }

        // ── Canvas ────────────────────────────────────────────────────────
        var canvasGo = new GameObject("HUD Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        EnsureEventSystem();

        var hud = canvasGo.AddComponent<HUDController>();

        // ── Panel Budget (haut-gauche) ────────────────────────────────────
        var budgetPanel = CreatePanel(canvasGo.transform, "Budget Panel",
            new Color(0f, 0f, 0f, 0.65f),
            anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(0f, 1f),
            pivot: new Vector2(0f, 1f),
            sizeDelta: new Vector2(280f, 54f),
            anchoredPos: new Vector2(16f, -16f));

        var budgetText = CreateTMPText(budgetPanel.transform, "Budget Text",
            "1 000 000 $", 22, TextAlignmentOptions.MidlineLeft,
            padding: new Vector4(12f, 0f, 12f, 0f));

        // ── Panel Horloge (haut-centre) ───────────────────────────────────
        var timePanel = CreatePanel(canvasGo.transform, "Time Panel",
            new Color(0f, 0f, 0f, 0.65f),
            anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(0.5f, 1f),
            pivot: new Vector2(0.5f, 1f),
            sizeDelta: new Vector2(200f, 54f),
            anchoredPos: new Vector2(0f, -16f));

        var timeText = CreateTMPText(timePanel.transform, "Time Text",
            "06:00  x1", 22, TextAlignmentOptions.Midline,
            padding: new Vector4(12f, 0f, 12f, 0f));

        // ── Assignation des références ────────────────────────────────────
        var so = new SerializedObject(hud);
        so.FindProperty("budgetText").objectReferenceValue = budgetText;
        so.FindProperty("timeText").objectReferenceValue  = timeText;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[AirportSim] HUD ajouté à la scène.");
        EditorUtility.DisplayDialog("AirportSim", "HUD ajouté.\nSauvegarde la scène (Ctrl+S).", "OK");
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 sizeDelta, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin     = anchorMin;
        rt.anchorMax     = anchorMax;
        rt.pivot         = pivot;
        rt.sizeDelta     = sizeDelta;
        rt.anchoredPosition = anchoredPos;

        return go;
    }

    private static TMP_Text CreateTMPText(Transform parent, string name,
        string text, float fontSize, TextAlignmentOptions alignment,
        Vector4 padding = default)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.alignment = alignment;
        tmp.color     = Color.white;
        tmp.margin    = padding;
        tmp.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return tmp;
    }

    // ── Étape 1D ──────────────────────────────────────────────────────────────

    [MenuItem("AirportSim/Setup 2A - Zones & Pathfinding", true)]
    public static bool Setup2AValidate() => !Application.isPlaying;

    [MenuItem("AirportSim/Setup 2A - Zones & Pathfinding")]
    public static void Setup2A()
    {
        EnsureFolder("Assets/_Game", "Materials");
        EnsureFolder("Assets/_Game/Materials", "Zones");
        EnsureFolder("Assets/_Game", "ScriptableObjects");
        EnsureFolder("Assets/_Game/ScriptableObjects", "Zones");
        EnsureFolder("Assets/_Game", "Prefabs");
        EnsureFolder("Assets/_Game/Prefabs", "Buildings");
        EnsureFolder("Assets/_Game/ScriptableObjects", "Buildings");

        // ══════════════════════════════════════════════════════════════════
        // 1. MATÉRIAUX DE ZONE (simples couleurs URP Lit + GPU instancing)
        // ══════════════════════════════════════════════════════════════════
        // (slug, hex color r,g,b)
        var zoneMats = new (string slug, Color col)[]
        {
            ("Runway",        new Color(0.541f, 0.541f, 0.541f)),
            ("Taxiway",       new Color(0.420f, 0.420f, 0.420f)),
            ("Apron",         new Color(0.333f, 0.333f, 0.333f)),
            ("TerminalHall",  new Color(0.941f, 0.929f, 0.910f)),
            ("CheckInArea",   new Color(0.839f, 0.910f, 0.941f)),
            ("SecurityArea",  new Color(0.784f, 0.784f, 0.784f)),
            ("CustomsArea",   new Color(0.647f, 0.647f, 0.667f)),
            ("BoardingLounge",new Color(0.290f, 0.435f, 0.647f)),
            ("Parking",       new Color(0.227f, 0.227f, 0.227f)),
            ("RoadAccess",    new Color(0.180f, 0.180f, 0.180f)),
            ("GreenArea",     new Color(0.353f, 0.541f, 0.235f)),
        };
        var matMap = new System.Collections.Generic.Dictionary<string, Material>();
        foreach (var (slug, col) in zoneMats)
        {
            var mat = CreateZoneMat($"Assets/_Game/Materials/Zones/{slug}Mat.mat", col);
            matMap[slug] = mat;
        }
        AssetDatabase.SaveAssets();

        // ══════════════════════════════════════════════════════════════════
        // 2. SCRIPTABLEOBJECTS ZoneData
        // ══════════════════════════════════════════════════════════════════
        // (slug, ZoneType, displayName, costPerCell, color, isAirside, isRestricted)
        var zoneDefs = new (string slug, ZoneType type, string display,
                            float cost, Color col, bool airside, bool restricted)[]
        {
            ("Runway",         ZoneType.Runway,         "Runway",          10_000f, new Color(0.541f,0.541f,0.541f), true,  false),
            ("Taxiway",        ZoneType.Taxiway,        "Taxiway",          3_000f, new Color(0.420f,0.420f,0.420f), true,  false),
            ("Apron",          ZoneType.Apron,          "Apron",            2_000f, new Color(0.333f,0.333f,0.333f), true,  false),
            ("TerminalHall",   ZoneType.TerminalHall,   "Terminal Hall",    5_000f, new Color(0.941f,0.929f,0.910f), false, false),
            ("CheckInArea",    ZoneType.CheckInArea,    "Check-In",         3_000f, new Color(0.839f,0.910f,0.941f), false, false),
            ("SecurityArea",   ZoneType.SecurityArea,   "Security",         4_000f, new Color(0.784f,0.784f,0.784f), false, true),
            ("CustomsArea",    ZoneType.CustomsArea,    "Customs",          3_500f, new Color(0.647f,0.647f,0.667f), false, true),
            ("BoardingLounge", ZoneType.BoardingLounge, "Boarding Lounge",  4_000f, new Color(0.290f,0.435f,0.647f), false, true),
            ("Parking",        ZoneType.Parking,        "Parking",          1_000f, new Color(0.227f,0.227f,0.227f), false, false),
            ("RoadAccess",     ZoneType.RoadAccess,     "Road Access",        800f, new Color(0.180f,0.180f,0.180f), false, false),
            ("GreenArea",      ZoneType.GreenArea,      "Green Area",         200f, new Color(0.353f,0.541f,0.235f), false, false),
        };
        var zoneSOs = new ZoneData[zoneDefs.Length];
        for (int i = 0; i < zoneDefs.Length; i++)
        {
            var d = zoneDefs[i];
            zoneSOs[i] = UpsertZoneData(
                $"Assets/_Game/ScriptableObjects/Zones/{d.slug}.asset",
                d.type, d.display, d.cost, d.col,
                matMap.TryGetValue(d.slug, out var m) ? m : null,
                d.airside, d.restricted);
        }
        AssetDatabase.SaveAssets();

        // ══════════════════════════════════════════════════════════════════
        // 3. BÂTIMENTS (5 objets placés sur grille)
        // ══════════════════════════════════════════════════════════════════
        var buildDefs = new (string n, BuildingCategory cat, BuildingType type,
                             float cost, int sx, int sz, float h, Color col)[]
        {
            ("ControlTower", BuildingCategory.ControlTower, BuildingType.ControlTower,
              50_000f, 1, 1, 4.0f, new Color(0.85f, 0.20f, 0.20f)),
            ("Gate",         BuildingCategory.Gate,         BuildingType.Gate,
              30_000f, 2, 2, 1.5f, new Color(0.20f, 0.45f, 0.85f)),
            ("FuelStation",  BuildingCategory.FuelStation,  BuildingType.FuelStation,
              40_000f, 2, 1, 1.5f, new Color(0.90f, 0.70f, 0.10f)),
            ("Shop",         BuildingCategory.Shop,         BuildingType.Shop,
              15_000f, 2, 2, 1.5f, new Color(0.75f, 0.40f, 0.90f)),
            ("Restaurant",   BuildingCategory.Restaurant,   BuildingType.Restaurant,
              20_000f, 3, 2, 1.5f, new Color(0.90f, 0.35f, 0.45f)),
        };
        var buildSOs = new BuildingData[buildDefs.Length];
        for (int i = 0; i < buildDefs.Length; i++)
        {
            var d    = buildDefs[i];
            var mat  = CreateOpaqueMat($"Assets/_Game/Materials/{d.n}Mat.mat", d.col);
            var pfab = CreateCubePrefab($"Assets/_Game/Prefabs/Buildings/{d.n}.prefab", mat);
            buildSOs[i] = UpsertBuildingData(
                $"Assets/_Game/ScriptableObjects/Buildings/{d.n}.asset",
                d.n, d.cat, d.type, d.cost, new Vector2Int(d.sx, d.sz), d.h, pfab, d.col);
        }
        AssetDatabase.SaveAssets();

        // ══════════════════════════════════════════════════════════════════
        // 4. SCÈNE : supprime anciens systèmes, ajoute les nouveaux
        // ══════════════════════════════════════════════════════════════════
        foreach (var old in new System.Type[]
            { typeof(TaxiwayGraph), typeof(ZoneSystem), typeof(ZonePainter),
              typeof(GroundRenderer), typeof(PathfindingSystem) })
        {
            var found = (MonoBehaviour)Object.FindAnyObjectByType(old);
            if (found != null) Object.DestroyImmediate(found.gameObject);
        }

        var zoneSystemGo = new GameObject("Zone System");
        zoneSystemGo.AddComponent<ZoneSystem>();

        var painterGo = new GameObject("Zone Painter");
        painterGo.AddComponent<ZonePainter>();

        var rendererGo = new GameObject("Ground Renderer");
        var gr         = rendererGo.AddComponent<GroundRenderer>();
        var grSO       = new SerializedObject(gr);
        var grList     = grSO.FindProperty("zoneDataList");
        grList.arraySize = zoneSOs.Length;
        for (int i = 0; i < zoneSOs.Length; i++)
            grList.GetArrayElementAtIndex(i).objectReferenceValue = zoneSOs[i];
        grSO.ApplyModifiedProperties();

        var pathGo = new GameObject("Pathfinding System");
        pathGo.AddComponent<PathfindingSystem>();

        // ══════════════════════════════════════════════════════════════════
        // 5. MENU DE CONSTRUCTION — recrée entièrement le canvas
        // ══════════════════════════════════════════════════════════════════
        var oldCanvas = GameObject.Find("Build Menu Canvas");
        if (oldCanvas != null) Object.DestroyImmediate(oldCanvas);

        // Canvas
        var canvasGo = new GameObject("Build Menu Canvas");
        var canvas   = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        // Panel (barre du bas)
        var panelGo = new GameObject("MenuPanel", typeof(RectTransform));
        panelGo.transform.SetParent(canvasGo.transform, false);
        panelGo.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.93f);
        var panelRT = panelGo.GetComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0f, 0f);
        panelRT.anchorMax        = new Vector2(1f, 0f);
        panelRT.pivot            = new Vector2(0.5f, 0f);
        panelRT.sizeDelta        = new Vector2(0f, 170f);
        panelRT.anchoredPosition = Vector2.zero;

        // BuildMenuController
        var menuCtrl = canvasGo.AddComponent<BuildMenuController>();
        var menuSO   = new SerializedObject(menuCtrl);
        menuSO.FindProperty("menuPanel").objectReferenceValue = panelRT;

        var zoneList = menuSO.FindProperty("availableZones");
        zoneList.arraySize = zoneSOs.Length;
        for (int i = 0; i < zoneSOs.Length; i++)
            zoneList.GetArrayElementAtIndex(i).objectReferenceValue = zoneSOs[i];

        var buildList = menuSO.FindProperty("availableBuildings");
        buildList.arraySize = buildSOs.Length;
        for (int i = 0; i < buildSOs.Length; i++)
            buildList.GetArrayElementAtIndex(i).objectReferenceValue = buildSOs[i];

        menuSO.ApplyModifiedProperties();

        // Vide startBuilding dans BuildSystem
        var bs = Object.FindAnyObjectByType<BuildSystem>();
        if (bs != null)
        {
            var bsSO = new SerializedObject(bs);
            bsSO.FindProperty("startBuilding").objectReferenceValue = null;
            bsSO.ApplyModifiedProperties();
        }

        // Matériau sol par défaut → herbe claire
        var defaultGround = CreateZoneMat("Assets/_Game/Materials/Zones/DefaultMat.mat",
                                          new Color(0.478f, 0.714f, 0.282f));
        var groundObj = GameObject.Find("Ground");
        if (groundObj != null)
            groundObj.GetComponent<MeshRenderer>().sharedMaterial = defaultGround;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.Refresh();

        Debug.Log("[AirportSim] Setup 2A terminé — zones dessinables, pathfinding dual, 5 bâtiments.");
        EditorUtility.DisplayDialog("AirportSim",
            "Setup 2A terminé !\n\n" +
            "• 11 zones dessinables (Runway / Terminal / Landside)\n" +
            "• ZonePainter : clic gauche = peindre, clic droit = effacer\n" +
            "• GroundRenderer : rendu GPU instancié par type de zone\n" +
            "• PathfindingSystem : AirsideGraph + LandsideGraph\n" +
            "• 5 bâtiments placés sur grille (onglet Bâtiments)\n\n" +
            "Sauvegarde (Ctrl+S) puis Play.\n" +
            "Onglet Zones → clique une zone → glisse sur la carte.", "OK");
    }

    private static Material CreateZoneMat(string path, Color color)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color            = color;
        mat.enableInstancing = true;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static ZoneData UpsertZoneData(string path, ZoneType type, string displayName,
        float costPerCell, Color zoneColor, Material groundMaterial, bool isAirside, bool isRestricted)
    {
        var existing = AssetDatabase.LoadAssetAtPath<ZoneData>(path);
        if (existing != null)
        {
            existing.type           = type;
            existing.displayName    = displayName;
            existing.costPerCell    = costPerCell;
            existing.zoneColor      = zoneColor;
            if (groundMaterial != null) existing.groundMaterial = groundMaterial;
            existing.isAirside      = isAirside;
            existing.isRestricted   = isRestricted;
            EditorUtility.SetDirty(existing);
            return existing;
        }
        var so = ScriptableObject.CreateInstance<ZoneData>();
        so.type           = type;
        so.displayName    = displayName;
        so.costPerCell    = costPerCell;
        so.zoneColor      = zoneColor;
        so.groundMaterial = groundMaterial;
        so.isAirside      = isAirside;
        so.isRestricted   = isRestricted;
        AssetDatabase.CreateAsset(so, path);
        return so;
    }

    [MenuItem("AirportSim/Setup 1E - Build Menu", true)]
    public static bool Setup1EValidate() => !Application.isPlaying;

    [MenuItem("AirportSim/Setup 1E - Build Menu")]
    public static void Setup1E()
    {
        if (Object.FindAnyObjectByType<BuildMenuController>() != null)
        {
            EditorUtility.DisplayDialog("AirportSim", "Un Build Menu existe déjà dans la scène.", "OK");
            return;
        }

        EnsureFolder("Assets/_Game/Prefabs", "Buildings");
        EnsureFolder("Assets/_Game/ScriptableObjects", "Buildings");

        // ── Matériaux ──────────────────────────────────────────────────────
        var terminalMat = CreateOpaqueMat("Assets/_Game/Materials/TerminalMat.mat",
                              new Color(0.90f, 0.90f, 0.90f));
        var gateMat     = CreateOpaqueMat("Assets/_Game/Materials/GateMat.mat",
                              new Color(0.20f, 0.45f, 0.85f));
        var towerMat    = CreateOpaqueMat("Assets/_Game/Materials/ControlTowerMat.mat",
                              new Color(0.85f, 0.20f, 0.20f));
        AssetDatabase.SaveAssets();

        // ── Prefabs ────────────────────────────────────────────────────────
        var runwayPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(
                                "Assets/_Game/Prefabs/Buildings/Runway.prefab");
        var terminalPrefab = CreateCubePrefab(
                                "Assets/_Game/Prefabs/Buildings/Terminal.prefab", terminalMat);
        var gatePrefab     = CreateCubePrefab(
                                "Assets/_Game/Prefabs/Buildings/Gate.prefab", gateMat);
        var towerPrefab    = CreateCubePrefab(
                                "Assets/_Game/Prefabs/Buildings/ControlTower.prefab", towerMat);

        // ── ScriptableObjects ──────────────────────────────────────────────
        var runwaySO  = UpsertBuildingData(
            "Assets/_Game/ScriptableObjects/Buildings/Runway.asset",
            "Runway", BuildingCategory.Runway, BuildingType.Runway,
            80_000f, new Vector2Int(8, 2), 0.3f, runwayPrefab,
            new Color(0.45f, 0.45f, 0.45f));

        var terminalSO = UpsertBuildingData(
            "Assets/_Game/ScriptableObjects/Buildings/Terminal.asset",
            "Terminal", BuildingCategory.Terminal, BuildingType.Terminal,
            150_000f, new Vector2Int(4, 4), 2f, terminalPrefab,
            new Color(0.90f, 0.90f, 0.90f));

        var gateSO = UpsertBuildingData(
            "Assets/_Game/ScriptableObjects/Buildings/Gate.asset",
            "Gate", BuildingCategory.Gate, BuildingType.Terminal,
            30_000f, new Vector2Int(2, 2), 1.5f, gatePrefab,
            new Color(0.20f, 0.45f, 0.85f));

        var towerSO = UpsertBuildingData(
            "Assets/_Game/ScriptableObjects/Buildings/ControlTower.asset",
            "Control Tower", BuildingCategory.ControlTower, BuildingType.ControlTower,
            50_000f, new Vector2Int(1, 1), 4f, towerPrefab,
            new Color(0.85f, 0.20f, 0.20f));

        AssetDatabase.SaveAssets();

        // ── Vide startBuilding dans BuildSystem (menu prend le relais) ─────
        var bs = Object.FindAnyObjectByType<BuildSystem>();
        if (bs != null)
        {
            var bsSO = new SerializedObject(bs);
            bsSO.FindProperty("startBuilding").objectReferenceValue = null;
            bsSO.ApplyModifiedProperties();
        }

        // ── Canvas Build Menu ──────────────────────────────────────────────
        var canvasGo = new GameObject("Build Menu Canvas");
        var canvas   = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode       = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // ── Panel (barre du bas) ───────────────────────────────────────────
        var panelGo  = new GameObject("MenuPanel", typeof(RectTransform));
        panelGo.transform.SetParent(canvasGo.transform, false);
        panelGo.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.93f);
        var panelRT  = panelGo.GetComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0f, 0f);
        panelRT.anchorMax        = new Vector2(1f, 0f);
        panelRT.pivot            = new Vector2(0.5f, 0f);
        panelRT.sizeDelta        = new Vector2(0f, 170f);
        panelRT.anchoredPosition = Vector2.zero;

        // ── BuildMenuController ────────────────────────────────────────────
        var menu   = canvasGo.AddComponent<BuildMenuController>();
        var menuSO = new SerializedObject(menu);
        menuSO.FindProperty("menuPanel").objectReferenceValue = panelRT;

        var listProp = menuSO.FindProperty("availableBuildings");
        listProp.arraySize = 4;
        listProp.GetArrayElementAtIndex(0).objectReferenceValue = runwaySO;
        listProp.GetArrayElementAtIndex(1).objectReferenceValue = terminalSO;
        listProp.GetArrayElementAtIndex(2).objectReferenceValue = gateSO;
        listProp.GetArrayElementAtIndex(3).objectReferenceValue = towerSO;
        menuSO.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.Refresh();

        Debug.Log("[AirportSim] Build Menu ajouté (4 bâtiments).");
        EditorUtility.DisplayDialog("AirportSim",
            "Build Menu créé !\n\nRunway · Terminal · Gate · Control Tower\n\n" +
            "Sauvegarde (Ctrl+S) puis Play.\n" +
            "• Clique un bouton pour sélectionner\n" +
            "• Clic droit / Échap pour annuler", "OK");
    }

    private static GameObject CreateCubePrefab(string path, Material mat)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.GetComponent<MeshRenderer>().sharedMaterial = mat;
        var prefab = PrefabUtility.SaveAsPrefabAsset(cube, path);
        GameObject.DestroyImmediate(cube);
        return prefab;
    }

    private static BuildingData UpsertBuildingData(string path, string buildingName,
        BuildingCategory category, BuildingType gridType, float cost,
        Vector2Int size, float height, GameObject prefab, Color iconColor)
    {
        var existing = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
        if (existing != null)
        {
            existing.buildingName = buildingName;
            existing.category     = category;
            existing.gridType     = gridType;
            existing.cost         = cost;
            existing.sizeInCells  = size;
            existing.height       = height;
            if (prefab != null) existing.prefab = prefab;
            existing.iconColor    = iconColor;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var so = ScriptableObject.CreateInstance<BuildingData>();
        so.buildingName = buildingName;
        so.category     = category;
        so.gridType     = gridType;
        so.cost         = cost;
        so.sizeInCells  = size;
        so.height       = height;
        so.prefab       = prefab;
        so.iconColor    = iconColor;
        AssetDatabase.CreateAsset(so, path);
        return so;
    }

    [MenuItem("AirportSim/Setup 1D - Build System", true)]
    public static bool Setup1DValidate() => !Application.isPlaying;

    [MenuItem("AirportSim/Setup 1D - Build System")]
    public static void Setup1D()
    {
        EnsureFolder("Assets/_Game", "Prefabs");
        EnsureFolder("Assets/_Game/Prefabs", "Buildings");
        EnsureFolder("Assets/_Game", "ScriptableObjects");
        EnsureFolder("Assets/_Game/ScriptableObjects", "Buildings");

        // ── Matériaux ─────────────────────────────────────────────────────
        var runwayMat  = CreateOpaqueMat("Assets/_Game/Materials/RunwayMat.mat",
                             new Color(0.45f, 0.45f, 0.45f));
        var ghostValid = CreateTransparentMat("Assets/_Game/Materials/GhostValid.mat",
                             new Color(0f, 1f, 0f, 0.45f));
        var ghostInvalid = CreateTransparentMat("Assets/_Game/Materials/GhostInvalid.mat",
                               new Color(1f, 0f, 0f, 0.45f));
        AssetDatabase.SaveAssets();

        // ── Prefab Runway (cube gris unitaire) ────────────────────────────
        const string prefabPath = "Assets/_Game/Prefabs/Buildings/Runway.prefab";
        var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab == null)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Runway";
            cube.GetComponent<MeshRenderer>().sharedMaterial = runwayMat;
            PrefabUtility.SaveAsPrefabAsset(cube, prefabPath);
            GameObject.DestroyImmediate(cube);
            existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        // ── ScriptableObject BuildingData ─────────────────────────────────
        const string soPath = "Assets/_Game/ScriptableObjects/Buildings/Runway.asset";
        var runwaySO = AssetDatabase.LoadAssetAtPath<BuildingData>(soPath);
        if (runwaySO == null)
        {
            runwaySO = ScriptableObject.CreateInstance<BuildingData>();
            runwaySO.buildingName  = "Runway";
            runwaySO.category      = BuildingCategory.Runway;
            runwaySO.cost          = 80_000f;
            runwaySO.sizeInCells   = new Vector2Int(8, 2);
            runwaySO.height        = 0.3f;
            runwaySO.gridType      = BuildingType.Runway;
            runwaySO.prefab        = existingPrefab;
            AssetDatabase.CreateAsset(runwaySO, soPath);
            AssetDatabase.SaveAssets();
        }

        // ── BuildSystem dans la scène ─────────────────────────────────────
        var existingBS = Object.FindAnyObjectByType<BuildSystem>();
        if (existingBS != null)
        {
            EditorUtility.DisplayDialog("AirportSim",
                "Un BuildSystem existe déjà dans la scène.", "OK");
            return;
        }

        var bsGo = new GameObject("Build System");
        var bs   = bsGo.AddComponent<BuildSystem>();

        var bsSO = new SerializedObject(bs);
        bsSO.FindProperty("startBuilding").objectReferenceValue     = runwaySO;
        bsSO.FindProperty("ghostValidMaterial").objectReferenceValue   = ghostValid;
        bsSO.FindProperty("ghostInvalidMaterial").objectReferenceValue = ghostInvalid;
        bsSO.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.Refresh();

        Debug.Log("[AirportSim] Build System ajouté (Runway 8×2, 80 000 $).");
        EditorUtility.DisplayDialog("AirportSim",
            "Build System ajouté !\n\nPrefab   : Assets/_Game/Prefabs/Buildings/Runway.prefab\n" +
            "Data      : Assets/_Game/ScriptableObjects/Buildings/Runway.asset\n\n" +
            "Sauvegarde la scène (Ctrl+S) puis Lance Play.\n" +
            "• Clic gauche = placer  • Clic droit = annuler", "OK");
    }

    private static Material CreateOpaqueMat(string path, Color color)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Material CreateTransparentMat(string path, Color color)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        // Transparency setup pour URP Unlit
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;

        var go = new GameObject("Event System");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
        Debug.Log("[AirportSim] EventSystem ajouté (InputSystemUIInputModule).");
    }

    // ── Setup 2E — Flight Planning & FlightBoard ──────────────────────────

    [MenuItem("AirportSim/Setup 2E - Flight Planning", true)]
    public static bool Setup2EValidate() => !Application.isPlaying;

    [MenuItem("AirportSim/Setup 2E - Flight Planning")]
    public static void Setup2E()
    {
        // FlightNotificationSystem
        if (Object.FindObjectsByType<FlightNotificationSystem>().Length == 0)
        {
            var ns = new GameObject("FlightNotificationSystem");
            ns.AddComponent<FlightNotificationSystem>();
        }

        // FlightBoard
        if (Object.FindObjectsByType<FlightBoard>().Length == 0)
        {
            var fb = new GameObject("FlightBoard");
            fb.AddComponent<FlightBoard>();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("AirportSim",
            "Setup 2E terminé !\n\n" +
            "• FlightNotificationSystem ajouté (toasts vols)\n" +
            "• FlightBoard ajouté (touche F pour ouvrir)\n\n" +
            "Le FlightScheduler génère des vols automatiquement.\n" +
            "Ajoutez des FlightData assets pour un planning manuel.", "OK");
    }

    // ── Setup 2C — Gates & Taxi ────────────────────────────────────────────

    [MenuItem("AirportSim/Setup 2C - Gates and Taxi", true)]
    public static bool Setup2CValidate() => !Application.isPlaying;

    [MenuItem("AirportSim/Setup 2C - Gates and Taxi")]
    public static void Setup2C()
    {
        // Supprimer les gates existantes
        foreach (var g in Object.FindObjectsByType<Gate>())
            Object.DestroyImmediate(g.gameObject);

        // 5 gates au bord sud de l'Apron (Z≈14, X espacés de 20 u)
        // Apron world : X [-60, 60], Z [8, 88] → on place à Z=14 (Y cell 67)
        var positions = new Vector3[]
        {
            new(-40f, 0f, 14f),
            new(-20f, 0f, 14f),
            new(  0f, 0f, 14f),
            new( 20f, 0f, 14f),
            new( 40f, 0f, 14f),
        };

        for (int i = 0; i < positions.Length; i++)
        {
            var go = new GameObject($"Gate_{i + 1}");
            go.transform.position = positions[i];
            go.AddComponent<Gate>();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("AirportSim",
            "Setup 2C terminé !\n\n" +
            "• 5 gates placées dans l'Apron\n\n" +
            "Sauvegarde (Ctrl+S) puis Play.\n" +
            "L'avion taxi automatiquement vers une gate après atterrissage.", "OK");
    }

    // ── Setup 2B — Premier avion ───────────────────────────────────────────

    [MenuItem("AirportSim/Setup 2B - Flight System", true)]
    public static bool Setup2BValidate() => !Application.isPlaying;

    [MenuItem("AirportSim/Setup 2B - Flight System")]
    public static void Setup2B()
    {
        // ── Dossiers ──────────────────────────────────────────────────────
        EnsureFolder("Assets/_Game/Prefabs",          "Aircraft");
        EnsureFolder("Assets/_Game/ScriptableObjects", "Aircraft");

        // ── Prefab avion (corps + ailes + dérive) ─────────────────────────
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.white;

        var root  = new GameObject("Boeing737");

        var body  = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform);
        body.transform.localScale = new Vector3(20f, 2f, 4f);
        body.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.DestroyImmediate(body.GetComponent<BoxCollider>());

        var wings  = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wings.name = "Wings";
        wings.transform.SetParent(root.transform);
        wings.transform.localPosition = new Vector3(0f, -0.4f, 0f);
        wings.transform.localScale    = new Vector3(5f, 0.4f, 18f);
        wings.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.DestroyImmediate(wings.GetComponent<BoxCollider>());

        var tail   = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tail.name  = "Tail";
        tail.transform.SetParent(root.transform);
        tail.transform.localPosition = new Vector3(-8f, 2f, 0f);
        tail.transform.localScale    = new Vector3(3f, 3f, 0.6f);
        tail.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.DestroyImmediate(tail.GetComponent<BoxCollider>());

        const string prefabPath = "Assets/_Game/Prefabs/Aircraft/Boeing737.prefab";
        bool         prefabNew  = !System.IO.File.Exists(
            System.IO.Path.Combine(Application.dataPath.Replace("Assets",""), prefabPath));
        var prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        // ── AircraftData ScriptableObject ─────────────────────────────────
        const string dataPath = "Assets/_Game/ScriptableObjects/Aircraft/Boeing737.asset";
        var data = AssetDatabase.LoadAssetAtPath<AircraftData>(dataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<AircraftData>();
            AssetDatabase.CreateAsset(data, dataPath);
        }
        data.aircraftName       = "Boeing 737";
        data.approachSpeed      = 80f;
        data.landingSpeed       = 30f;
        data.taxiSpeed          = 10f;
        data.size               = new Vector2Int(10, 2);
        data.passengerCapacity  = 150f;
        data.prefab             = prefabAsset;
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        // ── FlightScheduler dans la scène ─────────────────────────────────
        var existing = Object.FindAnyObjectByType<FlightScheduler>();
        if (existing == null)
        {
            var go = new GameObject("Flight Scheduler");
            var fs = go.AddComponent<FlightScheduler>();
            // Assigner via SerializedObject pour respecter [SerializeField]
            var so = new UnityEditor.SerializedObject(fs);
            so.FindProperty("aircraftData").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            var so = new UnityEditor.SerializedObject(existing);
            so.FindProperty("aircraftData").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("AirportSim",
            "Setup 2B terminé !\n\n" +
            "• Prefab Boeing737 créé\n" +
            "• AircraftData configuré\n" +
            "• FlightScheduler ajouté à la scène\n\n" +
            "Sauvegarde (Ctrl+S) puis Play.\n" +
            "Utilisez [Spawn Test Aircraft] dans l'Inspector pour tester.", "OK");
    }

    // ── Setup 2A-bis — Environnement prédéfini ─────────────────────────────

    [MenuItem("AirportSim/Setup 2A-bis - Airport Environment", true)]
    public static bool Setup2AbisValidate() => !Application.isPlaying;

    [MenuItem("AirportSim/Setup 2A-bis - Airport Environment")]
    public static void Setup2Abis()
    {
        if (Object.FindAnyObjectByType<AirportEnvironment>() != null)
        {
            EditorUtility.DisplayDialog("AirportSim",
                "Un AirportEnvironment existe déjà dans la scène.", "OK");
            return;
        }

        // Configurer la lumière directionnelle si elle existe
        var light = Object.FindAnyObjectByType<Light>();
        if (light != null && light.type == LightType.Directional)
        {
            light.transform.eulerAngles = new Vector3(45f, -30f, 0f);
            light.intensity             = 1.2f;
            light.shadows               = LightShadows.Soft;
            light.shadowStrength        = 0.55f;
        }

        // Ajouter AirportEnvironment à la scène
        var go = new GameObject("Airport Environment");
        go.AddComponent<AirportEnvironment>();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("AirportSim",
            "Airport Environment ajouté !\n\n" +
            "Sauvegarde (Ctrl+S) puis Play.\n" +
            "L'environnement se génère automatiquement au démarrage.\n\n" +
            "Bouton « Regenerate Environment » dans l'Inspector pour regénérer sans relancer.", "OK");
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
