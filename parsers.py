"""cognitive3D data stream parsers.

Each function accepts a pathlib.Path to a single JSON file and returns
a clean pandas DataFrame with standardised column names and SI units.

Unit conventions
----------------
time_s              Unix epoch seconds (float)
pos_*               metres  (x right, y up, z forward — Unity world space)
rot_*               quaternion components [x, y, z, w], unit-normalised
scale_*             dimensionless Unity local scale factor
gaze_*              world-space forward vector components (not unit-normalised;
                    magnitude reflects the gaze-endpoint distance in Unity units)
duration_ms         milliseconds
max_radius_rad      radians
*_deg               degrees

Data location
-------------
participants/{participant_id}/{session_id}/{prefix}_{session_id}.json
"""
from __future__ import annotations

import json
from pathlib import Path

import numpy as np
import pandas as pd


# ── internal helpers ───────────────────────────────────────────────────────────

def _session_id_from_path(path: Path) -> str:
    """Extract session ID from filename: {prefix}_{session_id}.json → session_id."""
    return path.stem.split("_", 1)[1]


# ══════════════════════════════════════════════════════════════════════════════
# parse_gaze
# ══════════════════════════════════════════════════════════════════════════════

def parse_gaze(path: Path | str) -> pd.DataFrame:
    """Parse gaze_*.json → HMD position, rotation, and forward gaze vector.

    Rows: one per gaze sample (recorded at ~10 Hz / 0.1 s intervals per
    the gazeInterval metadata field).

    Columns
    -------
    session_id   str    session identifier (from filename)
    time_s       float  Unix epoch seconds
    pos_x        float  HMD world position — right  (metres)
    pos_y        float  HMD world position — up     (metres)
    pos_z        float  HMD world position — forward (metres)
    rot_x        float  HMD quaternion x
    rot_y        float  HMD quaternion y
    rot_z        float  HMD quaternion z
    rot_w        float  HMD quaternion w
    gaze_x       float  world-space forward gaze vector x
    gaze_y       float  world-space forward gaze vector y
    gaze_z       float  world-space forward gaze vector z

    Notes
    -----
    The gaze_* forward vector ("f" field) is NOT a unit vector — its
    magnitude reflects the distance to the raycast hit point in Unity
    units. A default value of [0, 0, -10] is recorded when no hit is
    detected.
    """
    path = Path(path)
    session_id = _session_id_from_path(path)
    with open(path) as fh:
        raw = json.load(fh)

    rows = []
    for entry in raw["data"]:
        p, r, f = entry["p"], entry["r"], entry["f"]
        rows.append({
            "session_id": session_id,
            "time_s":     entry["time"],
            "pos_x": p[0], "pos_y": p[1], "pos_z": p[2],
            "rot_x": r[0], "rot_y": r[1], "rot_z": r[2], "rot_w": r[3],
            "gaze_x": f[0], "gaze_y": f[1], "gaze_z": f[2],
        })
    return pd.DataFrame(rows)


# ══════════════════════════════════════════════════════════════════════════════
# parse_fixations
# ══════════════════════════════════════════════════════════════════════════════

def parse_fixations(path: Path | str) -> pd.DataFrame:
    """Parse fixation_*.json → fixation events with duration and spatial position.

    Rows: one per fixation event, sorted by onset time.

    Columns
    -------
    session_id      str    session identifier
    time_s          float  fixation onset — Unix epoch seconds
    duration_ms     float  fixation duration (milliseconds)
    pos_x           float  fixation world position — right  (metres)
    pos_y           float  fixation world position — up     (metres)
    pos_z           float  fixation world position — forward (metres)
    max_radius_rad  float  maximum angular dispersion from fixation centre (radians)
    object_id       str    UUID of the attributed fixation target

    ⚠ Known quirk (DATA_README #1)
    --------------------------------
    All fixations are attributed to the spaceship (a single stable UUID),
    because the spaceship sits in the central visual field and occludes
    raycast-based fixation attribution to coins.  Do NOT use this stream
    for coin-level attention analysis.  Use CoinObserved / CoinObservationEnded
    events from parse_events() instead.
    """
    path = Path(path)
    with open(path) as fh:
        raw = json.load(fh)
    session_id = raw["sessionid"]

    rows = []
    for entry in raw["data"]:
        p = entry["p"]
        rows.append({
            "session_id":     session_id,
            "time_s":         entry["time"],
            "duration_ms":    float(entry["duration"]),
            "pos_x": p[0], "pos_y": p[1], "pos_z": p[2],
            "max_radius_rad": entry["maxradius"],
            "object_id":      entry["objectid"],
        })
    return pd.DataFrame(rows)


# ══════════════════════════════════════════════════════════════════════════════
# parse_events
# ══════════════════════════════════════════════════════════════════════════════

#: Event names that carry coin properties in their `properties` dict.
COIN_EVENT_NAMES: frozenset[str] = frozenset({
    "CoinSpawned",
    "CoinObserved",
    "CoinObservationEnded",
    "CoinCollected",
    "CoinDestroyed",
})


def parse_events(path: Path | str) -> pd.DataFrame:
    """Parse events_*.json → flat event log with optional coin properties.

    Rows: one per event, sorted by event time (ascending).
    Coin-property columns are None/NaN for non-coin events.

    Columns
    -------
    session_id                str    session identifier
    name                      str    event type (e.g. "CoinSpawned", "c3d.sessionStart")
    time_s                    float  Unix epoch seconds
    pos_x / pos_y / pos_z     float  world position at event time (metres)
    coin_id                   str    coin UUID — None for non-coin events
    coin_type                 str    e.g. "coinBF", "coinSF", "coinBS", "coinSS"
    is_fast                   bool   True = fast-moving coin
    is_large                  bool   True = large coin
    point_value               float  reward value — 1.0 or 2.0
    initial_eccentricity_deg  float  angular distance from gaze centre at spawn (°)
    current_eccentricity_deg  float  angular distance from gaze centre at event (°)
    eccentricity_range        float  eccentricity zone label 1–4

    Notes
    -----
    This is the PRIMARY ANALYSIS FILE for attention and collection outcomes.
    CoinObserved / CoinObservationEnded are the proxy for attentional
    allocation; CoinCollected / CoinDestroyed are the outcome variables.
    """
    path = Path(path)
    with open(path) as fh:
        raw = json.load(fh)
    session_id = raw["sessionid"]

    rows = []
    for event in raw["data"]:
        props = event.get("properties", {})
        point = event.get("point", [float("nan")] * 3)
        rows.append({
            "session_id":                session_id,
            "name":                      event["name"],
            "time_s":                    event["time"],
            "pos_x": point[0], "pos_y": point[1], "pos_z": point[2],
            "coin_id":                   props.get("coinId"),
            "coin_type":                 props.get("coinType"),
            "is_fast":                   props.get("isFast"),
            "is_large":                  props.get("isLarge"),
            "point_value":               props.get("pointValue"),
            "initial_eccentricity_deg":  props.get("initialEccentricity"),
            "current_eccentricity_deg":  props.get("currentEccentricity"),
            "eccentricity_range":        props.get("eccentricityRange"),
        })
    # Keep boolean columns as object dtype so True/False/None round-trips cleanly
    return pd.DataFrame(rows)


# ══════════════════════════════════════════════════════════════════════════════
# parse_dynamics
# ══════════════════════════════════════════════════════════════════════════════

def parse_dynamics(path: Path | str) -> pd.DataFrame:
    """Parse dynamics_*.json → continuous position/rotation snapshots for all
    tracked objects (coins, controllers, spaceship).

    Rows: one per (object, timestamp) snapshot.  Multiple objects are updated
    at overlapping timestamps, so the DataFrame is NOT globally monotonic in
    time_s.

    Columns
    -------
    session_id   str    session identifier
    time_s       float  Unix epoch seconds
    object_id    str    manifest UUID, or short int string "1"/"2"/"3"/"4"
                        for VR controllers / HMD
    object_name  str    human-readable name from manifest
                        (NaN for controller IDs not in manifest)
    pos_x        float  world position — right   (metres)
    pos_y        float  world position — up      (metres)
    pos_z        float  world position — forward  (metres)
    rot_x        float  quaternion x
    rot_y        float  quaternion y
    rot_z        float  quaternion z
    rot_w        float  quaternion w
    scale_x      float  local scale x  (NaN when field absent, e.g. controllers)
    scale_y      float  local scale y
    scale_z      float  local scale z
    enabled      bool   True/False from the properties array (NaN when absent)

    Notes
    -----
    Individual coin instances are linkable to events via object_id ↔ coinId,
    since the C3D SDK uses the same UUID for both.
    """
    path = Path(path)
    with open(path) as fh:
        raw = json.load(fh)
    session_id = raw["sessionid"]
    manifest: dict = raw.get("manifest", {})

    _nan3 = [float("nan"), float("nan"), float("nan")]

    rows = []
    for entry in raw["data"]:
        p = entry["p"]
        r = entry["r"]
        s = entry.get("s", _nan3)
        # properties is a list of single-key dicts; flatten to one dict
        props = {k: v for prop in entry.get("properties", []) for k, v in prop.items()}
        obj_id = entry["id"]
        rows.append({
            "session_id":  session_id,
            "time_s":      entry["time"],
            "object_id":   obj_id,
            "object_name": manifest.get(obj_id, {}).get("name"),
            "pos_x": p[0], "pos_y": p[1], "pos_z": p[2],
            "rot_x": r[0], "rot_y": r[1], "rot_z": r[2], "rot_w": r[3],
            "scale_x": s[0], "scale_y": s[1], "scale_z": s[2],
            "enabled": props.get("enabled"),
        })
    return pd.DataFrame(rows)


# ══════════════════════════════════════════════════════════════════════════════
# parse_sensors
# ══════════════════════════════════════════════════════════════════════════════

def parse_sensors(path: Path | str) -> pd.DataFrame:
    """Parse sensors_*.json → long-format sensor time series.

    Rows: one per (sensor channel, timestamp) measurement (~1 Hz per channel).

    Columns
    -------
    session_id  str    session identifier
    sensor      str    channel name (e.g. "c3d.hmd.yaw", "c3d.fps.avg")
    time_s      float  Unix epoch seconds
    value       float  reading in channel-specific units — see DATA_README.md

    Sensor channels and units
    -------------------------
    c3d.hmd.yaw                          degrees   head horizontal rotation
    c3d.hmd.pitch                        degrees   head vertical rotation
    c3d.controller.right.height.fromHMD  metres    right controller offset from HMD
    c3d.controller.left.height.fromHMD   metres    left controller offset from HMD
    c3d.fps.avg                          fps       average frame rate
    c3d.fps.1pl                          fps       1st-percentile frame rate
    c3d.profiler.mainThreadTimeInMs      ms        CPU frame time
    c3d.profiler.systemMemoryInMB        MB        RAM usage
    c3d.profiler.drawCallsCount          count     GPU draw calls
    c3d.app.WifiRSSI                     dBm       WiFi signal strength
    HMD Battery Level                    %         battery level
    HMD Battery Status                   enum      charging state (numeric code)
    """
    path = Path(path)
    with open(path) as fh:
        raw = json.load(fh)
    session_id = raw["sessionid"]

    rows = []
    for stream in raw["data"]:
        sensor_name = stream["name"]
        for timestamp, value in stream["data"]:
            rows.append({
                "session_id": session_id,
                "sensor":     sensor_name,
                "time_s":     float(timestamp),
                "value":      float(value),
            })
    return pd.DataFrame(rows)


# ══════════════════════════════════════════════════════════════════════════════
# parse_boundary
# ══════════════════════════════════════════════════════════════════════════════

def parse_boundary(path: Path | str) -> pd.DataFrame:
    """Parse boundary_*.json → sparse HMD snapshots at scene-boundary events.

    Rows: one per scene-transition snapshot (typically very few rows).

    Columns
    -------
    session_id  str    session identifier
    time_s      float  Unix epoch seconds
    pos_x       float  HMD world position — right   (metres)
    pos_y       float  HMD world position — up      (metres)
    pos_z       float  HMD world position — forward  (metres)
    rot_x       float  HMD quaternion x
    rot_y       float  HMD quaternion y
    rot_z       float  HMD quaternion z
    rot_w       float  HMD quaternion w

    Notes
    -----
    Too sparse for continuous time-series analysis.  Use sensors_*.json
    c3d.hmd.yaw / c3d.hmd.pitch for continuous head-orientation data.
    """
    path = Path(path)
    with open(path) as fh:
        raw = json.load(fh)
    session_id = raw["sessionid"]

    rows = []
    for entry in raw["data"]:
        p = entry["p"]
        r = entry["r"]
        rows.append({
            "session_id": session_id,
            "time_s":     entry["time"],
            "pos_x": p[0], "pos_y": p[1], "pos_z": p[2],
            "rot_x": r[0], "rot_y": r[1], "rot_z": r[2], "rot_w": r[3],
        })
    return pd.DataFrame(rows)
