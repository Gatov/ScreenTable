# Expected-Mini Count Cap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A GM-toolbar spin sets the expected mini count `N`; the detector keeps only the `N` most-confident detections (ranked by color distance), with `N = 0` auto-guessing the count from a score cliff — filtering fakes/flares without per-map recalibration.

**Architecture:** The existing size/fill hard gates run first and unchanged. Each surviving blob gets a color-distance score (mean BGR difference-vector magnitude inside the blob). A pure static `FigurineDetector.SelectCount` decides how many top-scored blobs to keep. The detector exposes `ExpectedCount`, fed from a new persisted `CameraSettings.ExpectedFigurines`, which a DevExpress `SpinEdit` on the GM toolbar drives.

**Tech Stack:** C# / .NET 8 WinForms, DevExpress XtraBars, OpenCvSharp, NUnit.

---

## File Structure

- `ScreenMap.Vision/FigurineDetector.cs` — add `ExpectedCount`, the per-blob score computation, the `SelectCount` selector, and reorder outputs to the selected set.
- `ScreenMap.Vision/FigurineDetection.cs` — add `float Score` to the struct.
- `ScreenMap.Vision/CameraSettings.cs` — add `ExpectedFigurines`.
- `ScreenMap.Vision/DetectionService.cs` — push `ExpectedFigurines` into the detector in `Apply`.
- `ScreenMap/GMMainForm.Designer.cs` + `ScreenMap/GMMainForm.cs` — add the toolbar spin and its handler.
- `ScreenMap.Tests/FigurineDetectionTests.cs` — unit tests for `SelectCount` and a detector-level cap/ranking test.

**Test command (all tasks):**
`dotnet test ScreenMap.Tests/ScreenMap.Tests.csproj --filter <name>`

---

## Task 1: `SelectCount` pure selector

**Files:**
- Modify: `ScreenMap.Vision/FigurineDetector.cs`
- Test: `ScreenMap.Tests/FigurineDetectionTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `FigurineDetectionTests.cs` (inside the class):

```csharp
[Test]
public void SelectCount_ManualN_KeepsTopN()
{
    var scores = new float[] { 100, 90, 80, 70, 60, 50, 40, 30, 20, 10 };
    Assert.That(FigurineDetector.SelectCount(scores, 3), Is.EqualTo(3));
}

[Test]
public void SelectCount_ManualN_AboveCount_KeepsAll()
{
    var scores = new float[] { 100, 50 };
    Assert.That(FigurineDetector.SelectCount(scores, 5), Is.EqualTo(2));
}

[Test]
public void SelectCount_Auto_BelowFloor_KeepsAll()
{
    var scores = new float[] { 100, 90, 70 };           // 3 < floor(5)
    Assert.That(FigurineDetector.SelectCount(scores, 0), Is.EqualTo(3));
}

[Test]
public void SelectCount_Auto_SmoothDescent_KeepsCeiling()
{
    var scores = new float[25];
    for (int i = 0; i < scores.Length; i++) scores[i] = 100 - i; // gentle, no cliff
    Assert.That(FigurineDetector.SelectCount(scores, 0), Is.EqualTo(20)); // ceiling
}

[Test]
public void SelectCount_Auto_CliffAfterEight_CutsAtCliff()
{
    // top group of 8 with ~1.0 gaps, then a 43-point cliff.
    var scores = new float[] { 100, 99, 98, 97, 96, 95, 94, 93, 50, 49, 48, 47 };
    Assert.That(FigurineDetector.SelectCount(scores, 0), Is.EqualTo(8));
}

[Test]
public void SelectCount_Auto_ClusterOfFiveThenCliff_RespectsFloor()
{
    // tight top 5, then a sharp drop exactly at index 5 -> floor and cliff agree on 5.
    var scores = new float[] { 100, 99, 98, 97, 96, 40, 39, 38, 37 };
    Assert.That(FigurineDetector.SelectCount(scores, 0), Is.EqualTo(5));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ScreenMap.Tests/ScreenMap.Tests.csproj --filter SelectCount`
Expected: FAIL to compile — `FigurineDetector` has no `SelectCount`.

- [ ] **Step 3: Implement `SelectCount`**

In `ScreenMap.Vision/FigurineDetector.cs`, add a `using System.Collections.Generic;` if missing, and add this method inside the `FigurineDetector` class (e.g. just after the `Detect` method):

```csharp
/// <summary>Decides how many of the score-sorted (descending) blobs to keep.
/// <paramref name="expected"/> &gt; 0 keeps the top N. <paramref name="expected"/> == 0
/// auto-guesses: keep at least <c>Floor</c> and at most <c>Ceiling</c>, cutting at the first
/// score drop that is sharply larger (&gt; <c>CliffFactor</c>x) than the typical drop across
/// the top <c>Floor</c>. Pure and OpenCV-free so it is unit-testable.</summary>
public static int SelectCount(IReadOnlyList<float> scoresDesc, int expected)
{
    const int floor = 5, ceiling = 20;
    const float cliffFactor = 2.5f;

    int n = scoresDesc.Count;
    if (n == 0) return 0;
    if (expected > 0) return Math.Min(expected, n);
    if (n <= floor) return n;

    // Typical consecutive drop across the assumed-real top group.
    float sumDrop = 0;
    for (int i = 1; i < floor; i++) sumDrop += scoresDesc[i - 1] - scoresDesc[i];
    float refGap = sumDrop / (floor - 1);

    int max = Math.Min(ceiling, n);
    for (int i = floor; i < max; i++)
    {
        float drop = scoresDesc[i - 1] - scoresDesc[i];
        if (drop > cliffFactor * refGap) return i; // cliff: keep the i blobs above it.
    }
    return max; // no cliff found within the window -> ceiling (or all, if fewer).
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test ScreenMap.Tests/ScreenMap.Tests.csproj --filter SelectCount`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add ScreenMap.Vision/FigurineDetector.cs ScreenMap.Tests/FigurineDetectionTests.cs
git commit -m "Add SelectCount selector for expected-mini count cap"
```

---

## Task 2: `Score` field on `FigurineDetection`

**Files:**
- Modify: `ScreenMap.Vision/FigurineDetection.cs`
- Modify: `ScreenMap.Vision/FigurineDetector.cs` (the one `new FigurineDetection(...)` call site)

> No standalone test — this is a struct field exercised by Task 3's tests. The build must stay green.

- [ ] **Step 1: Add the field**

Replace the body of `ScreenMap.Vision/FigurineDetection.cs` with:

```csharp
using System.Drawing;

namespace ScreenMap.Vision;

public readonly struct FigurineDetection
{
    public PointF Center { get; }
    public float Radius { get; }
    /// <summary>Detection confidence: mean color distance (BGR difference-vector magnitude)
    /// inside the blob. Higher = more strongly differs from the map. Used to rank which blobs
    /// to keep against the expected count.</summary>
    public float Score { get; }

    public FigurineDetection(PointF center, float radius, float score = 0f)
    {
        Center = center;
        Radius = radius;
        Score = score;
    }
}
```

(The `score` default keeps `TranslateToUnscaled` and any other constructor calls compiling; Task 3 fills it in and Task 4's translate is updated to carry it.)

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build ScreenMap.Vision/ScreenMap.Vision.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add ScreenMap.Vision/FigurineDetection.cs
git commit -m "Add Score field to FigurineDetection"
```

---

## Task 3: Color-distance score + cap in the detector

**Files:**
- Modify: `ScreenMap.Vision/FigurineDetector.cs`
- Test: `ScreenMap.Tests/FigurineDetectionTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `FigurineDetectionTests.cs`:

```csharp
[Test]
public void ExpectedCount_CapsToStrongestByColorDistance()
{
    using var view = RenderScene(960, 540);
    using var cameraBgra = BitmapConverter.ToMat(RenderScene(1280, 720));
    using var camera = cameraBgra.CvtColor(ColorConversionCodes.BGRA2BGR);
    Cv2.Add(camera, new Scalar(40, 40, 40), camera);
    // Left: black disc (differs from grey in brightness only).
    Cv2.Circle(camera, new OpenCvSharp.Point(280, 360), 26, new Scalar(0, 0, 0), -1);
    // Right: pure-blue disc (differs strongly in one channel -> larger color distance).
    Cv2.Circle(camera, new OpenCvSharp.Point(680, 360), 26, new Scalar(255, 0, 0), -1);

    using var detector = new FigurineDetector { MinBlobAreaPx = 200, ExpectedCount = 1 };
    var status = detector.Detect(camera, view, out var dets);

    Assert.That(status, Is.EqualTo(DetectStatus.Ok));
    Assert.That(dets.Length, Is.EqualTo(1), "cap keeps exactly one");
    Assert.That(dets[0].Center.X, Is.GreaterThan(view.Width / 2f),
        "the higher color-distance (blue) disc on the right is kept");
}

[Test]
public void ExpectedCount_Zero_KeepsAllBelowFloor()
{
    using var view = RenderScene(960, 540);
    using var cameraBgra = BitmapConverter.ToMat(RenderScene(1280, 720));
    using var camera = cameraBgra.CvtColor(ColorConversionCodes.BGRA2BGR);
    Cv2.Add(camera, new Scalar(40, 40, 40), camera);
    Cv2.Circle(camera, new OpenCvSharp.Point(280, 360), 26, new Scalar(0, 0, 0), -1);
    Cv2.Circle(camera, new OpenCvSharp.Point(680, 360), 26, new Scalar(255, 0, 0), -1);

    using var detector = new FigurineDetector { MinBlobAreaPx = 200, ExpectedCount = 0 };
    detector.Detect(camera, view, out var dets);

    Assert.That(dets.Length, Is.EqualTo(2), "auto with 2 < floor(5) keeps all");
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ScreenMap.Tests/ScreenMap.Tests.csproj --filter ExpectedCount`
Expected: FAIL to compile — `FigurineDetector` has no `ExpectedCount`.

- [ ] **Step 3: Add the `ExpectedCount` property**

In `ScreenMap.Vision/FigurineDetector.cs`, add next to the other tunable properties (after `MinFillRatio`):

```csharp
/// <summary>Expected number of figurines on the table. When &gt; 0 the detector keeps only
/// the <c>ExpectedCount</c> strongest blobs (by <see cref="FigurineDetection.Score"/>); when 0
/// it auto-guesses via <see cref="SelectCount"/>. Filters fakes/flares without recalibration.</summary>
public int ExpectedCount { get; set; }
```

- [ ] **Step 4: Compute the score and apply the cap**

In `ScreenMap.Vision/FigurineDetector.cs`, replace the contour loop and the array publishing — that is, the block from `var list = new List<FigurineDetection>(contours.Length);` down to (and including) the line `LastFillRatios = fillRatios.ToArray();` — with:

```csharp
        // Candidate carries everything we need to keep the per-detection arrays 1:1 after
        // the score-ranked cap reorders them.
        var candidates = new List<(FigurineDetection det, double rawRadius, double fill)>(contours.Length);
        foreach (var contour in contours)
        {
            Cv2.MinEnclosingCircle(contour, out var center, out float radius);
            float rawRadius = radius;
            double fill = radius > 0 ? Cv2.ContourArea(contour) / (Math.PI * radius * radius) : 0;
            // Reject diffuse / smeared blobs that don't fill their enclosing circle — a solid
            // token does, glare and registration artifacts do not.
            if (MinFillRatio > 0 && fill < MinFillRatio) continue;
            if (PixelsPerCell > 0)
            {
                // Grid scale known. Object size is measured as DIAMETER in cells (a mini occupies
                // ~one grid square == one cell across). Drop anything outside the size band.
                double cells = 2.0 * radius / PixelsPerCell;
                if (cells < MinObjectCells || cells > MaxObjectCells) continue;
            }
            else
            {
                // No grid: legacy pixel-area filter, raw (unsnapped) radius.
                if (Cv2.ContourArea(contour) < MinBlobAreaPx) continue;
            }
            // Score (computed ONLY for blobs that survived the size/fill gates above): mean color
            // distance — the magnitude of the per-pixel BGR difference vector inside the blob.
            float score = MeanColorDistance(_diffBgr, contour);
            candidates.Add((new FigurineDetection(new PointF(center.X, center.Y), radius, score), rawRadius, fill));
        }

        // Rank by confidence and keep only the expected number (or the auto-guessed count).
        candidates.Sort((a, b) => b.det.Score.CompareTo(a.det.Score));
        var scores = new float[candidates.Count];
        for (int i = 0; i < candidates.Count; i++) scores[i] = candidates[i].det.Score;
        int keep = SelectCount(scores, ExpectedCount);

        var list = new List<FigurineDetection>(keep);
        var rawRadii = new List<double>(keep);
        var fillRatios = new List<double>(keep);
        for (int i = 0; i < keep; i++)
        {
            list.Add(candidates[i].det);
            rawRadii.Add(candidates[i].rawRadius);
            fillRatios.Add(candidates[i].fill);
        }
        detections = list.ToArray();
        LastRawRadii = rawRadii.ToArray();
        LastFillRatios = fillRatios.ToArray();
```

- [ ] **Step 5: Add the `MeanColorDistance` helper**

In `ScreenMap.Vision/FigurineDetector.cs`, add this private static helper (e.g. next to `MaskAround`):

```csharp
/// <summary>Mean over the contour's filled interior of the per-pixel BGR difference-vector
/// magnitude (sqrt(b²+g²+r²)). <paramref name="diffBgr"/> is the absdiff(map, warped) image
/// (unchanged since it was built in Detect). Higher means the blob differs more strongly — in
/// brightness AND hue — from the map.</summary>
private static float MeanColorDistance(Mat diffBgr, OpenCvSharp.Point[] contour)
{
    var rect = Cv2.BoundingRect(contour).Intersect(new Rect(0, 0, diffBgr.Cols, diffBgr.Rows));
    if (rect.Width <= 0 || rect.Height <= 0) return 0f;

    // Mask = the contour's filled interior, in ROI-local coordinates.
    using var mask = new Mat(rect.Height, rect.Width, MatType.CV_8UC1, Scalar.Black);
    var local = new OpenCvSharp.Point[contour.Length];
    for (int i = 0; i < contour.Length; i++)
        local[i] = new OpenCvSharp.Point(contour[i].X - rect.X, contour[i].Y - rect.Y);
    Cv2.FillPoly(mask, new[] { local }, Scalar.White);

    // Per-pixel L2 norm across B,G,R -> single-channel float magnitude, then mean over the mask.
    using var roi = new Mat(diffBgr, rect);
    using var roiF = new Mat();
    roi.ConvertTo(roiF, MatType.CV_32FC3);
    Mat[] ch = Cv2.Split(roiF);
    using var acc = new Mat(rect.Height, rect.Width, MatType.CV_32FC1, Scalar.All(0));
    foreach (var c in ch) { using var c2 = c.Mul(c); Cv2.Add(acc, c2, acc); c.Dispose(); }
    Cv2.Sqrt(acc, acc);
    return (float)Cv2.Mean(acc, mask).Val0;
}
```

- [ ] **Step 6: Run the full detection test set**

Run: `dotnet test ScreenMap.Tests/ScreenMap.Tests.csproj --filter FigurineDetectionTests`
Expected: PASS — the new `ExpectedCount_*` tests plus the pre-existing detector tests (default `ExpectedCount = 0` with < 5 blobs keeps all, so they are unaffected).

- [ ] **Step 7: Commit**

```bash
git add ScreenMap.Vision/FigurineDetector.cs ScreenMap.Tests/FigurineDetectionTests.cs
git commit -m "Score blobs by color distance and cap to expected count"
```

---

## Task 4: Persist the setting and feed it to the detector

**Files:**
- Modify: `ScreenMap.Vision/CameraSettings.cs`
- Modify: `ScreenMap.Vision/DetectionService.cs:64-72` (the `Apply` method)

> No standalone test — covered end-to-end by the toolbar in Task 5; the build must stay green.

- [ ] **Step 1: Add the persisted property**

In `ScreenMap.Vision/CameraSettings.cs`, add after `DiffThreshold`:

```csharp
/// <summary>Expected number of figurines on the table. The detector keeps only this many of
/// the strongest detections; 0 means auto-guess the count. Filters fakes/flares per map.</summary>
public int ExpectedFigurines { get; set; } = 0;
```

- [ ] **Step 2: Wire it into `Apply`**

In `ScreenMap.Vision/DetectionService.cs`, in `Apply`, add the line after `_detector.DiffThreshold = settings.DiffThreshold;`:

```csharp
        _detector.ExpectedCount = settings.ExpectedFigurines;
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build ScreenMap.Vision/ScreenMap.Vision.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add ScreenMap.Vision/CameraSettings.cs ScreenMap.Vision/DetectionService.cs
git commit -m "Persist ExpectedFigurines and apply it to the detector"
```

---

## Task 5: Toolbar spin in the GM view

**Files:**
- Modify: `ScreenMap/GMMainForm.Designer.cs`
- Modify: `ScreenMap/GMMainForm.cs`

> WinForms/DevExpress UI — verified by build + a manual smoke check, not an automated test.

- [ ] **Step 1: Declare the new bar items**

In `ScreenMap/GMMainForm.Designer.cs`, in the field-declaration block at the bottom (after `private DevExpress.XtraBars.BarStaticItem barStaticItemCameraStatus;`), add:

```csharp
        private DevExpress.XtraBars.BarEditItem barEditItemMinis;
        private DevExpress.XtraEditors.Repository.RepositoryItemSpinEdit repositoryItemSpinEditMinis;
```

- [ ] **Step 2: Construct them in `InitializeComponent`**

In `InitializeComponent`, after the line
`barStaticItemCameraStatus = new DevExpress.XtraBars.BarStaticItem();`
add:

```csharp
            barEditItemMinis = new DevExpress.XtraBars.BarEditItem();
            repositoryItemSpinEditMinis = new DevExpress.XtraEditors.Repository.RepositoryItemSpinEdit();
```

- [ ] **Step 3: BeginInit the repository item**

Find the line `((System.ComponentModel.ISupportInitialize)barManager1).BeginInit();` and add immediately after it:

```csharp
            ((System.ComponentModel.ISupportInitialize)repositoryItemSpinEditMinis).BeginInit();
```

Find the matching `((System.ComponentModel.ISupportInitialize)barManager1).EndInit();` (near the end) and add immediately after it:

```csharp
            ((System.ComponentModel.ISupportInitialize)repositoryItemSpinEditMinis).EndInit();
```

- [ ] **Step 4: Register the item and its editor with the bar manager**

In the `barManager1.Items.AddRange(new DevExpress.XtraBars.BarItem[] { ... });` call, append `, barEditItemMinis` before the closing `}`.

Change `barManager1.MaxItemId = 12;` to:

```csharp
            barManager1.MaxItemId = 13;
```

Immediately after the `barManager1.MaxItemId = 13;` line, add:

```csharp
            barManager1.RepositoryItems.AddRange(new DevExpress.XtraEditors.Repository.RepositoryItem[] { repositoryItemSpinEditMinis });
```

- [ ] **Step 5: Add the item to the Tools bar links**

In the `bar1.LinksPersistInfo.AddRange(...)` array, append before the closing `}`:

```csharp
, new DevExpress.XtraBars.LinkPersistInfo(barEditItemMinis, true)
```

- [ ] **Step 6: Configure the item and editor**

After the `barStaticItemCameraStatus` configuration block (the lines ending at
`barStaticItemCameraStatus.Name = "barStaticItemCameraStatus";`), add:

```csharp
            //
            // barEditItemMinis
            //
            barEditItemMinis.Caption = "Minis";
            barEditItemMinis.Edit = repositoryItemSpinEditMinis;
            barEditItemMinis.EditWidth = 60;
            barEditItemMinis.Id = 12;
            barEditItemMinis.Name = "barEditItemMinis";
            barEditItemMinis.EditValueChanged += barEditItemMinis_EditValueChanged;
            //
            // repositoryItemSpinEditMinis
            //
            repositoryItemSpinEditMinis.AutoHeight = false;
            repositoryItemSpinEditMinis.IsFloatValue = false;
            repositoryItemSpinEditMinis.MaxValue = new decimal(new int[] { 20, 0, 0, 0 });
            repositoryItemSpinEditMinis.MinValue = new decimal(new int[] { 0, 0, 0, 0 });
            repositoryItemSpinEditMinis.Name = "repositoryItemSpinEditMinis";
```

- [ ] **Step 7: Build to verify the designer compiles**

Run: `dotnet build ScreenMap/ScreenMap.csproj`
Expected: FAIL — `barEditItemMinis_EditValueChanged` is not defined yet (added next step). If any OTHER error appears, fix the designer edits before continuing.

- [ ] **Step 8: Add the handler and seed the initial value**

In `ScreenMap/GMMainForm.cs`, add the handler (e.g. after `barButtonItemCamera_ItemClick`):

```csharp
        private void barEditItemMinis_EditValueChanged(object sender, EventArgs e)
        {
            _cameraSettings.ExpectedFigurines = Convert.ToInt32(barEditItemMinis.EditValue ?? 0);
            _cameraSettings.Save();
            _detectionService.Apply(_cameraSettings);
        }
```

Seed the spin from the loaded setting. In the constructor, immediately after the
`InitializeDetectionService();` call (the detection service must already exist, because
setting `EditValue` fires `barEditItemMinis_EditValueChanged`, which uses `_detectionService`),
add:

```csharp
            barEditItemMinis.EditValue = _cameraSettings.ExpectedFigurines;
```

- [ ] **Step 9: Build to verify it compiles**

Run: `dotnet build ScreenMap/ScreenMap.csproj`
Expected: Build succeeded.

- [ ] **Step 10: Manual smoke check**

Run the GM app (`dotnet run --project ScreenMap/ScreenMap.csproj`). Confirm:
1. A "Minis" spin appears on the Tools toolbar, showing `0`.
2. Spinning it up/down changes the value within 0–20 and does not throw.
3. The value persists across an app restart (written to `CameraSettings.json`).

- [ ] **Step 11: Commit**

```bash
git add ScreenMap/GMMainForm.Designer.cs ScreenMap/GMMainForm.cs
git commit -m "Add Minis count spin to the GM toolbar"
```

---

## Final verification

- [ ] Run the whole test project:

Run: `dotnet test ScreenMap.Tests/ScreenMap.Tests.csproj`
Expected: PASS (existing tests + the new `SelectCount_*` and `ExpectedCount_*` tests). The two `RealPair_*` tests still pass: they place 0 or 1 token, both below the auto floor of 5, so the cap leaves them unchanged.
