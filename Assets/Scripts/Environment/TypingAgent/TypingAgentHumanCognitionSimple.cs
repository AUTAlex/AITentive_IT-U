using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class TypingAgentHumanCognitionSimple : TypingAgentHumanCognition
{
    private char _lastPressedKey;

    private bool _keyPressed = false;

    private string _observationString;

    private bool _useActionMasking = false;


    public override void CollectObservations(VectorSensor sensor)
    {
        if (_currentQnA == null)
        {
            return;
        }

        _beliefTarget = GetTarget(_currentQnA.Answer, _beliefWrittenAnswer);

        sensor.AddOneHotObservation(MapCharToAction((int)_beliefTarget), 31);
        sensor.AddOneHotObservation(MapCharToAction(GetButtonCharValue(GetBeliefFingerButton())), 31);
        sensor.AddObservation(1 - _entropy01); //This was added later; moving it above other observations would break compatibility with older models.

        Vector2 distance = _coordinateConverter.ImageToKeyboardCanvasSpace((Vector2)_coordinateConverter.GetCoordinatesForGameObject(GetButton(_beliefTarget), FingerPositionStateSpace), KeyboardRectTransform, FingerPositionStateSpace) - GetBeliefFingerPositionKeyboardSpace();

        string observationString = string.Format("OBSERVATION: Finger Belief Position: {0} (Button: {1}), Target: {2}, Distance: {3}, Belief Written Answer: {4}", GetBeliefFingerPositionKeyboardSpace(), GetButtonCharValue(GetBeliefFingerButton()).ToEscapedString(), _beliefTarget, distance, _beliefWrittenAnswer);

        if (observationString != _observationString)
        {
            Debug.Log(observationString);
        }

        _observationString = observationString;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActionsOut = actionsOut.ContinuousActions;
        ActionSegment<int> discreteActionsOut = actionsOut.DiscreteActions;

        //random testing value for sigma
        continuousActionsOut[0] = SigmaHeuristic;

        discreteActionsOut[0] = 0;

        if (_keyPressed)
        {
            discreteActionsOut[1] = MapCharToAction(_lastPressedKey);
            _keyPressed = false;

            if (_lastPressedKey == '+')
            {
                discreteActionsOut[0] = 1;
            }
            if (_lastPressedKey == '-')
            {
                discreteActionsOut[1] = 27;
            }
            if (_lastPressedKey == '<')
            {
                discreteActionsOut[1] = 28;
            }
        }
        else
        {
            discreteActionsOut[1] = 31;
        }
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (_useActionMasking)
        {
            List<char> uniqueLetters = GetUniqueLettersInText(_currentQnA.Answer);

            for (int i = 0; i < 32; i++) 
            {
                actionMask.SetActionEnabled(branch: 1, actionIndex: i, isEnabled: uniqueLetters.Contains((char)MapActionToChar(i)));
            }

            actionMask.SetActionEnabled(branch: 1, actionIndex: 27, isEnabled: true); //DEL
            actionMask.SetActionEnabled(branch: 1, actionIndex: 28, isEnabled: true); //Enter
            //actionMask.SetActionEnabled(branch: 1, actionIndex: 31, isEnabled: true); //no key
        }
    }


    protected override Vector2 GetFingerVelocity(ActionBuffers actionBuffers)
    {
        int button = actionBuffers.DiscreteActions[1];

        if (button == 31)
        {
            return new Vector2(0, 0);
        }

        return (Vector2)_coordinateConverter.GetCoordinatesForGameObject(GetButton((char)MapActionToChar(button)), FingerPositionStateSpace) - _coordinateConverter.KeyboardCanvasToImageSpace(GetBeliefFingerPositionKeyboardSpace(), KeyboardRectTransform, FingerPositionStateSpace);
    }

    protected override float GetFingerSigma(ActionBuffers actionBuffers)
    {
        int buttonId = actionBuffers.DiscreteActions[1];
        GameObject button = GetButton((char)MapActionToChar(buttonId));

        if (button == null)
        {
            return 0;
        }

        Vector2 fingerVelocity = GetFingerVelocity(actionBuffers);
        float weff = CRUtil.GetEffectiveTargetWidth(fingerVelocity, _coordinateConverter.GetGameObjectSize(button, FingerPositionStateSpace));

        return UseViewportSpace ? ScaleSigma(actionBuffers.ContinuousActions[0]) * weff : Mathf.Clamp01(actionBuffers.ContinuousActions[0]);
    }

    protected override bool IsMouseMode() => false;

    protected override void UpdateEnvironmentParameters()
    {
        base.UpdateEnvironmentParameters();
        _useActionMasking = Academy.Instance.EnvironmentParameters.GetWithDefault("UseActionMasking", 0f) > 0.5f;
    }


    private int MapActionToChar(int index)
    {
        int result = -1;

        if (index < 26)
        {
            result = index + 97;
        }
        else if (index == 26) //space
        {
            return 32;
        }
        else if (index == 27) //DEL
        {
            return 127;
        }
        else if (index == 28) //enter
        {
            return 10;
        }
        else if (index == 29) //comma
        {
            return 44;
        }
        else if (index == 30) //point
        {
            return 46;
        }

        return result;
    }

    private int MapCharToAction(int c)
    {
        //no key pressed
        int result = 31;

        if (c >= 97 && c <= 122)
        {
            result = c - 97;
        }
        else if (c == 32) //space
        {
            return 26;
        }
        else if (c == 127) //DEL
        {
            return 27;
        }
        else if (c == 10) //enter
        {
            return 28;
        }
        else if (c == 44) //comma
        {
            return 29;
        }
        else if (c == 46) //point
        {
            return 30;
        }

        return result;
    }

    private void OnGUI()
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown)
        {
            _lastPressedKey = e.character;
            _keyPressed = true;
        }
    }

    private List<char> GetUniqueLettersInText(string input)
    {
        HashSet<char> uniqueChars = new HashSet<char>();

        foreach (char c in input)
        {
            uniqueChars.Add(c);
        }

        return new List<char>(uniqueChars);
    }
}
