"""
pytest assertions for segment_trials().

Run:  pytest test_segment_trials.py -v
      pytest test_segment_trials.py -v -k "temporal"

The module-scope fixture runs segment_trials once for the full HG101
dataset (16 sessions, 1793 trials) and caches results for all tests.
Expected runtime: 30–90 s on first run (JSON parsing + proximity matching
for 16 × 120 coins × ~122 dynamics objects = ~235 k comparisons).
"""
import sys
from pathlib import Path

import numpy as np
import pandas as pd
import pytest

sys.path.insert(0, str(Path(__file__).parent))
from segment_trials import VALID_CONDITIONS, segment_trials

# ── Test data location & ground truth ─────────────────────────────────────────
DATA_ROOT   = Path("/Users/arwaadib/Desktop/sap_data/participants/HG101")
VALID_COIN_TYPES  = {"coinBF", "coinSF", "coinBS", "coinSS"}
VALID_OUTCOMES    = {"collected", "destroyed", "unknown"}
EXPECTED_TRIAL_COUNT = 1793     # verified from raw CoinSpawned counts
REQUIRED_TRIAL_KEYS = {
    "trial_id", "participant_id", "session_id", "condition", "repetition",
    "coin_id", "coin_type", "is_fast", "is_large", "point_value",
    "eccentricity_range", "spawn_time_s", "end_time_s", "outcome", "streams",
}
REQUIRED_STREAM_KEYS = {"events", "gaze", "fixations", "dynamics", "sensors"}


def _make_test_condition_map() -> dict[str, str]:
    """Assign conditions cyclically c1–c6 in chronological session order.
    This is a structural test fixture only — NOT the real experimental mapping.
    """
    sessions   = sorted(
        [p for p in DATA_ROOT.iterdir() if p.is_dir()],
        key=lambda p: int(p.name.split("_")[0]),
    )
    conditions = ["c1", "c2", "c3", "c4", "c5", "c6"]
    return {s.name: conditions[i % 6] for i, s in enumerate(sessions)}


# ── Fixtures ───────────────────────────────────────────────────────────────────

@pytest.fixture(scope="module")
def trials() -> dict:
    return segment_trials("HG 101", condition_map=_make_test_condition_map())


@pytest.fixture(scope="module")
def summary(trials) -> pd.DataFrame:
    """One-row-per-trial DataFrame of scalar metadata + stream sizes.
    Used for vectorised bulk assertions without re-iterating trial dicts.
    """
    rows = []
    for tid, t in trials.items():
        gaze = t["streams"]["gaze"]
        sens = t["streams"]["sensors"]
        dyn  = t["streams"]["dynamics"]
        rows.append({
            "trial_id":           tid,
            "participant_id":     t["participant_id"],
            "session_id":         t["session_id"],
            "condition":          t["condition"],
            "repetition":         t["repetition"],
            "coin_id":            t["coin_id"],
            "coin_type":          t["coin_type"],
            "is_fast":            t["is_fast"],
            "is_large":           t["is_large"],
            "point_value":        t["point_value"],
            "eccentricity_range": t["eccentricity_range"],
            "spawn_time_s":       t["spawn_time_s"],
            "end_time_s":         t["end_time_s"],
            "outcome":            t["outcome"],
            "n_events":           len(t["streams"]["events"]),
            "n_gaze":             len(gaze),
            "n_fixations":        len(t["streams"]["fixations"]),
            "n_dynamics":         len(dyn),
            "n_sensors":          len(sens),
            # stream temporal bounds (NaN when slice is empty)
            "gaze_min_t":  gaze["time_s"].min()  if len(gaze) else float("nan"),
            "gaze_max_t":  gaze["time_s"].max()  if len(gaze) else float("nan"),
            "sens_min_t":  sens["time_s"].min()  if len(sens) else float("nan"),
            "sens_max_t":  sens["time_s"].max()  if len(sens) else float("nan"),
            "dyn_n_obj":   dyn["object_id"].nunique() if len(dyn) else 0,
        })
    return pd.DataFrame(rows)


@pytest.fixture(scope="module")
def sample_trials(trials) -> list[dict]:
    """First 30 trials for stream-content spot checks."""
    return list(trials.values())[:30]


# ══════════════════════════════════════════════════════════════════════════════
#  Top-level structure
# ══════════════════════════════════════════════════════════════════════════════
class TestStructure:
    def test_returns_dict(self, trials):
        assert isinstance(trials, dict)

    def test_expected_trial_count(self, trials):
        assert len(trials) == EXPECTED_TRIAL_COUNT, \
            f"Expected {EXPECTED_TRIAL_COUNT} trials, got {len(trials)}"

    def test_all_keys_are_strings(self, trials):
        assert all(isinstance(k, str) for k in trials)

    def test_all_values_are_dicts(self, trials):
        assert all(isinstance(v, dict) for v in trials.values())

    def test_required_trial_keys_present(self, trials):
        for tid, t in list(trials.items())[:10]:
            missing = REQUIRED_TRIAL_KEYS - set(t.keys())
            assert not missing, f"Trial {tid[:8]}… missing keys: {missing}"

    def test_required_stream_keys_present(self, trials):
        for tid, t in list(trials.items())[:10]:
            missing = REQUIRED_STREAM_KEYS - set(t["streams"].keys())
            assert not missing, f"Trial {tid[:8]}… missing stream keys: {missing}"

    def test_all_stream_values_are_dataframes(self, trials):
        for tid, t in list(trials.items())[:20]:
            for key, val in t["streams"].items():
                assert isinstance(val, pd.DataFrame), \
                    f"Trial {tid[:8]}… streams['{key}'] is {type(val).__name__}, not DataFrame"


# ══════════════════════════════════════════════════════════════════════════════
#  Scalar metadata (vectorised over summary DataFrame)
# ══════════════════════════════════════════════════════════════════════════════
class TestMetadata:
    def test_trial_id_equals_coin_id(self, summary):
        assert (summary["trial_id"] == summary["coin_id"]).all()

    def test_trial_id_equals_dict_key(self, trials, summary):
        keys = set(trials.keys())
        assert set(summary["trial_id"]) == keys

    def test_condition_is_valid(self, summary):
        bad = set(summary["condition"]) - VALID_CONDITIONS
        assert not bad, f"Invalid condition values: {bad}"

    def test_repetition_is_positive_integer(self, summary):
        assert (summary["repetition"] >= 1).all()
        assert summary["repetition"].apply(lambda v: isinstance(v, (int, np.integer))).all()

    def test_outcome_is_valid(self, summary):
        bad = set(summary["outcome"]) - VALID_OUTCOMES
        assert not bad, f"Invalid outcome values: {bad}"

    def test_coin_type_is_known(self, summary):
        bad = set(summary["coin_type"]) - VALID_COIN_TYPES
        assert not bad, f"Unknown coin_type values: {bad}"

    def test_is_fast_is_python_bool(self, summary):
        assert summary["is_fast"].map(lambda v: isinstance(v, bool)).all()

    def test_is_large_is_python_bool(self, summary):
        assert summary["is_large"].map(lambda v: isinstance(v, bool)).all()

    def test_point_value_is_1_or_2(self, summary):
        assert summary["point_value"].isin([1.0, 2.0]).all()

    def test_eccentricity_range_in_1_to_4(self, summary):
        assert summary["eccentricity_range"].between(1, 4).all()

    def test_participant_id_correct(self, summary):
        assert (summary["participant_id"] == "HG 101").all()

    def test_no_null_metadata(self, summary):
        meta_cols = [
            "trial_id", "participant_id", "session_id", "condition",
            "repetition", "coin_id", "coin_type", "point_value",
            "eccentricity_range", "spawn_time_s", "end_time_s", "outcome",
        ]
        for col in meta_cols:
            assert summary[col].notna().all(), f"Column '{col}' has null values"


# ══════════════════════════════════════════════════════════════════════════════
#  Temporal integrity
# ══════════════════════════════════════════════════════════════════════════════
class TestTemporalIntegrity:
    def test_spawn_before_end(self, summary):
        assert (summary["spawn_time_s"] < summary["end_time_s"]).all(), \
            "spawn_time_s must be strictly before end_time_s for every trial"

    def test_trial_duration_positive(self, summary):
        durations = summary["end_time_s"] - summary["spawn_time_s"]
        assert (durations > 0).all()

    def test_trial_duration_plausible(self, summary):
        """No trial should last > 120 s (full session is ~103 s in pilot)."""
        durations = summary["end_time_s"] - summary["spawn_time_s"]
        assert (durations < 120).all(), \
            f"Max trial duration {durations.max():.1f} s exceeds session length"

    def test_gaze_slice_within_trial_window(self, summary):
        """All gaze samples in the slice must be inside [spawn_time_s, end_time_s]."""
        has_gaze = summary["n_gaze"] > 0
        sub = summary[has_gaze]
        assert (sub["gaze_min_t"] >= sub["spawn_time_s"]).all(), \
            "gaze_min_t < spawn_time_s — gaze slice extends before trial start"
        assert (sub["gaze_max_t"] <= sub["end_time_s"]).all(), \
            "gaze_max_t > end_time_s — gaze slice extends after trial end"

    def test_sensors_slice_within_trial_window(self, summary):
        has_sens = summary["n_sensors"] > 0
        sub = summary[has_sens]
        assert (sub["sens_min_t"] >= sub["spawn_time_s"]).all()
        assert (sub["sens_max_t"] <= sub["end_time_s"]).all()

    def test_all_trials_have_gaze(self, summary):
        """Every trial window should contain at least one gaze sample (~10 Hz)."""
        empty = summary[summary["n_gaze"] == 0]
        assert len(empty) == 0, \
            f"{len(empty)} trials have zero gaze samples — check trial window or gaze file"

    def test_most_trials_have_sensors(self, summary):
        """Most trial windows should contain at least one sensor reading (~1 Hz).
        Very short trials may have zero sensor readings — allow up to 5 %."""
        frac_empty = (summary["n_sensors"] == 0).mean()
        assert frac_empty < 0.05, \
            f"{frac_empty:.1%} of trials have zero sensor readings (expected < 5%)"


# ══════════════════════════════════════════════════════════════════════════════
#  Events stream integrity
# ══════════════════════════════════════════════════════════════════════════════
class TestEventsStream:
    def test_events_stream_non_empty(self, sample_trials):
        for t in sample_trials:
            assert len(t["streams"]["events"]) > 0, \
                f"Trial {t['trial_id'][:8]}… has zero events (at minimum CoinSpawned expected)"

    def test_events_stream_has_spawned_row(self, sample_trials):
        for t in sample_trials:
            ev = t["streams"]["events"]
            assert (ev["name"] == "CoinSpawned").any(), \
                f"Trial {t['trial_id'][:8]}… events stream missing CoinSpawned"

    def test_events_stream_exactly_one_spawned(self, sample_trials):
        for t in sample_trials:
            ev = t["streams"]["events"]
            n = (ev["name"] == "CoinSpawned").sum()
            assert n == 1, \
                f"Trial {t['trial_id'][:8]}… has {n} CoinSpawned rows (expected 1)"

    def test_events_stream_coin_id_consistent(self, sample_trials):
        """All rows in the events slice must carry this trial's coin_id."""
        for t in sample_trials:
            ev = t["streams"]["events"]
            assert (ev["coin_id"] == t["coin_id"]).all(), \
                f"Trial {t['trial_id'][:8]}… events contain foreign coin_id"

    def test_spawned_time_matches_spawn_time_s(self, sample_trials):
        for t in sample_trials:
            ev = t["streams"]["events"]
            spawn_row = ev[ev["name"] == "CoinSpawned"].iloc[0]
            assert spawn_row["time_s"] == pytest.approx(t["spawn_time_s"], abs=1e-6), \
                "CoinSpawned time_s in events stream != trial spawn_time_s"

    def test_collected_outcome_has_collected_event(self, trials):
        for t in trials.values():
            if t["outcome"] == "collected":
                ev = t["streams"]["events"]
                assert (ev["name"] == "CoinCollected").any(), \
                    f"Trial {t['trial_id'][:8]}… outcome=collected but no CoinCollected event"

    def test_destroyed_outcome_has_destroyed_event(self, trials):
        for t in trials.values():
            if t["outcome"] == "destroyed":
                ev = t["streams"]["events"]
                assert (ev["name"] == "CoinDestroyed").any(), \
                    f"Trial {t['trial_id'][:8]}… outcome=destroyed but no CoinDestroyed event"

    def test_outcome_unknown_fraction_is_small(self, summary):
        """'unknown' outcome = coin was still airborne when c3d.sessionEnd fired.
        This is expected for a small fraction of trials at each session boundary.
        Verified: 24/1793 (1.3%) across HG101.  Flag if > 5 %."""
        frac = (summary["outcome"] == "unknown").mean()
        assert frac < 0.05, \
            f"{frac:.1%} of trials are 'unknown' — exceeds 5 % threshold; check session truncation"

    def test_each_session_has_trials(self, summary):
        sessions = summary["session_id"].unique()
        assert len(sessions) == 16, f"Expected 16 sessions, got {len(sessions)}"


# ══════════════════════════════════════════════════════════════════════════════
#  Dynamics stream integrity
# ══════════════════════════════════════════════════════════════════════════════
class TestDynamicsStream:
    def test_dynamics_match_rate_near_100pct(self, summary):
        """Position-proximity matching should succeed for ≥ 99 % of trials."""
        matched = (summary["n_dynamics"] > 0).mean()
        assert matched >= 0.99, \
            f"Dynamics match rate {matched:.1%} < 99 % — proximity matching may be broken"

    def test_dynamics_slice_single_object(self, sample_trials):
        """Each dynamics slice must contain snapshots for exactly one object_id."""
        for t in sample_trials:
            dyn = t["streams"]["dynamics"]
            if len(dyn) == 0:
                continue
            n_obj = dyn["object_id"].nunique()
            assert n_obj == 1, \
                f"Trial {t['trial_id'][:8]}… dynamics slice has {n_obj} distinct object_ids (expected 1)"

    def test_dynamics_object_name_matches_coin_type(self, sample_trials):
        """The dynamics object name should contain the coin's type prefix."""
        for t in sample_trials:
            dyn = t["streams"]["dynamics"]
            if len(dyn) == 0:
                continue
            obj_name = dyn["object_name"].iloc[0]
            # e.g. coin_type "coinBF" → object_name "coinBF(Clone)" or "coinBF"
            assert t["coin_type"].lower() in str(obj_name).lower(), \
                f"Trial {t['trial_id'][:8]}… dynamics object '{obj_name}' " \
                f"doesn't match coin_type '{t['coin_type']}'"

    def test_dynamics_within_trial_window(self, sample_trials):
        for t in sample_trials:
            dyn = t["streams"]["dynamics"]
            if len(dyn) == 0:
                continue
            assert (dyn["time_s"] >= t["spawn_time_s"]).all()
            assert (dyn["time_s"] <= t["end_time_s"]).all()


# ══════════════════════════════════════════════════════════════════════════════
#  Repetition structure
# ══════════════════════════════════════════════════════════════════════════════
class TestRepetitionStructure:
    def test_repetitions_start_at_1_per_condition(self, summary):
        for cond, group in summary.groupby("condition"):
            sessions = group.drop_duplicates("session_id").sort_values("session_id")
            min_rep = sessions["repetition"].min()
            assert min_rep == 1, \
                f"Condition '{cond}' repetitions start at {min_rep}, expected 1"

    def test_repetitions_consecutive_per_condition(self, summary):
        """No gaps in repetition numbers within a condition."""
        for cond, group in summary.groupby("condition"):
            reps = sorted(group["session_id"].map(
                group.drop_duplicates("session_id").set_index("session_id")["repetition"]
            ).unique())
            expected = list(range(1, len(reps) + 1))
            assert reps == expected, \
                f"Condition '{cond}' repetitions {reps} are not consecutive from 1"

    def test_all_16_sessions_represented(self, summary):
        assert summary["session_id"].nunique() == 16

    def test_each_session_maps_to_one_condition(self, summary):
        per_session = summary.groupby("session_id")["condition"].nunique()
        assert (per_session == 1).all(), \
            "Some sessions map to more than one condition"

    def test_each_session_maps_to_one_repetition(self, summary):
        per_session = summary.groupby("session_id")["repetition"].nunique()
        assert (per_session == 1).all()
