using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "NewZoneData", menuName = "AirportSim/Zone Data")]
public class ZoneData : ScriptableObject
{
    [FoldoutGroup("Zone Settings")] public ZoneType type;
    [FoldoutGroup("Zone Settings")] public string   displayName;
    [FoldoutGroup("Zone Settings")] public Color    zoneColor    = Color.white;
    [FoldoutGroup("Zone Settings")] public float    costPerCell;
    [FoldoutGroup("Zone Settings")] public Material groundMaterial;
    [FoldoutGroup("Zone Settings")] public bool     isAirside;
    [FoldoutGroup("Zone Settings")] public bool     isRestricted;
}
