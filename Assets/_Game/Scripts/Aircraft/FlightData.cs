using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "NewFlight", menuName = "AirportSim/Flight Data")]
public class FlightData : ScriptableObject
{
    [FoldoutGroup("Identification")]
    public string flightNumber = "AF1234";

    [FoldoutGroup("Identification")]
    public string airline = "Air France";

    [FoldoutGroup("Identification")]
    public Color airlineColor = new(0.01f, 0.34f, 0.72f);

    [FoldoutGroup("Aircraft")]
    public AircraftData aircraftType;

    [FoldoutGroup("Schedule"), Tooltip("Heure d'arrivée en heures décimales (ex : 6.5 = 06h30)")]
    public float scheduledArrival = 6.5f;

    [FoldoutGroup("Schedule"), Tooltip("Heure de départ en heures décimales (ex : 7.5 = 07h30)")]
    public float scheduledDeparture = 7.5f;
}
