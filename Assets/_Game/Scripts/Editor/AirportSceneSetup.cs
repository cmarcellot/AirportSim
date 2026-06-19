using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

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

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
