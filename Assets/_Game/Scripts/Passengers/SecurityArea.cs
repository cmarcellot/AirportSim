using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Gère l'ensemble des postes de contrôle sécurité.
/// Singleton — accessible via SecurityArea.Instance.
/// Dessine la ligne rouge au sol marquant la zone restreinte.
/// </summary>
public class SecurityArea : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────────────────────

    public static SecurityArea Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => Instance = null;

    // ── Config ───────────────────────────────────────────────────────────────

    [FoldoutGroup("Restricted Line")]
    [SerializeField] private Vector3 restrictedLineCenter = new Vector3(0f, 0f, -55f);
    [FoldoutGroup("Restricted Line")]
    [SerializeField] private float restrictedLineWidth = 80f;

    // ── État (Odin) ──────────────────────────────────────────────────────────

    [FoldoutGroup("Security Info"), ShowInInspector, ReadOnly]
    private int CheckpointCount => _checkpoints.Count;

    [FoldoutGroup("Security Info"), ShowInInspector, ReadOnly]
    private int TotalQueued
    {
        get
        {
            int n = 0;
            foreach (var cp in _checkpoints) n += cp.QueueLength;
            return n;
        }
    }

    // ── Interne ──────────────────────────────────────────────────────────────

    private readonly List<SecurityCheckpoint> _checkpoints = new();

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        Instance = this;
        _checkpoints.Clear();
        _checkpoints.AddRange(FindObjectsByType<SecurityCheckpoint>());

        BuildRestrictedLine();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── API ──────────────────────────────────────────────────────────────────

    /// <summary>Retourne le poste avec le moins de passagers (file + en cours).</summary>
    public SecurityCheckpoint GetLeastBusyCheckpoint()
    {
        SecurityCheckpoint best = null;
        int minQueue = int.MaxValue;
        foreach (var cp in _checkpoints)
        {
            if (cp == null) continue;
            if (cp.QueueLength < minQueue)
            {
                minQueue = cp.QueueLength;
                best     = cp;
            }
        }
        return best;
    }

    // ── Ligne zone restreinte ─────────────────────────────────────────────────

    private void BuildRestrictedLine()
    {
        // Supprime la ligne existante (réentrée OnEnable / Domain Reload)
        var existing = transform.Find("RestrictedLine");
        if (existing != null) DestroyImmediate(existing.gameObject);

        var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = "RestrictedLine";
        line.transform.SetParent(transform);
        line.transform.position    = new Vector3(restrictedLineCenter.x, 0.05f, restrictedLineCenter.z);
        line.transform.localScale  = new Vector3(restrictedLineWidth, 0.05f, 0.3f);
        DestroyImmediate(line.GetComponent<BoxCollider>());

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", new Color(0.85f, 0.05f, 0.05f, 1f));
        line.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // ── Odin ─────────────────────────────────────────────────────────────────

    [Button("Refresh Checkpoints"), FoldoutGroup("Security Info")]
    private void RefreshCheckpoints()
    {
        if (!Application.isPlaying) return;
        _checkpoints.Clear();
        _checkpoints.AddRange(FindObjectsByType<SecurityCheckpoint>());
        Debug.Log($"[SecurityArea] {_checkpoints.Count} poste(s) trouvé(s).");
    }
}
