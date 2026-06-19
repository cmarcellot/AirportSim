using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

public class PathfindingSystem : MonoBehaviour
{
    // ── Nœud A* partagé ───────────────────────────────────────────────────
    private class Node
    {
        public Vector2Int Cell;
        public Vector3    WorldPos;
        public List<Node> Neighbors = new();
        public float      G, H, F;
        public Node       Parent;
    }

    // ── Types navigables par graphe ────────────────────────────────────────
    private static readonly HashSet<BuildingType> AirsideTypes = new()
    {
        BuildingType.Runway, BuildingType.Taxiway, BuildingType.Apron,
        BuildingType.Gate, BuildingType.FuelStation, BuildingType.CargoArea,
    };

    private static readonly HashSet<BuildingType> LandsideTypes = new()
    {
        BuildingType.Terminal, BuildingType.Hall, BuildingType.CheckIn,
        BuildingType.SecurityCheckpoint, BuildingType.Customs, BuildingType.BoardingLounge,
        BuildingType.Shop, BuildingType.Restaurant,
        BuildingType.Parking, BuildingType.RoadAccess, BuildingType.BusStop, BuildingType.TaxiZone,
    };

    // ── Paramètres ─────────────────────────────────────────────────────────
    [FoldoutGroup("Airside Pathfinding")]
    [SerializeField] private float cellSize = 4f;
    [FoldoutGroup("Airside Pathfinding")]
    [SerializeField] private Vector2Int gridDimensions = new Vector2Int(128, 128);

    [FoldoutGroup("Airside Pathfinding")]
    [ShowInInspector, ReadOnly] public int AirsideNodeCount => _airside.Count;

    [FoldoutGroup("Landside Pathfinding")]
    [ShowInInspector, ReadOnly] public int LandsideNodeCount => _landside.Count;

    // ── Debug visuel ───────────────────────────────────────────────────────
    [FoldoutGroup("Airside Pathfinding")]
    [SerializeField] private bool showAirside = true;
    [FoldoutGroup("Airside Pathfinding")]
    [SerializeField] private Vector3 airsideDebugStart;
    [FoldoutGroup("Airside Pathfinding")]
    [SerializeField] private Vector3 airsideDebugEnd;

    [FoldoutGroup("Landside Pathfinding")]
    [SerializeField] private bool showLandside = true;
    [FoldoutGroup("Landside Pathfinding")]
    [SerializeField] private Vector3 landsideDebugStart;
    [FoldoutGroup("Landside Pathfinding")]
    [SerializeField] private Vector3 landsideDebugEnd;

    // ── État interne ───────────────────────────────────────────────────────
    private GridSystem _grid;
    private readonly Dictionary<Vector2Int, Node> _airside  = new();
    private readonly Dictionary<Vector2Int, Node> _landside = new();
    private List<Vector3> _debugAirside  = new();
    private List<Vector3> _debugLandside = new();

    private int   _lastAirsideCount  = -1;
    private int   _lastLandsideCount = -1;
    private float _checkTimer;
    private const float CheckInterval = 0.5f;

    private static readonly Vector2Int[] Dirs =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake() => _grid = FindAnyObjectByType<GridSystem>();

    private void Start() => RebuildAllGraphs();

    private void Update()
    {
        _checkTimer += Time.deltaTime;
        if (_checkTimer < CheckInterval) return;
        _checkTimer = 0f;

        int a = CountType(AirsideTypes);
        int l = CountType(LandsideTypes);
        if (a != _lastAirsideCount || l != _lastLandsideCount)
            RebuildAllGraphs();
    }

    // ── API publique ───────────────────────────────────────────────────────

    [Button("Rebuild All Graphs"), FoldoutGroup("Airside Pathfinding")]
    public void RebuildAllGraphs()
    {
        if (_grid == null) _grid = FindAnyObjectByType<GridSystem>();
        if (_grid == null) return;

        BuildGraph(_airside,  AirsideTypes);
        BuildGraph(_landside, LandsideTypes);

        _lastAirsideCount  = _airside.Count;
        _lastLandsideCount = _landside.Count;

        AutoDebugPath(_airside,  ref _debugAirside,  ref airsideDebugStart,  ref airsideDebugEnd);
        AutoDebugPath(_landside, ref _debugLandside, ref landsideDebugStart, ref landsideDebugEnd);
    }

    public List<Vector3> FindAirsidePath(Vector3 start, Vector3 end)
        => AStar(_airside, start, end);

    public List<Vector3> FindLandsidePath(Vector3 start, Vector3 end)
        => AStar(_landside, start, end);

    // ── Construction du graphe ─────────────────────────────────────────────

    private void BuildGraph(Dictionary<Vector2Int, Node> graph, HashSet<BuildingType> types)
    {
        graph.Clear();

        for (int x = 0; x < gridDimensions.x; x++)
        for (int z = 0; z < gridDimensions.y; z++)
        {
            var cell = new Vector2Int(x, z);
            if (types.Contains(_grid.GetCell(cell).Building))
                graph[cell] = new Node { Cell = cell, WorldPos = CellToWorld(cell) };
        }

        foreach (var (cell, node) in graph)
        foreach (var dir in Dirs)
        {
            if (graph.TryGetValue(cell + dir, out var nb))
                node.Neighbors.Add(nb);
        }
    }

    // ── A* ─────────────────────────────────────────────────────────────────

    private List<Vector3> AStar(Dictionary<Vector2Int, Node> graph,
                                Vector3 startW, Vector3 endW)
    {
        if (_grid == null) return new();

        var sc = _grid.GetCellFromWorldPos(startW);
        var ec = _grid.GetCellFromWorldPos(endW);

        if (!graph.TryGetValue(sc, out var startNode) || !graph.ContainsKey(ec))
            return new();

        foreach (var n in graph.Values)
        { n.G = float.MaxValue; n.H = 0; n.F = float.MaxValue; n.Parent = null; }

        startNode.G = 0;
        startNode.H = Heuristic(sc, ec);
        startNode.F = startNode.H;

        var open    = new List<Node> { startNode };
        var openSet = new HashSet<Vector2Int> { sc };
        var closed  = new HashSet<Vector2Int>();

        while (open.Count > 0)
        {
            var cur = open[0];
            for (int i = 1; i < open.Count; i++)
                if (open[i].F < cur.F) cur = open[i];

            if (cur.Cell == ec) return Reconstruct(cur);

            open.Remove(cur);
            openSet.Remove(cur.Cell);
            closed.Add(cur.Cell);

            foreach (var nb in cur.Neighbors)
            {
                if (closed.Contains(nb.Cell)) continue;
                float g = cur.G + 1f;
                if (g >= nb.G) continue;

                nb.G = g; nb.H = Heuristic(nb.Cell, ec);
                nb.F = g + nb.H; nb.Parent = cur;

                if (!openSet.Contains(nb.Cell)) { open.Add(nb); openSet.Add(nb.Cell); }
            }
        }
        return new();
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
        => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private static List<Vector3> Reconstruct(Node end)
    {
        var path = new List<Vector3>();
        for (var n = end; n != null; n = n.Parent) path.Add(n.WorldPos);
        path.Reverse();
        return path;
    }

    // ── Debug auto ─────────────────────────────────────────────────────────

    private void AutoDebugPath(Dictionary<Vector2Int, Node> graph, ref List<Vector3> path,
                               ref Vector3 startPos, ref Vector3 endPos)
    {
        path = new();
        if (graph.Count < 2) return;

        Node s = null, e = null;
        foreach (var n in graph.Values)
        {
            if (s == null || n.Cell.x + n.Cell.y < s.Cell.x + s.Cell.y) s = n;
            if (e == null || n.Cell.x + n.Cell.y > e.Cell.x + e.Cell.y) e = n;
        }
        startPos = s!.WorldPos;
        endPos   = e!.WorldPos;
        path     = AStar(graph, startPos, endPos);
    }

    // ── Comptage ───────────────────────────────────────────────────────────

    private int CountType(HashSet<BuildingType> types)
    {
        if (_grid == null) return 0;
        int c = 0;
        for (int x = 0; x < gridDimensions.x; x++)
        for (int z = 0; z < gridDimensions.y; z++)
            if (types.Contains(_grid.GetCell(new Vector2Int(x, z)).Building)) c++;
        return c;
    }

    // ── Gizmos ─────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        const float y  = 0.6f;
        const float y2 = 0.7f;

        if (showAirside)
        {
            Gizmos.color = Color.blue;
            foreach (var n in _airside.Values)
                Gizmos.DrawSphere(n.WorldPos + Vector3.up * y, 0.2f);

            Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.4f);
            foreach (var n in _airside.Values)
                foreach (var nb in n.Neighbors)
                    Gizmos.DrawLine(n.WorldPos + Vector3.up * y, nb.WorldPos + Vector3.up * y);

            if (_debugAirside is { Count: > 1 })
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < _debugAirside.Count - 1; i++)
                    Gizmos.DrawLine(_debugAirside[i]     + Vector3.up * y2,
                                    _debugAirside[i + 1] + Vector3.up * y2);
            }
        }

        if (showLandside)
        {
            Gizmos.color = Color.green;
            foreach (var n in _landside.Values)
                Gizmos.DrawSphere(n.WorldPos + Vector3.up * y, 0.2f);

            Gizmos.color = new Color(0.5f, 1f, 0.5f, 0.4f);
            foreach (var n in _landside.Values)
                foreach (var nb in n.Neighbors)
                    Gizmos.DrawLine(n.WorldPos + Vector3.up * y, nb.WorldPos + Vector3.up * y);

            if (_debugLandside is { Count: > 1 })
            {
                Gizmos.color = new Color(1f, 0.55f, 0f); // orange
                for (int i = 0; i < _debugLandside.Count - 1; i++)
                    Gizmos.DrawLine(_debugLandside[i]     + Vector3.up * y2,
                                    _debugLandside[i + 1] + Vector3.up * y2);
            }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private Vector3 CellToWorld(Vector2Int cell)
    {
        float hw = gridDimensions.x * cellSize * 0.5f;
        float hh = gridDimensions.y * cellSize * 0.5f;
        return new Vector3((cell.x + 0.5f) * cellSize - hw, 0f,
                           (cell.y + 0.5f) * cellSize - hh);
    }
}
