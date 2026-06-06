# Figurine crops on the map — design

**Date:** 2026-06-06
**Branch:** camera-figurine-detection

## Goal

Draw each detected figurine's isolated camera photo at its true map location on both
the **GM view** and the **web snapshot**, replacing the abstract green detection circle.
Add a checkbox to switch the overlay between figurine images and the plain green circles.
Remove the now-redundant `/figurines.png` montage path.

## Background — current state

- `FigurineDetector.Detect` finds figurines, returns `FigurineDetection[]` (`Center`,
  `Radius` in warped/reference pixels). When `ProduceCrops` is set it also fills
  `LastCrops` — circular-masked `CV_8UC3` crops on black, aligned 1:1 with the detections.
- `DetectionService.RunCycle` translates detections to unscaled map coords
  (`TranslateToUnscaled`) and stores them in `DetectionStore`. Crops are published
  separately into `_latestCrops` (guarded by `_cropLock`) and exposed only via
  `RenderCropsMontagePng` → web `/figurines.png` (a flat horizontal strip, not linked
  from the web HTML page).
- Overlays: `GMMapView.DrawDetectionOverlay` (GM view, gated by
  `ShowOnGmView`) and `PlayersMap.DrawDetectionOverlay` (web snapshot, always on) both
  draw a translucent green filled ellipse per detection at `[Center±Radius]`.

Detections and crops are index-aligned but never joined, so no renderer can place a crop
at its map position today.

## Design

### 1. Join crop with position in `DetectionStore`

Replace the store's bare `FigurineDetection[]` with an immutable frame bundling the
aligned crop bitmaps:

```csharp
private sealed class Frame
{
    public FigurineDetection[] Dets;
    public Bitmap[] Crops;   // index-aligned with Dets; element may be null
}
```

- `Snapshot()` keeps returning `FigurineDetection[]` (unchanged callers).
- New `CropsSnapshot()` returns the aligned `Bitmap[]`.
- `Update(FigurineDetection[] dets, Bitmap[] crops, status, text)` swaps the frame
  reference atomically (`volatile`).

**Bitmap lifetime (cross-thread disposal safety).** The store is written from the
detection timer threadpool thread and read (drawn) on the UI thread. GDI `Bitmap`s own
native handles, so a frame must not be disposed while a paint still references it. The
store keeps a **one-generation reserve**: on `Update` it disposes the frame that was
current *two* updates ago and keeps the immediately-previous frame alive. UI paints
complete in well under a frame (~16 ms) and updates land at the detection interval
(≥0.5 s, default 2 s), so a frame reference captured at paint-start is never disposed
mid-draw. The reserve frame's bitmaps are disposed on store `Dispose()` as well.

### 2. Transparent crops

Change `FigurineDetector.CropDetections` to emit 4-channel `CV_8UC4` (BGR + circular
alpha mask) instead of `CV_8UC3` on black. `BitmapConverter.ToBitmap` then produces a
32bpp ARGB bitmap whose outside-circle pixels are transparent, so the map shows through.
The alpha channel is the same circle currently drawn into the color mask.

### 3. Produce → convert → publish

In `DetectionService`:

- `ProduceCrops` is enabled when **any** consumer wants crops: the detector produces
  crops when the preview is open (`_previewActive`) **or** the figurine overlay is active
  (`ShowFigurines` true). The old `_webCropsWanted` flag is removed (see §6).
- On `DetectStatus.Ok`: translate detections to map coords as today; convert
  `_detector.LastCrops` (Mats) to GDI `Bitmap`s and pass both to `_store.Update(...)`.
  The store now owns those bitmaps (and their disposal); the detector still owns/disposes
  its Mats on the next cycle.
- The existing `_latestCrops` / `_cropLock` / `PublishCrops` / `TryGetLatestCrops`
  Mat plumbing **stays** — `CameraPreviewForm` reads `TryGetLatestCrops` (Mats) to build
  its own preview montage. The store-bound Bitmaps are published alongside it. Only the
  *web* montage is removed (see §6).

### 4. Render crop at map location (both views)

`GMMapView.DrawDetectionOverlay` and `PlayersMap.DrawDetectionOverlay`, per detection:

```
rect = [Center.X-Radius, Center.Y-Radius, 2*Radius, 2*Radius]   // existing
if (showFigurines && crops[i] != null && !crops[i].Empty)
    g.DrawImage(crops[i], rect);     // auto-scales warped-px crop into map-coord rect
else
    <existing green fill + outline ellipse>   // fallback
```

The crop bitmap is sized in warped/reference pixels; `DrawImage` into the map-coord
`rect` reuses the exact translation already applied to `Center`/`Radius`, so position and
scale come for free. Use `InterpolationMode.HighQualityBicubic` for the scaled draw.

### 5. Toggle: figurines vs circles

- Add `bool ShowFigurines { get; set; } = true;` to `CameraSettings`.
- Add a checkbox to `CameraSettingsForm`: **"Show figurine images (off = green circles)"**,
  placed under the existing "Show detections on GM view" checkbox; saved in the OK handler.
  Grow `ClientSize`/button Y to fit the extra row.
- Thread the flag into both overlays as a `Func<bool> showFigurines`:
  - `GMMapView.SetDetectionOverlay(store, showFlag, showFigurinesFlag)`.
  - `PlayersMap` / `PlayerController.SetDetectionOverlay(store, showFigurinesFlag)`
    (web always shows the overlay; the flag only chooses image vs circle).
  - Wired in `GMMainForm` from `_cameraSettings.ShowFigurines`.

**Gating:** crops are produced when `ShowFigurines` is true (overlay wants images) or a
preview is open. When `ShowFigurines` is false the renderers draw circles and the
detector can skip crop production.

### 6. Remove the web montage path

Delete only the **web** montage (the preview montage stays — see §3):
- `ScreenMapWebServer`: `/figurines.png` handling, `ServeFigurines`, the
  `Func<byte[]> renderFigurines` ctor parameter and field.
- `DetectionService.RenderCropsMontagePng`, `SetWebCropsWanted`, `_webCropsWanted`,
  `CropTilePx`.
- `GMMainForm`: the `renderFigurines` argument to the `ScreenMapWebServer` ctor.

**Preview window is untouched:** `CameraPreviewForm` keeps setting `_previewActive`
(via `SetPreviewActive`) and keeps reading Mats through `TryGetLatestCrops` to build its
own `BuildCropMontage`. None of that is removed.

## Error handling / edge cases

- **Missing/empty crop** for a present detection → draw the green circle (fallback).
- **Mat→Bitmap conversion failure** → log/skip that crop (null entry), circle fallback.
- **Crop bitmap larger/smaller than rect** → `DrawImage` scales; no clamp needed.
- **No map / NoMarkers / Empty** → unchanged status handling; no crops published.

## Testing

- `FigurineDetectionTests`: assert `CropDetections` output is `CV_8UC4` and alpha is 0
  outside the enclosing circle, 255 at center.
- Store: unit test that `Update` disposes the two-generations-old frame's bitmaps and
  keeps the previous frame's bitmaps alive (use sentinel disposable wrappers / check
  `Bitmap` disposed state via a probe).
- Manual: run app with a filmed mini, toggle the checkbox, confirm GM view and the web
  page (`/`) both switch between photo and green circle at the correct location.

## Out of scope

- True-perspective crops (the existing TODO in `CropDetections`): crops stay
  deskewed/aligned for now.
- Any web HTML/JS change beyond removing the dead endpoint (the map `/image.png` already
  carries the overlay).