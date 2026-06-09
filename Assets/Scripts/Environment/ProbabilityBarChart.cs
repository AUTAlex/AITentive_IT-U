using System.Linq;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.UI;

public class ProbabilityBarChart : MonoBehaviour
{
    public DrivingAgentHumanCognitionBase<float> Agent;
    public GameObject BarPrefab;
    public RectTransform BarContainer;
    public float MaxBarHeight = 1.0f;

    private Image[] _bars;
    private Color _barColor;
    private double[] _probs;

    void Start()
    {
        Agent = GetComponent<DrivingAgentHumanCognitionBase<float>>();

        if (Agent.BelievableObject is IBinState)
        {
            _probs = ((IBinState)(Agent.BelievableObject)).ProbabilityDistribution;
        }
        else
        {
            enabled = false;
            return;
        }

        CreateBars();
        _barColor = _bars[0].color;
    }

    void Update()
    {
        UpdateBars();
    }

    private void CreateBars()
    {
        int bins = Agent.BelievableObject.BelievableObjectConfig.NumberOfBins;
        _bars = new Image[bins];

        float roadWidth = RLDrivingExerciseCR.RANGEMAX - RLDrivingExerciseCR.RANGEMIN;
        float barWidth = roadWidth / bins;

        // Convert width from world space to canvas space
        float canvasWidth = BarContainer.rect.width;
        float scale = canvasWidth / roadWidth;

        float uiBarWidth = barWidth * scale;

        for (int i = 0; i < bins; i++)
        {
            GameObject barGO = Instantiate(BarPrefab, BarContainer);
            RectTransform rt = barGO.GetComponent<RectTransform>();

            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0.5f, 0);

            rt.sizeDelta = new Vector2(uiBarWidth, 1);

            float xPos = uiBarWidth * i;
            rt.anchoredPosition = new Vector2(xPos, 0);

            _bars[i] = barGO.GetComponent<Image>();
        }
    }

    private void UpdateBars()
    {
        if (_probs == null) return;

        float height = BarContainer.rect.height;
        int maxIndex = 0;
        float maxValue = -1f;

        // First pass: update heights & find max index
        for (int i = 0; i < _bars.Length; i++)
        {
            float p = (float)_probs[i];
            float barHeight = p * height;

            RectTransform rt = _bars[i].rectTransform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, barHeight);

            if (p > maxValue)
            {
                maxValue = p;
                maxIndex = i;
            }
        }

        // Second pass: apply colors
        for (int i = 0; i < _bars.Length; i++)
        {
            if (i == maxIndex)
            {
                _bars[i].color = Color.red;
            }
            else
            {
                _bars[i].color = _barColor;
            }
        }
    }

}
