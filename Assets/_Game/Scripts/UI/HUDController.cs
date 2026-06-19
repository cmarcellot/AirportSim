using UnityEngine;
using TMPro;

public class HUDController : MonoBehaviour
{
    [SerializeField] private TMP_Text budgetText;
    [SerializeField] private TMP_Text timeText;

    private float _lastBudget = -1f;
    private int   _lastSpeed  = -1;
    private float _lastHour   = -1f;

    private void Update()
    {
        RefreshBudget();
        RefreshTime();
    }

    private void RefreshBudget()
    {
        if (budgetText == null || EconomySystem.Instance == null) return;

        float budget = EconomySystem.Instance.Budget;
        if (Mathf.Approximately(budget, _lastBudget)) return;

        _lastBudget = budget;
        budgetText.text = FormatMoney(budget);
    }

    private void RefreshTime()
    {
        if (timeText == null || TimeManager.Instance == null) return;

        float hour  = TimeManager.Instance.CurrentHour;
        int   speed = TimeManager.Instance.SpeedMultiplier;
        if (Mathf.Approximately(hour, _lastHour) && speed == _lastSpeed) return;

        _lastHour  = hour;
        _lastSpeed = speed;
        timeText.text = $"{TimeManager.Instance.CurrentTimeString}  x{speed}";
    }

    private static string FormatMoney(float amount)
    {
        int value = Mathf.RoundToInt(amount);
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0:0,0}", value
        ).Replace(",", " ") + " $";
    }
}
