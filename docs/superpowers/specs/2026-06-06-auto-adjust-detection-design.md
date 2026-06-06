# Auto-adjust detection — grid-anchored sensitivity tuning & cell-snapped circles

## Goal

Add an **Auto-adjust** button to the camera preview that finds correct values for the
detection sliders (sensitivity + min object size) by capturing a single, minimal-size
token the user places on the screen and tuning the detector against it.

Two supporting changes make the result principled and stable:

1. The map grid gives a real physical scale — **1 grid cell = 2.5 cm** — so object size is
   expressed in cells instead of raw pixels, and survives zoom changes.
2. Detected (and drawn) figurine circles snap their radius to whole-cell steps
   (1 cell, 2 cells, …), with 1 cell as the minimum.

## Background — current state

- Detection lives in the `ScreenMap.Vision` library (extracted in commit `73eaaf9`).
- `FigurineDetector.Detect(cameraFrame, playerView, out detections)` warps the camera frame
  onto a rendered reference view via four ArUco markers, diffs them, thresholds the diff
  (`DiffThreshold`), and reports blobs above `MinBlobAreaPx` as figurines (center + radius in
  reference/snapshot pixel space).
- `DetectionService` owns the camera, runs the pipeline on a timer, and publishes the latest
  frame/crops. It is constructed in `GMMainForm` with callbacks:
  `Func<Size,Bitmap> renderPlayerView`, `Func<Size,RectangleF> getViewRect`, `Size playerViewSize`.
- `CameraSettings` (in Vision) persists `DiffThreshold` (default 70) and `MinBlobAreaPx`
  (default 800) to JSON.
- `CameraSettingsForm` exposes a sensitivity slider (`DiffThreshold`, 20–150) and a min-size
  slider (`MinBlobAreaPx`, 200–5000 px).
- `CameraPreviewForm` shows the live feed (ArUco overlay + optional crop montage). It takes
  only a `DetectionService`.
- `PlayersMap` renders the reference snapshot. The map→snapshot scale is
  `zoom = snapshotSize / worldRect`, where `worldRect = LiveViewRect(size)` and grid cell size
  in unscaled map pixels is `MapInfo.CellSize` (default 48). So **detection-space pixels per
  cell = `zoom × CellSize`**.

There is currently no physical scale in the detector and no link between detection and the grid.

## Design

### 1. cm→px anchor: pixels-per-cell

`PlayersMap` exposes the detection-space size of one grid cell:

```csharp
public float PixelsPerCell(Size snapshotSize)
```

- Returns `0` when no map is loaded or `CellSize <= 0` (unknown scale).
- Otherwise computes `worldRect = LiveViewRect(snapshotSize)` and returns
  `((snapshotSize.Width / worldRect.Width) + (snapshotSize.Height / worldRect.Height)) / 2 * CellSize`
  (average of the two axes; they differ only under anisotropic DPI).

`DetectionService` gains a new constructor callback `Func<Size,float> getPixelsPerCell`, wired in
`GMMainForm` to `playersMap.PixelsPerCell`, alongside the existing render/viewRect callbacks. The
service pushes the current value into the detector each cycle (and supplies it to auto-adjust).

### 2. Cell-snapped detection (`FigurineDetector`)

New public members:

- `float PixelsPerCell { get; set; }` — detection-space px for one cell; `0` = unknown.
- `double MinObjectCells { get; set; } = 1.0` — smallest object kept / drawn, in cells.
- `MinBlobAreaPx` is retained as the **no-grid fallback only**.

Blob handling at the end of `Detect`:

- When `PixelsPerCell > 0`:
  - For each contour, compute `radiusPx` (`MinEnclosingCircle`) and
    `cells = (int)Math.Round(radiusPx / PixelsPerCell)`.
  - **Reject** the blob if `cells < MinObjectCells` (noise / sub-token).
  - Otherwise emit the detection with radius = `cells * PixelsPerCell` — a whole-cell circle.
  - Snapping happens in detection space, before `TranslateToUnscaled`, so the "N cells" size is
    resolution-independent across the player screen, GM view, and web view.
- When `PixelsPerCell <= 0`:
  - Legacy path: filter by `area < MinBlobAreaPx`, emit the raw `MinEnclosingCircle` radius, no
    snapping. Preserves existing behavior and tests for the no-map/no-grid case.

Crops (`CropDetections`) continue to use the (now snapped) radius unchanged.

### 3. Min size expressed in cells (settings)

- `CameraSettings`: add `double MinObjectCells { get; set; } = 1.0`. Keep `MinBlobAreaPx` for the
  fallback path. `DetectionService.Apply` sets both `_detector.MinObjectCells` and
  `_detector.MinBlobAreaPx` from settings.
- `CameraSettingsForm`: the min-size slider becomes **"Min object size: N cells"**, integer range
  **1–6**, bound to `MinObjectCells`. The sensitivity slider (`DiffThreshold`) is unchanged.

### 4. Auto-adjust algorithm (`ScreenMap.Vision/AutoTuner.cs`)

Pure CV, no WinForms/camera dependency. Owns its **own** `FigurineDetector` instance so it never
races the live detector's scratch buffers.

```csharp
public sealed class AutoTuner
{
    public AutoTuneResult Tune(Mat cameraFrame, Bitmap referenceView, float pixelsPerCell);

    // Pure selection step, unit-testable without images:
    public static AutoTuneResult SelectThreshold(
        IReadOnlyList<(int threshold, double[] radiiPx)> sweep, float pixelsPerCell);
}

public sealed class AutoTuneResult
{
    public bool Success;
    public int DiffThreshold;
    public double MinObjectCells;
    public int MinBlobAreaPx;     // set only on the no-grid fallback
    public int BlobCount;         // blobs at the chosen threshold
    public double TokenRadiusCells;
    public string Message;
}
```

`Tune`:

1. Sweep `DiffThreshold` over **20 → 150 step 5**. For each, run its private detector with
   `PixelsPerCell = 0` and `MinBlobAreaPx = 0` (so every blob's raw `radiusPx` is collected,
   unsnapped, unfiltered).
2. Hard-abort (`Success = false`) when the pipeline returns `NoMarkers` / `Empty` at every
   threshold, or `referenceView`/`cameraFrame` is null — nothing to tune against.
3. Delegate the pick to `SelectThreshold(sweep, pixelsPerCell)`.

`SelectThreshold` (best-effort — always returns a recommendation unless aborted):

- Expected token radius = `pixelsPerCell` (one cell). Define a blob as "the token" when its
  `round(radiusPx / pixelsPerCell) == 1`.
- For each threshold count blobs and find whether exactly one token-sized blob is present.
- **Prefer the center threshold of the longest consecutive run** where exactly one token-sized
  blob exists (most robust plateau).
- If no threshold yields exactly one token-sized blob, fall back to the threshold with the
  fewest blobs, tie-broken by the blob radius closest to one cell.
- Output: `DiffThreshold` = chosen; `MinObjectCells` = snapped cells of the chosen token
  (normally 1); `TokenRadiusCells` for the message; `BlobCount` at that threshold.
- When `pixelsPerCell <= 0` (no grid), fall back to area mode: pick the single-blob plateau and
  set `MinBlobAreaPx = clamp(round(tokenAreaPx * 0.5), 200, 5000)`; leave `MinObjectCells` unset.

### 5. Orchestration & UI

`DetectionService.AutoTune()` (UI thread):

1. `TryGetLatestFrame(out frame)` — fail "no camera frame" if none.
2. `using view = RenderReferenceView()` — fail "no map" if null.
3. `float ppc = _getPixelsPerCell(_playerViewSize)`.
4. `result = new AutoTuner().Tune(frame, view, ppc)`; dispose frame/view.
5. On `Success`: write `_settings.DiffThreshold`, `_settings.MinObjectCells` (or `MinBlobAreaPx`
   on the fallback), update the live `_detector`, `_settings.Save()`, raise `DetectionsUpdated`.
6. Return the `AutoTuneResult` for the caller to display.

`CameraPreviewForm`:

- Add an **Auto-adjust** button docked top (beside the existing "Show isolated figurines" check).
- On click: message box — *"Place ONE token (smallest you use) on the map, clear everything
  else, then OK."*
- On OK: set wait cursor, disable the button, call `_service.AutoTune()`, restore cursor/button,
  then show the result via the title bar / message
  (e.g. `Auto-adjust: sensitivity 65, min size 1 cell (token≈1.0 cells)`), or the hard-abort
  reason.
- Stays thin — still constructed with only a `DetectionService`.

The sweep (~27 `Detect` calls, each re-warping ~1 MP) runs synchronously on the UI thread, a
brief (~1–2 s) freeze masked by the wait cursor. Acceptable for a one-shot button; can be made
async later if it becomes annoying (YAGNI for now).

### Threading & ownership

- Auto-adjust uses its own `FigurineDetector` and a cloned frame (`TryGetLatestFrame`) plus a
  freshly rendered reference view, so it never touches the live detector's buffers concurrently
  with the detection timer.
- `_settings` is the same instance shared with `GMMainForm`; mutating it makes the new values
  visible the next time `CameraSettingsForm` opens.

## Testing

- **`FigurineDetector` snapping** (`FigurineDetectionTests`): synthetic scene with a known object,
  `PixelsPerCell` set; assert emitted radius equals a whole-cell multiple and that a blob smaller
  than one cell is rejected.
- **`AutoTuner.SelectThreshold`** (`AutoTunerTests`): synthetic sweep tables (lists of
  `(threshold, radiiPx)`) → assert the plateau-center threshold is chosen and `MinObjectCells ≈ 1`;
  cover the no-token and multi-blob fallbacks; cover the `pixelsPerCell <= 0` area-mode fallback.
- **`AutoTuner.Tune` integration** (`AutoTunerTests`): real captured pair `20260531-185301`
  (one token) with a plausible `pixelsPerCell` → `Success`, `MinObjectCells ≈ 1`, `DiffThreshold`
  in a sane range, `BlobCount == 1`. Pair `20260531-171645` and the synthetic no-token scene →
  `BlobCount != 1` reflected in the message. Reuses the existing `LoadCapture` helper.
- **Regression**: existing `FigurineDetectionTests` continue to pass through the
  `PixelsPerCell <= 0` legacy path (they never set `PixelsPerCell`).

## Out of scope

- Asynchronous / progress-barred auto-adjust (synchronous one-shot is enough).
- Multi-frame averaging during capture (single best-effort frame, per decision).
- Re-tuning automatically on zoom changes — `MinObjectCells` is zoom-invariant by construction,
  and `PixelsPerCell` is recomputed each cycle, so no re-tune is needed; sensitivity is unaffected
  by zoom.
- Storing/using an explicit cm-per-pixel scale beyond the grid-cell relationship.
