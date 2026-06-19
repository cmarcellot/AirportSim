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
        EnsureFolder("Assets/_Game/Prefabs",          "Buildings");
        EnsureFolder("Assets/_Game/ScriptableObjects", "Buildings");

        // ── Définition complète des bâtiments ─────────────────────────────
        // (name, category, gridType, cost, sizeX, sizeZ, height, color)
        var defs = new (string n, BuildingCategory cat, BuildingType type,
                        float cost, int sx, int sz, float h, Color col)[]
        {
            // Piste (Airside)
            ("Runway",       BuildingCategory.Runway,       BuildingType.Runway,
              80_000f, 8, 2, 0.3f,  new Color(0.45f, 0.45f, 0.45f)),
            ("Taxiway",      BuildingCategory.Taxiway,      BuildingType.Taxiway,
               5_000f, 1, 1, 0.1f,  new Color(0.22f, 0.22f, 0.22f)),
            ("Apron",        BuildingCategory.Apron,        BuildingType.Apron,
              20_000f, 2, 2, 0.15f, new Color(0.35f, 0.35f, 0.35f)),
            ("Gate",         BuildingCategory.Gate,         BuildingType.Gate,
              30_000f, 2, 2, 1.5f,  new Color(0.20f, 0.45f, 0.85f)),
            ("FuelStation",  BuildingCategory.FuelStation,  BuildingType.FuelStation,
              40_000f, 2, 1, 1.5f,  new Color(0.90f, 0.70f, 0.10f)),
            ("ControlTower", BuildingCategory.ControlTower, BuildingType.ControlTower,
              50_000f, 1, 1, 4.0f,  new Color(0.85f, 0.20f, 0.20f)),

            // Terminal (Landside)
            ("Terminal",     BuildingCategory.Terminal,     BuildingType.Terminal,
             200_000f, 6, 8, 2.0f,  new Color(0.90f, 0.90f, 0.90f)),
            ("Hall",         BuildingCategory.Hall,         BuildingType.Hall,
              80_000f, 4, 4, 2.0f,  new Color(0.70f, 0.85f, 1.00f)),
            ("CheckIn",      BuildingCategory.CheckIn,      BuildingType.CheckIn,
              25_000f, 3, 2, 1.5f,  new Color(0.30f, 0.80f, 0.70f)),
            ("Security",     BuildingCategory.SecurityCheckpoint, BuildingType.SecurityCheckpoint,
              35_000f, 2, 2, 1.5f,  new Color(0.95f, 0.55f, 0.15f)),
            ("Shop",         BuildingCategory.Shop,         BuildingType.Shop,
              15_000f, 2, 2, 1.5f,  new Color(0.75f, 0.40f, 0.90f)),
            ("Restaurant",   BuildingCategory.Restaurant,   BuildingType.Restaurant,
              20_000f, 3, 2, 1.5f,  new Color(0.90f, 0.35f, 0.45f)),

            // Accès
            ("Parking",      BuildingCategory.Parking,      BuildingType.Parking,
              50_000f, 4, 4, 0.15f, new Color(0.25f, 0.30f, 0.35f)),
            ("RoadAccess",   BuildingCategory.RoadAccess,   BuildingType.RoadAccess,
               2_000f, 1, 1, 0.15f, new Color(0.30f, 0.30f, 0.30f)),
            ("BusStop",      BuildingCategory.BusStop,      BuildingType.BusStop,
              10_000f, 2, 1, 1.5f,  new Color(0.95f, 0.85f, 0.10f)),
        };

        // ── Création/mise à jour de tous les assets ───────────────────────
        var allSOs = new BuildingData[defs.Length];
        for (int i = 0; i < defs.Length; i++)
        {
            var d    = defs[i];
            var mat  = CreateOpaqueMat($"Assets/_Game/Materials/{d.n}Mat.mat", d.col);
            var pfab = CreateCubePrefab($"Assets/_Game/Prefabs/Buildings/{d.n}.prefab", mat);
            allSOs[i] = UpsertBuildingData(
                $"Assets/_Game/ScriptableObjects/Buildings/{d.n}.asset",
                d.n == "Security" ? "Security" : d.n.Replace("_", " "),
                d.cat, d.type, d.cost, new Vector2Int(d.sx, d.sz), d.h, pfab, d.col);
        }
        AssetDatabase.SaveAssets();

        // ── Met à jour la liste complète dans BuildMenuController ─────────
        var menu = Object.FindAnyObjectByType<BuildMenuController>();
        if (menu != null)
        {
            var menuSO = new SerializedObject(menu);
            var list   = menuSO.FindProperty("availableBuildings");
            list.arraySize = allSOs.Length;
            for (int i = 0; i < allSOs.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = allSOs[i];
            menuSO.ApplyModifiedProperties();
        }
        else
            Debug.LogWarning("[AirportSim] BuildMenuController introuvable — lance d'abord Setup 1E.");

        // ── Remplace TaxiwayGraph par ZoneSystem + PathfindingSystem ──────
        var oldGraph = Object.FindAnyObjectByType<TaxiwayGraph>();
        if (oldGraph != null) Object.DestroyImmediate(oldGraph.gameObject);

        if (Object.FindAnyObjectByType<ZoneSystem>() == null)
            new GameObject("Zone System").AddComponent<ZoneSystem>();

        if (Object.FindAnyObjectByType<PathfindingSystem>() == null)
            new GameObject("Pathfinding System").AddComponent<PathfindingSystem>();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.Refresh();

        Debug.Log("[AirportSim] Setup 2A terminé — 15 bâtiments, ZoneSystem, PathfindingSystem.");
        EditorUtility.DisplayDialog("AirportSim",
            "Setup 2A terminé !\n\n" +
            "• 15 bâtiments créés/mis à jour (Piste / Terminal / Accès)\n" +
            "• ZoneSystem et PathfindingSystem ajoutés\n\n" +
            "Sauvegarde (Ctrl+S) puis Play.\n" +
            "Pose des bâtiments → graphes reconstruits automatiquement.", "OK");
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
            "Control Tower", BuildingCategory.Service, BuildingType.ControlTower,
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

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
