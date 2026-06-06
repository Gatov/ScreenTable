# Expected-Mini Count Cap — Design

**Date:** 2026-06-06
**Branch:** camera-figurine-detection

## Problem

The figurine detector returns every blob that clears the size/fill gates. On a new
map, glare reflections and camouflaged-tile fragments produce false positives, and the
current remedy is re-running auto-tune per map. The GM almost always knows how many
minis are physically on the table. Letting them state that count lets us keep only the
strongest detections and discard fakes/flares **without recalibration on a map change**.

## Goal

A toolbar spin in the GM view sets the expected mini count `N`. The detector keeps at
most the `N` most-confident detections. `N = 0` means auto-guess the count from a score
cliff. Confidence is ranked by **color distance** (not just brightness) between the map
and the warped camera frame inside each blob.

## Components

### 1. Toolbar spin (`GMMainForm` + `.Designer.cs`)

- Add a DevExpress `BarEditItem` backed by a `RepositoryItemSpinEdit` to `bar1`
  ("Tools"), caption `Minis`.
- SpinEdit config: integer, `MinValue = 0`, `MaxValue = 20`, no mantissa/decimals.
- `EditValueChanged` handler:
  1. `_cameraSettings.ExpectedFigurines = (int)value;`
  2. `_cameraSettings.Save();`
  3. `_detectionService.Apply(_cameraSettings);`
- Initial `EditValue` seeded from `_cameraSettings.ExpectedFigurines` after load.

### 2. Persisted setting (`CameraSettings`)

- New property `int ExpectedFigurines { get; set; } = 0;`
- Serialized through the existing JSON `Save()` / `Load()` — no other change.

### 3. Per-blob score = color distance (`FigurineDetector` + `FigurineDetection`)

- Add `float Score { get; }` to the `FigurineDetection` struct (constructor gains a
  `score` parameter; existing callers pass it).
- The existing size/fill hard gates (`MinFillRatio`, `MinObjectCells`/`MaxObjectCells`,
  or the no-grid `MinBlobAreaPx`) run **first and unchanged** — too-small and too-big
  blobs are dropped *before* any score is computed. The score is calculated only for
  contours that survive those gates.
- In `FigurineDetector.Detect`, for each surviving contour: build a
  single-channel mask of the contour, and compute the **mean** over masked pixels of the
  per-pixel BGR difference-vector magnitude `sqrt(b² + g² + r²)`, sampled from the
  existing `_diffBgr` (already `Absdiff(player, warped)` per channel). This is the
  confidence score — a colored mini against a similarly-bright but differently-hued tile
  still scores high.

### 4. Selection / cap (`FigurineDetector`)

- New property `int ExpectedCount { get; set; }` on the detector (`0` = auto), set from
  settings in `DetectionService.Apply`.
- Hard gates (`MinFillRatio`, `MinObjectCells`/`MaxObjectCells` or `MinBlobAreaPx`) run
  first and build the candidate list, each candidate carrying `{ detection, rawRadius,
  fill, score }`.
- Sort candidates by `Score` descending, then call the pure static selector
  `SelectCount(scores, expected)` which returns the number to keep:
  - **`expected > 0`** → `min(expected, count)`.
  - **`expected == 0` (auto)**:
    - `count <= 5` → keep all (`count`).
    - else:
      - `refGap` = mean of the consecutive score drops among the top 5
        (`s[i-1] - s[i]` for `i = 1..4`).
      - Scan `i = 5 .. min(20, count) - 1`: the first `i` where
        `s[i-1] - s[i] > k * refGap` (cliff) → keep `i`.
      - No cliff found → keep `min(20, count)` (ceiling).
      - **Floor 5, ceiling 20** always hold.
  - `k` (cliff factor) = `2.5`, a tunable `const`.
- Keep the first `SelectCount(...)` candidates. Build `detections`, `LastRawRadii`,
  `LastFillRatios`, and crops from this selected, reordered set so all stay 1:1.

### Data flow

Unchanged downstream: selected detections → `DetectionService.TranslateToUnscaled` →
`DetectionStore` → GM/player overlay.

## Edge cases

- **Fewer than 5 candidates, `N = 0`:** keep all (floor cannot be enforced beyond what
  exists).
- **`refGap == 0`** (top-5 scores identical): treat as no cliff in the top region; the
  `> k * refGap` test becomes `> 0`, so the first non-zero drop after index 5 cuts.
  Acceptable; ceiling/floor still bound the result.
- **`N` larger than candidate count:** keep all candidates (the `min`).
- **`ProduceCrops` off:** selection still applies to detections; crops simply not built.

## Testing

`SelectCount(IReadOnlyList<float> scoresDesc, int expected)` is a pure function with no
OpenCV dependency. Unit tests in `FigurineDetectionTests`:

- `N = 3`, 10 candidates → returns 3.
- `N = 3`, 2 candidates → returns 2.
- `N = 0`, 3 candidates → returns 3 (below floor, keep all).
- `N = 0`, smooth descending scores, no cliff → returns 20 (ceiling) when ≥20, else count.
- `N = 0`, scores with a sharp cliff after index 8 → returns 8.
- `N = 0`, tight top-5 cluster then a cliff at index 5 → returns 5 (floor respected).

## Out of scope

- Temporal/cross-frame tracking of detections.
- Changing the existing size/fill hard gates or auto-tune.
- Any player-app UI; the spin lives only in the GM toolbar.
