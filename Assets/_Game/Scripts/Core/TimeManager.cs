using UnityEngine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [FoldoutGroup("Time Settings")]
    [SerializeField] private float startHour = 6f;

    // 1 game minute = 1 real second à x1 → un jour complet = 24 min réelles
    [FoldoutGroup("Time Settings")]
    [SerializeField] private float realSecondsPerGameMinute = 1f;

    [ShowInInspector, ReadOnly, FoldoutGroup("Runtime")]
    public string CurrentTimeString => FormatTime(_currentHour);

    [ShowInInspector, ReadOnly, FoldoutGroup("Runtime")]
    public int SpeedMultiplier => _speeds[_speedIndex];

    /// <summary>Time.timeScale appliqué (1 / 2 / 4). Utilisé par DOTween et Unity automatiquement.</summary>
    public float TimeScale { get; private set; } = 1f;

    public float CurrentHour => _currentHour;

    private float _currentHour;
    private int _speedIndex = 0;
    private static readonly int[] _speeds = { 1, 2, 4 };

    public event System.Action<string, int> OnTimeChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _currentHour = startHour;
        ApplyTimeScale();
    }

    private void Update()
    {
        CycleSpeedInput();
        AdvanceTime();
    }

    private void CycleSpeedInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (!kb.tKey.wasPressedThisFrame) return;

        _speedIndex = (_speedIndex + 1) % _speeds.Length;
        ApplyTimeScale();
        OnTimeChanged?.Invoke(CurrentTimeString, SpeedMultiplier);
    }

    private void ApplyTimeScale()
    {
        TimeScale          = SpeedMultiplier;
        Time.timeScale     = TimeScale;
    }

    private void AdvanceTime()
    {
        // On utilise unscaledDeltaTime : Time.timeScale accélère déjà tout le reste
        // (DOTween, physics, coroutines WaitForSeconds…).
        // La formule de l'horloge virtuelle reste identique, indépendante de timeScale.
        float minutesPerSecond = SpeedMultiplier / realSecondsPerGameMinute;
        _currentHour += Time.unscaledDeltaTime * minutesPerSecond / 60f;
        if (_currentHour >= 24f) _currentHour -= 24f;

        OnTimeChanged?.Invoke(CurrentTimeString, SpeedMultiplier);
    }

    private static string FormatTime(float hour)
    {
        int h = Mathf.FloorToInt(hour);
        int m = Mathf.FloorToInt((hour - h) * 60f);
        return $"{h:D2}:{m:D2}";
    }
}
