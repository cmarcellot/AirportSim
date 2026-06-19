using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "NewBuilding", menuName = "AirportSim/Building Data")]
public class BuildingData : ScriptableObject
{
    [FoldoutGroup("Info")] public string buildingName = "Building";
    [FoldoutGroup("Info")] public BuildingCategory category = BuildingCategory.Terminal;
    [FoldoutGroup("Info")] public float cost = 10_000f;
    [FoldoutGroup("Info")] public Vector2Int sizeInCells = Vector2Int.one;
    [FoldoutGroup("Info")] public float height = 1f;
    [FoldoutGroup("Info")] public BuildingType gridType = BuildingType.None;

    [FoldoutGroup("Visual")] public Color iconColor = Color.white;
    [FoldoutGroup("Visual")] public GameObject prefab;
}
