using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

public class PathfindingSystem : MonoBehaviour
{
    // ── Nœud A* ────────────────────────────────────────────────────────────
    private class Node
    {
        public Vector2Int Cell;
        public Vector3    WorldPos;
        public List<Node> Neighbors = new();
        public float      G, H, F;
        public Node       Parent;
    }

    // ── Types de zones par graphe ──────────────────────────────────────────
    private static readonly HashSet<ZoneType> AirsideZones  = new()
        { ZoneType.Runway, ZoneType.Taxiway, ZoneType.Apron };

    private static readonly HashSet<ZoneType> LandsideZones = new()
        { ZoneType.TerminalHall, ZoneType.CheckInArea, ZoneType.SecurityArea,
          ZoneType.CustomsArea,  ZoneType.BoardingLounge };

    // ── Config ─────────────────────────────────────────────────────────────
    [FoldoutGroup("Pathfinding")] [SerializeField] private float      cellSize       = 4f;
    [FoldoutGroup("Pathfinding")] [SerializeField] private Vector2Int gridDimensions = new(128, 128);

    // ── Stats ──────────────────────────────────────────────────────────────
    [ShowInInspector, ReadOnly, FoldoutGroup("Pathfinding")]
    public int AirsideNodeCount  => _airside.Count;
    [ShowInInspector, ReadOnly, FoldoutGroup("Pathfinding")]
    public int LandsideNodeCount => _landside.Count;

    // ── Debug visuel ───────────────────────────────────────────────────────
    [FoldoutGroup("Pathfinding")] [SerializeField] private bool showAirside  = true;
    [FoldoutGroup("Pathfinding")] [SerializeField] private bool showLandside = true;

    // ── Runtime ────────────────────────────────────────────────────────────
    private ZoneSystem _zones;
    private readonly Dictionary<Vector2Int, Node> _airside  = new();
    private readonly Dictionary<Vector2Int, Node> _landside = new();
    private List<Vector3> _debugAirside  = new();
    private List<Vector3> _debugLandside = new();

    private static readonly Vector2Int[] Dirs =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        _zones = FindAnyObjectByType<ZoneSystem>();
        if (_zones != null) _zones.OnZoneChanged += RebuildAllGraphs;
    }

    private void Start() => RebuildAllGraphs();

    private void OnDestroy()
    {
        if (_zones != null) _zones.OnZoneChanged -= RebuildAllGraphs;
    }

    // ── API publique ───────────────────────────────────────────────────────

    [Button("Rebuild Pathfinding"), FoldoutGroup("Pathfinding")]
    public void RebuildAllGraphs()
    {
        if (_zones == null) _zones = FindAnyObjectByType<ZoneSystem>();
        if (_zones == null) return;

        BuildGraph(_airside,  AirsideZones);
        BuildGraph(_landside, LandsideZones);

        AutoDebugPath(_airside,  ref _debugAirside);
        AutoDebugPath(_landside, ref _debugLandside);
    }

    public List<Vector3> FindAirsidePath(Vector3 start, Vector3 end)
        => AStar(_airside, start, end);

    public List<Vector3> FindLandsidePath(Vector3 start, Vector3 end)
        => AStar(_landside, start, end);

    // ── Construction du graphe ─────────────────────────────────────────────

    private void BuildGraph(Dictionary<Vector2Int, Node> graph, HashSet<ZoneType> types)
    {
        graph.Clear();
        for (int x = 0; x < gridDimensions.x; x++)
        for (int z = 0; z < gridDimensions.y; z++)
        {
            var cell = new Vector2Int(x, z);
            var zone = _zones.GetZone(cell);
            if (zone.HasValue && types.Contains(zone.Value))
                graph[cell] = new Node { Cell = cell, WorldPos = CellToWorld(cell) };
        }
        foreach (var (cell, node) in graph)
        foreach (var dir in Dirs)
            if (graph.TryGetValue(cell + dir, out var nb))
                node.Neighbors.Add(nb);
    }

    // ── A* ─────────────────────────────────────────────────────────────────

    private List<Vector3> AStar(Dictionary<Vector2Int, Node> graph, Vector3 startW, Vector3 endW)
    {
        var sc = WorldToCell(startW);
        var ec = WorldToCell(endW);

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

            open.Remove(cur); openSet.Remove(cur.Cell); closed.Add(cur.Cell);

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

    private void AutoDebugPath(Dictionary<Vector2Int, Node> graph, ref List<Vector3> path)
    {
        path = new();
        if (graph.Count < 2) return;

        Node s = null, e = null;
        foreach (var n in graph.Values)
        {
            if (s == null || n.Cell.x + n.Cell.y < s.Cell.x + s.Cell.y) s = n;
            if (e == null || n.Cell.x + n.Cell.y > e.Cell.x + e.Cell.y) e = n;
        }
        path = AStar(graph, s!.WorldPos, e!.WorldPos);
    }

    // ── Gizmos ─────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        const float y  = 0.6f;
        const float y2 = 0.8f;

        if (showAirside)
        {
            Gizmos.color = Color.blue;
            foreach (var n in _airside.Values) Gizmos.DrawSphere(n.WorldPos + Vector3.up * y, 0.15f);
            Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f);
            foreach (var n in _airside.Values)
                foreach (var nb in n.Neighbors)
                    Gizmos.DrawLine(n.WorldPos + Vector3.up * y, nb.WorldPos + Vector3.up * y);
            if (_debugAirside is { Count: > 1 })
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < _debugAirside.Count - 1; i++)
                    Gizmos.DrawLine(_debugAirside[i] + Vector3.up * y2, _debugAirside[i+1] + Vector3.up * y2);
            }
        }

        if (showLandside)
        {
            Gizmos.color = Color.green;
            foreach (var n in _landside.Values) Gizmos.DrawSphere(n.WorldPos + Vector3.up * y, 0.15f);
            Gizmos.color = new Color(0.5f, 1f, 0.5f, 0.3f);
            foreach (var n in _landside.Values)
                foreach (var nb in n.Neighbors)
                    Gizmos.DrawLine(n.WorldPos + Vector3.up * y, nb.WorldPos + Vector3.up * y);
            if (_debugLandside is { Count: > 1 })
            {
                Gizmos.color = new Color(1f, 0.55f, 0f);
                for (int i = 0; i < _debugLandside.Count - 1; i++)
                    Gizmos.DrawLine(_debugLandside[i] + Vector3.up * y2, _debugLandside[i+1] + Vector3.up * y2);
            }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private Vector2Int WorldToCell(Vector3 world)
    {
        float hw = gridDimensions.x * cellSize * 0.5f;
        float hh = gridDimensions.y * cellSize * 0.5f;
        return new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt((world.x + hw) / cellSize), 0, gridDimensions.x - 1),
            Mathf.Clamp(Mathf.FloorToInt((world.z + hh) / cellSize), 0, gridDimensions.y - 1));
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        float hw = gridDimensions.x * cellSize * 0.5f;
        float hh = gridDimensions.y * cellSize * 0.5f;
        return new Vector3((cell.x + 0.5f) * cellSize - hw, 0f,
                           (cell.y + 0.5f) * cellSize - hh);
    }
}
