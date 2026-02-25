"""
pytest assertions for every cognitive3D data-stream parser.

Run:  pytest test_parsers.py -v
      pytest test_parsers.py -v -k "gaze"      # single stream
"""
import sys
from pathlib import Path

import numpy as np
import pandas as pd
import pytest

sys.path.insert(0, str(Path(__file__).parent))
from parsers import (
    COIN_EVENT_NAMES,
    parse_boundary,
    parse_dynamics,
    parse_events,
    parse_fixations,
    parse_gaze,
    parse_sensors,
)

# ── Test data location ─────────────────────────────────────────────────────────
DATA_ROOT = Path("/Users/arwaadib/Desktop/sap_data/participants/HG101")
SESSION_DIR = sorted(p for p in DATA_ROOT.iterdir() if p.is_dir())[0]
SESSION_ID = SESSION_DIR.name   # e.g. "1770234534_98b683447531a78ca23cd6c333403fd4"

KNOWN_SENSORS = {
    "c3d.hmd.yaw",
    "c3d.hmd.pitch",
    "c3d.controller.right.height.fromHMD",
    "c3d.controller.left.height.fromHMD",
    "c3d.fps.avg",
    "c3d.fps.1pl",
    "c3d.profiler.mainThreadTimeInMs",
    "c3d.profiler.systemMemoryInMB",
    "c3d.profiler.drawCallsCount",
    "c3d.app.WifiRSSI",
    "HMD Battery Level",
    "HMD Battery Status",
}

# ── Shared helpers ─────────────────────────────────────────────────────────────
QUAT_COLS = ["rot_x", "rot_y", "rot_z", "rot_w"]
POS_COLS  = ["pos_x", "pos_y", "pos_z"]
UNIX_EPOCH_FLOOR = 1_000_000_000.0   # ≈ year 2001 — sanity floor


def _file(prefix: str) -> Path:
    matches = list(SESSION_DIR.glob(f"{prefix}_*.json"))
    assert len(matches) == 1, f"Expected one {prefix}_*.json, found {matches}"
    return matches[0]


def _assert_finite(df: pd.DataFrame, cols: list[str]) -> None:
    for col in cols:
        assert df[col].notna().all(), f"'{col}' contains NaN"
        assert np.isfinite(df[col]).all(), f"'{col}' contains inf / -inf"


def _assert_unit_quaternions(df: pd.DataFrame, tol: float = 1e-3) -> None:
    norms = np.sqrt((df[QUAT_COLS] ** 2).sum(axis=1))
    max_err = (norms - 1.0).abs().max()
    assert max_err < tol, f"Quaternion not unit-normalised: max |‖q‖ − 1| = {max_err:.6f}"


# ══════════════════════════════════════════════════════════════════════════════
#  parse_gaze
# ══════════════════════════════════════════════════════════════════════════════
class TestParseGaze:
    @pytest.fixture(scope="class")
    def df(self) -> pd.DataFrame:
        return parse_gaze(_file("gaze"))

    # ── schema ─────────────────────────────────────────────────────────────────
    def test_returns_dataframe(self, df):
        assert isinstance(df, pd.DataFrame)

    def test_expected_columns_present(self, df):
        required = {
            "session_id", "time_s",
            "pos_x", "pos_y", "pos_z",
            "rot_x", "rot_y", "rot_z", "rot_w",
            "gaze_x", "gaze_y", "gaze_z",
        }
        missing = required - set(df.columns)
        assert not missing, f"Missing columns: {missing}"

    def test_no_extra_unexpected_nulls(self, df):
        assert df.notna().all().all(), "Unexpected NaN values in gaze DataFrame"

    # ── session identity ───────────────────────────────────────────────────────
    def test_session_id_dtype(self, df):
        assert pd.api.types.is_string_dtype(df["session_id"])

    def test_session_id_matches_filename(self, df):
        assert (df["session_id"] == SESSION_ID).all()

    # ── timestamps ─────────────────────────────────────────────────────────────
    def test_time_s_is_float(self, df):
        assert pd.api.types.is_float_dtype(df["time_s"])

    def test_time_s_in_unix_epoch_range(self, df):
        assert (df["time_s"] > UNIX_EPOCH_FLOOR).all(), \
            "time_s values are not Unix epoch seconds"

    def test_time_s_monotonic_increasing(self, df):
        assert df["time_s"].is_monotonic_increasing

    def test_gaze_interval_approx_100ms(self, df):
        """gazeInterval metadata specifies 0.1 s between samples."""
        dt = df["time_s"].diff().dropna()
        assert dt.median() == pytest.approx(0.1, abs=0.05), \
            f"Expected ~0.1 s gaze interval; got median={dt.median():.4f} s"

    def test_has_sufficient_rows(self, df):
        assert len(df) > 100, f"Unexpectedly few gaze samples: {len(df)}"

    # ── position ───────────────────────────────────────────────────────────────
    def test_hmd_position_finite(self, df):
        _assert_finite(df, POS_COLS)

    # ── rotation ───────────────────────────────────────────────────────────────
    def test_hmd_rotation_finite(self, df):
        _assert_finite(df, QUAT_COLS)

    def test_hmd_quaternion_unit_norm(self, df):
        _assert_unit_quaternions(df)

    # ── gaze vector ────────────────────────────────────────────────────────────
    def test_gaze_vector_finite(self, df):
        _assert_finite(df, ["gaze_x", "gaze_y", "gaze_z"])

    def test_gaze_vector_nonzero_for_majority(self, df):
        """Most samples should have a nonzero forward vector."""
        norms = np.sqrt(df["gaze_x"]**2 + df["gaze_y"]**2 + df["gaze_z"]**2)
        assert (norms > 0).mean() > 0.5, \
            "More than half of gaze forward vectors are zero — possible data dropout"


# ══════════════════════════════════════════════════════════════════════════════
#  parse_fixations
# ══════════════════════════════════════════════════════════════════════════════
class TestParseFixations:
    @pytest.fixture(scope="class")
    def df(self) -> pd.DataFrame:
        return parse_fixations(_file("fixation"))

    # ── schema ─────────────────────────────────────────────────────────────────
    def test_returns_dataframe(self, df):
        assert isinstance(df, pd.DataFrame)

    def test_expected_columns_present(self, df):
        required = {
            "session_id", "time_s", "duration_ms",
            "pos_x", "pos_y", "pos_z",
            "max_radius_rad", "object_id",
        }
        assert not (required - set(df.columns))

    def test_no_nulls(self, df):
        assert df.notna().all().all()

    def test_has_rows(self, df):
        assert len(df) > 0

    # ── session identity ───────────────────────────────────────────────────────
    def test_session_id_matches(self, df):
        assert (df["session_id"] == SESSION_ID).all()

    # ── timestamps ─────────────────────────────────────────────────────────────
    def test_time_s_in_unix_epoch_range(self, df):
        assert (df["time_s"] > UNIX_EPOCH_FLOOR).all()

    def test_time_s_monotonic_increasing(self, df):
        assert df["time_s"].is_monotonic_increasing

    # ── duration ───────────────────────────────────────────────────────────────
    def test_duration_ms_is_float(self, df):
        assert pd.api.types.is_float_dtype(df["duration_ms"])

    def test_duration_ms_positive(self, df):
        assert (df["duration_ms"] > 0).all(), "All fixation durations must be > 0 ms"

    def test_duration_ms_plausible_upper_bound(self, df):
        """No fixation should last > 30 s — would indicate a parsing unit error."""
        assert (df["duration_ms"] < 30_000).all(), \
            f"max duration_ms={df['duration_ms'].max()} — check units (ms, not s?)"

    # ── dispersion radius ──────────────────────────────────────────────────────
    def test_max_radius_rad_positive(self, df):
        assert (df["max_radius_rad"] > 0).all()

    def test_max_radius_rad_in_radian_range(self, df):
        """~0.17 rad per DATA_README — well below π/2.  Values > π/2 would
        suggest degrees were stored instead of radians."""
        assert (df["max_radius_rad"] < np.pi / 2).all(), \
            "max_radius_rad > π/2 — likely stored in degrees, not radians"

    # ── object attribution ─────────────────────────────────────────────────────
    def test_object_id_non_empty(self, df):
        assert df["object_id"].notna().all()
        assert (df["object_id"].str.len() > 0).all()

    def test_single_target_object(self, df):
        """DATA_README confirms all fixations hit the spaceship (one UUID)."""
        assert df["object_id"].nunique() == 1, \
            f"Expected 1 unique object_id (spaceship), got {df['object_id'].nunique()}"

    # ── position ───────────────────────────────────────────────────────────────
    def test_position_finite(self, df):
        _assert_finite(df, POS_COLS)


# ══════════════════════════════════════════════════════════════════════════════
#  parse_events
# ══════════════════════════════════════════════════════════════════════════════
class TestParseEvents:
    @pytest.fixture(scope="class")
    def df(self) -> pd.DataFrame:
        return parse_events(_file("events"))

    @pytest.fixture(scope="class")
    def coin_df(self, df) -> pd.DataFrame:
        return df[df["name"].isin(COIN_EVENT_NAMES)].copy()

    @pytest.fixture(scope="class")
    def spawned(self, df) -> pd.DataFrame:
        return df[df["name"] == "CoinSpawned"].copy()

    @pytest.fixture(scope="class")
    def collected(self, df) -> pd.DataFrame:
        return df[df["name"] == "CoinCollected"].copy()

    @pytest.fixture(scope="class")
    def observed(self, df) -> pd.DataFrame:
        return df[df["name"] == "CoinObserved"].copy()

    # ── schema ─────────────────────────────────────────────────────────────────
    def test_returns_dataframe(self, df):
        assert isinstance(df, pd.DataFrame)

    def test_expected_columns_present(self, df):
        required = {
            "session_id", "name", "time_s",
            "pos_x", "pos_y", "pos_z",
            "coin_id", "coin_type",
            "is_fast", "is_large", "point_value",
            "initial_eccentricity_deg", "current_eccentricity_deg",
            "eccentricity_range",
        }
        assert not (required - set(df.columns))

    def test_has_rows(self, df):
        assert len(df) > 0

    # ── session identity ───────────────────────────────────────────────────────
    def test_session_id_matches(self, df):
        assert (df["session_id"] == SESSION_ID).all()

    # ── timestamps ─────────────────────────────────────────────────────────────
    def test_time_s_is_float(self, df):
        assert pd.api.types.is_float_dtype(df["time_s"])

    def test_time_s_in_unix_epoch_range(self, df):
        assert (df["time_s"] > UNIX_EPOCH_FLOOR).all()

    def test_time_s_monotonic_increasing(self, df):
        assert df["time_s"].is_monotonic_increasing

    # ── session bookends ───────────────────────────────────────────────────────
    def test_session_start_present(self, df):
        assert "c3d.sessionStart" in df["name"].values

    def test_session_end_present(self, df):
        assert "c3d.sessionEnd" in df["name"].values

    def test_session_start_is_first_event(self, df):
        assert df.iloc[0]["name"] == "c3d.sessionStart"

    def test_session_end_is_last_event(self, df):
        assert df.iloc[-1]["name"] == "c3d.sessionEnd"

    # ── coin vs non-coin property nullability ──────────────────────────────────
    def test_coin_events_have_coin_id(self, coin_df):
        assert coin_df["coin_id"].notna().all(), \
            "Coin events must all carry a non-null coin_id"

    def test_non_coin_events_have_null_coin_id(self, df):
        non_coin = df[~df["name"].isin(COIN_EVENT_NAMES)]
        assert non_coin["coin_id"].isna().all(), \
            "Non-coin events must have null coin_id"

    # ── CoinSpawned completeness ───────────────────────────────────────────────
    def test_spawned_events_have_all_coin_props(self, spawned):
        for col in (
            "coin_id", "coin_type", "is_fast", "is_large",
            "point_value", "initial_eccentricity_deg", "eccentricity_range",
        ):
            assert spawned[col].notna().all(), \
                f"CoinSpawned missing required property '{col}'"

    def test_coin_types_are_known_codes(self, coin_df):
        valid = {"coinBF", "coinSF", "coinBS", "coinSS"}
        observed = set(coin_df["coin_type"].dropna())
        unknown = observed - valid
        assert not unknown, f"Unknown coin_type codes: {unknown}"

    def test_point_value_is_1_or_2(self, spawned):
        assert spawned["point_value"].isin([1.0, 2.0]).all(), \
            "pointValue must be exactly 1.0 or 2.0"

    def test_eccentricity_range_is_1_to_4(self, spawned):
        assert spawned["eccentricity_range"].between(1, 4).all(), \
            "eccentricityRange must be in 1–4"

    def test_initial_eccentricity_nonnegative(self, spawned):
        assert (spawned["initial_eccentricity_deg"] >= 0).all()

    # ── boolean columns ────────────────────────────────────────────────────────
    def test_is_fast_is_python_bool(self, coin_df):
        non_null = coin_df["is_fast"].dropna()
        assert non_null.map(lambda v: isinstance(v, bool)).all(), \
            "is_fast must contain Python bool (True/False), not int or string"

    def test_is_large_is_python_bool(self, coin_df):
        non_null = coin_df["is_large"].dropna()
        assert non_null.map(lambda v: isinstance(v, bool)).all()

    # ── inter-event consistency ────────────────────────────────────────────────
    def test_every_collected_coin_was_spawned(self, collected, spawned):
        collected_ids = set(collected["coin_id"])
        spawned_ids   = set(spawned["coin_id"])
        orphaned = collected_ids - spawned_ids
        assert not orphaned, \
            f"CoinCollected with no matching CoinSpawned: {orphaned}"

    def test_collected_timestamp_after_spawned(self, df):
        spawn_time   = df[df["name"] == "CoinSpawned"].set_index("coin_id")["time_s"]
        collect_time = df[df["name"] == "CoinCollected"].set_index("coin_id")["time_s"]
        shared = spawn_time.index.intersection(collect_time.index)
        assert (collect_time[shared] >= spawn_time[shared]).all(), \
            "CoinCollected time precedes CoinSpawned for some coins"

    def test_observed_timestamp_after_spawned(self, df):
        spawn_time    = df[df["name"] == "CoinSpawned"].set_index("coin_id")["time_s"]
        observed_time = (
            df[df["name"] == "CoinObserved"]
            .sort_values("time_s")
            .drop_duplicates("coin_id", keep="first")
            .set_index("coin_id")["time_s"]
        )
        shared = spawn_time.index.intersection(observed_time.index)
        assert (observed_time[shared] >= spawn_time[shared]).all(), \
            "First CoinObserved precedes CoinSpawned for some coins"

    def test_no_duplicate_spawned_per_coin(self, spawned):
        dups = spawned["coin_id"].duplicated()
        assert not dups.any(), \
            f"Duplicate CoinSpawned for coin_ids: {spawned.loc[dups, 'coin_id'].tolist()}"

    def test_spawns_exist(self, spawned):
        assert len(spawned) > 0, "No CoinSpawned events found"

    # ── position ───────────────────────────────────────────────────────────────
    def test_position_finite(self, df):
        _assert_finite(df, POS_COLS)


# ══════════════════════════════════════════════════════════════════════════════
#  parse_dynamics
# ══════════════════════════════════════════════════════════════════════════════
class TestParseDynamics:
    @pytest.fixture(scope="class")
    def df(self) -> pd.DataFrame:
        return parse_dynamics(_file("dynamics"))

    @pytest.fixture(scope="class")
    def manifest_objects(self, df) -> pd.DataFrame:
        """Rows whose object_id is a UUID (i.e. in the manifest, not a controller)."""
        return df[~df["object_id"].isin(["1", "2", "3", "4"])].copy()

    # ── schema ─────────────────────────────────────────────────────────────────
    def test_returns_dataframe(self, df):
        assert isinstance(df, pd.DataFrame)

    def test_expected_columns_present(self, df):
        required = {
            "session_id", "time_s", "object_id", "object_name",
            "pos_x", "pos_y", "pos_z",
            "rot_x", "rot_y", "rot_z", "rot_w",
            "scale_x", "scale_y", "scale_z", "enabled",
        }
        assert not (required - set(df.columns))

    def test_has_rows(self, df):
        assert len(df) > 0

    # ── session identity ───────────────────────────────────────────────────────
    def test_session_id_matches(self, df):
        assert (df["session_id"] == SESSION_ID).all()

    # ── timestamps ─────────────────────────────────────────────────────────────
    def test_time_s_in_unix_epoch_range(self, df):
        assert (df["time_s"] > UNIX_EPOCH_FLOOR).all()

    def test_time_s_is_float(self, df):
        assert pd.api.types.is_float_dtype(df["time_s"])

    # ── object identity ────────────────────────────────────────────────────────
    def test_object_id_non_null(self, df):
        assert df["object_id"].notna().all()

    def test_object_id_non_empty(self, df):
        assert (df["object_id"].str.len() > 0).all()

    def test_multiple_distinct_objects(self, df):
        assert df["object_id"].nunique() > 1

    def test_manifest_objects_have_object_name(self, manifest_objects):
        assert manifest_objects["object_name"].notna().all(), \
            "Objects in manifest must have a non-null object_name"

    # ── position ───────────────────────────────────────────────────────────────
    def test_position_finite(self, df):
        _assert_finite(df, POS_COLS)

    # ── rotation ───────────────────────────────────────────────────────────────
    def test_rotation_finite(self, df):
        _assert_finite(df, QUAT_COLS)

    def test_quaternion_unit_norm(self, df):
        _assert_unit_quaternions(df)

    # ── scale ──────────────────────────────────────────────────────────────────
    def test_scale_nonnegative_where_present(self, df):
        for col in ("scale_x", "scale_y", "scale_z"):
            non_null = df[col].dropna()
            assert (non_null >= 0).all(), f"Negative scale found in '{col}'"

    # ── enabled flag ───────────────────────────────────────────────────────────
    def test_enabled_is_python_bool_where_present(self, df):
        non_null = df["enabled"].dropna()
        assert non_null.map(lambda v: isinstance(v, bool)).all(), \
            "enabled must be Python bool (True/False)"


# ══════════════════════════════════════════════════════════════════════════════
#  parse_sensors
# ══════════════════════════════════════════════════════════════════════════════
class TestParseSensors:
    @pytest.fixture(scope="class")
    def df(self) -> pd.DataFrame:
        return parse_sensors(_file("sensors"))

    # ── schema ─────────────────────────────────────────────────────────────────
    def test_returns_dataframe(self, df):
        assert isinstance(df, pd.DataFrame)

    def test_expected_columns_present(self, df):
        assert {"session_id", "sensor", "time_s", "value"}.issubset(df.columns)

    def test_has_rows(self, df):
        assert len(df) > 0

    # ── session identity ───────────────────────────────────────────────────────
    def test_session_id_matches(self, df):
        assert (df["session_id"] == SESSION_ID).all()

    # ── timestamps ─────────────────────────────────────────────────────────────
    def test_time_s_is_float(self, df):
        assert pd.api.types.is_float_dtype(df["time_s"])

    def test_time_s_in_unix_epoch_range(self, df):
        assert (df["time_s"] > UNIX_EPOCH_FLOOR).all()

    def test_time_s_monotonic_within_each_sensor(self, df):
        for sensor_name, group in df.groupby("sensor"):
            assert group["time_s"].is_monotonic_increasing, \
                f"time_s not monotonically increasing for sensor '{sensor_name}'"

    # ── sensor channels ────────────────────────────────────────────────────────
    def test_no_unknown_sensor_names(self, df):
        unknown = set(df["sensor"]) - KNOWN_SENSORS
        assert not unknown, f"Unknown sensor channel names: {unknown}"

    def test_all_observed_sensors_are_known(self, df):
        """Any channel present in the file must be in our documented sensor list.
        Not all sessions record every channel (e.g. WifiRSSI absent on ethernet),
        so we check the direction: observed ⊆ known, not known ⊆ observed."""
        unknown = set(df["sensor"]) - KNOWN_SENSORS
        assert not unknown, f"Undocumented sensor channel names: {unknown}"

    def test_each_sensor_has_multiple_samples(self, df):
        counts = df.groupby("sensor").size()
        single = counts[counts < 2].index.tolist()
        assert not single, f"Sensors with fewer than 2 samples: {single}"

    def test_approx_1hz_sampling_per_sensor(self, df):
        # Most channels are ~1 Hz. HMD Battery Level/Status are ~0.1 Hz (10 s).
        # Use a 15 s ceiling to catch genuine drop-outs while allowing slow channels.
        for sensor_name, group in df.groupby("sensor"):
            if len(group) < 2:
                continue
            median_dt = group["time_s"].diff().dropna().median()
            assert median_dt < 15.0, \
                f"Sensor '{sensor_name}' median interval {median_dt:.2f} s > 15 s — possible data gap"

    # ── values ─────────────────────────────────────────────────────────────────
    def test_value_is_float_dtype(self, df):
        assert pd.api.types.is_float_dtype(df["value"])

    def test_value_no_nulls(self, df):
        assert df["value"].notna().all()

    def test_fps_sensors_nonnegative(self, df):
        fps = df[df["sensor"].str.startswith("c3d.fps")]["value"]
        if len(fps) > 0:
            assert (fps >= 0).all(), "FPS sensor values must be >= 0"

    def test_hmd_yaw_in_degree_range(self, df):
        yaw = df[df["sensor"] == "c3d.hmd.yaw"]["value"]
        if len(yaw) > 0:
            assert yaw.between(-360, 360).all(), \
                "c3d.hmd.yaw values out of ±360° range — check units"

    def test_hmd_pitch_in_degree_range(self, df):
        pitch = df[df["sensor"] == "c3d.hmd.pitch"]["value"]
        if len(pitch) > 0:
            assert pitch.between(-90, 90).all(), \
                "c3d.hmd.pitch values out of ±90° range — check units"

    def test_battery_level_0_to_100(self, df):
        batt = df[df["sensor"] == "HMD Battery Level"]["value"]
        if len(batt) > 0:
            assert batt.between(0, 100).all(), \
                "HMD Battery Level outside 0–100 % range"

    def test_memory_mb_positive(self, df):
        mem = df[df["sensor"] == "c3d.profiler.systemMemoryInMB"]["value"]
        if len(mem) > 0:
            assert (mem > 0).all(), "systemMemoryInMB must be positive"


# ══════════════════════════════════════════════════════════════════════════════
#  parse_boundary
# ══════════════════════════════════════════════════════════════════════════════
class TestParseBoundary:
    @pytest.fixture(scope="class")
    def df(self) -> pd.DataFrame:
        return parse_boundary(_file("boundary"))

    # ── schema ─────────────────────────────────────────────────────────────────
    def test_returns_dataframe(self, df):
        assert isinstance(df, pd.DataFrame)

    def test_expected_columns_present(self, df):
        required = {
            "session_id", "time_s",
            "pos_x", "pos_y", "pos_z",
            "rot_x", "rot_y", "rot_z", "rot_w",
        }
        assert not (required - set(df.columns))

    def test_no_nulls(self, df):
        assert df.notna().all().all()

    def test_has_at_least_one_row(self, df):
        assert len(df) >= 1, \
            "Boundary file must contain at least one snapshot"

    # ── session identity ───────────────────────────────────────────────────────
    def test_session_id_matches(self, df):
        assert (df["session_id"] == SESSION_ID).all()

    # ── timestamps ─────────────────────────────────────────────────────────────
    def test_time_s_is_float(self, df):
        assert pd.api.types.is_float_dtype(df["time_s"])

    def test_time_s_in_unix_epoch_range(self, df):
        assert (df["time_s"] > UNIX_EPOCH_FLOOR).all()

    def test_no_duplicate_timestamps(self, df):
        assert not df["time_s"].duplicated().any()

    # ── position ───────────────────────────────────────────────────────────────
    def test_position_finite(self, df):
        _assert_finite(df, POS_COLS)

    # ── rotation ───────────────────────────────────────────────────────────────
    def test_rotation_finite(self, df):
        _assert_finite(df, QUAT_COLS)

    def test_quaternion_unit_norm(self, df):
        _assert_unit_quaternions(df)
