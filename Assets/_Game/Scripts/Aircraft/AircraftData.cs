using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "NewAircraft", menuName = "AirportSim/Aircraft Data")]
public class AircraftData : ScriptableObject
{
    [FoldoutGroup("Identity")]
    public string aircraftName = "Boeing 737";

    [FoldoutGroup("Speeds")]
    public float approachSpeed = 80f;   // unités/sec en descente
    [FoldoutGroup("Speeds")]
    public float landingSpeed  = 30f;   // vitesse visée à l'atterro avant freinage complet
    [FoldoutGroup("Speeds")]
    public float taxiSpeed     = 10f;

    [FoldoutGroup("Specs")]
    public Vector2Int size               = new(10, 2);  // cellules
    [FoldoutGroup("Specs")]
    public float      passengerCapacity  = 150f;

    [FoldoutGroup("Visuals")]
    public GameObject prefab;
}
