using UnityEngine;

public class FollowCarZOnly : MonoBehaviour
{
    public Transform Target;   // assign the Agent transform
    public float FixedX = 0f;  // stays constant (middle of lane)
    public float FixedY = 2f;  // height above ground
    public float OffsetZ = 5f; // forward offset from the car

    private void LateUpdate()
    {
        if (Target == null)
            return;

        Vector3 pos = transform.position;

        transform.position = new Vector3(
            FixedX,                 // X stays constant
            FixedY,                 // constant Y offset above road
            Target.position.z + OffsetZ   // follow car on Z only
        );
    }
}
