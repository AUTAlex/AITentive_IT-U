using UnityEngine.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Validator
{
    public static void ValidateTraining(Dictionary<Type, ISettings> settings)
    {
        Hyperparameters hyperparameters = settings[typeof(Hyperparameters)] as Hyperparameters;
        SupervisorSettings supervisorSettings = null;

        if (settings.ContainsKey(typeof(SupervisorSettings)))
        {
            supervisorSettings = settings[typeof(SupervisorSettings)] as SupervisorSettings;
        }

        Assert.IsFalse(hyperparameters.saveBehavioralData.GetValueOrDefault());
        Assert.AreNotEqual(0, hyperparameters.tasks.Length);

        if (!hyperparameters.autonomous.GetValueOrDefault() && !supervisorSettings.randomSupervisor.GetValueOrDefault())
        {
            Assert.AreNotEqual(0, supervisorSettings.vectorObservationSize);
        }
    }

    public static void ValidateAbc(Dictionary<Type, ISettings> settings)
    {
        Hyperparameters hyperparameters = settings[typeof(Hyperparameters)] as Hyperparameters;
        SupervisorSettings supervisorSettings = settings[typeof(SupervisorSettings)] as SupervisorSettings;
        BalancingTaskSettings balancingTaskSettings = settings[typeof(BalancingTaskSettings)] as BalancingTaskSettings;
        Ball3DAgentHumanCognitionSettings ball3DAgentHumanCognitionSettings = settings[typeof(Ball3DAgentHumanCognitionSettings)] as Ball3DAgentHumanCognitionSettings;
        BehavioralDataCollectionSettings behavioralDataCollectionSettings = settings[typeof(BehavioralDataCollectionSettings)] as BehavioralDataCollectionSettings;

        Assert.IsFalse(hyperparameters.autonomous.GetValueOrDefault());
        //Assert.IsTrue(hyperparameters.agentChoice == "Ball3DAgentHumanCognition" || hyperparameters.agentChoice == "Ball3DAgentHumanCognitionSingleProbabilityDistribution");
        Assert.AreNotEqual("", hyperparameters.taskModels["BallAgent"]);
        Assert.IsTrue(hyperparameters.taskModels.ContainsKey("BallAgent"));
        Assert.IsTrue(supervisorSettings.randomSupervisor.GetValueOrDefault() || hyperparameters.supervisorModelName != "");
        Assert.IsTrue(hyperparameters.saveBehavioralData.GetValueOrDefault());
        Assert.AreNotEqual(0, hyperparameters.tasks.Length);

        Assert.AreNotEqual(0, supervisorSettings.vectorObservationSize);
        Assert.AreEqual(0, supervisorSettings.advanceNoticeInSeconds);
        Assert.AreNotEqual(0, supervisorSettings.decisionRequestIntervalInSeconds);
        Assert.AreNotEqual(0, supervisorSettings.decisionRequestIntervalRangeInSeconds);
        Assert.AreNotEqual(0, balancingTaskSettings.ballStartingRadius);
        Assert.AreNotEqual(0, balancingTaskSettings.resetSpeed);

        Assert.IsFalse(ball3DAgentHumanCognitionSettings.fullVision.GetValueOrDefault());

        Assert.IsFalse(behavioralDataCollectionSettings.collectDataForComparison.GetValueOrDefault());
        Assert.IsFalse(behavioralDataCollectionSettings.updateExistingModelBehavior.GetValueOrDefault());
        Assert.AreEqual(0, behavioralDataCollectionSettings.maxNumberOfActions);
        Assert.AreNotEqual(0, behavioralDataCollectionSettings.numberOfAreaBins_BehavioralData);
        Assert.AreNotEqual(0, behavioralDataCollectionSettings.numberOfBallVelocityBinsPerAxis_BehavioralData);
        Assert.AreNotEqual(0, behavioralDataCollectionSettings.numberOfAngleBinsPerAxis);
        Assert.AreNotEqual(0, behavioralDataCollectionSettings.numberOfDistanceBins);
        Assert.AreNotEqual(0, behavioralDataCollectionSettings.numberOfDistanceBins_velocity);
        Assert.AreNotEqual(0, behavioralDataCollectionSettings.numberOfActionBinsPerAxis);
        Assert.AreNotEqual(0, behavioralDataCollectionSettings.numberOfTimeBins);

        if (hyperparameters.useFocusAgent.GetValueOrDefault())
        {
            Assert.AreNotEqual("", hyperparameters.focusAgentModelName);
            Assert.AreNotEqual(null, hyperparameters.focusAgentModelName);
        }
    }

    public static void ValidateEvaluation(Dictionary<Type, ISettings> settings)
    {
        Hyperparameters hyperparameters = settings[typeof(Hyperparameters)] as Hyperparameters;
        SupervisorSettings supervisorSettings = settings[typeof(SupervisorSettings)] as SupervisorSettings;
        BalancingTaskSettings balancingTaskSettings = settings[typeof(BalancingTaskSettings)] as BalancingTaskSettings;
        Ball3DAgentHumanCognitionSettings ball3DAgentHumanCognitionSettings = settings[typeof(Ball3DAgentHumanCognitionSettings)] as Ball3DAgentHumanCognitionSettings;
        BehavioralDataCollectionSettings behavioralDataCollectionSettings = settings[typeof(BehavioralDataCollectionSettings)] as BehavioralDataCollectionSettings;

        Assert.IsTrue(hyperparameters.taskModels.ContainsKey("BallAgent"));
        Assert.IsTrue(supervisorSettings.randomSupervisor.GetValueOrDefault() || hyperparameters.supervisorModelName != "");
        Assert.AreNotEqual(0, hyperparameters.tasks.Length);

        Assert.AreNotEqual(0, supervisorSettings.vectorObservationSize);
        Assert.AreEqual(0, supervisorSettings.advanceNoticeInSeconds);
        Assert.AreNotEqual(0, supervisorSettings.decisionRequestIntervalInSeconds);
        Assert.AreNotEqual(0, supervisorSettings.decisionRequestIntervalRangeInSeconds);
        Assert.AreNotEqual(0, balancingTaskSettings.resetSpeed);

        if (hyperparameters.tasks.Contains("Ball3DAgentHumanCognition") || hyperparameters.tasks.Contains("Ball3DAgentHumanCognitionSingleProbabilityDistribution"))
        {
            Assert.AreNotEqual(0, ball3DAgentHumanCognitionSettings.numberOfBins);
            Assert.AreNotEqual(0, ball3DAgentHumanCognitionSettings.numberOfSamples);
            Assert.AreNotEqual(0, ball3DAgentHumanCognitionSettings.observationProbability);
            Assert.IsFalse(ball3DAgentHumanCognitionSettings.fullVision.GetValueOrDefault());
        }

        Assert.AreNotEqual(0, behavioralDataCollectionSettings.maxNumberOfActions);
        Assert.AreNotEqual(0, behavioralDataCollectionSettings.numberOfAreaBins_BehavioralData);
        Assert.AreNotEqual(0, behavioralDataCollectionSettings.numberOfBallVelocityBinsPerAxis_BehavioralData);
        Assert.AreNotEqual(0, behavioralDataCollectionSettings.numberOfAngleBinsPerAxis);
        Assert.AreNotEqual(0, behavioralDataCollectionSettings.numberOfDistanceBins);
        Assert.AreNotEqual(0, behavioralDataCollectionSettings.numberOfDistanceBins_velocity);
        Assert.AreNotEqual(0, behavioralDataCollectionSettings.numberOfActionBinsPerAxis);
        Assert.AreNotEqual(0, behavioralDataCollectionSettings.numberOfTimeBins);

        if (hyperparameters.useFocusAgent.GetValueOrDefault())
        {
            Assert.AreNotEqual("", hyperparameters.focusAgentModelName);
            Assert.AreNotEqual(null, hyperparameters.focusAgentModelName);
        }
    }

    public static void ValidateProjectSettings(ProjectSettings projectSettings)
    {
        Assert.AreNotEqual(0, projectSettings.TasksGameObjects.Length, "ProjectSettings invalid: Number of tasks should not be 0.");

        if (projectSettings.AtLeastOneTaskUsesVisionAgent() && !projectSettings.HeuristicModeForVisionAgent)
        {
            Assert.IsFalse(projectSettings.GetVisionModels().IsNullOrEmpty(), "ProjectSettings invalid: Focus agent is not defined although at least one Task uses it.");
        }

        if (!projectSettings.SupervisorIsRandomSupervisor() && projectSettings.SupervisorChoice != SupervisorChoice.NoSupport)
        {
            Assert.IsFalse(projectSettings.GetSupervisorModels().IsNullOrEmpty(), "ProjectSettings invalid: Supervisor agent is not defined.");
        }
    }

    public static void ValidateExperimentSettings(Dictionary<Type, ISettings> settings)
    {
        Assert.IsTrue(settings.ContainsKey(typeof(Hyperparameters)));
        Assert.IsTrue(settings.ContainsKey(typeof(SupervisorSettings)));

        Hyperparameters hyperparameters = settings[typeof(Hyperparameters)] as Hyperparameters;

        Assert.AreNotEqual(0, hyperparameters.tasks.Length, "ProjectSettings invalid: Number of tasks should not be 0.");
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnPlaymodeStart()
    {
        try
        {
            // Make sure we can see inactive objects too
            var settings = UnityEngine.Object.FindFirstObjectByType<ProjectSettings>(FindObjectsInactive.Include);
            if (settings == null)
                throw new InvalidOperationException("ProjectSettings GameObject (with ProjectSettings component) was not found in the loaded scene.");

            // Your existing NUnit-based checks
            Validator.ValidateProjectSettings(settings);

            Debug.Log("Startup validation passed.");
        }
        catch (AssertionException ex) // NUnit assertions
        {
            FailHard("Startup validation failed (assertion): " + ex.Message, ex);
        }
        catch (Exception ex) // anything else
        {
            FailHard("Startup validation failed: " + ex.Message, ex);
        }
    }

    private static void FailHard(string message, Exception ex)
    {
        Debug.LogError(message + "\n" + ex.StackTrace);
        // Stop Play mode immediately and show a dialog so it’s obvious
        EditorApplication.isPlaying = false;
        EditorUtility.DisplayDialog("Startup validation failed", message, "OK");
    }
#endif
}
