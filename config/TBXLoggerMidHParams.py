# --- imports ----------------------------------------------------
import numpy as np
from ray.tune.logger.tensorboardx import TBXLoggerCallback, VALID_SUMMARY_TYPES
from ray.tune.result import TIME_TOTAL_S, TIMESTEPS_TOTAL
from ray.air.constants import TRAINING_ITERATION
from ray.tune.utils import flatten_dict
# ----------------------------------------------------------------

class TBXLoggerMidHParams(TBXLoggerCallback):
    """
    Write HParams once, mid-run, using ALL numeric metrics from the *current*
    result (so it's never empty). Mirrors parent prefixes ('ray/tune/...').
    """
    def __init__(self):
        super().__init__()


    def log_trial_result(self, iteration, trial, result):
        super().log_trial_result(iteration, trial, result)

        if trial in self._trial_writer:
            if trial and trial.evaluated_params and self._trial_result[trial]:
                flat_result = flatten_dict(self._trial_result[trial], delimiter="/")
                scrubbed_result = {
                    k: value
                    for k, value in flat_result.items()
                    if isinstance(value, tuple(VALID_SUMMARY_TYPES))
                }

                self._try_log_hparams(trial, scrubbed_result)
