using UnityEngine;
using Sirenix.OdinInspector;

public class EconomySystem : MonoBehaviour
{
    public static EconomySystem Instance { get; private set; }

    [FoldoutGroup("Economy Settings")]
    [SerializeField] private float startingBudget = 1_000_000f;

    [ShowInInspector, ReadOnly, FoldoutGroup("Runtime")]
    public float Budget { get; private set; }

    public event System.Action<float> OnBudgetChanged;

    // Réinitialise le pointeur statique avant chaque session (Domain Reload désactivé).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        // Appelé à chaque entrée en Play Mode, même sans Scene Reload.
        if (Instance == null) Instance = this;
        Budget = startingBudget;
        OnBudgetChanged?.Invoke(Budget);
    }

    public bool TrySpend(float amount)
    {
        if (amount < 0f || Budget < amount) return false;
        Budget -= amount;
        OnBudgetChanged?.Invoke(Budget);
        return true;
    }

    public void AddRevenue(float amount)
    {
        if (amount < 0f) return;
        Budget += amount;
        OnBudgetChanged?.Invoke(Budget);
    }
}
