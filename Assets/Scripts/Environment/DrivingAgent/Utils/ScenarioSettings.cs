using UnityEngine;

[CreateAssetMenu(fileName = "ScenarioSettings", menuName = "HighwayDrivingSimulator/Scenario", order = 1)]
public class ScenarioSettings : ScriptableObject 
{
    [field: SerializeField, Header("General")]
    public bool IsDeterministicScenario { get; set; } = false;


    [field: SerializeField, Header("Goal")]
    public float ScenarioDistance { get; set; } = 5000;

    [field: SerializeField, Tooltip("Min Speed on Sign"), Header("Speed Sign Settings")]
    public int MinSpeed { get; set; } = 100;
    
    [field: SerializeField, Tooltip("Max Speed on Sign")]
    public int MaxSpeed { get; set; } = 150;
    
    [field: SerializeField, Tooltip("Step size on sign")]
    public int Step { get; set; } = 10;
    

    [field: SerializeField, Header("Overhead Sign Settings")]
    public int SignEveryXRoad { get; set; } = 1;
    

    [field: SerializeField, Header("Self driving Cars")]
    public bool SpawnCars { get; set; } = true;

    [field: SerializeField]
    public float TimeBetweenSpawns { get; set; } = 5f;

    [field: SerializeField]
    public int MaxCars { get; set; } = 10;
    
    [field: SerializeField, Tooltip("Speed in correlation to the current target speed of the player")]
    public float FasterCarsSpeed { get; set; } = 10f;
    
    [field: SerializeField, Tooltip("Speed in correlation to the current target speed of the player")]
    public float SlowerCarsSpeed { get; set; } = -10f;


    [field: SerializeField, Header("Ml Agents")]
    public bool AgentActive { get; set; }
    

    [field: SerializeField, Header("Performance Log Settings")]
    public bool WritePerformanceLog { get; set; } = true;
    
}