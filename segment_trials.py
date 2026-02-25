"""
Trial segmentation for the SAP VR Study cognitive3D pipeline.

    segment_trials(participant_id)

Loads every session for a participant, resolves cross-stream linkage,
and returns a flat dict keyed by trial_id (coin UUID) where each value
contains per-trial metadata and five sliced DataFrames.

One trial = one CoinSpawned event (one coin appearance).
Trial window = [CoinSpawned.time_s, CoinCollected|CoinDestroyed.time_s].

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
⚠ CONDITION_MAP must be populated before running.
  Condition tags (c1–c6) are stored only in the Cognitive3D
  session metadata — NOT in any of the six JSON files.
  See DATA_README.md §Known Data Quirks #4.
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Dynamics linkage
----------------
The C3D SDK assigns separate UUID namespaces to events (coinId) and
dynamics (object_id).  They are linked here via exact world-position
match at spawn time: the dynamics object whose recorded position at
t ≈ spawn_time is ≤ 1 cm from the CoinSpawned event point is treated
as the same physical coin.  Empirically verified: 120/120 matched with
0.0000 m error, zero ambiguity, across HG101 session 1.

Slice semantics
---------------
gaze / fixations / sensors slices span the full trial window and
therefore include data concurrent with other active trials.  This is
intentional: the slice represents the full perceptual context during
which this coin was present, not exclusive attention toward it.
"""
from __future__ import annotations

from collections import defaultdict
from pathlib import Path

import numpy as np
import pandas as pd

from parsers import (
    parse_dynamics,
    parse_events,
    parse_fixations,
    parse_gaze,
    parse_sensors,
)

# ── Configuration ──────────────────────────────────────────────────────────────

DATA_ROOT: Path = Path("/Users/arwaadib/Desktop/sap_data/participants")

PARTICIPANT_MAP: dict[str, str] = {
    "98b683447531a78ca23cd6c333403fd4": "HG 101",
    # Add new participants here as collected.
}

# Map session_id → condition tag ("c1"–"c6").
# Populate from the Cognitive3D dashboard session export or manual lookup.
# See DATA_README.md §Known Data Quirks #4 and ANALYSIS_PLAN.md §Step 3.
CONDITION_MAP: dict[str, str] = {
    # "1770234534_98b683447531a78ca23cd6c333403fd4": "c1",  # HG 101 session 1
    # "1770234645_98b683447531a78ca23cd6c333403fd4": "c2",  # HG 101 session 2
    # … fill in all sessions before running confirmatory pipeline
}

VALID_CONDITIONS: frozenset[str] = frozenset({"c1", "c2", "c3", "c4", "c5", "c6"})

# ── Internal helpers ────────────────────────────────────────────────────────────

def _participant_dir(participant_id: str) -> Path:
    """Return the participant data directory (e.g. HG101/) for a label like "HG 101"."""
    label = participant_id.replace(" ", "")      # "HG 101" → "HG101"
    candidate = DATA_ROOT / label
    if candidate.is_dir():
        return candidate
    raise FileNotFoundError(
        f"No data directory found for participant '{participant_id}' "
        f"(looked for {candidate})"
    )


def _session_dirs(participant_dir: Path) -> list[Path]:
    """Session subdirectories sorted chronologically by Unix-timestamp prefix."""
    return sorted(
        [p for p in participant_dir.iterdir() if p.is_dir()],
        key=lambda p: int(p.name.split("_")[0]),
    )


def _get_file(session_dir: Path, prefix: str) -> Path:
    matches = list(session_dir.glob(f"{prefix}_*.json"))
    if len(matches) != 1:
        raise FileNotFoundError(
            f"Expected exactly one {prefix}_*.json in {session_dir}, "
            f"found {len(matches)}: {matches}"
        )
    return matches[0]


def _build_coin_to_dyn_map(
    spawned: pd.DataFrame,
    dynamics_df: pd.DataFrame,
) -> dict[str, str]:
    """Map each events coinId to its corresponding dynamics object_id.

    The match is resolved by finding the dynamics object whose world position
    at t ≈ spawn_time is closest to the CoinSpawned event position.
    Only matches within 1 cm are accepted.

    Parameters
    ----------
    spawned :
        Rows of the events DataFrame where name == "CoinSpawned".
    dynamics_df :
        Full dynamics DataFrame from parse_dynamics().

    Returns
    -------
    dict[coinId → dynamics object_id]
        Coins with no sub-1 cm match are omitted (empty dynamics slice).
    """
    coin_dyn = dynamics_df[
        dynamics_df["object_name"].str.contains("coin", case=False, na=False)
    ]
    if coin_dyn.empty or spawned.empty:
        return {}

    # Pre-group once to avoid repeated groupby inside the coin loop.
    dyn_groups: dict[str, pd.DataFrame] = {
        oid: grp for oid, grp in coin_dyn.groupby("object_id", sort=False)
    }

    result: dict[str, str] = {}
    for _, row in spawned.iterrows():
        coin_id = row["coin_id"]
        spawn_t = float(row["time_s"])
        spawn_p = np.array([row["pos_x"], row["pos_y"], row["pos_z"]], dtype=float)

        best_oid: str | None = None
        best_dist: float = float("inf")

        for oid, grp in dyn_groups.items():
            # Snapshot closest in time to the spawn event.
            idx  = (grp["time_s"] - spawn_t).abs().idxmin()
            snap = grp.loc[idx]
            pos  = np.array([snap["pos_x"], snap["pos_y"], snap["pos_z"]], dtype=float)
            dist = float(np.linalg.norm(pos - spawn_p))
            if dist < best_dist:
                best_dist, best_oid = dist, oid

        if best_dist < 0.01:          # < 1 cm → accept
            result[coin_id] = best_oid   # type: ignore[assignment]

    return result


# ══════════════════════════════════════════════════════════════════════════════
# segment_trials
# ══════════════════════════════════════════════════════════════════════════════

def segment_trials(
    participant_id: str,
    condition_map: dict[str, str] | None = None,
) -> dict[str, dict]:
    """Segment all streams for a participant into per-trial windows.

    Parameters
    ----------
    participant_id :
        Participant label, e.g. "HG 101".
    condition_map :
        Optional override for the module-level CONDITION_MAP.
        Maps session_id → "c1"|"c2"|"c3"|"c4"|"c5"|"c6".
        Every session directory must have an entry.

    Returns
    -------
    dict[trial_id → trial_dict]

    trial_id
        coinId UUID — globally unique per coin appearance.

    trial_dict keys
    ---------------
    trial_id           str    same as the dict key (coinId UUID)
    participant_id     str    e.g. "HG 101"
    session_id         str    e.g. "1770234534_98b683447531a78ca23cd6c333403fd4"
    condition          str    "c1"–"c6"
    repetition         int    1-based rank of this session among all sessions
                              sharing the same condition for this participant
    coin_id            str    coinId UUID (same as trial_id)
    coin_type          str    "coinBF" | "coinSF" | "coinBS" | "coinSS"
    is_fast            bool
    is_large           bool
    point_value        float  1.0 or 2.0
    eccentricity_range float  1–4
    spawn_time_s       float  Unix epoch seconds
    end_time_s         float  Unix epoch seconds
    outcome            str    "collected" | "destroyed" | "unknown"
    streams            dict   five sliced DataFrames (see below)

    streams dict
    ------------
    events     DataFrame  All events whose coin_id == this trial's coin_id
                          (CoinSpawned, CoinObserved*, CoinObservationEnded*,
                           CoinCollected | CoinDestroyed).
    gaze       DataFrame  Gaze samples in [spawn_time_s, end_time_s].
    fixations  DataFrame  Fixation events in [spawn_time_s, end_time_s].
    dynamics   DataFrame  Position/rotation snapshots for *this coin only*
                          in [spawn_time_s, end_time_s], resolved via
                          position-proximity matching (see module docstring).
    sensors    DataFrame  Sensor readings in [spawn_time_s, end_time_s].

    Raises
    ------
    KeyError
        If any session directory is missing from the condition_map.
    FileNotFoundError
        If the participant directory or a required JSON file is not found.
    """
    cmap = condition_map if condition_map is not None else CONDITION_MAP
    part_dir = _participant_dir(participant_id)
    sessions  = _session_dirs(part_dir)

    # ── Validate condition_map coverage ────────────────────────────────────────
    missing = [s.name for s in sessions if s.name not in cmap]
    if missing:
        raise KeyError(
            f"session_ids missing from condition_map "
            f"(populate CONDITION_MAP in segment_trials.py or pass condition_map=):\n"
            + "\n".join(f"  {s}" for s in missing)
        )

    bad_conds = {cmap[s.name] for s in sessions} - VALID_CONDITIONS
    if bad_conds:
        raise ValueError(
            f"condition_map contains invalid condition tags: {bad_conds}. "
            f"Valid values: {sorted(VALID_CONDITIONS)}"
        )

    # ── Repetition numbers: 1-based rank within (participant, condition) ────────
    condition_sessions: dict[str, list[str]] = defaultdict(list)
    for sess in sessions:
        condition_sessions[cmap[sess.name]].append(sess.name)
    # sessions are already chronological, so enumeration gives correct order

    session_repetition: dict[str, int] = {
        sess_id: rep
        for sess_list in condition_sessions.values()
        for rep, sess_id in enumerate(sess_list, start=1)
    }

    # ── Process sessions ────────────────────────────────────────────────────────
    trials: dict[str, dict] = {}

    for sess_dir in sessions:
        sess_id    = sess_dir.name
        condition  = cmap[sess_id]
        repetition = session_repetition[sess_id]

        # Load all five streams.
        events_df    = parse_events(_get_file(sess_dir, "events"))
        gaze_df      = parse_gaze(_get_file(sess_dir, "gaze"))
        fixations_df = parse_fixations(_get_file(sess_dir, "fixation"))
        dynamics_df  = parse_dynamics(_get_file(sess_dir, "dynamics"))
        sensors_df   = parse_sensors(_get_file(sess_dir, "sensors"))

        spawned       = events_df[events_df["name"] == "CoinSpawned"]
        coin_to_dyn   = _build_coin_to_dyn_map(spawned, dynamics_df)

        # Empty-schema DataFrame for trials whose coin has no dynamics match.
        empty_dyn = dynamics_df.iloc[:0].copy()

        for _, spawn in spawned.iterrows():
            coin_id    = spawn["coin_id"]
            spawn_time = float(spawn["time_s"])

            # All events for this coin from spawn onwards.
            coin_events = events_df[
                (events_df["coin_id"] == coin_id) &
                (events_df["time_s"] >= spawn_time)
            ].copy()

            # Determine trial end time and outcome.
            collected_ev = coin_events[coin_events["name"] == "CoinCollected"]
            destroyed_ev = coin_events[coin_events["name"] == "CoinDestroyed"]

            if not collected_ev.empty:
                end_time = float(collected_ev["time_s"].iloc[0])
                outcome  = "collected"
            elif not destroyed_ev.empty:
                end_time = float(destroyed_ev["time_s"].iloc[0])
                outcome  = "destroyed"
            else:
                session_end = events_df[events_df["name"] == "c3d.sessionEnd"]
                end_time = (
                    float(session_end["time_s"].iloc[0])
                    if not session_end.empty
                    else float(gaze_df["time_s"].max())
                )
                outcome = "unknown"

            # ── Stream slices ──────────────────────────────────────────────────
            def _window(df: pd.DataFrame, t_col: str = "time_s") -> pd.DataFrame:
                return df[
                    (df[t_col] >= spawn_time) & (df[t_col] <= end_time)
                ].copy()

            dyn_oid = coin_to_dyn.get(coin_id)
            dyn_sl  = (
                _window(dynamics_df[dynamics_df["object_id"] == dyn_oid])
                if dyn_oid is not None
                else empty_dyn
            )

            trials[coin_id] = {
                "trial_id":           coin_id,
                "participant_id":     participant_id,
                "session_id":         sess_id,
                "condition":          condition,
                "repetition":         repetition,
                "coin_id":            coin_id,
                "coin_type":          spawn["coin_type"],
                "is_fast":            spawn["is_fast"],
                "is_large":           spawn["is_large"],
                "point_value":        float(spawn["point_value"]),
                "eccentricity_range": float(spawn["eccentricity_range"]),
                "spawn_time_s":       spawn_time,
                "end_time_s":         end_time,
                "outcome":            outcome,
                "streams": {
                    "events":    coin_events,
                    "gaze":      _window(gaze_df),
                    "fixations": _window(fixations_df),
                    "dynamics":  dyn_sl,
                    "sensors":   _window(sensors_df),
                },
            }

    return trials
