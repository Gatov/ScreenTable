# Split lens-distortion and sensitivity auto-tune

## Goal

Separate the two things the current coupled "Auto-tune" does into independent operations:

1. **Auto-distortion** — find the lens fisheye coefficient (K1) that best removes barrel
   distortion from the camera frame.
2. **Auto-sensitivity** — find the detection sensitivity (`DiffThreshold`) and minimum object
   size (`MinObjectCells` / `MinBlobAreaPx`) that isolate a single placed token.

Both results, plus manual slider edits, are **persisted** in `CameraSettings` and **applied to
live detection**. A new **Lens distortion** slider lets the user set / fine-tune K1 by hand with
a step of 0.005.

## Background — current state

- `AutoTuner.Tune(cameraFrame, referenceView, pixelsPerCell)` runs a **coupled** sweep: an outer
  loop over K1 (`0.0 → 0.2 step 0.025`) and, for each K1, an inner threshold sweep. It picks K1 by
  minimum total confidence energy and the threshold by single-token isolation.
- The K1 it computes is written into `AutoTuneResult.LensDistortionK1`, but
  `DetectionService.AutoTune()` **discards it** — only `DiffThreshold` and `MinObjectCells` are
  stored. `CameraSettings` has no K1 field.
- `FigurineDetector.Detect` **never undistorts** — it warps the raw camera frame. Lens distortion
  correction is therefore not wired into the live app at all; only `TestHarness/TestRunner`
  undistorts (manually, using the tuner's reported K1).
- One toolbar button `barButtonItemAutoTune` (`GMMainForm.barButtonItemAutoTune_ItemClick`) drives
  `DetectionService.AutoTune()`.
- `CameraSettingsForm` (rebuilt with auto-layout group boxes) has sliders for detection interval,
  sensitivity threshold (20–150), and min object size (1–6 cells), plus display checkboxes.

## Design

### 1. Storage — `CameraSettings` (Vision)

Add a persisted field:

```csharp
/// <summary>Fisheye/barrel lens distortion coefficient (OpenCV k1). 0 = no correction.
/// Set by Auto-distortion or the manual slider; applied to every detection frame.</summary>
public double LensDistortionK1 { get; set; } = 0.0;
```

Single source of truth, written by Auto-distortion **and** the manual slider, serialized to the
existing `CameraSettings.json`.

### 2. Apply distortion live — `FigurineDetector` (Vision)

Add:

```csharp
/// <summary>Fisheye/barrel correction coefficient (OpenCV k1) applied to the camera frame
/// before marker detection / warping. 0 disables it.</summary>
public double LensDistortionK1 { get; set; } = 0.0;
```

At the top of `Detect`, when `Math.Abs(LensDistortionK1) > 0.001`, undistort `cameraFrame` into a
reusable scratch Mat `_undistorted` and run the remainder of the pipeline against it instead of
the raw frame:

- Intrinsics: `fx = fy = width`, `cx = width/2`, `cy = height/2` (same approximation the tuner
  already uses).
- `distCoeffs` = `[k1, 0, 0, 0, 0]`.
- `Cv2.Undistort(cameraFrame, _undistorted, K, distCoeffs)`.

The `K` and `distCoeffs` Mats are built per call (cheap, 3×3 / 1×5) and disposed; `_undistorted`
is a member reused via the existing `EnsureMat` pattern and disposed in `Dispose`. This
centralizes undistortion so live detection and the tuners share one path.

### 3. Split the tuner — `AutoTuner` (Vision)

Factor the existing coupled `Tune` into two independent public methods. `SelectThreshold` (the
pure pick step) is unchanged.

```csharp
// Sweeps k1 0.000 -> 0.200 step 0.025. For each k1: undistort, run the threshold sweep,
// sum FigurineDetector.LastTotalConfidence. Returns the min-confidence k1.
public AutoTuneResult TuneDistortion(Mat cameraFrame, Bitmap referenceView, float pixelsPerCell);

// Undistorts ONCE with the supplied k1, runs the threshold sweep, delegates to SelectThreshold.
public AutoTuneResult TuneSensitivity(Mat cameraFrame, Bitmap referenceView,
                                      float pixelsPerCell, double k1);
```

- `TuneDistortion` result carries `Success`, `LensDistortionK1`, `LensDistortionCx/Cy`, `Message`
  (e.g. `"lens distortion k1 = 0.050"`). It does **not** set threshold/size fields.
- `TuneSensitivity` result carries `Success`, `DiffThreshold`, `MinObjectCells` /
  `MinBlobAreaPx`, `BlobCount`, `TokenDiameterCells`, `Message` (the current sensitivity message).
  It also echoes the `k1` it was given into `LensDistortionK1` for diagnostics.
- A shared private helper `Undistort(frame, k1) -> Mat` (or inline) avoids duplicating the K /
  distCoeffs construction between the two methods and the detector.
- Keep a thin convenience wrapper so existing callers/tests are unaffected:

```csharp
public AutoTuneResult Tune(Mat cameraFrame, Bitmap referenceView, float pixelsPerCell)
{
    var d = TuneDistortion(cameraFrame, referenceView, pixelsPerCell);
    if (!d.Success) return d;
    var s = TuneSensitivity(cameraFrame, referenceView, pixelsPerCell, d.LensDistortionK1);
    s.LensDistortionK1 = d.LensDistortionK1;
    s.LensDistortionCx = d.LensDistortionCx;
    s.LensDistortionCy = d.LensDistortionCy;
    return s;
}
```

`TestHarness/TestRunner` and the `AutoTunerTests` integration tests continue to call `Tune`
unchanged.

### 4. Orchestration — `DetectionService` (Vision)

- `Apply`: add `_detector.LensDistortionK1 = settings.LensDistortionK1;`.
- Replace `AutoTune()` with two methods (same frame/view/ppc plumbing as today):

```csharp
// Stores _settings.LensDistortionK1, applies to _detector, saves, raises DetectionsUpdated.
public AutoTuneResult AutoTuneDistortion();

// Uses _settings.LensDistortionK1 as the k1 input. Stores DiffThreshold + MinObjectCells
// (or MinBlobAreaPx fallback), applies, saves, raises DetectionsUpdated.
public AutoTuneResult AutoTuneSensitivity();
```

Both reuse the existing guards (`_settings == null`, `TryGetLatestFrame`, `RenderReferenceView`,
`_getPixelsPerCell`).

### 5. Two buttons — `GMMainForm` + `GMMainForm.Designer`

Split `barButtonItemAutoTune` into `barButtonItemAutoDistortion` ("Auto-distortion") and
`barButtonItemAutoSensitivity` ("Auto-sensitivity"), both on the existing bar, runnable in any
order. Each handler mirrors the current one (wait cursor, warn on failure, `UpdateCameraStatus`)
with its own prompt:

- Distortion: *"Clear all tokens so only the map and its four markers are visible, then click OK.
  Auto-distortion will find the lens correction that best straightens the map."*
- Sensitivity: the current prompt (*"Place ONE token … isolate just that token."*).

### 6. Slider — `CameraSettingsForm`

Add a **Lens distortion** label + `TrackBar` to the Detection group (below Min object size).
`TrackBar` is integer-only, so map slider units `0–40` to `k1 = value / 200.0`, giving step
**0.005** over **0.000–0.200**:

- Init: `Value = Math.Clamp((int)Math.Round(settings.LensDistortionK1 * 200), 0, 40)`.
- Label (live): `$"Lens distortion: {_distortionSlider.Value / 200.0:0.000}"`.
- On OK: `_settings.LensDistortionK1 = _distortionSlider.Value / 200.0;`.

`TickFrequency = 5` (a tick every 0.025).

## Testing

- **`AutoTunerTests`**: add a test that `TuneSensitivity(frame, view, ppc, k1)` returns a
  successful sensitivity result and echoes the passed `k1`; add a test that `TuneDistortion`
  returns `Success` with `LensDistortionK1` in `[0, 0.2]`. Existing `Tune` plateau/area/integration
  tests stay green via the wrapper.
- **`FigurineDetectionTests`** (or `AutoTunerTests`): with a captured pair, set
  `FigurineDetector.LensDistortionK1` to a small non-zero value and assert detection still returns
  `Ok` (undistort path runs without breaking the pipeline).
- **Regression**: existing tests that never set K1 exercise the `|k1| <= 0.001` no-op path.

## Out of scope

- Auto-distortion objective changes — keep the existing min-total-confidence-energy metric.
- Estimating cx/cy or higher-order distortion terms (k2…); only k1 is tuned/stored.
- Async/progress-bar tuning — synchronous one-shot with a wait cursor, as today.
- Auto-running either tune on map/zoom change.
