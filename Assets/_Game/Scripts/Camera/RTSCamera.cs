using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using Sirenix.OdinInspector;

[RequireComponent(typeof(Camera))]
public class RTSCamera : MonoBehaviour
{
    [FoldoutGroup("Movement")]
    [SerializeField] private float moveSpeed = 20f;

    [FoldoutGroup("Movement")]
    [SerializeField] private Vector2 mapBounds = new Vector2(128f, 128f);

    [FoldoutGroup("Zoom")]
    [SerializeField] private float zoomSpeed = 10f;

    [FoldoutGroup("Zoom")]
    [SerializeField] private float minOrthoSize = 5f;

    [FoldoutGroup("Zoom")]
    [SerializeField] private float maxOrthoSize = 20f;

    [FoldoutGroup("Zoom")]
    [SerializeField] private float zoomDuration = 0.25f;

    [FoldoutGroup("Rotation")]
    [SerializeField] private float rotationDuration = 0.3f;

    private Camera _cam;
    private float _targetOrthoSize;
    private bool _isRotating;

    private const float PitchAngle = 45f;
    private const float CameraHeight = 20f;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _cam.orthographic = true;
        _targetOrthoSize = _cam.orthographicSize;
    }

    private void Update()
    {
        HandleMovement();
        HandleZoom();
        HandleRotation();
    }

    private void HandleMovement()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float h = 0f, v = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h = -1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h =  1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v = -1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v =  1f;
        if (h == 0f && v == 0f) return;

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 pos = transform.position + (right * h + forward * v) * (moveSpeed * Time.deltaTime);

        float halfX = mapBounds.x * 0.5f;
        float halfZ = mapBounds.y * 0.5f;
        pos.x = Mathf.Clamp(pos.x, -halfX, halfX);
        pos.z = Mathf.Clamp(pos.z, -halfZ, halfZ);
        pos.y = CameraHeight;

        transform.position = pos;
    }

    private void HandleZoom()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        _targetOrthoSize = Mathf.Clamp(_targetOrthoSize - scroll * zoomSpeed * 0.05f, minOrthoSize, maxOrthoSize);
        _cam.DOKill();
        _cam.DOOrthoSize(_targetOrthoSize, zoomDuration).SetEase(Ease.OutQuad);
    }

    private void HandleRotation()
    {
        if (_isRotating) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        float angle = 0f;
        if (kb.qKey.wasPressedThisFrame)      angle = -45f;
        else if (kb.eKey.wasPressedThisFrame) angle =  45f;
        else return;

        _isRotating = true;
        float targetY = transform.eulerAngles.y + angle;
        transform.DORotate(new Vector3(PitchAngle, targetY, 0f), rotationDuration, RotateMode.Fast)
                 .SetEase(Ease.OutQuad)
                 .OnComplete(() => _isRotating = false);
    }
}
