using UnityEngine;
using NUnit.Framework;
using NSubstitute;


public class TypingAgentTest
{

    [Test]
    public void GetFinalRewardCorrectTextTest()
    {
        var go = new GameObject();

        var agent = go.AddComponent<TypingAgentHumanCognitionSimpleNormalized>();
        agent._currentQnA = new()
        {
            Question = "",
            Answer = "us"
        };
        agent._typingMetrics = new TypingMetrics();

        agent.AnswerText = new();
        agent.AnswerText.text = "us";

        Assert.AreEqual(0, agent.GetFinalReward());
    }

    [Test]
    public void GetFinalRewardCorrectTextPerfectSpeedTest()
    {
        var go = new GameObject();

        var agent = go.AddComponent<TypingAgentHumanCognitionSimpleNormalized>();
        agent._currentQnA = new()
        {
            Question = "",
            Answer = "us"
        };
        agent._typingMetrics = Substitute.For<ITypingMetrics>();
        agent._typingMetrics.GetFinalAccuracy().Returns(1);
        agent._typingMetrics
            .GetGrossWPM(Arg.Any<double>())
            .Returns(TypingAgentHumanCognitionSimpleNormalized.WPMMAX);
        agent._normalizedTypingRewardSum = 1;

        agent.AnswerText = new();
        agent.AnswerText.text = "us";

        Assert.AreEqual(TypingAgentHumanCognitionSimpleNormalized.SPEEDTYPINGREWARDRATIO, agent.GetFinalReward());
    }

    [Test]
    public void GetFinalRewardCorrectTextSlowSpeedTest()
    {
        var go = new GameObject();

        var agent = go.AddComponent<TypingAgentHumanCognitionSimpleNormalized>();
        agent._currentQnA = new()
        {
            Question = "",
            Answer = "us"
        };
        agent._typingMetrics = Substitute.For<ITypingMetrics>();
        agent._typingMetrics.GetFinalAccuracy().Returns(1);
        agent._typingMetrics
            .GetGrossWPM(Arg.Any<double>())
            .Returns(TypingAgentHumanCognitionSimpleNormalized.WPMMAX/2);
        agent._normalizedTypingRewardSum = 1;

        agent.AnswerText = new();
        agent.AnswerText.text = "us";

        Assert.AreEqual(TypingAgentHumanCognitionSimpleNormalized.SPEEDTYPINGREWARDRATIO * 0.5f, agent.GetFinalReward());
    }
}
