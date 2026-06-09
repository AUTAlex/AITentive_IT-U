using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID
using CandyCoded.HapticFeedback;
#endif

public class KeyboardScript : MonoBehaviour
{

    public TextMeshProUGUI TextField;
    public GameObject EngLayoutSml, SymbLayout;

    public char LastPressedButton {  get; set; }

    public bool ButtonWasPressed { get; set; }


    public void AlphabetFunction(string alphabet)
    {
        TextField.text=TextField.text + alphabet;
        LastPressedButton = alphabet[0];
        ButtonWasPressed = true;
        ExecuteHapticFeedback();
    }

    public void BackSpace()
    {
        if(TextField.text.Length>0) TextField.text= TextField.text.Remove(TextField.text.Length-1);
        LastPressedButton = '\x7F';
        ButtonWasPressed = true;
        ExecuteHapticFeedback();
    }

    public void CloseAllLayouts()
    {
        EngLayoutSml.SetActive(false);
        SymbLayout.SetActive(false);
    }

    public void ShowLayout(GameObject SetLayout)
    {
        CloseAllLayouts();
        SetLayout.SetActive(true);
    }

    public void Highlight(Image image)
    {
        StartCoroutine(HighlightButton(image));
    }


    private void ExecuteHapticFeedback()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        HapticFeedback.MediumFeedback();
#else
        GetComponent<VibrationController>().TriggerVibration();
#endif

    }

    private IEnumerator HighlightButton(Image image)
    {
        image.color = new Color(1, 1, 1, 2);
        yield return new WaitForSeconds(0.1f);
        image.color = new Color(1, 1, 1, 0f);
    }
}
