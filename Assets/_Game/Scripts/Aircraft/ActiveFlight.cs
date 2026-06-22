using UnityEngine;

public enum FlightStatus
{
    Scheduled,
    Approaching,
    Landing,
    AtGate,
    Departing,
    Departed
}

[System.Serializable]
public class ActiveFlight
{
    public string       FlightNumber;
    public string       Airline;
    public Color        AirlineColor;
    public float        ScheduledArrival; // heures décimales
    public AircraftData AircraftType;

    // Runtime
    public Aircraft     Aircraft;
    public Gate         AssignedGate;
    public FlightStatus Status = FlightStatus.Scheduled;

    public string ArrivalLabel
    {
        get
        {
            int h = Mathf.FloorToInt(ScheduledArrival);
            int m = Mathf.FloorToInt((ScheduledArrival - h) * 60f);
            return $"{h:00}:{m:00}";
        }
    }

    // "Gate_3" → "A3"
    public string GateLabel =>
        AssignedGate != null ? AssignedGate.gameObject.name.Replace("Gate_", "A") : "—";

    public string StatusLabel => Status switch
    {
        FlightStatus.Scheduled   => "Planifié",
        FlightStatus.Approaching => "En approche",
        FlightStatus.Landing     => "Atterrissage",
        FlightStatus.AtGate      => "À la gate",
        FlightStatus.Departing   => "En départ",
        FlightStatus.Departed    => "Décollé",
        _                        => "—"
    };

    public Color StatusColor => Status switch
    {
        FlightStatus.Scheduled   => new Color(0.55f, 0.55f, 0.55f),
        FlightStatus.Approaching => new Color(1.00f, 0.85f, 0.20f),
        FlightStatus.Landing     => new Color(1.00f, 0.60f, 0.10f),
        FlightStatus.AtGate      => new Color(0.20f, 0.85f, 0.20f),
        FlightStatus.Departing   => new Color(0.50f, 0.70f, 1.00f),
        FlightStatus.Departed    => new Color(0.38f, 0.38f, 0.38f),
        _                        => Color.white
    };

    public override string ToString() =>
        $"{FlightNumber} | {Airline} | {ArrivalLabel} | {GateLabel} | {StatusLabel}";
}
