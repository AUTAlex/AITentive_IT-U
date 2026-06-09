using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;

using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = System.Random;

namespace Supervisor
{
    public class SupervisorAgentRandom : SupervisorAgent, ISupervisorAgentRandom
    {
        [field: SerializeField, Tooltip("Defines the range around the DecisionRequestIntervalInSeconds in which the agent should perform an action. " +
            "For example if the range and DecisionRequestIntervalInSeconds are equal to 1 then decisions are requested between 0.5 and 1.5 seconds."), ProjectAssign]
        public float DecisionRequestIntervalRangeInSeconds { get; set; }


        private readonly Random _rand = new();

        private double _chosenInterval = 0;


        //The active instance will be selected according to the selected action of the agent. The Reward will be increased every x seconds like
        //defined in DecisionRequestIntervalInSeconds. If the Ball fell off a platform, the episode ends and a negative reward is given.
        public override void OnActionReceived(ActionBuffers actionBuffers)
        {
            int action = UsesHeuristic ? actionBuffers.DiscreteActions[0] : GetRandomNotIdleTask();

            Act(action);
            ResolveInteraction(action);
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask) {}


        protected override void OnEnable()
        {
            base.OnEnable();

            SupervisorAgent.OnTaskSwitchCompleted += UpdateSwitchingInterval;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SupervisorAgent.OnTaskSwitchCompleted -= UpdateSwitchingInterval;
        }

        protected override void RequestInteractionAfterInterval()
        {
            bool hasIntervalExpired = _fixedUpdateTimer > _chosenInterval + AdvanceNoticeInSeconds;

            if (SetConstantDecisionRequestInterval)
            {
                RequestInteractionAfterConstantInterval(hasIntervalExpired);
            }
            else
            {
                RequestInteractionAfterVariableInterval(hasIntervalExpired);
            }
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            sensor.AddObservation(0); //Dummy Value;
        }


        private new void Awake()
        {
            base.Awake();
            AssignDummyModel();
            UpdateSwitchingInterval();
        }

        private void UpdateSwitchingInterval(double timeBetweenSwitches = 0, int targetTask = default, bool isNewEpisode = false, bool wasDecisionRequestedBySystem = false, Switcher switcher = default)
        {
            _chosenInterval = DecisionRequestIntervalInSeconds + _rand.NextDouble() * DecisionRequestIntervalRangeInSeconds - DecisionRequestIntervalRangeInSeconds / 2;

            //Give the player time to react. If the next switch would be fast again, it could be the case that the player had not enough time to react
            //which leads to no measurement of a reaction time.
            if (timeBetweenSwitches < 0.8)
            {
                _chosenInterval = 1.5;
            }

            Debug.Log(string.Format("Time between task switches: {0}", timeBetweenSwitches));
            Debug.Log(string.Format("New update interval: {0}", _chosenInterval));
        }

        private void AssignDummyModel()
        {
            if (enabled) 
            {
                var model = Resources.Load<Unity.InferenceEngine.ModelAsset>("AUI");

                BehaviorParameters behaviorParameters = GetComponent<BehaviorParameters>();

                behaviorParameters.Model = model;
                behaviorParameters.BrainParameters.VectorObservationSize = 1;
            }
        }

        private int GetRandomNotIdleTask()
        {
            List<ITask> NonIdleTasks = Tasks.ToList().FindAll(task => !task.IsIdle);
            int i = _rand.Next(0, NonIdleTasks.Count);

            if (NonIdleTasks.IsNullOrEmpty())
            {
                return 0;
            }

            return GetTaskNumber(NonIdleTasks[i]);
        }
    }
}