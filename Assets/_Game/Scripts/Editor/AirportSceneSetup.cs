using UnityEngine;
using UnityEngine.UI;
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
        var existing = UnityEngine.Object.FindFirstObjectByType<GridSystem>();
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
        if (Object.FindFirstObjectByType<HUDController>() != null)
        {
            EditorUtility.DisplayDialog("AirportSim", "Un HUD existe déjà dans la scène.", "OK");
            return;
        }

        // ── Systèmes ──────────────────────────────────────────────────────
        if (Object.FindFirstObjectByType<EconomySystem>() == null)
        {
            var eco = new GameObject("Economy System");
            eco.AddComponent<EconomySystem>();
        }
        if (Object.FindFirstObjectByType<TimeManager>() == null)
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

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
