using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Sirenix.OdinInspector;

/// <summary>
/// Génère automatiquement le layout prédéfini de l'aéroport au démarrage.
/// Peint les zones via ZoneSystem, crée les murs du terminal et les marquages au sol.
/// </summary>
public class AirportEnvironment : MonoBehaviour
{
    // ── Config ─────────────────────────────────────────────────────────────
    [FoldoutGroup("Environment Settings")]
    [SerializeField] private float      cellSize        = 4f;
    [FoldoutGroup("Environment Settings")]
    [SerializeField] private Vector2Int gridDimensions  = new(128, 128);
    [FoldoutGroup("Environment Settings")]
    [SerializeField] private bool       generateOnStart = true;

    [FoldoutGroup("Environment Settings")]
    [SerializeField] private Vector2Int terminalSize  = new(40, 30); // largeur × profondeur
    [FoldoutGroup("Environment Settings")]
    [SerializeField] private Vector2Int parkingSize   = new(20, 16); // largeur × profondeur
    [FoldoutGroup("Environment Settings")]
    [SerializeField] private int        runwayCount   = 2;
    [FoldoutGroup("Environment Settings")]
    [SerializeField] private int        taxiwayWidth  = 4;           // cellules
    [FoldoutGroup("Environment Settings")]
    [SerializeField] private float      wallHeight    = 8f;
    [FoldoutGroup("Environment Settings")]
    [SerializeField] private float      wallThickness = 0.3f;

    // ── Runtime ────────────────────────────────────────────────────────────
    private ZoneSystem _zones;
    private GameObject _wallsRoot;
    private GameObject _markingsRoot;

    // Layout calculé (en cellules, Z = profondeur, Y dans le monde)
    private int _termX,    _termY,    _termW,  _termH;
    private int _apronX,   _apronY,   _apronW, _apronH;
    private int _taxiLX,   _taxiRX,   _taxiY,  _taxiH;
    private int _hTaxiX,   _hTaxiY,   _hTaxiW;
    private int _runwayX,  _runwayW,  _runwayH;
    private int _runway1Y, _runway2Y;
    private int _parkingX, _parkingY, _parkingW, _parkingH;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake() => _zones = FindAnyObjectByType<ZoneSystem>();

    private void Start()
    {
        if (generateOnStart) GenerateEnvironment();
    }

    // ── Boutons ────────────────────────────────────────────────────────────

    [Button("Regenerate Environment"), FoldoutGroup("Environment Settings")]
    public void GenerateEnvironment()
    {
        if (_zones == null) _zones = FindAnyObjectByType<ZoneSystem>();
        if (_zones == null)
        {
            Debug.LogWarning("[AirportEnvironment] ZoneSystem introuvable.");
            return;
        }

        ClearEnvironment();
        PaintZones();
        CreateWalls();
        CreateMarkings();
        SetupLighting();
    }

    [Button("Clear Environment"), FoldoutGroup("Environment Settings")]
    public void ClearEnvironment()
    {
        if (_zones == null) _zones = FindAnyObjectByType<ZoneSystem>();
        _zones?.ClearAllZones();
        SmartDestroy(ref _wallsRoot);
        SmartDestroy(ref _markingsRoot);
    }

    // ── Zones ──────────────────────────────────────────────────────────────

    private void PaintZones()
    {
        int gcx = gridDimensions.x / 2; // 64

        // Layout de bas (sud/ville) vers le haut (nord/pistes) :
        // Y 12  → parking
        // Y 28  → route d'accès
        // Y 36  → terminal
        // Y 66  → apron
        // Y 86  → taxiways verticaux
        // Y 94  → taxiway horizontal
        // Y 100 → runway 1
        // Y 110 → runway 2 (si runwayCount >= 2)

        // ── Parking ───────────────────────────────────────────────────────
        _parkingW = parkingSize.x; _parkingH = parkingSize.y;
        _parkingX = gcx - _parkingW / 2;
        _parkingY = 12;
        PaintRect(_parkingX, _parkingY, _parkingW, _parkingH, ZoneType.Parking);

        // ── Route d'accès ─────────────────────────────────────────────────
        int roadX = gcx - taxiwayWidth / 2;
        int roadY = _parkingY + _parkingH; // 28
        int roadH = 8;
        PaintRect(roadX, roadY, taxiwayWidth, roadH, ZoneType.RoadAccess);

        // ── Terminal ──────────────────────────────────────────────────────
        _termW = terminalSize.x; _termH = terminalSize.y;
        _termX = gcx - _termW / 2;
        _termY = roadY + roadH; // 36
        PaintRect(_termX, _termY, _termW, _termH, ZoneType.TerminalHall);

        // ── Apron ─────────────────────────────────────────────────────────
        _apronW = 30; _apronH = 20;
        _apronX = gcx - _apronW / 2;
        _apronY = _termY + _termH; // 66
        PaintRect(_apronX, _apronY, _apronW, _apronH, ZoneType.Apron);

        // ── Taxiways verticaux (gauche + droite) ──────────────────────────
        _taxiH  = 8;
        _taxiY  = _apronY + _apronH; // 86
        _taxiLX = _apronX;
        _taxiRX = _apronX + _apronW - taxiwayWidth;
        PaintRect(_taxiLX, _taxiY, taxiwayWidth, _taxiH, ZoneType.Taxiway);
        PaintRect(_taxiRX, _taxiY, taxiwayWidth, _taxiH, ZoneType.Taxiway);

        // ── Taxiway horizontal reliant L et R ─────────────────────────────
        _hTaxiX = _taxiLX;
        _hTaxiY = _taxiY + _taxiH; // 94
        _hTaxiW = _taxiRX + taxiwayWidth - _taxiLX;
        PaintRect(_hTaxiX, _hTaxiY, _hTaxiW, taxiwayWidth, ZoneType.Taxiway);

        // ── Runways ────────────────────────────────────────────────────────
        _runwayW = 60; _runwayH = 8;
        _runwayX = gcx - _runwayW / 2;
        _runway1Y = _hTaxiY + taxiwayWidth + 2; // 100
        PaintRect(_runwayX, _runway1Y, _runwayW, _runwayH, ZoneType.Runway);

        if (runwayCount >= 2)
        {
            _runway2Y = _runway1Y + _runwayH + 2; // 110
            PaintRect(_runwayX, _runway2Y, _runwayW, _runwayH, ZoneType.Runway);
        }
    }

    private void PaintRect(int x, int y, int w, int h, ZoneType type)
    {
        var cells = new List<Vector2Int>(w * h);
        for (int cx = x; cx < x + w; cx++)
        for (int cy = y; cy < y + h; cy++)
            cells.Add(new Vector2Int(cx, cy));
        _zones.SetZoneBatch(cells, type);
    }

    // ── Murs du terminal ───────────────────────────────────────────────────

    private void CreateWalls()
    {
        _wallsRoot = new GameObject("Terminal Walls");

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.88f));
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_ZWrite",  0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;

        float minX = Edge(_termX),          maxX = Edge(_termX + _termW);
        float minZ = Edge(_termY, isZ:true), maxZ = Edge(_termY + _termH, isZ:true);
        float cxW  = (minX + maxX) * 0.5f,  czW  = (minZ + maxZ) * 0.5f;
        float lenX = maxX - minX,            lenZ = maxZ - minZ;
        float wt   = wallThickness,          wh   = wallHeight;

        MakeWall("Wall_S", mat, new Vector3(cxW,            wh * 0.5f, minZ - wt * 0.5f), new Vector3(lenX + wt * 2, wh, wt));
        MakeWall("Wall_N", mat, new Vector3(cxW,            wh * 0.5f, maxZ + wt * 0.5f), new Vector3(lenX + wt * 2, wh, wt));
        MakeWall("Wall_W", mat, new Vector3(minX - wt * 0.5f, wh * 0.5f, czW),            new Vector3(wt, wh, lenZ));
        MakeWall("Wall_E", mat, new Vector3(maxX + wt * 0.5f, wh * 0.5f, czW),            new Vector3(wt, wh, lenZ));
    }

    private void MakeWall(string name, Material mat, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(_wallsRoot.transform);
        go.transform.position   = pos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        SmartDestroyComp(go.GetComponent<BoxCollider>());
    }

    // ── Marquages au sol ───────────────────────────────────────────────────

    private void CreateMarkings()
    {
        _markingsRoot = new GameObject("Airport Markings");
        var whiteMat  = UnlitMat(Color.white);
        var yellowMat = UnlitMat(new Color(1f, 0.85f, 0f));

        RunwayMarkings(whiteMat);
        TaxiwayMarkings(yellowMat);
        ParkingMarkings(whiteMat);
    }

    // Tirets blancs sur l'axe central + bandes de seuil
    private void RunwayMarkings(Material mat)
    {
        int[] rwYs = runwayCount >= 2
            ? new[] { _runway1Y, _runway2Y }
            : new[] { _runway1Y };

        foreach (int ry in rwYs)
        {
            float zCenter = Edge(ry + _runwayH * 0.5f, isZ: true);
            float xStart  = Edge(_runwayX);
            float xEnd    = Edge(_runwayX + _runwayW);
            float dashL   = cellSize * 0.7f;
            float dashGap = cellSize * 0.8f;

            // Tirets centraux
            float x = xStart + dashGap;
            while (x + dashL < xEnd - dashGap)
            {
                Mark("Dash", mat,
                     new Vector3(x + dashL * 0.5f, 0.04f, zCenter),
                     new Vector3(dashL, 0.02f, 0.35f));
                x += dashL + dashGap;
            }

            // Bandes de seuil (début et fin)
            ThresholdBands(mat, xStart + 5f, zCenter);
            ThresholdBands(mat, xEnd   - 5f, zCenter);
        }
    }

    private void ThresholdBands(Material mat, float xCenter, float zCenter)
    {
        float rwWorldH = _runwayH * cellSize; // 32 units
        float bW  = 1.2f;
        float bL  = rwWorldH * 0.38f;
        float gap = 1.0f;
        int   n   = 4;
        float total = n * bW + (n - 1) * gap;
        float zStart = zCenter - total * 0.5f;

        for (int i = 0; i < n; i++)
        {
            float zp = zStart + i * (bW + gap) + bW * 0.5f;
            Mark("Threshold", mat,
                 new Vector3(xCenter, 0.04f, zp),
                 new Vector3(bL, 0.02f, bW));
        }
    }

    // Ligne jaune continue sur l'axe de chaque taxiway
    private void TaxiwayMarkings(Material mat)
    {
        const float lW = 0.28f;

        // Vertical gauche
        float lx = Edge(_taxiLX + taxiwayWidth * 0.5f);
        float zS = Edge(_taxiY,          isZ: true);
        float zE = Edge(_taxiY + _taxiH, isZ: true);
        Mark("Taxi_V_L", mat,
             new Vector3(lx, 0.04f, (zS + zE) * 0.5f),
             new Vector3(lW, 0.02f, Mathf.Abs(zE - zS)));

        // Vertical droit
        float rx = Edge(_taxiRX + taxiwayWidth * 0.5f);
        Mark("Taxi_V_R", mat,
             new Vector3(rx, 0.04f, (zS + zE) * 0.5f),
             new Vector3(lW, 0.02f, Mathf.Abs(zE - zS)));

        // Horizontal
        float hz  = Edge(_hTaxiY + taxiwayWidth * 0.5f, isZ: true);
        float hxS = Edge(_hTaxiX);
        float hxE = Edge(_hTaxiX + _hTaxiW);
        Mark("Taxi_H", mat,
             new Vector3((hxS + hxE) * 0.5f, 0.04f, hz),
             new Vector3(Mathf.Abs(hxE - hxS), 0.02f, lW));
    }

    // Grille blanche délimitant les places de parking
    private void ParkingMarkings(Material mat)
    {
        const float lW    = 0.15f;
        const int   spW   = 2;    // cellules / place en largeur
        const int   spD   = 4;    // cellules / place en profondeur

        float pxS = Edge(_parkingX);
        float pxE = Edge(_parkingX + _parkingW);
        float pzS = Edge(_parkingY,             isZ: true);
        float pzE = Edge(_parkingY + _parkingH, isZ: true);

        for (int xi = 0; xi <= _parkingW; xi += spW)
        {
            float wx = Edge(_parkingX + xi);
            Mark("Park_V", mat,
                 new Vector3(wx, 0.04f, (pzS + pzE) * 0.5f),
                 new Vector3(lW, 0.02f, Mathf.Abs(pzE - pzS)));
        }

        for (int zi = 0; zi <= _parkingH; zi += spD)
        {
            float wz = Edge(_parkingY + zi, isZ: true);
            Mark("Park_H", mat,
                 new Vector3((pxS + pxE) * 0.5f, 0.04f, wz),
                 new Vector3(Mathf.Abs(pxE - pxS), 0.02f, lW));
        }
    }

    // ── Éclairage ──────────────────────────────────────────────────────────

    private static void SetupLighting()
    {
        var sun = FindAnyObjectByType<Light>();
        if (sun != null && sun.type == LightType.Directional)
        {
            sun.transform.eulerAngles = new Vector3(45f, -30f, 0f);
            sun.intensity             = 1.2f;
            sun.shadows               = LightShadows.Soft;
            sun.shadowStrength        = 0.55f;
        }

        RenderSettings.ambientMode  = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.58f, 0.66f, 0.80f);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    // Bord gauche/bas d'une position fractionnaire en cellules → world
    // CellEdge(x)   = bord gauche de la colonne x
    // CellEdge(x+w) = bord droit de la colonne x+w−1
    // CellEdge(x + w*0.5f) = centre de la plage [x, x+w[
    private float Edge(float index, bool isZ = false)
    {
        float half = (isZ ? gridDimensions.y : gridDimensions.x) * cellSize * 0.5f;
        return index * cellSize - half;
    }

    private GameObject Mark(string name, Material mat, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(_markingsRoot.transform);
        go.transform.position   = pos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        SmartDestroyComp(go.GetComponent<BoxCollider>());
        return go;
    }

    private static Material UnlitMat(Color color)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", color);
        return mat;
    }

    private static void SmartDestroy(ref GameObject go)
    {
        if (go == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) Object.DestroyImmediate(go);
        else                        Object.Destroy(go);
#else
        Object.Destroy(go);
#endif
        go = null;
    }

    private static void SmartDestroyComp(Component c)
    {
        if (c == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) Object.DestroyImmediate(c);
        else                        Object.Destroy(c);
#else
        Object.Destroy(c);
#endif
    }
}
