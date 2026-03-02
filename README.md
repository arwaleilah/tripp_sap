---


## Project Overview

**Study**: VR gaze-controlled coin collection task examining how reward value, eccentricity, speed, and size drive task-relevant visual engagement with dynamic objects in immersive 3D navigation.

**Attention measure**: `CoinObserved` / `CoinObservationEnded` events are the **primary proxy for attentional allocation** — not fixations. These represent sustained alignment between the participant's eye-gaze vector and a specific coin, exceeding a minimum temporal stability threshold. Captures intentional, task-relevant visual engagement behaviorally predictive of collection outcomes. See **Known Data Quirks #1** for why `fixation_*.json` cannot be used for coin-level attention.

**Platform**: Cognitive3D, HTC Vive Focus Vision, Unity app `SAP{initials}` (e.g. `SAPHG` for HG 101), SDK `2.1.0`  
**Device**: Desktop VR (Windows 10), eye tracking confirmed working

---

## Participant ID System

Participants are identified by **`{initials} {sequential number}`**, e.g. `HG 101`, `JH 102`. Initials vary per person; the number is assigned sequentially across all participants.

Each participant maps to a Cognitive3D `deviceid` (device hash) and a Qualtrics `QID11` Participant ID field.

| Participant | C3D device hash | Unity build | Notes |
|---|---|---|---|
| HG 101 | `98b683447531a78ca23cd6c333403fd4` | `SAPHG` | First pilot participant |
| *(future)* | *(add as collected)* | `SAP{initials}` | |

> Update this table when adding new participants. Also update `PARTICIPANT_MAP` in the pipeline.

---

## Folder Layout

```
sap_data/
  participants/
    HG101/
      boundary_1770234534_98b683447531a78ca23cd6c333403fd4.json
      dynamics_1770234534_98b683447531a78ca23cd6c333403fd4.json
      events_1770234534_98b683447531a78ca23cd6c333403fd4.json
      fixation_1770234534_98b683447531a78ca23cd6c333403fd4.json
      gaze_1770234534_98b683447531a78ca23cd6c333403fd4.json
      sensors_1770234534_98b683447531a78ca23cd6c333403fd4.json
      HG_Demographics.csv
    JH102/
      ...
      JH_Demographics.csv
```

Each participant has their own folder containing their 6 JSON files per session and their own demographics CSV. Demographics files are not pooled across participants at this time — each is a single-participant Qualtrics export.

**JSON filename format**: `{filetype}_{sessionid}.json`  
**Session ID format**: `{unix_timestamp}_{device_hash}`

---

## Conditions

Each session in Cognitive3D is tagged `c1`–`c6`. **This tag is the ground truth condition identifier** — do not infer condition from `version_id` or `pointValue` alone.

| Tag | Small Coin Speed | Large Coin Speed | Value Assignment |
|---|---|---|---|
| `c1` | Slow | Slow | Equal |
| `c2` | Fast | Fast | Equal |
| `c3` | Slow | Fast | Equal |
| `c4` | Fast | Slow | Equal |
| `c5` | Slow | Fast | Faster coins worth more |
| `c6` | Fast | Slow | Faster coins worth more |

**Notes on design:**
- Conditions 1–4 are equal-value baselines varying speed assignment across coin sizes
- Conditions 5–6 introduce the value manipulation: the faster coin type is worth more (pointValue = 2.0); the slower type is worth less (pointValue = 1.0)
- Speed is assigned *per size class*, not uniformly — in c3/c5, small=slow and large=fast; in c4/c6, small=fast and large=slow

---

## JSON File Schemas

All timestamps in JSON files are **Unix epoch in seconds**.

### events_*.json — PRIMARY ANALYSIS FILE

**Top-level fields:** `sessionid`, `duration` (s), `formatversion` (`"artificial 2.0"`), `data` (array of event objects)

**Each event object:**

| Field | Type | Description |
|---|---|---|
| `name` | str | Event type (see Event Types below) |
| `time` | float (s) | Event timestamp |
| `point` | [x, y, z] | Coin world position at event time (meters) |
| `properties.coinId` | str (UUID) | Unique per coin instance |
| `properties.coinType` | str | e.g. `coinBF`, `coinSF`, `coinBS`, `coinSS` |
| `properties.isFast` | bool | True = fast-moving coin |
| `properties.isLarge` | bool | True = large coin |
| `properties.pointValue` | float | Reward value — `1.0` or `2.0` |
| `properties.initialEccentricity` | float | Angular distance (°) from gaze center at spawn |
| `properties.currentEccentricity` | float | Angular distance (°) from gaze center at event time |
| `properties.eccentricityRange` | int | Eccentricity zone 1–4 (1=near-center, 4=far periphery) |

**Event Types:**

| Event Name | Meaning | Analysis Role |
|---|---|---|
| `CoinSpawned` | Coin enters scene with `initialEccentricity` | Defines each coin trial |
| `CoinObserved` | Gaze vector aligns with coin within observation radius | **Primary attention onset** |
| `CoinObservationEnded` | Gaze exits coin's observation radius | Closes observation window |
| `CoinCollected` | Coin successfully collected | **Primary outcome** |
| `CoinDestroyed` | Coin missed — reached end of track uncollected | Survival censoring event |
| `c3d.sessionStart` / `c3d.sessionEnd` | Session bookends | QC / timing |
| `c3d.User removed headset` | HMD removed mid-session | Data quality flag |
| `c3d.SceneLoad` / `c3d.SceneUnload` | Scene transitions | Trial boundary marker |
| `c3d.Right/Left Controller Lost/regained tracking` | Controller dropout | Data quality flag |

### fixation_*.json

> **DO NOT USE for coin attention analysis.**  
> Eye tracking hardware is working correctly. However, the spaceship — the gaze-controlled movement mechanism — sits in the central visual field and occludes raycast-based fixation attribution to coins. All fixations are attributed to the spaceship object, not individual coins. Only useful for confirming eye tracking was active and characterizing general fixation behavior.

**Each fixation:** `time` (s), `duration` (ms), `maxradius` (radians; ~0.17 rad ≈ 10°), `p` ([x,y,z]), `objectid` (always the spaceship UUID)

### gaze_*.json

Continuous raw gaze at high sampling rate (~60–90 Hz).

**Each sample:** `time` (s), `p` ([x,y,z] HMD position), `r` ([x,y,z,w] quaternion rotation), `f` ([x,y,z] forward gaze vector)

Valid for exploratory gaze trajectory and scan path analyses. For coin-level attention, use `CoinObserved`/`CoinObservationEnded` from `events_*.json`.

### dynamics_*.json

Continuous position/rotation snapshots of all dynamic objects (coins + controllers).

**Manifest:** dict mapping object UUIDs → `{name, fileType, mesh}` (e.g. `"coinBF(Clone)"`)  
**Each entry:** `id` (manifest UUID), `time` (s), `p` ([x,y,z]), `r` ([x,y,z,w]), `buttons` dict, `properties` array

Individual coin instances are **not** linkable to `coinId` in events via any ID field — the two files use completely separate UUID systems with zero overlap. The only reliable linkage is **position-proximity at spawn time**: match a `CoinSpawned` event to the dynamics object whose recorded position at that timestamp is within 1 cm. Validated on HG 101: 120/120 coins matched 1-to-1, 0.0000 m error, zero ambiguity.

### sensors_*.json

Time-series sensor readings at ~1 Hz.

**Each stream:** `name` (sensor channel), `data` (array of `[timestamp, value]` pairs)

| Sensor Name | Unit | Use |
|---|---|---|
| `c3d.hmd.yaw` | degrees | Head horizontal rotation |
| `c3d.hmd.pitch` | degrees | Head vertical rotation |
| `c3d.controller.right.height.fromHMD` | meters | Right controller offset from HMD |
| `c3d.controller.left.height.fromHMD` | meters | Left controller offset from HMD |
| `c3d.fps.avg` | fps | Average frame rate |
| `c3d.fps.1pl` | fps | 1st percentile FPS |
| `c3d.profiler.mainThreadTimeInMs` | ms | CPU frame time |
| `c3d.profiler.systemMemoryInMB` | MB | RAM usage |
| `c3d.profiler.drawCallsCount` | count | GPU draw calls |
| `c3d.app.WifiRSSI` | dBm | WiFi signal strength |
| `HMD Battery Level` | % | Battery level |
| `HMD Battery Status` | enum | Charging state |

### boundary_*.json

Sparse HMD position/orientation snapshots at scene transitions. Too sparse for time-series analysis. Use `sensors_*.json` `c3d.hmd.yaw`/`c3d.hmd.pitch` for continuous head orientation.

---

## Demographics CSV Schema (`{initials}_Demographics.csv`)

One Qualtrics export per participant, stored in their folder. **3-row header — skip rows 2 and 3 when loading:**

```python
df = pd.read_csv("{initials}_Demographics.csv", skiprows=[1, 2])
```

| Column | Label | Notes |
|---|---|---|
| `QID11` | Participant ID | The participant's assigned ID (e.g. `"101"`) |
| `Gender` | Gender | |
| `Age` | Age | Years |
| `Ethnicity1` | Hispanic/Latino origin | Yes / No |
| `Ethnicity2` | Race/ethnicity | Multi-select, comma-separated |
| `Education` | Highest education level | Ordinal |
| `Q16` | Corrective lenses during study | Yes-glasses / Yes-contacts / No |
| `Q19_1` | Perceived difficulty — coins overall | 7-point: 1=Very Difficult, 7=Very Easy |
| `Q19_2` | Perceived difficulty — fast coins | 7-point |
| `Q19_3` | Perceived difficulty — small coins | 7-point |
| `Q19_4` | Perceived difficulty — peripheral coins | 7-point |
| `Q20` | Strategy selection | Multi-select free text |
| `Q21_1` | Strategy switching when coin values changed | Likert 1–7 |
| `Q22_1` | Larger coins felt closer | Likert 1–7 |
| `Q22_2` | Faster coins felt farther away | Likert 1–7 |
| `Q22_3` | Harder to judge depth for fast coins | Likert 1–7 |
| `Q22_4` | Harder to judge depth for peripheral coins | Likert 1–7 |
| `Q23_1` | Peripheral coins harder to notice | Likert 1–7 |
| `Q23_2` | Fast coins grabbed attention automatically | Likert 1–7 |
| `Q23_3` | Noticed coins before looking directly | Likert 1–7 |
| `Q23_4` | Ignored coins even though noticed | Likert 1–7 |
| `Q24_1` | Visual fatigue by end of study | Likert 1–7 |
| `Q24_2` | Task required sustained concentration | Likert 1–7 |
| `Q24_3` | Performance declined toward end | Likert 1–7 |
| `Q15` | Open-ended strategy/observation notes | Free text |

**Likert parsing:** Values formatted as `"N, Label"` — extract leading integer.  
**Scale directions:** Q19 difficulty: 1=Very Difficult, 7=Very Easy (higher = easier). Q21–Q24 agreement: 1=Strongly Disagree, 7=Strongly Agree. Verify direction per question block before modeling.

### HG 101 Survey Reference

| | |
|---|---|
| Gender | Female |
| Age | 23 |
| Race | White, non-Hispanic |
| Education | Bachelor's degree |
| Corrective lenses | Yes — contacts |
| Overall difficulty | 3 — Somewhat Difficult |
| Fast coin difficulty | 3 — Somewhat Difficult |
| Small coin difficulty | 2 — Difficult |
| Peripheral difficulty | 1 — Very Difficult |
| Strategy | Closest coins, biggest coins, as many as possible |
| Open-ended | "Prioritized big/slower coins, then tried small/fast if time allowed" |

---

## Derived Variables — Compute in Pipeline

| Variable | Computation | Source |
|---|---|---|
| `participant_id` | Device hash → lookup table (e.g. `"HG 101"`) | Filename |
| `condition` | Session tag `c1`–`c6` from C3D session metadata | C3D session |
| `was_observed` | Any `CoinObserved` exists for this `coinId` | events_*.json |
| `first_observation_time_s` | `time` of first `CoinObserved` for `coinId` | events_*.json |
| `latency_to_observation_s` | `first_observation_time_s − CoinSpawned.time` | events_*.json |
| `total_observation_dwell_s` | Sum of (`CoinObservationEnded.time − CoinObserved.time`) per `coinId` | events_*.json |
| `n_observation_windows` | Count of `CoinObserved` per `coinId` | events_*.json |
| `eccentricity_at_spawn` | `initialEccentricity` from `CoinSpawned` | events_*.json |
| `eccentricity_at_first_observation` | `currentEccentricity` from first `CoinObserved` | events_*.json |
| `collected` | `CoinCollected` exists for `coinId` (binary 0/1) | events_*.json |
| `was_destroyed_unobserved` | `CoinDestroyed` with no prior `CoinObserved` | events_*.json |
| `value_condition` | `"high"` if `pointValue == 2.0`, else `"standard"` | events_*.json |
| `size_condition` | `"large"` if `isLarge`, else `"small"` | events_*.json |
| `speed_condition` | `"fast"` if `isFast`, else `"slow"` | events_*.json |

---

## Known Data Quirks

### #1 — Fixation attribution blocked by spaceship 
All fixations in `fixation_*.json` are attributed to the spaceship controller. Cannot be used for coin-level attention analysis. Use `CoinObserved`/`CoinObservationEnded` as the attention proxy.

### #2 — Timestamps: all JSON files use seconds
All six JSON files use Unix epoch **seconds**. 

### #3 — Qualtrics CSV has 3 header rows
Skip rows 2 and 3: `pd.read_csv(..., skiprows=[1, 2])`.

### #4 — Condition is the session tag (c1–c6), not version_id
`version_id` encodes the app build version, not the experimental condition. Always use the `c1`–`c6` session tag.

### #5 — eccentricityRange (1–4) is a zone label, not degrees
Use `initialEccentricity` (continuous degrees) as the predictor in regression models. `eccentricityRange` is for stratified descriptives only.
