using Algorithms;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;


public enum SupervisorRewardType
{
    Messaging,
    MessagingIntermediate,
    TypingWhileDriving
}


[Serializable, JsonObject]
public class TypingAgent : Task
{
    [field: SerializeField, ProjectAssign(Header = "General Settings")]
    public bool ShowFingerPosition { get; set; }

    [field: SerializeField, ProjectAssign]
    public string QuizName { get; set; } = "quiz1.csv";

    [field: SerializeField, Tooltip("Only Text shorter than MaxLengthText is taken from QuizName."), ProjectAssign]
    public int MaxLengthText { get; set; } = 1000;

    [field: SerializeField, Tooltip("Only Text longer than MinLengthText is taken from QuizName."), ProjectAssign]
    public int MinLengthText { get; set; } = 0;

    [field: SerializeField, Tooltip("Used to determine the ppi of the screen needed to accurately calculate the distances of the eye movement."), ProjectAssign]
    public DisplayConfiguration DisplayConfiguration { get; set; } = new(27, 0, 16, 9);

    [field: SerializeField, Tooltip("Defines the reward signal provided to the supervisor."), ProjectAssign]
    public SupervisorRewardType SupervisorRewardType { get; set; }

    [field: SerializeField]
    public int NumberOfSplits { get; set; } = 1;

    public TextMeshProUGUI QuestionText;

    public TextMeshProUGUI AnswerText;

    public VisualStateSpace FingerPositionStateSpace;

    public KeyboardScript KeyboardScript;

    public override int DecisionPeriod 
    { 
        get 
        { 
            return 0; 
        } 
    }

    public override bool IsIdle
    {
        get
        {
            return base.IsIdle;
        }
        set
        {
            base.IsIdle = value;
            if (!value) 
            {
                _typingMetrics.StartTyping();
            }
        }
    }

    public override bool IsPaused 
    { 
        get => base.IsPaused;

        set 
        { 
            base.IsPaused = value;

            if (value)
            {
                _typingMetrics.PauseTyping();
            }
            else
            {
                _typingMetrics.ResumeTyping();
            }
        } 
    }

    public override IStateInformation StateInformation
    {
        get
        {
            _typingStateInformation ??= new TypingStateInformation();

            string targetText = _currentQnA != null ? _currentQnA.Answer : "";

            _typingStateInformation.FingerVelocityX = _lastFingerVelocity.x;
            _typingStateInformation.FingerVelocityY = _lastFingerVelocity.y;
            _typingStateInformation.LastPressedButton = _lastPressedButtonStateInformation != _lastPressedButton ? _lastPressedButtonStateInformation : '\0';
            _typingStateInformation.WrittenText = AnswerText.text;
            _typingStateInformation.TargetText = targetText;
            _typingStateInformation.LevenshteinDistance = TextDistance.CalculateLevenshtein(targetText.ToLower(), AnswerText.text.ToLower());

            _lastPressedButtonStateInformation = _lastPressedButton;
            _lastFingerVelocity = Vector2.zero;
            return _typingStateInformation;
        }
        set
        {
            _typingStateInformation = value as TypingStateInformation;
        }
    }

    public override Dictionary<string, double> Performance => _typingMetrics != null ? _typingMetrics.Performance: null;

    public override Dictionary<string, string> PerformanceNotes => new()
    {
        { "WrittenAnswer", AnswerText.text.ToLower() },
        { "CorrectAnswer", _currentQnA != null ? _currentQnA.Answer.ToLower() : "" }
    };

    public override string StateDescription 
    { 
        get
        { 
            if (AnswerText == null || AnswerText.text == "")
            {
                return "New Conversation...";
            }
            else
            {
                return AnswerText.text;
            }
        }
    }


    internal protected QnA _currentQnA;

    internal protected float _timeOfEpisode;

    internal protected ITypingMetrics _typingMetrics;

    protected string _previousAnswer;

    protected int _startingButton;

    protected int _maxNumberOfActions = 0;


    private List<QnA> _qnAs;

    private static System.Random _rnd = new System.Random();

    private FadeImageAnimation _correctImageAnimation;

    private FadeImageAnimation _wrongImageAnimation;

    private TypingStateInformation _typingStateInformation;

    private int _previousLevenshteinDistance = 0;

    private char _lastPressedButton;

    private char _lastPressedButtonStateInformation;

    private Vector2 _lastFingerVelocity;

    private float _previousTextDistance;

    private int _previousNumberOfCorrectLetters;


    /// <summary>
    /// Will not be called in heuristic mode
    /// </summary>
    /// <param name="actionBuffers"></param>
    public override void OnActionReceivedInternal(ActionBuffers actionBuffers)
    {
        IsTaskProcessed = true;
        List<dynamic> actions = new();
        var discreteActionsOut = actionBuffers.DiscreteActions;

        int index = actionBuffers.DiscreteActions[0];
        actions.Add(index);

        ITask.InvokeOnAction(actions, this);

        if (IsActive || IsAutonomous)
        {
            FingerPositionStateSpace.ActivateSingleElement(index);
            FingerPositionStateSpace.VisualElements[index].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

            if (ShowFingerPosition) { ProjectImage(FingerPositionStateSpace, "FingerClick"); }

            float reward = GetReward();
            float supervisorReward = GetIntermediateSupervisorAgentRewardForConfiguration();
            if (supervisorReward != 0) { TaskRewardForSupervisorAgent.Enqueue((supervisorReward, Priority)); }
            SetReward(reward);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        AddTrueObservationsToSensor(sensor);
    }

    public override void AddTrueObservationsToSensor(VectorSensor sensor) 
    {
        sensor.AddOneHotObservation((int)_lastPressedButton, 128);
        sensor.AddObservation(AnswerText.text.Length / (float)_currentQnA.Answer.Length);
        sensor.AddObservation((float)_currentQnA.Answer.Length);
        sensor.AddObservation(GetFinalSupervisorAgentRewardForConfiguration());
    }

    public override void OnMove(InputValue value) { }

    public override void UpdateDifficultyLevel() { }

    //reward b
    public void Enter()
    {
        float reward = GetFinalReward();
        float focusReward = reward >= 0 ? reward : 0;

        SetReward(reward);
        TaskRewardForFocusAgent.Enqueue((focusReward, Priority));
        TaskRewardForSupervisorAgent.Enqueue((GetFinalSupervisorAgentRewardForConfiguration(), Priority));
        //Debug.Log($"SupervisorReward: {supervisorReward}; Priority: {Priority}");

        Debug.Log($" Final reward: {reward}, Total reward of episode: {GetCumulativeReward()}.");
        _typingMetrics.EndTyping();

        HandleTaskTermination();
    }

    public virtual float GetFinalReward()
    {
        float reward;

        float levenshteinDistance = TextDistance.CalculateLevenshtein(_currentQnA.Answer.ToLower(), AnswerText.text.ToLower());
        float frequencyDistance = TextDistance.CalculateLetterFrequencyDistance(AnswerText.text.ToLower(), _currentQnA.Answer.ToLower());

        reward = _currentQnA.Answer.Length - levenshteinDistance/2 - frequencyDistance;
        //distance to answer length reward dependent on time
        reward *= reward < 0 ? MaximizeAtC(AnswerText.text.Length, 0, 3f, 0.1f) : MaximizeAtC(AnswerText.text.Length, _currentQnA.Answer.Length, _currentQnA.Answer.Length/_timeOfEpisode, 0.02f) * GetNumberOfCorrectLetters(AnswerText.text);
        reward = _currentQnA.Answer.ToLower() == AnswerText.text.ToLower() ? reward * 2 : reward;

        return reward;
    }

    public override void OnEpisodeBegin()
    {
        //base.OnEpisodeBegin();
        Debug.Log("Begin Episode");

        UpdateEnvironmentParameters();

        List<QnA> qnAs = _qnAs.Where(x => x.Answer.Length <= MaxLengthText && x.Answer.Length >= MinLengthText).ToList();

        int r = _rnd.Next(qnAs.Count);

        _currentQnA = qnAs[r];

        _previousAnswer = "";
        AnswerText.text = "";
        QuestionText.text = _currentQnA.Question;
        _timeOfEpisode = 0;

        _previousTextDistance = _currentQnA.Answer.Length;
        _typingMetrics = new TypingMetrics();
        _previousLevenshteinDistance = _currentQnA.Answer.Length;
        _lastPressedButton = default;
        IsTaskProcessed = false;
    }


    protected int GetNumberOfCorrectLetters(string writtenAnswer)
    {
        int correctLetters = 0;

        for (int i = 0; i < writtenAnswer.Length && i < _currentQnA.Answer.Length; i++)
        {
            if (writtenAnswer.ToLower()[i] == _currentQnA.Answer.ToLower()[i])
            {
                correctLetters += 1;
            }
            else
            {
                break;
            }
        }

        return correctLetters;
    }

    protected override void OnEnable()
    {
        Canvas.ForceUpdateCanvases();
        _qnAs = Util.ReadDatafromCSV<QnA>(Path.Combine(Application.streamingAssetsPath, QuizName));
        _correctImageAnimation = transform.GetChildInHierarchyByName("Correct").GetComponent<FadeImageAnimation>();
        _wrongImageAnimation = transform.GetChildInHierarchyByName("Wrong").GetComponent<FadeImageAnimation>();

        _startingButton = GetComponent<BehaviorParameters>().BehaviorType == BehaviorType.HeuristicOnly ? 30 : 14;
        FingerPositionStateSpace.ActivateElement(_startingButton);

        base.OnEnable();
    }


    protected void ProjectImage(VisualStateSpace visualStateSpace, string imageName= "Finger")
    {
        Transform fingerCanvas = transform.GetChildByName("FingerCanvas");

        foreach (Transform child in fingerCanvas)
        {
            child.gameObject.SetActive(false);
        }

        if(visualStateSpace.GetFirstActiveElement() != null)
        {
            Vector3 position = visualStateSpace.GetFirstActiveElement().transform.position;
            GameObject fingerImage = fingerCanvas.GetChildByName(imageName).gameObject;
            fingerImage.SetActive(true);
            fingerImage.transform.position = position;
        }
    }

    protected char GetTarget(string targetString, string currentString)
    {
        char result;

        if (currentString.Length <= targetString.Length && (currentString.Length == 0 || currentString.ToLower() == targetString[..(currentString.Length)].ToLower()))
        {
            if (currentString.Length == targetString.Length)
            {
                result = '\n';
            }
            else
            {
                result = Char.ToLower(targetString[currentString.Length]);
            }
        }
        else
        {
            result = '\x7F';
        }

        if (GetButton(result) == null)
        {
            Debug.LogWarning($"Belief target is null: Target String: {targetString}, Current String: {currentString}");
            EndEpisode();
        }

        return result;
    }

    protected GameObject GetTargetButton(string targetString, string currentString)
    {
        char target = GetTarget(targetString, currentString);

        foreach (GameObject gameObject in FingerPositionStateSpace.VisualElements)
        {
            if (GetButtonStringValue(gameObject) == target.ToString())
            {
                return gameObject;
            }
        }

        return null;
    }

    protected bool IsButtonVisible(char button)
    {
        foreach (GameObject gameObject in FingerPositionStateSpace.VisualElements)
        {
            if (GetButtonCharValue(gameObject) == Char.ToLower(button))
            {
                if (gameObject.activeInHierarchy)
                {
                    return true;
                }
            }
        }

        return false;
    }

    protected virtual float GetReward()
    {
        float reward = 0;
        int numberOfCorrectLetters = GetNumberOfCorrectLetters(AnswerText.text);
        char target = GetTarget(_currentQnA.Answer.ToLower(), _previousAnswer.ToLower());
        char lastClickedButton = KeyboardScript.LastPressedButton;

        if (lastClickedButton == target && lastClickedButton != '\x7F')
        {
            reward += 0.5f * (numberOfCorrectLetters + 1);
        }
        else if (lastClickedButton == target && lastClickedButton == '\x7F')
        {
            reward += 0.5f;
        }
        else if (lastClickedButton == '\x7F' && numberOfCorrectLetters < _previousNumberOfCorrectLetters)
        {
            reward += -0.5f * (_previousNumberOfCorrectLetters + 1);
        }
        else
        {
            reward += -0.5f;
        }

        _previousAnswer = AnswerText.text;
        _previousNumberOfCorrectLetters = numberOfCorrectLetters;

        Debug.Log(string.Format("Reward: {0}, Clicked Button: {1}, Target: {2}", reward, lastClickedButton.ToEscapedString(), target.ToEscapedString()));

        return reward;
    }

    protected GameObject GetButton(char button)
    {
        foreach (GameObject gameObject in FingerPositionStateSpace.VisualElements)
        {
            if (GetButtonCharValue(gameObject) == Char.ToLower(button))
            {
                return gameObject;
            }
        }

        return null;
    }

    protected string GetButtonStringValue(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return "\x00";
        }

        string st = gameObject.name;
        string start = "Button (";

        int pFrom = st.IndexOf(start) + start.Length;
        int pTo = st.LastIndexOf(")");

        return st.Substring(pFrom, pTo - pFrom);
    }

    protected char GetButtonCharValue(GameObject gameObject)
    {
        string st = GetButtonStringValue(gameObject);

        return char.Parse(Regex.Unescape(st));
    }

    protected char GetTrueFingerPosition()
    {
        if (FingerPositionStateSpace.GetFirstActiveElement() != null)
        {
            GameObject gameObject = FingerPositionStateSpace.GetFirstActiveElement();

            return GetButtonCharValue(gameObject);
        }

        return '\0';
    }

    protected void UpdateTypingMetricKeystroke()
    {
        int levenshteinDistance = TextDistance.CalculateLevenshtein(_currentQnA.Answer.ToLower(), AnswerText.text.ToLower());

        if (_previousLevenshteinDistance > levenshteinDistance)
        {
            _typingMetrics.RecordCorrectKeystroke();
        }
        else
        {
            _typingMetrics.RecordIncorrectKeystroke();
        }

        _previousLevenshteinDistance = levenshteinDistance;
    }

    protected void UpdateTypingMetric(ITypingMetrics typingMetric)
    {
        if (_currentQnA == null)
        {
            return;
        }

        typingMetric.TargetSentence = _currentQnA.Answer;
        typingMetric.CurrentSentence = AnswerText.text;
        typingMetric.Priority = Priority;
    }

    protected override void OnUpdate()
    {
        if (!IsIdle)
        {
            UpdateTypingMetric(_typingMetrics);
        }

        if (KeyboardScript.ButtonWasPressed)
        {
            float supervisorReward = GetIntermediateSupervisorAgentRewardForConfiguration();
            if (supervisorReward != 0) { TaskRewardForSupervisorAgent.Enqueue((supervisorReward, Priority)); }
            KeyboardScript.ButtonWasPressed = false;
            UpdateStateInformation();
            UpdateTypingMetricKeystroke();
        }
    }

    protected float GetFinalSupervisorAgentRewardForConfiguration()
    {
        switch (SupervisorRewardType)
        {
            case SupervisorRewardType.Messaging:
                return 2 * (AnswerText.text.Length - TextDistance.CalculateLevenshtein(_currentQnA.Answer.ToLower(), AnswerText.text.ToLower())) - _typingMetrics.GetTypingDurationExcludingPausedTime(); //float supervisorReward = _typingMetrics.GetGrossWPM() * _typingMetrics.GetFinalAccuracy();
            case SupervisorRewardType.TypingWhileDriving:
                return 0;
            case SupervisorRewardType.MessagingIntermediate:
                return 0;
            default:
                return GetFinalReward();
        }
    }

    protected float GetIntermediateSupervisorAgentRewardForConfiguration()
    {
        switch (SupervisorRewardType)
        {
            case SupervisorRewardType.Messaging:
                return 0;
            case SupervisorRewardType.TypingWhileDriving:
                return GetIntermediateSupervisorAgentRewardTypingWhileDriving();
            case SupervisorRewardType.MessagingIntermediate:
                return 2 * (AnswerText.text.Length - TextDistance.CalculateLevenshtein(_currentQnA.Answer.ToLower(), AnswerText.text.ToLower())) - _typingMetrics.GetTypingDurationExcludingPausedTime();
            default:
                return 0;
        }
    }

    protected override void OnFixedUpdate()
    {
        if (GetComponent<BehaviorParameters>().BehaviorType != BehaviorType.HeuristicOnly)
        {
            RequestSimpleDecision();
        }
   
        _timeOfEpisode += Time.fixedDeltaTime;
    }

    protected void HandleTaskTermination()
    {
        EndEpisode();
        Debug.Log("End Episode: Enter button was pressed.");
    }

    protected virtual void UpdateEnvironmentParameters()
    {
        var envParams = Academy.Instance.EnvironmentParameters;

        MaxLengthText = (int)envParams.GetWithDefault("MaxLengthText", MaxLengthText);
        MinLengthText = (int)envParams.GetWithDefault("MinLengthText", MinLengthText);
        _maxNumberOfActions = (int)envParams.GetWithDefault("MaxNumberOfActions", 0);
    }


    private void RequestSimpleDecision()
    {
        if (IsAutonomous || IsActive)
        {
            RequestDecision();
        }
    }

    private void UpdateStateInformation()
    {
        ITask.InvokeOnAction(new(), this);

        char pressedButton = KeyboardScript.LastPressedButton;
        _lastFingerVelocity = GetButton(_lastPressedButton) == null ? new Vector2(-999, -999) : FingerPositionStateSpace.GetScreenCoordinatesForGameObject(GetButton(pressedButton)) - FingerPositionStateSpace.GetScreenCoordinatesForGameObject(GetButton(_lastPressedButton));
        _lastPressedButton = pressedButton;
    }

    /// <summary>
    /// Function to maximize a function at a certain point c
    /// </summary>
    /// <param name="x"></param>
    /// <param name="c">Point of maximum</param>
    /// <param name="C">Maximum value of c</param>
    /// <param name="k">Slope</param>
    /// <returns>The computed function value, which is a maximum at 'c' and converges to 0 as x moves away from 'c'.</returns>
    private float MaximizeAtC(float x, float c, float C, float k)
    {
        return C / (1 + k * (float)Math.Pow(x - c, 2));
    }

    private float GetIntermediateSupervisorAgentRewardTypingWhileDriving()
    {
        float distance = TextDistance.CalculateLevenshtein(_currentQnA.Answer.ToLower(), AnswerText.text.ToLower());
        float result = distance != _previousTextDistance ? _previousTextDistance - distance : 0;

        _previousTextDistance = distance;

        return result;
    }
}


public class QnA
{
    public QnA() { }

    public string Question { get; set; }

    public string Answer { get; set; }
}
