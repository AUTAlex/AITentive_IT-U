using UnityEngine;
using Utils;

public static class DrivingUtil
{
    public static float GetXLocationForLane(Lane lane, GameObject gameObject)
    {
        Transform _spawnContainer = gameObject.GetSpawnContainer().transform;

        return lane switch
        {
            Lane.Left => _spawnContainer.position.x - 4.25f,
            Lane.Center => _spawnContainer.position.x,
            Lane.Right => _spawnContainer.position.x + 4.25f,
            _ => _spawnContainer.position.x
        };
    }
}
