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

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Budget = startingBudget;
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
