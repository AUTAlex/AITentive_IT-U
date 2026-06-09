# Hyperparameter Optimization (HPO)

This repository supports **hyperparameter optimization** using [Ray Tune](https://docs.ray.io/en/latest/tune/index.html) integrated with Unity ML-Agents.  
It allows running multiple ML-Agents trainings in parallel with different hyperparameter configurations, automatically tracking metrics, managing early stopping, and selecting the best-performing configuration.


## Usage

To start the hyperparameter optimization, change to the `config` directory and run:

```
python train.py hpo_config/hpo_H0_TypingAgentPixel.yaml session_config/H0_2025_TypingAgent_single.json HPO_Session
```

Each Ray Tune trial launches its own Unity ML-Agents training instance.  
The optimization progress and intermediate results are reported to both the **Ray dashboard** and **TensorBoard**.


## Configuration Files

Two configuration files are involved in the HPO process:

1. **PPO Template YAML** – defines the base ML-Agents configuration with placeholders for tunable parameters.  
2. **HPO Configuration YAML** – defines the hyperparameter search space, optimization settings, and experiment metadata.


### PPO Template Example

File: `hpo_config/ppo_H0_TypingAgentPixel.yaml`

This file defines all agent behaviors and tunable placeholders (marked with `__PLACEHOLDER__`), which will be replaced by sampled values during optimization.

```
behaviors:
  Focus:
    trainer_type: ppo
    hyperparameters:
      batch_size: __FOCUS_BATCH_SIZE__
      buffer_size: __FOCUS_BUFFER_SIZE__
      learning_rate: __FOCUS_LR__
      beta: __FOCUS_BETA__
      epsilon: __FOCUS_EPSILON__
      lambd: __FOCUS_LAMBD__
      num_epoch: 3
      learning_rate_schedule: __FOCUS_LR_SCHEDULE__
    network_settings:
      normalize: true
      hidden_units: __FOCUS_HIDDEN_UNITS__
      num_layers: __FOCUS_NUM_LAYERS__
      vis_encode_type: nature_cnn
    reward_signals:
      extrinsic:
        gamma: __FOCUS_GAMMA__
        strength: 1.0
    keep_checkpoints: 5
    max_steps: __MAX_STEPS__
    time_horizon: __FOCUS_TIME_HORIZON__
    summary_freq: 2000

  TypingAgent:
    trainer_type: ppo
    hyperparameters:
      batch_size: __TYP_BATCH_SIZE__
      buffer_size: __TYP_BUFFER_SIZE__
      learning_rate: __TYP_LR__
      beta: __TYP_BETA__
      epsilon: __TYP_EPSILON__
      lambd: __TYP_LAMBD__
      num_epoch: 3
      learning_rate_schedule: __TYP_LR_SCHEDULE__
    network_settings:
      normalize: false
      hidden_units: __TYP_HIDDEN_UNITS__
      num_layers: __TYP_NUM_LAYERS__
    reward_signals:
      extrinsic:
        gamma: __TYP_GAMMA__
        strength: 1.0
    keep_checkpoints: 5
    max_steps: __MAX_STEPS__
    time_horizon: __TYP_TIME_HORIZON__
    summary_freq: 100

environment_parameters:
  FullVision:
    curriculum:
      - name: Lesson0
        completion_criteria:
          measure: reward
          behavior: TypingAgent
          threshold: 0.8
          min_lesson_length: 12000
        value: 1
      - name: Lesson1
        completion_criteria:
          measure: reward
          behavior: TypingAgent
          threshold: 2.5
          min_lesson_length: 12000
        value: 1
      - name: Lesson2
        completion_criteria:
          measure: reward
          behavior: TypingAgent
          threshold: 10
          min_lesson_length: 12000
        value: 1
      - name: Lesson3
        completion_criteria:
          measure: reward
          behavior: TypingAgent
          threshold: 32.5
          min_lesson_length: 12000
        value: 0
      - name: Lesson4
        completion_criteria:
          measure: reward
          behavior: TypingAgent
          threshold: 115
          min_lesson_length: 12000
        value: 0
      - name: Lesson5
        value: 0
```


### HPO Configuration Example

File: `hpo_config/hpo_H0_TypingAgentPixel.yaml`

This file defines how Ray Tune explores the hyperparameter space.  
Each key under `search_space` corresponds to one placeholder in the PPO template.

```
hpo:
  template_yaml: "hpo_config/ppo_H0_TypingAgentPixel.yaml"

  metric_tag: "TypingAgent/Environment/Cumulative Reward"
  direction: "maximize"

  num_samples: 40

  sampler: "optuna"
  scheduler: "asha"
  grace_iter: 3

  run_name: "hpo_focus_typing"
  local_dir: "ray_results"

  time_scale: 20
  max_steps_per_trial: 3000000
  per_trial_num_envs: 1

  search_space:
    FOCUS_BATCH_SIZE:
      type: choice
      values: [512, 1024, 2048, 4096]
    FOCUS_BUFFER_SIZE:
      type: choice
      values: [65536, 131072, 262144]
    FOCUS_LR:
      type: loguniform
      low: 1e-5
      high: 3e-3
    FOCUS_BETA:
      type: loguniform
      low: 1e-4
      high: 1e-2
    FOCUS_EPSILON:
      type: uniform
      low: 0.05
      high: 0.3
    FOCUS_LAMBD:
      type: uniform
      low: 0.85
      high: 0.99
    FOCUS_HIDDEN_UNITS:
      type: choice
      values: [128, 256, 512]
    FOCUS_NUM_LAYERS:
      type: choice
      values: [2, 3, 4]
    FOCUS_GAMMA:
      type: uniform
      low: 0.95
      high: 0.999
    FOCUS_TIME_HORIZON:
      type: choice
      values: [128, 256, 512]
    FOCUS_LR_SCHEDULE:
      type: choice
      values: ["constant", "linear"]

    TYP_BATCH_SIZE:
      type: choice
      values: [512, 1024, 2048, 4096]
    TYP_BUFFER_SIZE:
      type: choice
      values: [65536, 131072, 262144]
    TYP_LR:
      type: loguniform
      low: 1e-5
      high: 3e-3
    TYP_BETA:
      type: loguniform
      low: 1e-4
      high: 1e-2
    TYP_EPSILON:
      type: uniform
      low: 0.05
      high: 0.3
    TYP_LAMBD:
      type: uniform
      low: 0.85
      high: 0.99
    TYP_HIDDEN_UNITS:
      type: choice
      values: [64, 128, 256, 512]
    TYP_NUM_LAYERS:
      type: choice
      values: [2, 3, 4]
    TYP_GAMMA:
      type: uniform
      low: 0.95
      high: 0.999
    TYP_TIME_HORIZON:
      type: choice
      values: [128, 256, 512]
    TYP_LR_SCHEDULE:
      type: choice
      values: ["constant", "linear"]
```


## Monitoring Progress

During execution, progress can be tracked in real time via:

**Ray Dashboard**
```
http://127.0.0.1:8265
```

**TensorBoard**
```
tensorboard --logdir C:\Users\<USER>\AppData\Local\Temp\ray
```

Each trial corresponds to a separate ML-Agents training run with its own parameter combination.  
The temporary results and logs are stored under:

```
C:\Users\<USER>\AppData\Local\Temp\ray
```


## Example Output

```
Trial __trainable_78e7f5bf completed after 3 iterations
Best trial found for metric 'reward':
  reward: 2.317
  config: {'TYP_LR': 0.0001, 'TYP_HIDDEN_UNITS': 256, ...}
```


## Notes

- The `metric_tag` must correspond to a valid TensorBoard scalar (e.g., `"TypingAgent/Environment/Cumulative Reward"`).
- All intermediate files and trial outputs are stored under the system temp directory.
- You can disable early stopping by setting:
  ```
  scheduler: none
  ```
- To inspect rewards and learning curves, open TensorBoard and select the relevant trial tags.
