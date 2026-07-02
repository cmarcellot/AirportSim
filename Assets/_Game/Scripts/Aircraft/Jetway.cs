using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Passerelle d'embarquement rattachée à une Gate.
/// Se déclenche via Gate.AssignAircraft / ReleaseAircraft.
/// </summary>
public class Jetway : MonoBehaviour
{
    public enum JetwayState { Retracted, Extending, Docked, Retracting }

    // ── Config ─────────────────────────────────────────────────────────────

    [FoldoutGroup("Jetway Config")]
    [SerializeField] private float extendDuration  = 2f;

    [FoldoutGroup("Jetway Config")]
    [SerializeField] private float retractDuration = 1.2f;

    [FoldoutGroup("Jetway Config")]
    [SerializeField] private float armElevation    = 3.5f;  // hauteur de la passerelle

    [FoldoutGroup("Jetway Config")]
    [SerializeField] private float armBaseOffset   = 3f;    // recul vers le terminal (-Z)

    // ── État ────────────────────────────────────────────────────────────────

    [FoldoutGroup("Jetway State"), ShowInInspector, ReadOnly]
    public JetwayState State { get; private set; }

    [FoldoutGroup("Jetway State"), ShowInInspector, ReadOnly]
    public float ArmLength { get; private set; }

    // ── Interne ─────────────────────────────────────────────────────────────

    private Transform _wrapper;   // pivot : se tourne vers l'avion
    private Transform _arm;       // tube gris, s'étend en +Z local
    private Transform _head;      // cube blanc à l'extrémité

    private Tweener _rotTween;
    private Tweener _animTween;

    private const float MinLen = 0.3f;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        CacheOrBuildVisuals();
    }

    private void OnEnable()
    {
        // Compatible Domain Reload désactivé : re-cache les refs si nécessaire
        CacheOrBuildVisuals();

        _rotTween?.Kill();
        _animTween?.Kill();

        State     = JetwayState.Retracted;
        ArmLength = MinLen;

        if (_wrapper != null)
            _wrapper.localRotation = Quaternion.identity;

        UpdateArmVisual(MinLen);
    }

    private void OnDestroy()
    {
        _rotTween?.Kill();
        _animTween?.Kill();
    }

    // ── API ─────────────────────────────────────────────────────────────────

    public void Extend(Aircraft aircraft)
    {
        if (aircraft == null) return;
        if (State == JetwayState.Extending || State == JetwayState.Docked) return;

        State = JetwayState.Extending;
        _rotTween?.Kill();
        _animTween?.Kill();

        // Position cible : avion à la même hauteur que le wrapper
        Vector3 wrapperPos = _wrapper.position;
        Vector3 targetPos  = aircraft.transform.position;
        targetPos.y        = wrapperPos.y;

        Vector3 dir = targetPos - wrapperPos;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;

        float      targetLen = Mathf.Clamp(dir.magnitude + 1.5f, 3f, 12f);
        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);

        // 1. Rotation vers l'avion → 2. Extension du bras
        _rotTween = _wrapper.DORotateQuaternion(targetRot, 0.4f)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                _animTween = DOVirtual.Float(ArmLength, targetLen, extendDuration, len =>
                    { ArmLength = len; UpdateArmVisual(len); })
                    .SetEase(Ease.InOutQuad)
                    .SetLink(gameObject)
                    .OnComplete(() => State = JetwayState.Docked);
            });
    }

    public void Retract()
    {
        if (State == JetwayState.Retracted || State == JetwayState.Retracting) return;

        State = JetwayState.Retracting;
        _rotTween?.Kill();
        _animTween?.Kill();

        _animTween = DOVirtual.Float(ArmLength, MinLen, retractDuration, len =>
            { ArmLength = len; UpdateArmVisual(len); })
            .SetEase(Ease.InOutQuad)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                State     = JetwayState.Retracted;
                _rotTween = _wrapper.DOLocalRotate(Vector3.zero, 0.5f)
                    .SetEase(Ease.OutQuad)
                    .SetLink(gameObject);
            });
    }

    // ── Visuel ───────────────────────────────────────────────────────────────

    private void UpdateArmVisual(float len)
    {
        if (_arm == null) return;
        _arm.localScale    = new Vector3(0.7f, 0.55f, len);
        _arm.localPosition = new Vector3(0f,   0f,    len / 2f);

        if (_head != null)
            _head.localPosition = new Vector3(0f, 0f, len);
    }

    private void CacheOrBuildVisuals()
    {
        if (_wrapper != null) return;

        // Récupère les enfants existants si la scène persiste (Scene Reload désactivé)
        _wrapper = transform.Find("JetwayWrapper");
        if (_wrapper != null)
        {
            _arm  = _wrapper.Find("JetwayArm");
            _head = _wrapper.Find("JetwayHead");
            return;
        }

        BuildVisuals();
    }

    private void BuildVisuals()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");

        // Wrapper : élevé et légèrement décalé vers le terminal
        var wrapperGo = new GameObject("JetwayWrapper");
        _wrapper = wrapperGo.transform;
        _wrapper.SetParent(transform);
        _wrapper.localPosition = new Vector3(0f, armElevation, -armBaseOffset);
        _wrapper.localRotation = Quaternion.identity;

        // Bras (tube gris acier, s'étend en +Z local)
        var armGo  = GameObject.CreatePrimitive(PrimitiveType.Cube);
        armGo.name = "JetwayArm";
        armGo.transform.SetParent(_wrapper);
        armGo.transform.localPosition = new Vector3(0f, 0f, MinLen / 2f);
        armGo.transform.localScale    = new Vector3(0.7f, 0.55f, MinLen);
        UnityEngine.Object.DestroyImmediate(armGo.GetComponent<BoxCollider>());
        var armMat   = new Material(shader);
        armMat.color = new Color(0.72f, 0.74f, 0.78f);
        armGo.GetComponent<MeshRenderer>().sharedMaterial = armMat;
        _arm = armGo.transform;

        // Tête de connexion (cube blanc cassé)
        var headGo  = GameObject.CreatePrimitive(PrimitiveType.Cube);
        headGo.name = "JetwayHead";
        headGo.transform.SetParent(_wrapper);
        headGo.transform.localPosition = new Vector3(0f, 0f, MinLen);
        headGo.transform.localScale    = new Vector3(0.9f, 0.7f, 0.9f);
        UnityEngine.Object.DestroyImmediate(headGo.GetComponent<BoxCollider>());
        var headMat   = new Material(shader);
        headMat.color = new Color(0.95f, 0.95f, 0.95f);
        headGo.GetComponent<MeshRenderer>().sharedMaterial = headMat;
        _head = headGo.transform;
    }

    // ── Odin ─────────────────────────────────────────────────────────────────

    [Button("Test Extend"), FoldoutGroup("Jetway State")]
    private void TestExtend()
    {
        if (!Application.isPlaying) return;
        var aircraft = FindAnyObjectByType<Aircraft>();
        if (aircraft != null) Extend(aircraft);
        else                  Debug.LogWarning("[Jetway] Aucun avion dans la scène.");
    }

    [Button("Test Retract"), FoldoutGroup("Jetway State")]
    private void TestRetract()
    {
        if (!Application.isPlaying) return;
        Retract();
    }
}
