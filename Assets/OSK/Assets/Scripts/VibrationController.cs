using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class VibrationController : MonoBehaviour
{
    [field: SerializeField, Tooltip("IP address of the phone that should vibrate."), ProjectAssign]
    public string PhoneIP;

    public void TriggerVibration()
    {
        if(PhoneIP != null && PhoneIP != "")
        {
            StartCoroutine(CallVibrationAPI());
        }
    }

    private IEnumerator CallVibrationAPI()
    {
        UnityWebRequest request = UnityWebRequest.Get($"http://{PhoneIP}:8080/vibrate");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.Log("Error: " + request.error);
        }
    }
}
