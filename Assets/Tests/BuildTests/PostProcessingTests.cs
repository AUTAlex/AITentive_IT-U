using System;
using System.IO;
using NSubstitute;
using NUnit.Framework;
using UnityEditor;
using Debug = UnityEngine.Debug;


public class PostProcessingTests
{
    private ICommandLineInterface _commandLineInterfaceMock;
    private IProjectSettings _projectSettings;
    private string _postProcessingPath;
    private string _modelPath;


    [SetUp]
    public void SetUp()
    {
        _commandLineInterfaceMock = Substitute.For<ICommandLineInterface>();
        _projectSettings = Substitute.For<IProjectSettings>();
        _postProcessingPath = Path.Combine("Assets", "Tests", "BuildTests", "postprocessing");
        _modelPath = Path.Combine(_postProcessingPath, "TEST2", "TEST_AUIDrivingTypingFullModelSingleH2_AUI_2024_Driving_Typing_FullModelSingleStep_TMP");

        Util.CopyDirectory(Path.Combine(_postProcessingPath, "TEST2", "TEST_AUIDrivingTypingFullModelSingleH2_AUI_2024_Driving_Typing_FullModelSingleStep"), _modelPath);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_modelPath, true);
            File.Delete(_modelPath + ".meta");
        }
        catch (Exception e)
        {
            Debug.Log(String.Format("Could not delete directory: {0}", e.Message));
        }
    }

    [Test]
    public void PostProcessingSupervisorAgentTest()
    {
        EnrichModels(false, Path.Combine(_modelPath, "H2_AUI_2024_Driving_Typing_FullModelSingleStep.json"));
        string modelPath = Path.Combine(_modelPath, "AUITEST_AUIDrivingTypingFullModelSingleH2_AUI_2024_Driving_Typing_FullModelSingleStep_TMP.asset");

        AssetDatabase.ImportAsset(modelPath);
        AITentiveModel model = AssetDatabase.LoadAssetAtPath(modelPath, typeof(AITentiveModel)) as AITentiveModel;

        Assert.AreEqual(1, model.DecisionPeriod);
        Assert.AreEqual("SupervisorAgent", model.Type);
        Assert.IsFalse(model.SupervisorSettings.randomSupervisor);
        Assert.AreEqual(149, model.SupervisorSettings.vectorObservationSize);
        Assert.IsTrue(model.SupervisorSettings.setConstantDecisionRequestInterval);
        Assert.AreEqual(0.8f, model.SupervisorSettings.decisionRequestIntervalInSeconds);
        Assert.AreEqual(100, model.SupervisorSettings.difficultyIncrementInterval);
        Assert.AreEqual(0.3f, model.SupervisorSettings.advanceNoticeInSeconds);

        Assert.AreEqual("AUITEST_AUIDrivingTypingFullModelSingleH2_AUI_2024_Driving_Typing_FullModelSingleStep_TMP", model.Model.name);
    }

    [Test]
    public void PostProcessingFocusAgentTest()
    {
        EnrichModels(false, Path.Combine(_modelPath, "H2_AUI_2024_Driving_Typing_FullModelSingleStep.json"));
        string modelPath = Path.Combine(_modelPath, "FocusTEST_AUIDrivingTypingFullModelSingleH2_AUI_2024_Driving_Typing_FullModelSingleStep_TMP.asset");

        AssetDatabase.ImportAsset(modelPath);
        AITentiveModel model = AssetDatabase.LoadAssetAtPath(modelPath, typeof(AITentiveModel)) as AITentiveModel;

        Assert.AreEqual(0, model.DecisionPeriod);
        Assert.AreEqual("FocusAgent", model.Type);

        Assert.AreEqual("FocusTEST_AUIDrivingTypingFullModelSingleH2_AUI_2024_Driving_Typing_FullModelSingleStep_TMP", model.Model.name);
    }

    [Test]
    public void PostProcessingTaskAgentTest()
    {
        EnrichModels(false, Path.Combine(_modelPath, "H2_AUI_2024_Driving_Typing_FullModelSingleStep.json"));
        string modelPath = Path.Combine(_modelPath, "DrivingAgentTEST_AUIDrivingTypingFullModelSingleH2_AUI_2024_Driving_Typing_FullModelSingleStep_TMP.asset");

        AssetDatabase.ImportAsset(modelPath);
        AITentiveModel model = AssetDatabase.LoadAssetAtPath(modelPath, typeof(AITentiveModel)) as AITentiveModel;

        Assert.AreEqual(4, model.DecisionPeriod);
        Assert.AreEqual("DrivingAgent", model.Type);

        Assert.AreEqual("DrivingAgentTEST_AUIDrivingTypingFullModelSingleH2_AUI_2024_Driving_Typing_FullModelSingleStep_TMP", model.Model.name);


        modelPath = Path.Combine(_modelPath, "TypingAgentTEST_AUIDrivingTypingFullModelSingleH2_AUI_2024_Driving_Typing_FullModelSingleStep_TMP.asset");

        AssetDatabase.ImportAsset(modelPath);
        model = AssetDatabase.LoadAssetAtPath(modelPath, typeof(AITentiveModel)) as AITentiveModel;

        Assert.AreEqual(0, model.DecisionPeriod);
        Assert.AreEqual("TypingAgent", model.Type);

        Assert.AreEqual("TypingAgentTEST_AUIDrivingTypingFullModelSingleH2_AUI_2024_Driving_Typing_FullModelSingleStep_TMP", model.Model.name);
    }


    private void EnrichModels(bool useMock, string configFilePath)
    {
        _commandLineInterfaceMock.GetCommandLineArgs().Returns(new string[] { "Dummy", "-executeMethod", "PostProcessing.EnrichModels", configFilePath, "-o" });
        APIHelper.CommandLineInterface = _commandLineInterfaceMock;

        DefineProjectSettings(useMock);

        PostProcessing.EnrichModels();
    }

    private void DefineProjectSettings(bool useMock)
    {
        if (useMock)
        {
            SceneManagement.ProjectSettings = _projectSettings;
        }
        else
        {
            SceneManagement.ProjectSettings = null;
        }
    }
}


