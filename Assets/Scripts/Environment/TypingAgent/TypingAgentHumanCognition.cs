using Algorithms;
using MathNet.Numerics;
using MathNet.Numerics.Distributions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Jobs;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;
using static ObjectIn2DGridProbabilitiesUpdateJob;


public class TypingAgentHumanCognition : TypingAgent, ICRTask
{
    [field: SerializeField, Tooltip("Visualizes the current belief finger position."), ProjectAssign(Header = "CR Settings")]
    public bool ShowBeliefState { get; set; }

    [field: SerializeField, Tooltip("Observation is active for this agent independent of the focus/supervisor agent."), ProjectAssign]
    public bool FullVision { get; set; } = false;

    [field: SerializeField, Tooltip("Use viewport coordinates rather than screen coordinates when calculating distances. Screen coordinates are " +
    "considered legacy and do not support hardware-independent training, furthermore sigma determination is inaccurate. If you use screen " +
    "coordinates, training must be performed on the specific device intended for deployment."), ProjectAssign]
    public bool UseViewportSpace { get; set; } = false;

    [field: SerializeField, Tooltip("Defines how much samples should be taken to calculate the probability distributions."), ProjectAssign]
    public int NumberOfSamples { get; set; } = 100;

    [field: SerializeField, Tooltip("Describes O(s,a,o) of the formula b`(s_) = O(s_,a,o) SUM(s_e_S){ T(s,a,s_)*b(s)}."), ProjectAssign]
    public double ObservationProbability { get; set; } = 0.9;

    [field: SerializeField, Tooltip("Specify the number of bins in which the keyboard should be divided."), ProjectAssign]
    public int NumberOfBins { get; set; } = 1000;

    [field: SerializeField, Tooltip("Time needed to start typing after the agent gets active."), ProjectAssign]
    public float ConstantReactionTime { get; set; } = 0f;

    [field: SerializeField, ProjectAssign]
    public float X_0 { get; set; } = 0.092f;

    [field: SerializeField, ProjectAssign]
    public float Y_0 { get; set; } = 0.0018f;

    [field: SerializeField, ProjectAssign]
    public float Alpha { get; set; } = 0.6f;

    [field: SerializeField, ProjectAssign]
    public float KAlpha { get; set; } = 0.12f;

    [field: SerializeField]
    public RectTransform KeyboardRectTransform { get; set; }

    [field: SerializeField, Tooltip("Testing value for sigma in heuristic mode [-1,1]."), ProjectAssign(Header = "CR Settings")]
    public float SigmaHeuristic { get; set; } = 0;

    public bool IsVisible
    {
        get
        {
            return FullVision;
        }

        set => throw new NotImplementedException();
    }

    public override bool IsActive
    {
        get
        {
            return _isActive;
        }
        set
        {
            _isActive = value;
            if (value)
            {
                _reactionTimeTimer = 0;
            }
        }

    }


    private const float OBSERVATIONTYPINGREWARDRATIO = 0.5f;


    protected char _beliefTarget;

    protected string _beliefWrittenAnswer;

    protected float _entropy01;

    protected List<double> _pCorrect;

    protected Vector2 _fingerPosition;

    protected float _movementTime;

    protected ICoordinateConverter _coordinateConverter;


    private bool _isActive;

    private System.Random _rand;

    private float _movementTimer = 0.0f;

    private float _reactionTimeTimer = 0.0f;

    private double[] _fingerLocationProbabilities;

    private double[] _fingerLocationProbabilitiesBinSpace;

    private int[] _binOverlapCount;

    private VisualStateSpace _beliefFingerPositionStateSpace;

    private string _previousBeliefWrittenAnswer;

    private int _mouseClicked;

    private Vector2 _previousMousePosition;

    private Vector2 _maxDistanceBetweenButtons;

    private char _beliefClickedButton;

    private Vector2 _mouseVelocity;

    private Vector2 _mouseStartingPosition;

    private bool EpisodeStarted = false;

    private int _stepCount;

    private Vector2 _lastPerformedAction;

    private int _fingerExitCount;

    private float _lastTypingReward = 0;

    const float _gamma = 0.98f;

    private float _weight = 1f;

    /// <summary>
    /// The sensor input for the focus agent must reflect the current uncertainties of the task. In combination with the reward signal, the focus
    /// agent can learn to focus on the most relevant elements of the task s.t. the reward of the subtasks is maximized.
    /// </summary>
    /// <param name="sensor"></param>
    public void AddBeliefObservationsToSensor(VectorSensor sensor)
    {
        sensor.AddObservation(1 - _entropy01);
        sensor.AddObservation((float)GetTextCorrectnessProability());
        sensor.AddOneHotObservation((int)GetTarget(_currentQnA.Answer, _beliefWrittenAnswer), 128);
    }

    public double[] GetLocationProbabilities()
    {
        return _fingerLocationProbabilitiesBinSpace;
    }

    public override void OnActionReceivedInternal(ActionBuffers actionBuffers)
    {
        List<dynamic> actions = new();
        var continuousActionsOut = actionBuffers.ContinuousActions;

        if (IsInvalidAction(actionBuffers))
        {
            return;
        }

        Vector2 fingerVelocity = GetFingerVelocity(actionBuffers);
        actions.Add(fingerVelocity);
        float sigma = GetFingerSigma(actionBuffers);
        actions.Add(sigma);
        int actionType = actionBuffers.DiscreteActions[0];
        actions.Add(actionType);

        ITask.InvokeOnAction(actions, this);
        PerformAction(actionBuffers);

        int distance = TextDistance.CalculateLevenshtein(_currentQnA.Answer.ToLower(), AnswerText.text.ToLower());

        float supervisorReward = GetIntermediateSupervisorAgentRewardForConfiguration();
        if (supervisorReward != 0) { TaskRewardForSupervisorAgent.Enqueue((supervisorReward, Priority)); }
        CheckEndingConditions(distance);
    }

    /// <summary>
    /// Heuristic is for testing purpose of the WHo model and the probability updates. Therefore, use the Typing agent for experiments with human 
    /// participants.
    /// </summary>
    /// <param name="actionsOut"></param>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActionsOut = actionsOut.ContinuousActions;
        ActionSegment<int> discreteActionsOut = actionsOut.DiscreteActions;

        Vector2 mouseVelocity = _mouseVelocity;

        continuousActionsOut[0] = mouseVelocity.x;
        continuousActionsOut[1] = mouseVelocity.y;

        //random testing value for sigma
        continuousActionsOut[2] = SigmaHeuristic;

        discreteActionsOut[0] = _mouseClicked;
        _mouseClicked = 0;
    }

    public override void Initialize()
    {
        base.Initialize();

        _coordinateConverter = UseViewportSpace ? new ViewportSpaceCoordinateConverter() : new ScreenSpaceCoordinateConverter();

        _rand = new System.Random();
        (int, int) dimensions = PositionConverter.GetBinDimensions(KeyboardRectTransform.rect.width, KeyboardRectTransform.rect.height, NumberOfBins);
        NumberOfBins = dimensions.Item1 * dimensions.Item2;
    }

    public override void OnEpisodeBegin()
    {
        base.OnEpisodeBegin();

        VisionAgent visionAgent = gameObject.GetFirstParentWithEnabledComponent<VisionAgent>();
        if (visionAgent != null) visionAgent.NullObservationPenalty = 0.5f;
            
        Canvas.ForceUpdateCanvases();

        _lastTypingReward = 0;
        _weight = 0;
        _stepCount = 0;
        _beliefFingerPositionStateSpace = FingerPositionStateSpace.Copy();
        _previousBeliefWrittenAnswer = _beliefWrittenAnswer = "";
        _maxDistanceBetweenButtons = _coordinateConverter.CalculateMaxDistanceBetweenButtons(FingerPositionStateSpace);
        InitializeFingerLocationProbabilities();

        _lastPerformedAction = Vector2.zero;
        _pCorrect = new();

        if (this.GetBehaviorType() == BehaviorType.HeuristicOnly)
        {
            StartCoroutine(InitMousePositionNextFrame());
        }

        StartCoroutine(MarkEpisodeStartedNextFrame());

        _fingerExitCount = 0;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        _beliefTarget = GetTarget(_currentQnA.Answer, _beliefWrittenAnswer);
        string buttomName = $"Button ({_beliefTarget.ToEscapedString()})";

        GameObject targetButton = FingerPositionStateSpace.GetGameObjectForName(buttomName);
        Vector2 distanceBetweenTargetAndFinger = GetVelocityInScreenSpace(GetObservableDistanceToFingerPositionInKeyboardCanvasSpace(targetButton));
        Vector2 normDistanceBetweenTargetAndFinger = new(NormalizeValue(distanceBetweenTargetAndFinger.x, -_maxDistanceBetweenButtons.x, _maxDistanceBetweenButtons.x),
                                                         NormalizeValue(distanceBetweenTargetAndFinger.y, -_maxDistanceBetweenButtons.y, _maxDistanceBetweenButtons.y));

        sensor.AddObservation(normDistanceBetweenTargetAndFinger); //same format as the finger velocity performed by the agent
        sensor.AddObservation(_lastPerformedAction);
        sensor.AddOneHotObservation((int)_beliefTarget, 128);
        sensor.AddOneHotObservation(GetButtonCharValue(GetBeliefFingerButton()), 128);
        sensor.AddObservation(GetBeliefFingerPositionKeyboardSpace());
        sensor.AddObservation(1 - _entropy01);
        sensor.AddObservation((float)GetTextCorrectnessProability());

        Debug.Log(string.Format("OBSERVATION: Finger Belief Position: {0} (Button: {1}), Target: {2}, Distance Between Target and Finger: {3} ({4}), Belief Written Answer: {5}", GetBeliefFingerPositionKeyboardSpace(), GetButtonCharValue(GetBeliefFingerButton()).ToEscapedString(), _beliefTarget, distanceBetweenTargetAndFinger, normDistanceBetweenTargetAndFinger, _beliefWrittenAnswer));
    }


    protected virtual Vector2 GetFingerVelocity(ActionBuffers actionBuffers)
    {
        float pixelScale = FingerPositionStateSpace.Canvas.scaleFactor;

        if (IsMouseMode())
        {
            return new Vector2(actionBuffers.ContinuousActions[0], actionBuffers.ContinuousActions[1]);
        }
        else
        {
            _lastPerformedAction = new Vector2(Mathf.Clamp(actionBuffers.ContinuousActions[0], -1, 1f), Mathf.Clamp(actionBuffers.ContinuousActions[1], -1, 1f));

            float xVelocity = ScaleAction(_lastPerformedAction.x, -_maxDistanceBetweenButtons.x, _maxDistanceBetweenButtons.x);
            float yVelocity = ScaleAction(_lastPerformedAction.y, -_maxDistanceBetweenButtons.y, _maxDistanceBetweenButtons.y);

            return new Vector2(xVelocity, yVelocity) / pixelScale;
        }
    }

    protected virtual float GetFingerSigma(ActionBuffers actionBuffers)
    {
        if (IsMouseMode())
        {
            return actionBuffers.ContinuousActions[2];
        }
        else
        {
            return UseViewportSpace ? ScaleSigma(actionBuffers.ContinuousActions[2]) : Mathf.Clamp01(actionBuffers.ContinuousActions[2]);
        }
    }

    protected float ScaleSigma(float rawAction)
    {
        float minSigma = 0.001f;
        float maxSigma = 0.3f;

        // Map [-1, 1] to [minSigma, maxSigma]:
        float t = (rawAction + 1.0f) / 2.0f; // t in [0, 1]
        float sigma = minSigma + (maxSigma - minSigma) * t;

        return sigma;
    }

    protected virtual bool IsMouseMode()
    {
        BehaviorType behaviorType = gameObject.GetComponent<BehaviorParameters>().BehaviorType;
        return gameObject.GetComponent<BehaviorParameters>().BehaviorType == BehaviorType.HeuristicOnly;
    }

    protected Vector2 GetObservableDistanceToFingerPositionInKeyboardCanvasSpace(GameObject target)
    {
        Vector2 distanceBetweenTargetAndFinger = new Vector2(-999999, -999999);

        if (FocusStateSpace.IsActiveElement(target) || IsVisible)
        {
            distanceBetweenTargetAndFinger = _coordinateConverter.ImageToKeyboardCanvasSpace(_coordinateConverter.GetCoordinatesForGameObject(target, FingerPositionStateSpace), KeyboardRectTransform, FingerPositionStateSpace) - GetBeliefFingerPositionKeyboardSpace();
            if ((int)GetButtonCharValue(GetBeliefFingerButton()) == (int)_beliefTarget)
            {
                distanceBetweenTargetAndFinger = Vector2.zero;
            }
        }

        return distanceBetweenTargetAndFinger;
    }

    protected GameObject GetBeliefFingerButton()
    {
        GameObject buttonAtFingerLocation = _coordinateConverter.GetGameObjectForCoordinates(_coordinateConverter.KeyboardCanvasToImageSpace(GetBeliefFingerPositionKeyboardSpace(), KeyboardRectTransform, FingerPositionStateSpace), FingerPositionStateSpace);

        return buttonAtFingerLocation;
    }

    protected int GetBeliefFingerBin()
    {
        double maxValue = _fingerLocationProbabilitiesBinSpace.Max();

        return _fingerLocationProbabilitiesBinSpace.ToList().IndexOf(maxValue);
    }

    protected Vector2 GetBeliefFingerPositionKeyboardSpace()
    {
        return PositionConverter.BinToRectangleCoordinates(GetBeliefFingerBin(), KeyboardRectTransform.rect.width, KeyboardRectTransform.rect.height, NumberOfBins);
    }

    protected bool IsTextEncoded()
    {
        GameObject textGameObject = FocusStateSpace.VisualElements.FirstOrDefault(a => a.name == "TextA");
        return FocusStateSpace.IsActiveElement(textGameObject) || IsVisible;
    }

    protected Vector2[] GetVelocitiesInKeyboardCanvasSpace(Vector2[] screenNorm)
    {
        Vector2[] keyboardNorm = new Vector2[screenNorm.Length];

        for (int i = 0; i < screenNorm.Length; i++)
        {
            keyboardNorm[i] = GetVelocityInKeyboardCanvasSpace(screenNorm[i]);
            //Assert.AreEqual(screenNorm[i], GetVelocityInScreenSpace(keyboardNorm[i]));
        }

        return keyboardNorm;
    }

    protected Vector2 GetVelocityInKeyboardCanvasSpace(Vector3 screenVelocity)
    {
        return _coordinateConverter.ImageToKeyboardCanvasSpace(screenVelocity, KeyboardRectTransform, FingerPositionStateSpace) - _coordinateConverter.ImageToKeyboardCanvasSpace(Vector3.zero, KeyboardRectTransform, FingerPositionStateSpace);
    }

    protected Vector2 GetVelocityInScreenSpace(Vector3 keyboardCanvasVelocity)
    {
        return _coordinateConverter.KeyboardCanvasToImageSpace(keyboardCanvasVelocity, KeyboardRectTransform, FingerPositionStateSpace) - _coordinateConverter.KeyboardCanvasToImageSpace(Vector3.zero, KeyboardRectTransform, FingerPositionStateSpace);
    }

    protected override void OnUpdate()
    {
        base.OnUpdate();

        if (this.GetBehaviorType() == BehaviorType.HeuristicOnly && Mouse.current.leftButton.wasPressedThisFrame && _mouseClicked != 1)
        {
            _mouseClicked = 1;
        }
    }

    protected override void OnFixedUpdate()
    {
        if (!EpisodeStarted)
        {
            return;
        }
        //Debug.Log($"OBSI: _reactionTimeTimer: {_reactionTimeTimer}, _movementTimer: {_movementTimer}, _movementTime: {_movementTime}, RequestDecision: {_movementTimer >= _movementTime && _reactionTimeTimer >= ConstantReactionTime && !IsMouseMode()}, IsActive: {IsActive}");

        _movementTime = _movementTime == float.NaN ? 5 : _movementTime;

        if (IsActive || IsAutonomous)
        {
            if (_movementTimer >= _movementTime && _reactionTimeTimer >= ConstantReactionTime && !IsMouseMode())
            {
                RequestDecision();
                _movementTimer = 0.0f;
            }

            if (IsMouseMode() && ((HasMouseStopedMoving() && _mouseStartingPosition != (Vector2)Mouse.current.position.ReadValue()) || _mouseClicked == 1))
            {
                _mouseVelocity = (Vector2)Mouse.current.position.ReadValue() - _mouseStartingPosition;
                _mouseStartingPosition = Mouse.current.position.ReadValue();
                RequestDecision();
            }
        }

        if (ShowBeliefState)
        {
            ShowProbabilities();
        }

        _movementTimer += Time.fixedDeltaTime;

        UpdateBeliefWrittenAnswer();
        UpdateFingerPosition();

        //Debug.Log("(float)GetFingerPositionProbability(): " + (float)GetFingerPositionProbability());

        _timeOfEpisode += Time.fixedDeltaTime;
        _reactionTimeTimer += Time.fixedDeltaTime;
    }


    private bool ClipVelocity(ref Vector2 fingerVelocity)
    {
        Vector2 fingerKeyboardPosition = _coordinateConverter.ImageToKeyboardCanvasSpace(_fingerPosition, KeyboardRectTransform, FingerPositionStateSpace);
        Vector2 fingerKeyboardVelocity = GetVelocityInKeyboardCanvasSpace(fingerVelocity);

        Vector2 clippedFingerKeyboardVelocity = PositionConverter.GetClippedVelocity(KeyboardRectTransform.rect, fingerKeyboardPosition, fingerKeyboardVelocity, 10);
        fingerVelocity = GetVelocityInScreenSpace(clippedFingerKeyboardVelocity);

        return fingerKeyboardVelocity != clippedFingerKeyboardVelocity;
    }

    private int GetFingerLocationProbabilityBinForCoordinates(Vector2 coordinates)
    {
        return PositionConverter.RectangleCoordinatesToBin(_coordinateConverter.ImageToKeyboardCanvasSpace(coordinates, KeyboardRectTransform, FingerPositionStateSpace), KeyboardRectTransform.rect.width, KeyboardRectTransform.rect.height, NumberOfBins);
    }

    private Vector2 KeyboardCanvasToBinCenter(Vector2 keyboardCanvasPosition)
    {
        return PositionConverter.RectangleCoordinatesToBinCenter(keyboardCanvasPosition, KeyboardRectTransform.rect.width, KeyboardRectTransform.rect.height, NumberOfBins);
    }

    private int KeyboardCanvasToBin(Vector2 keyboardCanvasPosition)
    {
        return PositionConverter.RectangleCoordinatesToBin(keyboardCanvasPosition, KeyboardRectTransform.rect.width, KeyboardRectTransform.rect.height, NumberOfBins);
    }

    private Vector2 BinToKeyboardCanvas(int bin)
    {
        return PositionConverter.BinToRectangleCoordinates(bin, KeyboardRectTransform.rect.width, KeyboardRectTransform.rect.height, NumberOfBins);
    }

    private void LogMovement(Vector2 fingerVelocity, Vector2 oldFingerVelocity, float sigma)
    {
        if (oldFingerVelocity == Vector2.zero)
        {
            return;
        }

        Vector2 fingerKeyboardPosition = _coordinateConverter.ImageToKeyboardCanvasSpace(_fingerPosition, KeyboardRectTransform, FingerPositionStateSpace);
        Vector2 fingerKeyboardVelocity = GetVelocityInKeyboardCanvasSpace(fingerVelocity);

        float distance = _coordinateConverter.CalculateDistanceCM(fingerVelocity, DisplayConfiguration, FingerPositionStateSpace);

        Debug.Log(string.Format("ACTION: Move finger with velocity {0} ({1}) over a distance of {2} cm and sigma {3} in {4} seconds (VisionAgents factor: {5}).", fingerVelocity, fingerKeyboardVelocity, distance, sigma, _movementTime, GetFingerPositionProbability()));
    }

    private void LogScreenLeftAllowedArea(Vector2 fingerVelocity)
    {
        Vector2 fingerKeyboardPosition = _coordinateConverter.ImageToKeyboardCanvasSpace(_fingerPosition, KeyboardRectTransform, FingerPositionStateSpace);
        Vector2 fingerKeyboardVelocity = GetVelocityInKeyboardCanvasSpace(fingerVelocity);

        Vector2 clippedFingerKeyboardVelocity = PositionConverter.GetClippedVelocity(KeyboardRectTransform.rect, fingerKeyboardPosition, fingerKeyboardVelocity, 10);
        Vector2 clippedFingerScreenVelocity = GetVelocityInScreenSpace(clippedFingerKeyboardVelocity);

        Debug.Log(string.Format("Finger left allowed area: {0} (screen space: {1}). Reset finger position to {2} (screen space: {3}).", fingerKeyboardPosition + fingerKeyboardVelocity, _fingerPosition + fingerVelocity, fingerKeyboardPosition + clippedFingerKeyboardVelocity, _fingerPosition + clippedFingerScreenVelocity));
    }

    private bool IsInvalidAction(ActionBuffers actionBuffers)
    {
        foreach (int val in actionBuffers.DiscreteActions)
        {
            if (val == -1)
            {
                Debug.LogWarning("DiscreteActions equals -1.");
                return true;
            }
        }

        return false;
    }

    private void PerformAction(ActionBuffers actionBuffers)
    {
        Vector2 fingerVelocity = GetFingerVelocity(actionBuffers);
        float sigma = GetFingerSigma(actionBuffers);
        int actionType = actionBuffers.DiscreteActions[0];

        if (actionType == 0)
        {
            if (fingerVelocity != Vector2.zero)
            {
                MoveFinger(fingerVelocity, sigma);
                if (ShowFingerPosition) { ProjectImage(FingerPositionStateSpace, "Finger"); }
            }
            else
            {
                Debug.Log("Finger did not move.");

                //add penalty
                AddReward(_lastTypingReward >= 0 ? -_lastTypingReward : _lastTypingReward);
            }
        }
        else
        {
            ClickButton();

            if (ShowFingerPosition) { ProjectImage(FingerPositionStateSpace, "FingerClick"); }

            if (GetTrueFingerPosition() != '\n')
            {
                _lastTypingReward = GetReward();
                _weight = 1f;

                AddReward(_lastTypingReward);
                //Debug.Log($"REWARD: {GetCumulativeReward()}");

                string pCorrectStr = _pCorrect != null
                    ? "[" + string.Join(", ", _pCorrect.Select(p => p.ToString("F3"))) + "]"
                    : "null";

                Debug.Log($"Cumulative Reward: {GetCumulativeReward()}");

                /**
                Debug.Log(
                    $"TypingReward={typingReward:F6}, " +
                    $"FingerCertainty={fingerCertainty:F3}, " +
                    $"TextCorrectProb={GetTextCorrectnessProability():F3}, " +
                    $"ObsTypingRatio={OBSERVATIONTYPINGREWARDRATIO:F3}, " +
                    $"focusRewardFactor={focusRewardFactor}, " +
                    $"FocusReward={focusReward:F6}, " +
                    $"CumulativeReward={GetCumulativeReward():F3}, " +
                    $"pCorrect={pCorrectStr:F3}"
                );
                **/
            }
        }

        float w = (1f - _gamma) * _weight;

        float fingerCertainty = 1f - _entropy01;
        float textProb = Mathf.Clamp01((float)GetTextCorrectnessProability());
        float focusRewardFactor = (fingerCertainty + textProb) / 2f;
        float focusReward = _lastTypingReward >= 0 ? _lastTypingReward * w * focusRewardFactor : _lastTypingReward * w * (1 - focusRewardFactor);
        TaskRewardForFocusAgent.Enqueue((focusReward, Priority)); 

        _weight *= _gamma;
        _stepCount += 1;
    }

    private void CheckEndingConditions(float distance)
    {
        if (distance > _currentQnA.Answer.Length + 50 || _stepCount > _currentQnA.Answer.Length * 100 || (_maxNumberOfActions != 0 && _stepCount >= _maxNumberOfActions))
        {
            Debug.Log(string.Format("End of episode: Levenshtein distance: {0}, step count: {1}", distance, _stepCount));
            SetReward(GetFinalReward());

            EndEpisode();
        }
    }

    private float NormalizeValue(float value, float minRange, float maxRange)
    {
        // Ensure value is within the min and max range to avoid unexpected results
        value = Mathf.Clamp(value, minRange, maxRange);

        // Normalize the value between -1 and 1
        return (value - minRange) / (maxRange - minRange) * 2f - 1f;
    }

    private void InitializeFingerLocationProbabilities()
    {
        _fingerLocationProbabilities = new double[FingerPositionStateSpace.VisualElements.Count];
        _fingerLocationProbabilitiesBinSpace = new double[NumberOfBins];

        for (int i = 0; i < FingerPositionStateSpace.VisualElements.Count; i++)
        {
            if (i == _startingButton)
            {
                _fingerPosition = _coordinateConverter.GetCoordinatesForGameObjectIndex(i, FingerPositionStateSpace);
                _fingerLocationProbabilities[i] = 1;
                _fingerLocationProbabilitiesBinSpace[GetFingerLocationProbabilityBinForCoordinates(_fingerPosition)] = 1;
            }
            else
            {
                _fingerLocationProbabilities[i] = 0;
            }
        }
    }

    private double GetFingerPositionProbability()
    {
        return _fingerLocationProbabilitiesBinSpace[GetFingerLocationProbabilityBinForCoordinates(_fingerPosition)];
    }

    private double GetTextCorrectnessProability()
    {
        return _pCorrect.Aggregate(1.0, (acc, n) => acc * n);
    }

    private void MoveFinger(Vector2 fingerVelocity, float sigma)
    {
        Vector2 oldFingerScreenVelocity = fingerVelocity;
        bool wasClipped = ClipVelocity(ref fingerVelocity);

        Vector2[] screenNormal = !UseViewportSpace ? CRUtil.GetNormalDistributionForVelocity(NumberOfSamples, fingerVelocity, sigma, _rand) :  CRUtil.GetNormalDistributionForVelocity(NumberOfSamples, fingerVelocity,  new Vector2(sigma, sigma / (DisplayConfiguration.AspectWidth/ DisplayConfiguration.AspectHeight)), _rand);

        if (wasClipped)
        {
            LogScreenLeftAllowedArea(oldFingerScreenVelocity);
            AddReward(-1);
            _fingerExitCount++;

            if(_fingerExitCount > 5)
            {
                HandleTaskTermination();
            }
        }
        else
        {
            _fingerExitCount = 0;
        }

        //It must be guaranteed that the screen velocity is inside the normal distribution, otherwise it could be the case that the finger
        //location probability of the bin where the finger is located is 0 and an update in case of vision of this bin would not work.
        int pick = _rand.Next(0, screenNormal.Length);
        fingerVelocity = screenNormal[pick];
        ClipVelocity(ref fingerVelocity);
        screenNormal[pick] = fingerVelocity;

        Vector2[] keyboardNormal = GetVelocitiesInKeyboardCanvasSpace(screenNormal);

        float distance = _coordinateConverter.CalculateDistanceCM(fingerVelocity, DisplayConfiguration, FingerPositionStateSpace);
        _movementTime = GetWHoMovementTime(distance, !UseViewportSpace ? sigma : CRUtil.SigmaToCM(sigma, DisplayConfiguration));

        if (_movementTime > 1.5 || _movementTime.Equals(float.NaN))
        {
            Debug.Log(string.Format("Performed unrealistic behavior: movement time of {0} for sigma {1} and velocity {2}.", _movementTime, sigma, fingerVelocity));
            _movementTime = 0;
            AddReward(-1);
            return;
        }

        LogMovement(fingerVelocity, oldFingerScreenVelocity, sigma);

        UpdateFingerPosition(fingerVelocity, keyboardNormal);
    }

    private void UpdateFingerPosition(Vector2? fingerVelocity = null, Vector2[] normal = null)
    {
        int fingerScreenPositionBin = GetFingerLocationProbabilityBinForCoordinates(_fingerPosition);
        Vector2 fingerScreenPositionNew = fingerVelocity != null ? TransformScreenCoordinateToBinCenter(_fingerPosition) + fingerVelocity.Value : _fingerPosition;
        int fingerScreenPositionNewBin = KeyboardCanvasToBin(_coordinateConverter.ImageToKeyboardCanvasSpace(fingerScreenPositionNew, KeyboardRectTransform, FingerPositionStateSpace));

        //Debug.Log(string.Format("Move finger from position {0} (Bin: {1}, Prob: {2}) to {3} (Bin: {4}) {5}", GetScreenPositionInKeyboardCanvasSpace(_fingerScreenPosition), fingerScreenPositionBin, _fingerLocationProbabilitiesBinSpace[fingerScreenPositionBin], GetScreenPositionInKeyboardCanvasSpace(fingerScreenPositionNew), fingerScreenPositionNewBin, this.GetHashCode()));
        _fingerPosition = fingerScreenPositionNew;

        int maxIndex = _fingerLocationProbabilitiesBinSpace.ToList().IndexOf(_fingerLocationProbabilities.Max());
        Vector2 maxPosition = BinToKeyboardCanvas(maxIndex);

        GameObject buttonAtFingerLocation = _coordinateConverter.GetGameObjectForCoordinates(_fingerPosition, FingerPositionStateSpace);
        FingerPositionStateSpace.ActivateSingleElement(buttonAtFingerLocation);

        UpdateBeliefState(normal);
    }

    private Vector2 TransformScreenCoordinateToBinCenter(Vector2 screenPosition)
    {
        Vector2 keyboardPosition = _coordinateConverter.ImageToKeyboardCanvasSpace(screenPosition, KeyboardRectTransform, FingerPositionStateSpace);
        Vector2 centeredKeyboardPosition = KeyboardCanvasToBinCenter(keyboardPosition);

        return _coordinateConverter.KeyboardCanvasToImageSpace(centeredKeyboardPosition, KeyboardRectTransform, FingerPositionStateSpace);
    }

    private void UpdateBeliefState(Vector2[] normal = null)
    {
        UpdateFingerPositionProbabilities(normal);
        UpdateOneHotEncodingBeliefState();
    }

    private void UpdateFingerPositionProbabilities(Vector2[] normal = null)
    {
        double[] currentFingerLocationProbabilitiesBinSpace = (double[])_fingerLocationProbabilitiesBinSpace.Clone();

        normal ??= new Vector2[1];

        NativeArray<Vector2> normalNative = new NativeArray<Vector2>(normal, Allocator.TempJob);
        NativeArray<double> currentFingerLocationProbabilitiesBinSpaceNative = new NativeArray<double>(currentFingerLocationProbabilitiesBinSpace, Allocator.TempJob);
        NativeArray<double> fingerLocationProbabilitiesBinSpaceNative = new NativeArray<double>(_fingerLocationProbabilitiesBinSpace, Allocator.TempJob);

        List<GameObjectPosition> gameObjectPositions = new();
        gameObjectPositions.AddRange(FingerPositionStateSpace.VisualElements.Where(a => a.activeInHierarchy).Select(a => CRUtil.ConvertToGameObjectPosition(a, FingerPositionStateSpace.Camera)).ToArray());
        NativeArray<GameObjectPosition> gameObjectPositionsNative = new NativeArray<GameObjectPosition>(gameObjectPositions.ToArray(), Allocator.TempJob);

        bool isFocusOnFinger = _coordinateConverter.IsActiveElementAtPosition(_fingerPosition, FocusStateSpace);

        ObjectIn2DRectangleLocationProbabilitiesUpdateJob fingerLocationProbabilitiesUpdateJob = new ObjectIn2DRectangleLocationProbabilitiesUpdateJob
        {
            NormalDistributionForVelocity = normalNative,
            CurrentObjectLocationProbabilities = currentFingerLocationProbabilitiesBinSpaceNative,
            ObjectLocationProbabilities = fingerLocationProbabilitiesBinSpaceNative,
            ObjectPosition = _coordinateConverter.ImageToKeyboardCanvasSpace(_fingerPosition, KeyboardRectTransform, FingerPositionStateSpace),
            IsVisibleInstance = isFocusOnFinger || IsVisible,
            RectangleWidth = KeyboardRectTransform.rect.width,
            RectangleHight = KeyboardRectTransform.rect.height,
            NumberOFBins = NumberOfBins,
            ObservationProbability = ObservationProbability,
            ConsiderEdgeBins = true
        };

        JobHandle jobHandle = fingerLocationProbabilitiesUpdateJob.Schedule(NumberOfBins, 16);
        jobHandle.Complete();

        //direct assignment of the array would overwrite the reference to the original array (e.g. reference of subclass)
        Array.Copy(fingerLocationProbabilitiesBinSpaceNative.ToArray(), _fingerLocationProbabilitiesBinSpace, _fingerLocationProbabilitiesBinSpace.Length);

        //Normalize the updated belief b`(s_)
        double sum = _fingerLocationProbabilitiesBinSpace.Sum();

        if (sum != 0)
        {
            for (int i = 0; i < _fingerLocationProbabilitiesBinSpace.Length; i++)
            {
                _fingerLocationProbabilitiesBinSpace[i] = _fingerLocationProbabilitiesBinSpace[i] / sum;
            }

            _entropy01 = Util.Entropy01(_fingerLocationProbabilities);
        }
        else
        {
            Debug.LogWarning("Sum of belief states is zero.");
        }

        normalNative.Dispose();
        currentFingerLocationProbabilitiesBinSpaceNative.Dispose();
        fingerLocationProbabilitiesBinSpaceNative.Dispose();
        gameObjectPositionsNative.Dispose();

        TransferFingerLocationProbabilitiesFromBinSpaceToVisualSpace();
    }

    private void ClickButton()
    {
        if (IsButtonVisible(GetTarget(_currentQnA.Answer, _beliefWrittenAnswer)))
        {
            _beliefClickedButton = GetButtonCharValue(GetBeliefFingerButton());
            UpdateBeliefWrittenAnswer(_beliefClickedButton.ToEscapedString());
        }

        if (FingerPositionStateSpace.GetFirstActiveElement() != null)
        {
            FingerPositionStateSpace.GetFirstActiveElement().GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            _movementTime = 0.1f;
        }

        Debug.Log(string.Format("ACTION: Click on {0} button", _beliefClickedButton.ToEscapedString()));
    }

    private float GetWHoMovementTime(float distance, float sigma)
    {
        if (distance == 0)
        {
            return 0;
        }

        if (sigma < 0.01f)
        {
            sigma = 0.01f;
        }

        return (float)(X_0 + Math.Pow(KAlpha / Math.Pow(sigma / distance - Y_0, 1 - Alpha), 1 / Alpha));
    }

    private void UpdateOneHotEncodingBeliefState()
    {
        double maxValue = _fingerLocationProbabilities.Max();
        int maxIndex = _fingerLocationProbabilities.ToList().IndexOf(maxValue);

        _beliefFingerPositionStateSpace.ActivateSingleElement(maxIndex);
    }

    private void UpdateBeliefWrittenAnswer(string typedText = null)
    {
        if (typedText != null)
        {
            if (_beliefWrittenAnswer.Length > 0 && typedText == "\\x7F")
            {
                _beliefWrittenAnswer = _beliefWrittenAnswer[..^1];

                if (!_pCorrect.IsNullOrEmpty())
                {
                    _pCorrect.RemoveAt(_pCorrect.Count - 1);
                }
            }
            else if (typedText != "\\x7F")
            {
                _beliefWrittenAnswer += typedText;
                _pCorrect.Add(_fingerLocationProbabilities.Max());
            }
        }

        if (IsTextEncoded())
        {
            _beliefWrittenAnswer = AnswerText.text;
            _pCorrect = _pCorrect.Select(_ => 1.0).ToList();
        }
    }

    private void ShowProbabilities()
    {
        if (_beliefFingerPositionStateSpace != null)
        {
            for (int i = 0; i < _beliefFingerPositionStateSpace.VisualElements.Count; i++)
            {
                GameObject button = _beliefFingerPositionStateSpace.VisualElements[i];
                TextMeshProUGUI text = button.transform.GetChildInHierarchyByName("ProbabilityText").GetComponent<TextMeshProUGUI>();

                text.text = string.Format("{0:N2}%", _fingerLocationProbabilities[i] * 100);

                if (_fingerLocationProbabilities[i] == _fingerLocationProbabilities.Max())
                {
                    text.color = Color.red;
                }
                else
                {
                    Color color = Color.white;
                    color.a = _fingerLocationProbabilities[i] != 0 ? (float)_fingerLocationProbabilities[i] * 2f + 0.2f : 0;
                    text.color = color;
                }
            }
        }
    }

    private Vector2 GetMouseMovement()
    {
        Vector2 currentMousePosition = Mouse.current.position.ReadValue();
        Vector2 delta = currentMousePosition - _previousMousePosition;
        _previousMousePosition = currentMousePosition;

        return delta;
    }

    private IEnumerator InitMousePositionNextFrame()
    {
        yield return null;

        _mouseStartingPosition = Mouse.current.position.ReadValue();
        _previousMousePosition = _mouseStartingPosition;
    }

    private IEnumerator MarkEpisodeStartedNextFrame()
    {
        yield return null;
        EpisodeStarted = true;
    }

    private bool HasMouseStopedMoving()
    {
        return GetMouseMovement() == Vector2.zero;
    }

    private void TransferFingerLocationProbabilitiesFromBinSpaceToVisualSpace()
    {
        _fingerLocationProbabilities = new double[FingerPositionStateSpace.VisualElements.Count];
        _binOverlapCount ??= CountBinOverlapOccurrences();

        for (int i = 0; i < FingerPositionStateSpace.VisualElements.Count; i++)
        {
            Vector2 rectCenter = _coordinateConverter.ImageToKeyboardCanvasSpace(_coordinateConverter.GetCoordinatesForGameObjectIndex(i, FingerPositionStateSpace), KeyboardRectTransform, FingerPositionStateSpace);
            GameObject button = FingerPositionStateSpace.VisualElements[i];
            Vector2 rectSize = button.GetComponent<RectTransform>().rect.size;
            List<int> binsInsideButton = PositionConverter.GetBinsInsideRect(rectCenter, rectSize, KeyboardRectTransform.rect.width, KeyboardRectTransform.rect.height, NumberOfBins);

            //foreach (int bin in binsInsideButton.FindAll(x => _fingerLocationProbabilitiesBinSpace[x] != 0)) //TODO: test if I work and if I am faster
            foreach (int bin in binsInsideButton)
            {
                _fingerLocationProbabilities[i] += _fingerLocationProbabilitiesBinSpace[bin] / _binOverlapCount[bin];
                if (_binOverlapCount[bin] == 0)
                {
                    Debug.LogWarning($"Bin overlap count for bin {i} is equal to 0!");
                    _binOverlapCount = CountBinOverlapOccurrences();
                    TransferFingerLocationProbabilitiesFromBinSpaceToVisualSpace();
                }
            }
        }
    }

    private int[] CountBinOverlapOccurrences()
    {
        int[] binCount = new int[NumberOfBins];

        for (int i = 0; i < FingerPositionStateSpace.VisualElements.Count; i++)
        {
            Vector2 rectCenter = _coordinateConverter.ImageToKeyboardCanvasSpace(_coordinateConverter.GetCoordinatesForGameObjectIndex(i, FingerPositionStateSpace), KeyboardRectTransform, FingerPositionStateSpace);
            GameObject button = FingerPositionStateSpace.VisualElements[i];
            Vector2 rectSize = button.GetComponent<RectTransform>().rect.size;
            List<int> binsInsideButton = PositionConverter.GetBinsInsideRect(rectCenter, rectSize, KeyboardRectTransform.rect.width, KeyboardRectTransform.rect.height, NumberOfBins);

            foreach (int bin in binsInsideButton)
            {
                binCount[bin] += 1;
            }
        }

        return binCount;
    }

    protected override void UpdateEnvironmentParameters()
    {
        base.UpdateEnvironmentParameters();
        FullVision = Academy.Instance.EnvironmentParameters.GetWithDefault("FullVision", FullVision ? 1 : 0) > 0.5f;
    }
}