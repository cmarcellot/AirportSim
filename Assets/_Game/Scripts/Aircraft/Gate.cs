using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public class Gate : MonoBehaviour
{
    public enum GateState { Available, Occupied }

    // ── État ───────────────────────────────────────────────────────────────
    [FoldoutGroup("Gate"), ShowInInspector, ReadOnly]
    public GateState State { get; private set; }

    [FoldoutGroup("Gate"), ShowInInspector, ReadOnly]
    public Aircraft AssignedAircraft { get; private set; }

    // ── Interne ────────────────────────────────────────────────────────────
    private Material  _indicatorMat;
    private Transform _indicatorT;

    private static readonly Color ColorAvailable = new(0f,  0.75f, 0f,   1f);
    private static readonly Color ColorOccupied  = new(0.8f, 0f,  0f,   1f);
    private static readonly Color ColorReserved  = new(1f,  0.55f, 0f,   1f);

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        BuildIndicator();
        UpdateIndicator();
    }

    // ── API ────────────────────────────────────────────────────────────────

    public bool IsAvailable() => State == GateState.Available;

    /// <summary>Réserve la gate avant l'arrivée de l'avion (évite les doublons).</summary>
    public void Reserve()
    {
        State = GateState.Occupied;
        UpdateIndicator();
    }

    public void AssignAircraft(Aircraft aircraft)
    {
        AssignedAircraft = aircraft;
        State            = GateState.Occupied;
        UpdateIndicator();
    }

    public void ReleaseAircraft()
    {
        AssignedAircraft = null;
        State            = GateState.Available;
        UpdateIndicator();
    }

    // ── Indicateur visuel ──────────────────────────────────────────────────

    private void BuildIndicator()
    {
        // Disque coloré flottant au-dessus de la gate
        var go   = new GameObject("GateIndicator");
        _indicatorT = go.transform;
        _indicatorT.SetParent(transform);
        _indicatorT.localPosition = new Vector3(0f, 6f, 0f);
        _indicatorT.localScale    = Vector3.one;

        var disc   = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name  = "Disc";
        disc.transform.SetParent(_indicatorT);
        disc.transform.localPosition = Vector3.zero;
        disc.transform.localScale    = new Vector3(3f, 0.15f, 3f);

        _indicatorMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        disc.GetComponent<MeshRenderer>().sharedMaterial = _indicatorMat;
        Object.Destroy(disc.GetComponent<CapsuleCollider>());

        // Légère pulsation de scale
        _indicatorT.DOScale(Vector3.one * 1.15f, 0.9f)
                   .SetLoops(-1, LoopType.Yoyo)
                   .SetEase(Ease.InOutSine);
    }

    private void UpdateIndicator()
    {
        if (_indicatorMat == null) return;
        Color target = State == GateState.Available ? ColorAvailable : ColorOccupied;
        _indicatorMat.SetColor("_BaseColor", target);
    }
}
