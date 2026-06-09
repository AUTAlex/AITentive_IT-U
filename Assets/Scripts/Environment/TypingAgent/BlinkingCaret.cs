using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VisualCaret : MonoBehaviour
{
    [field: SerializeField] public TextMeshProUGUI TextMeshPro { get; private set; }
    
    [field: SerializeField] public Color CaretColor { get; private set; } = Color.black;


    private const float CARETWIDTH = 2.5f;

    private RectTransform _caret;
    
    private bool _isCaretVisible = true;
    
    private float _blinkTimer;
    
    private const float _blinkInterval = 0.5f;


    void Start()
    {
        CreateCaret();
        UpdateCaretSize();
    }

    void Update()
    {
        HandleCaretBlinking();
        UpdateCaretPosition();
        UpdateCaretSize();
    }

    private void CreateCaret()
    {
        GameObject caretObject = new GameObject("Caret");
        caretObject.transform.SetParent(TextMeshPro.transform.parent, false);

        Image caretImage = caretObject.AddComponent<Image>();
        caretImage.color = CaretColor;

        _caret = caretObject.GetComponent<RectTransform>();

        UpdateCaretPosition();
    }

    private void HandleCaretBlinking()
    {
        _blinkTimer += Time.deltaTime;
        if (_blinkTimer >= _blinkInterval)
        {
            _isCaretVisible = !_isCaretVisible;
            _caret.gameObject.SetActive(_isCaretVisible);
            _blinkTimer = 0f;
        }
    }

    private void UpdateCaretPosition()
    {;
        Vector2 caretPos;

        if (TextMeshPro.textInfo.characterCount == 0)
        {
            Vector3 scale = TextMeshPro.rectTransform.localScale;

            float leftEdge = -TextMeshPro.rectTransform.rect.width * 0.5f * scale.x;
            float topEdge = TextMeshPro.rectTransform.rect.height * 0.25f * scale.y;
            caretPos = new Vector2(leftEdge, topEdge);
        }
        else
        {
            caretPos = GetLastCharacterPosition();
        }

        _caret.anchoredPosition = caretPos;
    }

    private void UpdateCaretSize()
    {
        float textHeight = TextMeshPro.GetPreferredValues("|").y * 1.2f;
        _caret.sizeDelta = new Vector2(CARETWIDTH, textHeight);
    }


    private Vector2 GetLastCharacterPosition()
    {
        int characterCount = TextMeshPro.textInfo.characterCount;
        if (characterCount == 0)
            return Vector2.zero;

        var lastCharacter = TextMeshPro.textInfo.characterInfo[characterCount - 1];
        Vector3 scale = TextMeshPro.rectTransform.localScale;

        float xPos = lastCharacter.xAdvance;
        float yPos = lastCharacter.baseLine + (lastCharacter.ascender - lastCharacter.baseLine) / 2f;
        xPos *= scale.x;

        return new Vector2(xPos, yPos);
    }

}
