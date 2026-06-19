using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

public class TaxiwayGraph : MonoBehaviour
{
    // ── Node interne ───────────────────────────────────────────────────────
    private class Node
    {
        public Vector2Int     Cell;
        public Vector3        WorldPos;
        public List<Node>     Neighbors = new();
        public float          G, H, F;
        public Node           Parent;
    }

    // ── Paramètres ─────────────────────────────────────────────────────────
    [FoldoutGroup("Graph Settings")]
    [SerializeField] private float cellSize = 4f;

    [FoldoutGroup("Graph Settings")]
    [SerializeField] private Vector2Int gridDimensions = new Vector2Int(128, 128);

    // ── Debug ──────────────────────────────────────────────────────────────
    [FoldoutGroup("Pathfinding Debug")]
    [SerializeField] private Vector3 debugStartPos;

    [FoldoutGroup("Pathfinding Debug")]
    [SerializeField] private Vector3 debugEndPos;

    [ShowInInspector, ReadOnly, FoldoutGroup("Pathfinding Debug")]
    public int NodeCount => _nodes.Count;

    // ── État interne ───────────────────────────────────────────────────────
    private GridSystem _grid;
    private readonly Dictionary<Vector2Int, Node> _nodes = new();
    private List<Vector3> _debugPath = new();
    private int   _lastNodeCount = -1;
    private float _checkTimer;

    private const float CheckInterval = 0.5f;
    private static readonly Vector2Int[] Dirs =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        _grid = FindAnyObjectByType<GridSystem>();
    }

    private void Start()
    {
        RebuildGraph();
    }

    private void Update()
    {
        _checkTimer += Time.deltaTime;
        if (_checkTimer < CheckInterval) return;
        _checkTimer = 0f;

        int count = CountNavigable();
        if (count != _lastNodeCount)
            RebuildGraph();
    }

    // ── API publique ───────────────────────────────────────────────────────

    [Button("Rebuild Graph"), FoldoutGroup("Pathfinding Debug")]
    public void RebuildGraph()
    {
        if (_grid == null) _grid = FindAnyObjectByType<GridSystem>();
        if (_grid == null) return;

        _nodes.Clear();

        // Passe 1 : créer les nœuds pour chaque cellule navigable
        for (int x = 0; x < gridDimensions.x; x++)
        for (int z = 0; z < gridDimensions.y; z++)
        {
            var cell = new Vector2Int(x, z);
            var data = _grid.GetCell(cell);
            if (data.Building == BuildingType.Taxiway || data.Building == BuildingType.Runway)
                _nodes[cell] = new Node { Cell = cell, WorldPos = CellToWorld(cell) };
        }

        // Passe 2 : connecter les voisins (4 directions)
        foreach (var (cell, node) in _nodes)
        foreach (var dir in Dirs)
        {
            if (_nodes.TryGetValue(cell + dir, out var neighbor))
                node.Neighbors.Add(neighbor);
        }

        _lastNodeCount = _nodes.Count;

        // Chemin de debug automatique entre le premier et le dernier nœud
        AutoComputeDebugPath();
    }

    public List<Vector3> FindPath(Vector3 start, Vector3 end)
    {
        if (_grid == null) return new List<Vector3>();

        var startCell = _grid.GetCellFromWorldPos(start);
        var endCell   = _grid.GetCellFromWorldPos(end);

        if (!_nodes.TryGetValue(startCell, out var startNode) ||
            !_nodes.TryGetValue(endCell,   out _))
            return new List<Vector3>();

        // Réinitialise les coûts A*
        foreach (var n in _nodes.Values)
        { n.G = float.MaxValue; n.H = 0; n.F = float.MaxValue; n.Parent = null; }

        startNode.G = 0;
        startNode.H = Heuristic(startCell, endCell);
        startNode.F = startNode.H;

        var openList = new List<Node> { startNode };
        var openSet  = new HashSet<Vector2Int> { startCell };
        var closed   = new HashSet<Vector2Int>();

        while (openList.Count > 0)
        {
            // Nœud avec le F le plus bas
            var current = openList[0];
            for (int i = 1; i < openList.Count; i++)
                if (openList[i].F < current.F) current = openList[i];

            if (current.Cell == endCell)
                return ReconstructPath(current);

            openList.Remove(current);
            openSet.Remove(current.Cell);
            closed.Add(current.Cell);

            foreach (var neighbor in current.Neighbors)
            {
                if (closed.Contains(neighbor.Cell)) continue;

                float g = current.G + 1f;
                if (g >= neighbor.G) continue;

                neighbor.G      = g;
                neighbor.H      = Heuristic(neighbor.Cell, endCell);
                neighbor.F      = g + neighbor.H;
                neighbor.Parent = current;

                if (!openSet.Contains(neighbor.Cell))
                {
                    openList.Add(neighbor);
                    openSet.Add(neighbor.Cell);
                }
            }
        }

        return new List<Vector3>();
    }

    // ── Boutons debug ──────────────────────────────────────────────────────

    [Button("Compute Debug Path"), FoldoutGroup("Pathfinding Debug")]
    private void ComputeDebugPath()
    {
        _debugPath = FindPath(debugStartPos, debugEndPos);
        Debug.Log($"[TaxiwayGraph] Chemin : {_debugPath.Count} nœuds " +
                  (_debugPath.Count == 0 ? "(aucun chemin trouvé)" : "trouvé"));
    }

    // Sélectionne automatiquement deux nœuds distants et calcule le chemin
    private void AutoComputeDebugPath()
    {
        if (_nodes.Count < 2) { _debugPath = new List<Vector3>(); return; }

        var list = new List<Node>(_nodes.Values);

        // Extrémités : nœud le plus au sud-ouest et nœud le plus au nord-est
        Node start = list[0], end = list[0];
        foreach (var n in list)
        {
            if (n.Cell.x + n.Cell.y < start.Cell.x + start.Cell.y) start = n;
            if (n.Cell.x + n.Cell.y > end.Cell.x   + end.Cell.y)   end   = n;
        }

        debugStartPos = start.WorldPos;
        debugEndPos   = end.WorldPos;
        _debugPath    = FindPath(debugStartPos, debugEndPos);
    }

    // ── Gizmos (éditeur uniquement) ────────────────────────────────────────

    private void OnDrawGizmos()
    {
        if (_nodes == null || _nodes.Count == 0) return;

        const float y = 0.5f;

        Gizmos.color = Color.green;
        foreach (var node in _nodes.Values)
            Gizmos.DrawSphere(node.WorldPos + Vector3.up * y, 0.25f);

        Gizmos.color = Color.white;
        foreach (var node in _nodes.Values)
            foreach (var nb in node.Neighbors)
                Gizmos.DrawLine(node.WorldPos + Vector3.up * y,
                                nb.WorldPos   + Vector3.up * y);

        if (_debugPath == null || _debugPath.Count < 2) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < _debugPath.Count - 1; i++)
            Gizmos.DrawLine(_debugPath[i]     + Vector3.up * (y + 0.1f),
                            _debugPath[i + 1] + Vector3.up * (y + 0.1f));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private int CountNavigable()
    {
        if (_grid == null) return 0;
        int count = 0;
        for (int x = 0; x < gridDimensions.x; x++)
        for (int z = 0; z < gridDimensions.y; z++)
        {
            var b = _grid.GetCell(new Vector2Int(x, z)).Building;
            if (b == BuildingType.Taxiway || b == BuildingType.Runway)
                count++;
        }
        return count;
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        float hw = gridDimensions.x * cellSize * 0.5f;
        float hh = gridDimensions.y * cellSize * 0.5f;
        return new Vector3(
            (cell.x + 0.5f) * cellSize - hw,
            0f,
            (cell.y + 0.5f) * cellSize - hh);
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
        => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private static List<Vector3> ReconstructPath(Node end)
    {
        var path = new List<Vector3>();
        for (var n = end; n != null; n = n.Parent)
            path.Add(n.WorldPos);
        path.Reverse();
        return path;
    }
}
